using System.Collections.Generic;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>The win condition: the side with no soldiers left loses.</summary>
    public class VictoryTests
    {
        private readonly List<Object> created = new List<Object>();
        private SoldierTemplate shooterT, fragileT;
        private Ability frag;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
            var rifle = Make<WeaponTemplate>();
            rifle.baseDamage = 4;
            rifle.penetration = 1;
            var grenade = Make<WeaponTemplate>();
            grenade.baseDamage = 2;
            frag = Make<Ability>();
            frag.kind = AbilityKind.Active; frag.targets = AbilityTarget.Point; frag.affects = AuraAffects.Everyone;
            frag.range = 10f; frag.areaRadius = 2.5f; frag.areaWeapon = grenade;

            shooterT = Make<SoldierTemplate>();
            shooterT.health = 10;
            shooterT.accuracy = 100;
            shooterT.weapons.Add(rifle);
            shooterT.abilities = new List<Ability> { frag };
            fragileT = Make<SoldierTemplate>();
            fragileT.health = 1;
            fragileT.weapons.Add(rifle);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit MakeUnit(string name, Side side, SoldierTemplate t, params Vector3[] positions)
        {
            var go = new GameObject(name);
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, name);
            for (int i = 0; i < positions.Length; i++)
            {
                var s = new GameObject($"{name}_{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = positions[i];
                s.AddComponent<Soldier>().Configure(t, i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        [Test]
        public void TheBattleGoesOnWhileBothSidesHaveSoldiers()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, fragileT, new Vector3(0, 0, 10), new Vector3(3, 0, 10));
            MakeUnit("Them too", Side.AI, fragileT, new Vector3(-20, 0, 10));
            UnitShooting.Resolve(us, them, false, new System.Random(1)); // kills one of them
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(Victory.SoldiersLeft(Side.AI), Is.EqualTo(2));
        }

        [Test]
        public void WhenTheAIHasNoSoldiersLeftThePlayerWins()
        {
            // Each shooter picks the enemy straight ahead of them (the closest), so both enemies are shot.
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero, new Vector3(3, 0, 0));
            var them = MakeUnit("Them", Side.AI, fragileT, new Vector3(0, 0, 10), new Vector3(3, 0, 10));
            var ended = new List<BattleOutcome>();
            System.Action<BattleOutcome> handler = o => ended.Add(o);
            Victory.BattleEnded += handler;
            try
            {
                UnitShooting.Resolve(us, them, false, new System.Random(1));
            }
            finally
            {
                Victory.BattleEnded -= handler;
            }
            Assert.That(them.IsDestroyed, Is.True);
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.PlayerWins));
            Assert.That(ended, Is.EqualTo(new[] { BattleOutcome.PlayerWins }), "announced once");
        }

        [Test]
        public void ItIsDecidedOnlyWhenTheWholeSideIsGoneAcrossAllItsUnits()
        {
            var a = MakeUnit("A", Side.Player, fragileT, Vector3.zero);
            var b = MakeUnit("B", Side.Player, fragileT, new Vector3(20, 0, 0));
            MakeUnit("Enemy", Side.AI, fragileT, new Vector3(0, 0, 30));
            a.Soldiers[0].Kill();
            Assert.That(Victory.IsOver, Is.False, "the player still has unit B");
            b.Soldiers[0].Kill();
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.AIWins));
        }

        [Test]
        public void IfBothSidesLoseTheirLastSoldiersTogetherItIsADraw()
        {
            var thrower = MakeUnit("Thrower", Side.Player, shooterT, Vector3.zero).Soldiers[0];
            var mine = MakeUnit("Mine", Side.Player, fragileT, new Vector3(0, 0, 7));
            MakeUnit("Theirs", Side.AI, fragileT, new Vector3(0, 0, 9));
            thrower.TakeDamage(9); // 1 health left
            thrower.transform.position = new Vector3(0, 0, 6);
            ActiveAbilities.Use(thrower, frag, targetPoint: new Vector3(0, 0, 8));
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.Draw));
        }

        [Test]
        public void OnceDecidedTheOutcomeDoesntChange()
        {
            MakeUnit("Us", Side.Player, fragileT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, fragileT, new Vector3(0, 0, 10));
            them.Soldiers[0].Kill();
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.PlayerWins));
            Assert.That(Victory.Check(), Is.EqualTo(BattleOutcome.PlayerWins));
            Turns.ResetCounter();
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.InProgress), "a new battle starts undecided");
        }
    }
}
