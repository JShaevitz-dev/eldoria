using System;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoria.Network
{
    public enum NetRole { Host, Client }

    /// <summary>
    /// A two-player game over the network (kept across scenes). The host waits for the other player to join by IP; then
    /// the host picks the map and both players build their armies. When both are ready the host starts the battle: it
    /// rolls who deploys south and moves first and a shared dice seed, and both games load the same map with the same
    /// armies. In battle the games stay in step by sending each other the orders of the player whose turn it is
    /// (lockstep): each game applies them through the same rules and dice, and compares a state checksum after each one.
    /// </summary>
    public class MultiplayerSession : MonoBehaviour
    {
        /// <summary>Bumped when the messages change: both games must match.</summary>
        public const int ProtocolVersion = 1;

        /// <summary>The multiplayer game in progress (null in single player).</summary>
        public static MultiplayerSession Active { get; private set; }
        public static bool IsMultiplayer => Active != null;

        public NetRole Role { get; private set; }
        private NetConnection connection;
        private bool helloSent;

        /// <summary>Connected to the other player (and they're running the same version).</summary>
        public bool IsConnected => IsOffline || (connection != null && connection.IsConnected && remoteHello);
        public bool IsWaitingForPlayer => connection != null && connection.IsListening;
        /// <summary>Why things failed or ended (null while all is well).</summary>
        public string Error { get; private set; }
        public bool Lost { get; private set; }
        private bool remoteHello;

        // ---- Lobby ----
        /// <summary>The map (scene) the host chose; null until then.</summary>
        public string MapScene { get; private set; }
        public ArmyMsg LocalArmy { get; private set; }
        public ArmyMsg RemoteArmy { get; private set; }
        public bool BattleStarted { get; private set; }

        // ---- Battle ----
        /// <summary>The side this game's player commands (south = <see cref="Side.Player"/>, moves first).</summary>
        public Side LocalSide { get; private set; } = Side.Player;
        public Side RemoteSide => LocalSide == Side.Player ? Side.AI : Side.Player;
        private readonly Queue<OrderMsg> orders = new Queue<OrderMsg>();
        private int sentSeq;
        /// <summary>Orders sent (kept for tests and checks).</summary>
        public readonly List<OrderMsg> Sent = new List<OrderMsg>();
        /// <summary>How many orders gave a different state here than in the other game.</summary>
        public int Desyncs { get; private set; }

        private static MultiplayerSession Create(NetRole role)
        {
            Leave();
            var go = new GameObject("Multiplayer");
            if (Application.isPlaying) DontDestroyOnLoad(go);   // kept from the menu into the battle
            var s = go.AddComponent<MultiplayerSession>();
            s.Role = role;
            Active = s;
            return s;
        }

        /// <summary>Host a game: wait on the port for the other player.</summary>
        public static MultiplayerSession HostGame(int port = NetConnection.DefaultPort, bool loopbackOnly = false)
        {
            var s = Create(NetRole.Host);
            s.connection = NetConnection.Host(port, loopbackOnly);
            return s;
        }

        /// <summary>Join a game hosted at <paramref name="address"/>.</summary>
        public static MultiplayerSession JoinGame(string address, int port = NetConnection.DefaultPort)
        {
            var s = Create(NetRole.Client);
            s.connection = NetConnection.Join(address, port);
            return s;
        }

        /// <summary>
        /// For tests: a session with no network. Orders sent are only recorded (<see cref="Sent"/>); orders "received" are
        /// given with <see cref="Inject"/>.
        /// </summary>
        public static MultiplayerSession Offline(Side localSide)
        {
            var s = Create(NetRole.Host);
            s.SetOffline();
            s.LocalSide = localSide;
            s.BattleStarted = true;
            return s;
        }

        private bool offlineFlag;
        private void SetOffline() => offlineFlag = true;
        private bool IsOffline => offlineFlag;

        /// <summary>Ends the multiplayer game (tells the other player, closes the connection).</summary>
        public static void Leave()
        {
            if (Active == null) return;
            var s = Active;
            Active = null;
            if (s.connection != null && s.connection.IsConnected) s.connection.Send(NetEnvelope.Pack(MessageTypes.Bye, null));
            s.connection?.Close();
            ArmySelection.LocalSide = Side.Player;
            if (s != null) { if (Application.isPlaying) Destroy(s.gameObject); else DestroyImmediate(s.gameObject); }
        }

        private void OnDestroy()
        {
            connection?.Close();
            if (Active == this) Active = null;
        }

        private void Send(string type, object payload)
        {
            if (IsOffline) return;
            if (connection == null || !connection.Send(NetEnvelope.Pack(type, payload))) Error ??= connection?.Error ?? "Not connected";
        }

        private void Update() => Pump();

        /// <summary>Handles what has arrived from the other player (every frame; tests can call it directly).</summary>
        public void Pump()
        {
            if (connection == null) return;
            if (connection.IsConnected && !helloSent)
            {
                helloSent = true;
                Send(MessageTypes.Hello, new HelloMsg { version = ProtocolVersion, build = Application.version });
            }
            foreach (var line in connection.Poll()) Handle(line);
            if (connection.Error != null && Error == null) Error = connection.Error;
            if (connection.Lost && !Lost) Lost = true;
        }

        private void Handle(string line)
        {
            var env = NetEnvelope.Unpack(line);
            if (env == null) return;
            switch (env.type)
            {
                case MessageTypes.Hello:
                    var hello = env.Payload<HelloMsg>();
                    if (hello.version != ProtocolVersion)
                    {
                        Error = $"The other player's game is a different version ({hello.version}, this is {ProtocolVersion}).";
                        connection.Close();
                        Lost = true;
                    }
                    else remoteHello = true;
                    break;
                case MessageTypes.Map:
                    MapScene = env.Payload<MapMsg>().scene;
                    break;
                case MessageTypes.Army:
                    RemoteArmy = env.Payload<ArmyMsg>();
                    if (Role == NetRole.Host) TryStart();
                    break;
                case MessageTypes.Start:
                    if (Role == NetRole.Client) Begin(env.Payload<StartMsg>());
                    break;
                case MessageTypes.Order:
                    orders.Enqueue(env.Payload<OrderMsg>());
                    break;
                case MessageTypes.Bye:
                    Lost = true;
                    Error ??= "The other player left.";
                    connection.Close();
                    break;
            }
        }

        // ------------------------------------------------------------------ Lobby

        /// <summary>The host chooses the map (and tells the other player).</summary>
        public void ChooseMap(string scene)
        {
            if (Role != NetRole.Host) return;
            MapScene = scene;
            Send(MessageTypes.Map, new MapMsg { scene = scene });
        }

        /// <summary>This player's army is ready (sent to the other player). The host starts once both are ready.</summary>
        public void SubmitArmy(Faction faction, IReadOnlyList<UnitTemplate> army, IReadOnlyList<UnitLoadout> loadouts)
        {
            LocalArmy = ArmyMsg.From(faction, army, loadouts);
            Send(MessageTypes.Army, LocalArmy);
            if (Role == NetRole.Host) TryStart();
        }

        /// <summary>The host: both armies are in and the map is chosen, so roll who goes first and the dice seed, and start.</summary>
        private void TryStart()
        {
            if (BattleStarted || LocalArmy == null || RemoteArmy == null || string.IsNullOrEmpty(MapScene)) return;
            var roll = new System.Random();
            var start = new StartMsg
            {
                scene = MapScene,
                seed = roll.Next(1, int.MaxValue),
                hostIsSouth = roll.Next(2) == 0,
                hostArmy = LocalArmy,
                clientArmy = RemoteArmy,
            };
            Send(MessageTypes.Start, start);
            Begin(start);
        }

        /// <summary>Both games: set up the armies, the shared dice and this player's side, and load the map.</summary>
        private void Begin(StartMsg start)
        {
            var catalog = UnitCatalog.Load();
            if (!start.hostArmy.TryResolve(catalog, out var hostFaction, out var hostArmy, out var hostLoadouts, out var e1)
                || !start.clientArmy.TryResolve(catalog, out var clientFaction, out var clientArmy, out var clientLoadouts, out var e2))
            {
                Error = "The armies don't match this game's units (are both games the same version?).";
                return;
            }
            bool localIsHost = Role == NetRole.Host;
            LocalSide = localIsHost == start.hostIsSouth ? Side.Player : Side.AI;
            // South (Side.Player) deploys at the bottom of the map and moves first.
            if (start.hostIsSouth)
                ArmySelection.Set(hostArmy, clientArmy, hostFaction, clientFaction, hostLoadouts, clientLoadouts);
            else
                ArmySelection.Set(clientArmy, hostArmy, clientFaction, hostFaction, clientLoadouts, hostLoadouts);
            ArmySelection.LocalSide = LocalSide;
            Core.GameRandom.Seed(start.seed);
            BattleStarted = true;
            SceneManager.LoadScene(start.scene);
        }

        // ------------------------------------------------------------------ Battle orders

        /// <summary>Sends an order given by this player (with the state checksum after it).</summary>
        public void SendOrder(OrderMsg order)
        {
            order.seq = ++sentSeq;
            Sent.Add(order);
            Send(MessageTypes.Order, order);
        }

        /// <summary>The next order from the other player, if one has arrived.</summary>
        public bool TryNextOrder(out OrderMsg order)
        {
            if (orders.Count > 0) { order = orders.Dequeue(); return true; }
            order = null;
            return false;
        }

        /// <summary>For tests: an order "from the other player".</summary>
        public void Inject(OrderMsg order) => orders.Enqueue(order);

        /// <summary>The other game's state after an order didn't match this one's.</summary>
        public void ReportDesync(OrderMsg order, long localChecksum)
        {
            Desyncs++;
            Debug.LogWarning($"[Eldoria] Multiplayer out of sync after order {order.seq} ({order.kind}): theirs {order.checksum}, ours {localChecksum}.");
        }
    }
}
