using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Eldoria.Network
{
    /// <summary>
    /// A direct TCP connection between two copies of the game: one hosts (listens on a port), the other joins (connects
    /// to the host's IP address). Messages are lines of text (JSON); they're sent from any thread and received on a
    /// background thread, then handed to the game through <see cref="Poll"/> on the main thread.
    /// </summary>
    public sealed class NetConnection : IDisposable
    {
        public const int DefaultPort = 7777;

        private TcpListener listener;
        private TcpClient client;
        private StreamWriter writer;
        private Thread worker;
        private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        private readonly object sendLock = new object();
        private volatile bool closed;

        /// <summary>Connected to the other player.</summary>
        public bool IsConnected { get; private set; }
        /// <summary>Waiting for the other player to join (hosting).</summary>
        public bool IsListening => listener != null && !IsConnected && !closed;
        /// <summary>Why the connection failed or ended (null while all is well).</summary>
        public string Error { get; private set; }
        /// <summary>The connection was open and has ended (the other player left, or the network dropped).</summary>
        public bool Lost { get; private set; }

        /// <summary>Starts hosting: waits on <paramref name="port"/> for the other player to join.</summary>
        /// <param name="loopbackOnly">Only accept connections from this computer (tests).</param>
        public static NetConnection Host(int port = DefaultPort, bool loopbackOnly = false)
        {
            var c = new NetConnection();
            try
            {
                c.listener = new TcpListener(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
                c.listener.Start(1);
                c.worker = new Thread(c.AcceptThenRead) { IsBackground = true, Name = "Eldoria host" };
                c.worker.Start();
            }
            catch (Exception e)
            {
                c.Error = $"Couldn't host on port {port}: {e.Message}";
                c.Close();
            }
            return c;
        }

        /// <summary>Joins a host at <paramref name="address"/> (an IP address or host name).</summary>
        public static NetConnection Join(string address, int port = DefaultPort)
        {
            var c = new NetConnection();
            c.worker = new Thread(() => c.ConnectThenRead(address, port)) { IsBackground = true, Name = "Eldoria join" };
            c.worker.Start();
            return c;
        }

        private void AcceptThenRead()
        {
            try
            {
                var accepted = listener.AcceptTcpClient();
                listener.Stop();
                Attach(accepted);
                ReadLoop();
            }
            catch (Exception e)
            {
                if (!closed) Error = $"Hosting stopped: {e.Message}";
            }
        }

        private void ConnectThenRead(string address, int port)
        {
            try
            {
                var tcp = new TcpClient();
                var connecting = tcp.BeginConnect(address, port, null, null);
                if (!connecting.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(8)) || !tcp.Connected)
                {
                    tcp.Close();
                    if (!closed) Error = $"Couldn't reach {address}:{port}. Check the address, that the host is waiting, and their firewall.";
                    return;
                }
                tcp.EndConnect(connecting);
                Attach(tcp);
                ReadLoop();
            }
            catch (Exception e)
            {
                if (!closed) Error = $"Couldn't join {address}:{port}: {e.Message}";
            }
        }

        private void Attach(TcpClient tcp)
        {
            client = tcp;
            client.NoDelay = true;
            var stream = client.GetStream();
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            IsConnected = true;
        }

        private void ReadLoop()
        {
            using (var reader = new StreamReader(client.GetStream(), new UTF8Encoding(false)))
            {
                try
                {
                    string line;
                    while (!closed && (line = reader.ReadLine()) != null)
                        if (line.Length > 0) inbox.Enqueue(line);
                }
                catch (Exception e)
                {
                    if (!closed) Error = $"Connection lost: {e.Message}";
                }
            }
            if (!closed)
            {
                Lost = true;
                Error ??= "The other player left.";
                IsConnected = false;
            }
        }

        /// <summary>Sends one message (a line of text). False if not connected.</summary>
        public bool Send(string line)
        {
            if (!IsConnected || writer == null) return false;
            try
            {
                lock (sendLock) writer.WriteLine(line.Replace("\n", " "));
                return true;
            }
            catch (Exception e)
            {
                Error = $"Connection lost: {e.Message}";
                Lost = true;
                IsConnected = false;
                return false;
            }
        }

        /// <summary>Messages received since the last call, oldest first (call on the main thread).</summary>
        public List<string> Poll()
        {
            var list = new List<string>();
            while (inbox.TryDequeue(out var line)) list.Add(line);
            return list;
        }

        public void Close()
        {
            closed = true;
            IsConnected = false;
            try { listener?.Stop(); } catch { }
            try { client?.Close(); } catch { }
        }

        public void Dispose() => Close();

        /// <summary>This computer's IPv4 addresses on the local network (to tell the other player), best guesses first.</summary>
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) list.Add(ip.ToString());
            }
            catch { }
            // Private LAN ranges (192.168.x, 10.x, 172.16-31.x) first.
            return list.Distinct().OrderBy(a => a.StartsWith("192.168.") ? 0 : a.StartsWith("10.") ? 1 : a.StartsWith("172.") ? 2 : 3).ToList();
        }
    }
}
