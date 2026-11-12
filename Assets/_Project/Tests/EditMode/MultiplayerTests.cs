using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Eldoria.Gameplay;
using Eldoria.Network;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Multiplayer: the messages between the two games, and the connection itself (on this computer).</summary>
    public class MultiplayerTests
    {
        [TearDown]
        public void CleanUp()
        {
            MultiplayerSession.Leave();
            ArmySelection.Clear();
        }

        [Test]
        public void AnOrderSurvivesTheTripExactly()
        {
            var order = new OrderMsg { kind = OrderKinds.FollowerMove, seq = 7, unit = 103, soldier = 2, targetUnit = 4, targetSoldier = 1, ability = "FragGrenade", checksum = -1234567890123L };
            var point = new Vector3(-12.3456789f, 0.1f, 1e-7f);
            order.Point = point;
            var back = NetEnvelope.Unpack(NetEnvelope.Pack(MessageTypes.Order, order));
            Assert.That(back.type, Is.EqualTo(MessageTypes.Order));
            var o = back.Payload<OrderMsg>();
            Assert.That(o.kind, Is.EqualTo(OrderKinds.FollowerMove));
            Assert.That((o.seq, o.unit, o.soldier, o.targetUnit, o.targetSoldier), Is.EqualTo((7, 103, 2, 4, 1)));
            Assert.That(o.ability, Is.EqualTo("FragGrenade"));
            Assert.That(o.checksum, Is.EqualTo(-1234567890123L));
            Assert.That(o.hasPoint, Is.True);
            // Bit for bit, so both games walk to exactly the same spot.
            Assert.That(o.Point.x, Is.EqualTo(point.x));
            Assert.That(o.Point.y, Is.EqualTo(point.y));
            Assert.That(o.Point.z, Is.EqualTo(point.z));
        }

        [Test]
        public void AnArmyListSurvivesTheTrip()
        {
            var catalog = UnitCatalog.Load();
            var faction = catalog.Factions.First();
            var units = catalog.UnitsFor(faction).Take(4).ToList();
            var loadouts = units.Select(_ => new UnitLoadout()).ToList();
            // Give a weapon swap and equipment wherever the catalog offers them.
            for (int i = 0; i < units.Count; i++)
                foreach (var soldier in units[i].Roster().Distinct())
                {
                    var option = soldier.weaponOptions.FirstOrDefault(o => o != null && o.IsValid);
                    if (option != null) loadouts[i].For(soldier).Swaps[option.weapon] = option.alternatives.First(a => a != null && a != option.weapon);
                    var item = soldier.equipmentOptions.FirstOrDefault(e => e != null);
                    if (item != null) loadouts[i].For(soldier).Equipment.Add(item);
                }

            var msg = ArmyMsg.From(faction, units, loadouts);
            var sent = NetEnvelope.Unpack(NetEnvelope.Pack(MessageTypes.Army, msg)).Payload<ArmyMsg>();
            Assert.That(sent.TryResolve(catalog, out var f, out var army, out var back, out var error), Is.True, error);
            Assert.That(f, Is.SameAs(faction));
            Assert.That(army, Is.EqualTo(units));
            for (int i = 0; i < units.Count; i++)
                foreach (var soldier in units[i].Roster().Distinct())
                {
                    var want = loadouts[i].Get(soldier);
                    var got = back[i].Get(soldier);
                    if (want == null || want.IsEmpty) { Assert.That(got == null || got.IsEmpty, Is.True); continue; }
                    Assert.That(got.Swaps, Is.EquivalentTo(want.Swaps));
                    Assert.That(got.Equipment, Is.EqualTo(want.Equipment));
                }
        }

        [Test]
        public void AnArmyWithUnknownUnitsIsRejected()
        {
            var msg = new ArmyMsg { faction = UnitCatalog.Load().Factions.First().name, units = new List<string> { "NoSuchUnit" } };
            Assert.That(msg.TryResolve(UnitCatalog.Load(), out _, out _, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NoSuchUnit"));
        }

        [Test]
        public void TheEnemyIsWhicheverSideThisPlayerIsNot()
        {
            var units = new[] { UnitCatalog.Load().Available.First() };
            Assert.That(ArmySelection.LocalSide, Is.EqualTo(Side.Player));
            Assert.That(ArmyDeployment.UnitNames(units, Side.AI)[0], Does.EndWith("(Enemy)"));
            Assert.That(ArmyDeployment.UnitNames(units, Side.Player)[0], Does.Not.EndWith("(Enemy)"));

            ArmySelection.LocalSide = Side.AI;   // this player is north in a multiplayer game
            Assert.That(ArmyDeployment.UnitNames(units, Side.Player)[0], Does.EndWith("(Enemy)"));
            Assert.That(ArmyDeployment.UnitNames(units, Side.AI)[0], Does.Not.EndWith("(Enemy)"));

            ArmySelection.Clear();
            Assert.That(ArmySelection.LocalSide, Is.EqualTo(Side.Player), "single player again");
        }

        [Test]
        public void UnitIdsAreTheSameInBothGames()
        {
            Assert.That(ArmyDeployment.NetIdOf(Side.Player, 0), Is.EqualTo(0));
            Assert.That(ArmyDeployment.NetIdOf(Side.Player, 5), Is.EqualTo(5));
            Assert.That(ArmyDeployment.NetIdOf(Side.AI, 0), Is.EqualTo(100));
            Assert.That(ArmyDeployment.NetIdOf(Side.AI, 5), Is.EqualTo(105));
        }

        private static bool WaitFor(System.Func<bool> condition, int ms = 5000)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                if (clock.ElapsedMilliseconds > ms) return false;
                Thread.Sleep(10);
            }
            return true;
        }

        [Test]
        public void TwoGamesOnThisComputerConnectAndTalk()
        {
            const int port = 7791;
            using var host = NetConnection.Host(port, loopbackOnly: true);
            Assert.That(host.Error, Is.Null);
            Assert.That(host.IsListening, Is.True);
            using var guest = NetConnection.Join("127.0.0.1", port);
            Assert.That(WaitFor(() => host.IsConnected && guest.IsConnected), Is.True, guest.Error ?? host.Error);

            Assert.That(host.Send("hello from the host"), Is.True);
            Assert.That(guest.Send("first"), Is.True);
            Assert.That(guest.Send("second"), Is.True);
            var atGuest = new List<string>();
            var atHost = new List<string>();
            Assert.That(WaitFor(() => { atGuest.AddRange(guest.Poll()); atHost.AddRange(host.Poll()); return atGuest.Count >= 1 && atHost.Count >= 2; }), Is.True);
            Assert.That(atGuest, Is.EqualTo(new[] { "hello from the host" }));
            Assert.That(atHost, Is.EqualTo(new[] { "first", "second" }), "in order");

            guest.Close();
            Assert.That(WaitFor(() => host.Lost), Is.True, "the host notices the other player left");
        }

        [Test]
        public void JoiningNobodyFailsWithAReason()
        {
            using var guest = NetConnection.Join("127.0.0.1", 7792);
            Assert.That(WaitFor(() => guest.Error != null, 12000), Is.True);
            Assert.That(guest.IsConnected, Is.False);
        }

        [Test]
        public void TheHostTellsTheOtherPlayerTheMap()
        {
            // The lobby over a real connection: versions are checked, then the host's map choice reaches the other player.
            const int port = 7793;
            var hostSession = MultiplayerSession.HostGame(port, loopbackOnly: true);
            // A second session can't be Active at the same time, so the guest is a raw connection speaking the protocol.
            using var guest = NetConnection.Join("127.0.0.1", port);
            Assert.That(WaitFor(() => guest.IsConnected), Is.True, guest.Error);
            guest.Send(NetEnvelope.Pack(MessageTypes.Hello, new HelloMsg { version = MultiplayerSession.ProtocolVersion }));
            Assert.That(WaitFor(() => { hostSession.Pump(); return hostSession.IsConnected; }), Is.True);

            hostSession.ChooseMap("Countryside");
            var got = new List<NetEnvelope>();
            Assert.That(WaitFor(() => { got.AddRange(guest.Poll().Select(NetEnvelope.Unpack)); return got.Any(e => e.type == MessageTypes.Map); }), Is.True);
            Assert.That(got.First(e => e.type == MessageTypes.Map).Payload<MapMsg>().scene, Is.EqualTo("Countryside"));
            Assert.That(got.First(e => e.type == MessageTypes.Hello).Payload<HelloMsg>().version, Is.EqualTo(MultiplayerSession.ProtocolVersion));
        }
    }
}
