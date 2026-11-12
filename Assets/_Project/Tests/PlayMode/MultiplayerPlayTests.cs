using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Core;
using Eldoria.Gameplay;
using Eldoria.Network;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoria.Tests
{
    /// <summary>
    /// Multiplayer battles stay in step: orders given in one game, replayed in another game started the same way (same
    /// armies, map and dice seed), give the same battle. Both games run here one after the other, with no network.
    /// </summary>
    public class MultiplayerPlayTests
    {
        private const int Seed = 4242;

        private static UnitTemplate Unit(string name) => UnitCatalog.Load().Available.First(u => u.displayName == name);
        private static Faction Faction(string name) => UnitCatalog.Load().Factions.First(f => f.displayName == name);

        /// <summary>Starts a multiplayer battle (no network) with this player on <paramref name="localSide"/>.</summary>
        private static IEnumerator StartBattle(Side localSide)
        {
            MultiplayerSession.Leave();
            Victory.Reset();
            ArmySelection.Set(
                new[] { Unit("Field Commander"), Unit("Infantry Squad"), Unit("Machine Gun Team") },
                new[] { Unit("Field Commander"), Unit("Infantry Squad"), Unit("Sniper") },
                Faction("United States of Liberteria"), Faction("The Draksa Reich"));
            ArmySelection.LocalSide = localSide;
            GameRandom.Seed(Seed);
            MultiplayerSession.Offline(localSide);
            SceneManager.LoadScene(Battlefields.EnglishCountryside.Scene);
            yield return null;
            yield return null;
            BringTheArmiesClose();
        }

        /// <summary>The same in both games: north's army is moved to 15 m in front of south's, within shooting range.</summary>
        private static void BringTheArmiesClose()
        {
            var soldiers = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None);
            var south = soldiers.Where(s => s.Unit.Side == Side.Player).ToList();
            var north = soldiers.Where(s => s.Unit.Side == Side.AI).ToList();
            float southFront = south.Max(s => s.Position.z), northBack = north.Min(s => s.Position.z);
            var shift = new Vector3(0f, 0f, southFront + 15f - northBack);
            foreach (var s in north) s.transform.position += shift;
        }

        [TearDown]
        public void CleanUp()
        {
            MultiplayerSession.Leave();
            ArmySelection.Clear();
            Victory.Reset();
        }

        private static MovementController Controller => Object.FindFirstObjectByType<MovementController>();

        private static Unit Enemy(Side mySide) =>
            Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side != mySide).OrderBy(u => u.NetId).First();

        [UnityTest]
        public IEnumerator EachPlayerCommandsTheirOwnSide()
        {
            yield return StartBattle(Side.AI);
            var c = Controller;
            Assert.That(c.IsMultiplayer, Is.True);
            Assert.That(c.LocalSide, Is.EqualTo(Side.AI));
            Assert.That(c.PlayerUnits.All(u => u.Side == Side.AI), Is.True, "north commands the north army");
            Assert.That(c.PlayerUnits.All(u => !u.DisplayName.EndsWith("(Enemy)")), Is.True);
            Assert.That(Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.Player)
                .All(u => u.DisplayName.EndsWith("(Enemy)")), Is.True);

            // South moves first: north can only watch.
            Assert.That(c.IsRemoteTurn, Is.True);
            Assert.That(c.SelectUnit(c.PlayerUnits[0]), Is.False);
            Assert.That(c.SelectNextUnit(), Is.False);
            c.EndTurn();
            Assert.That(Turns.ActiveSide, Is.EqualTo(Side.Player), "north can't end south's turn");
            Assert.That(MultiplayerSession.Active.Sent, Is.Empty, "nothing was sent");
        }

        [UnityTest]
        public IEnumerator OrdersAreSentWithTheStateAfterThem()
        {
            yield return StartBattle(Side.Player);
            var c = Controller;
            Assert.That(c.IsRemoteTurn, Is.False, "south moves first");
            var unit = c.PlayerUnits[0];
            Assert.That(c.SelectUnit(unit), Is.True);
            Assert.That(c.OrderHold(), Is.True);
            var sent = MultiplayerSession.Active.Sent;
            Assert.That(sent.Select(o => o.kind), Is.EqualTo(new[] { OrderKinds.Select, OrderKinds.Hold }));
            Assert.That(sent[0].unit, Is.EqualTo(unit.NetId));
            Assert.That(sent[1].checksum, Is.EqualTo(c.StateChecksum()));
            Assert.That(sent.Select(o => o.seq), Is.EqualTo(new[] { 1, 2 }));

            // Ending south's turn hands it to north (the other player).
            c.OrderEndMove();
            c.EndTurn();
            Assert.That(sent.Last().kind, Is.EqualTo(OrderKinds.EndTurn));
            Assert.That(Turns.ActiveSide, Is.EqualTo(Side.AI));
            Assert.That(c.IsRemoteTurn, Is.True);
            Assert.That(c.SelectUnit(c.PlayerUnits[1]), Is.False, "not on the other player's turn");
        }

        [UnityTest]
        public IEnumerator ReplayedOrdersGiveTheSameBattle()
        {
            // Game 1: south plays a turn (moves, shoots, ends the turn).
            yield return StartBattle(Side.Player);
            var c = Controller;
            int shots = 0;
            foreach (var unit in c.PlayerUnits.ToList())
            {
                if (unit.IsDestroyed) continue;
                Assert.That(c.SelectUnit(unit), Is.True, unit.DisplayName);
                var leader = unit.Leader;
                // The first unit holds (a Deployed weapon can't fire after moving); the others move a little.
                if (unit == c.PlayerUnits[0] || !c.OrderLeaderMove(leader.Position + Vector3.forward * 3f + Vector3.right * 1.5f)) Assert.That(c.OrderHold(), Is.True);
                yield return null;
                c.OrderGather();
                Assert.That(c.OrderEndMove(), Is.True, $"{unit.DisplayName} finishes its move");
                foreach (var enemy in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.AI).OrderBy(u => u.NetId))
                    if (c.OrderShoot(enemy) != null) { shots++; break; }
                yield return null;
            }
            c.EndTurn();
            var orders = MultiplayerSession.Active.Sent.ToList();
            long final = c.StateChecksum();
            var health = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None)
                .ToDictionary(s => $"{s.Unit.NetId}/{s.name}", s => (s.Health, s.IsAlive));
            Assert.That(orders.Last().kind, Is.EqualTo(OrderKinds.EndTurn));
            Debug.Log($"[MultiplayerPlayTests] South gave {orders.Count} orders, {shots} volleys.");
            Assert.That(shots, Is.GreaterThan(0), "some volleys were fired, so the dice are replayed too");

            // Game 2: north's game replays them (the same armies, map and dice seed).
            yield return StartBattle(Side.AI);
            c = Controller;
            var session = MultiplayerSession.Active;
            foreach (var o in orders) session.Inject(o);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.That(session.Desyncs, Is.EqualTo(0), "every order gave the same state in both games");
            Assert.That(c.StateChecksum(), Is.EqualTo(final));
            var health2 = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None)
                .ToDictionary(s => $"{s.Unit.NetId}/{s.name}", s => (s.Health, s.IsAlive));
            Assert.That(health2, Is.EquivalentTo(health));
            Assert.That(c.IsRemoteTurn, Is.False, "now it's north's turn");
            Assert.That(Turns.ActiveSide, Is.EqualTo(Side.AI));
            Assert.That(c.SelectNextUnit(), Is.True, "north can command their units");
        }

        [UnityTest]
        public IEnumerator AnOrderThatGivesADifferentResultIsNoticed()
        {
            yield return StartBattle(Side.AI);
            var c = Controller;
            var session = MultiplayerSession.Active;
            var south = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.Player).OrderBy(u => u.NetId).First();
            session.Inject(new OrderMsg { kind = OrderKinds.Select, unit = south.NetId, checksum = 12345 });
            yield return null;
            Assert.That(c.ActiveUnit, Is.SameAs(south), "the other player's unit is selected for them");
            Assert.That(session.Desyncs, Is.EqualTo(1), "the checksum didn't match");
        }

        [UnityTest]
        public IEnumerator VictoryAndDefeatAreEachPlayersOwn()
        {
            yield return StartBattle(Side.AI);
            var c = Controller;
            // Kill everyone on the south side: north (this player) wins.
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None).Where(s => s.Unit.Side == Side.Player).ToList())
                s.Kill();
            Victory.Check();
            yield return null;
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.AIWins), "the north side won");
            Assert.That(c.BattleOver, Is.True);
            c.Restart();   // not in multiplayer
            yield return null;
            Assert.That(Controller, Is.SameAs(c), "the battle wasn't restarted");
        }
    }
}
