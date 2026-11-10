using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>
    /// Flamethrower (10 damage, no penetration value, 10 m; Incendiary, Unstable) and "on death" triggers:
    /// Incendiary - only usable on Biological targets; Unstable - when the holder dies, 10% chance to explode for 5
    /// damage to ANY soldier within 5 m.
    /// </summary>
    public class FlamethrowerTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, incendiary;
        private WeaponTemplate flamerT, explosionT, rifleT;
        private SoldierTemplate trooperT, robotT, armouredT;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        private Keyword Kw(string name)
        {
            var k = Make<Keyword>();
            k.displayName = name;
            return k;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
            KeywordTriggers.Rng = new System.Random(1);
            biological = Kw("Biological");
            incendiary = Kw("Incendiary");
            incendiary.interactions.Add(new KeywordInteraction
                { party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect });

            explosionT = Make<WeaponTemplate>();
            explosionT.displayName = "Unstable Explosion";
            explosionT.baseDamage = 5;
            explosionT.hasPenetration = false;
            rifleT = Make<WeaponTemplate>();
            rifleT.baseDamage = 4; rifleT.penetration = 1;

            trooperT = Soldier(5, 1, rifleT);
            trooperT.keywords.Add(biological);
            robotT = Soldier(10, 1, rifleT);
            armouredT = Soldier(20, 5, rifleT);
            armouredT.keywords.Add(biological);
        }

        /// <summary>A flamethrower whose Unstable explodes with the given chance.</summary>
        private WeaponTemplate Flamer(int chancePercent)
        {
            var unstable = Kw("Unstable");
            unstable.triggers.Add(new KeywordTrigger
            {
                when = TriggerEvent.Died, action = TriggerAction.Explode, chancePercent = chancePercent,
                explosionWeapon = explosionT, explosionRadius = 5f
            });
            var w = Make<WeaponTemplate>();
            w.displayName = "Flamethrower";
            w.baseDamage = 10;
            w.hasPenetration = false;
            w.range = 10f;
            w.keywords.Add(incendiary);
            w.keywords.Add(unstable);
            return w;
        }

        private SoldierTemplate Soldier(int health, int armor, params WeaponTemplate[] weapons)
        {
            var t = Make<SoldierTemplate>();
            t.health = health;
            t.armorLevel = armor;
            t.accuracy = 100;
            t.weapons.AddRange(weapons);
            return t;
        }

        private SoldierTemplate Burner(int chancePercent)
        {
            var t = Soldier(5, 1, Flamer(chancePercent));
            t.keywords.Add(biological);
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            KeywordTriggers.Rng = null;
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

        // ---- The flamethrower ----

        [Test]
        public void ItCanOnlyBeUsedOnBiologicalTargets()
        {
            var burner = MakeUnit("Burners", Side.Player, Burner(0), Vector3.zero);
            var them = MakeUnit("Them", Side.AI, robotT, new Vector3(0, 0, 4));        // closest, but not Biological
            var people = MakeUnit("People", Side.AI, trooperT, new Vector3(0, 0, 8));
            var r = Attack.Resolve(burner.Soldiers[0], burner.Soldiers[0].Weapons[0], them.Soldiers[0], false, new System.Random(1));
            Assert.That(r.NoEffect, Is.True);
            Assert.That(them.Soldiers[0].Health, Is.EqualTo(10));

            // Shooting at the robots' unit: no valid target there, so nobody can usefully shoot at it.
            Assert.That(UnitShooting.SoldiersAbleToShoot(burner, them), Is.EqualTo(0));
            Assert.That(UnitShooting.SoldiersAbleToShoot(burner, people), Is.EqualTo(1));
        }

        [Test]
        public void ItDoes10DamageWhateverTheArmor()
        {
            var burner = MakeUnit("Burners", Side.Player, Burner(0), Vector3.zero).Soldiers[0];
            var tank = MakeUnit("Tank", Side.AI, armouredT, new Vector3(0, 0, 6)).Soldiers[0];   // Armor Level 5
            var r = Attack.Resolve(burner, burner.Weapons[0], tank, false, new System.Random(1));
            Assert.That(r.Damage.Penetration.Result, Is.EqualTo(PenetrationResult.IgnoresArmor));
            Assert.That(tank.Health, Is.EqualTo(10));
        }

        // ---- Unstable: on-death explosion ----

        [Test]
        public void WhenTheHolderDiesTheyCanExplodeHittingAnySoldierWithin5Metres()
        {
            var burners = MakeUnit("Burners", Side.Player, Burner(100), Vector3.zero, new Vector3(30, 0, 0));
            var holder = burners.Soldiers[0];
            var friend = MakeUnit("Friends", Side.Player, armouredT, new Vector3(4.5f, 0, 0)).Soldiers[0];
            var foe = MakeUnit("Foes", Side.AI, trooperT, new Vector3(0, 0, 3)).Soldiers[0];
            var tooFar = MakeUnit("Far", Side.AI, trooperT, new Vector3(0, 0, 5.5f)).Soldiers[0];

            holder.TakeDamage(5);   // killed

            Assert.That(friend.Health, Is.EqualTo(15), "friends too, and armor 5 doesn't stop it: always 5");
            Assert.That(foe.IsAlive, Is.False, "5 damage kills a 5-health trooper");
            Assert.That(tooFar.Health, Is.EqualTo(5), "5.5 m away");
            Assert.That(foe.gameObject.activeSelf, Is.False, "removed once the explosion is over");
        }

        [Test]
        public void WithNoLuckTheHolderJustDies()
        {
            var holder = MakeUnit("Burners", Side.Player, Burner(0), Vector3.zero).Soldiers[0];
            var friend = MakeUnit("Friends", Side.Player, trooperT, new Vector3(2, 0, 0)).Soldiers[0];
            holder.Kill();
            Assert.That(friend.Health, Is.EqualTo(5));
        }

        [Test]
        public void TheChanceIsRolledEachTime()
        {
            // With a 10% chance, over many deaths roughly one in ten explode.
            KeywordTriggers.Rng = new System.Random(12345);
            int explosions = 0;
            System.Action<Soldier, AreaAttackResult> count = (s, r) => explosions++;
            KeywordTriggers.Exploded += count;
            try
            {
                var burnerT = Burner(10);
                for (int i = 0; i < 300; i++)
                    MakeUnit($"B{i}", Side.Player, burnerT, new Vector3(i * 20, 0, 0)).Soldiers[0].Kill();
            }
            finally
            {
                KeywordTriggers.Exploded -= count;
            }
            Assert.That(explosions, Is.InRange(15, 50), $"{explosions} of 300");
        }

        [Test]
        public void TerrainBetweenShieldsSoldiersFromTheExplosion()
        {
            var holder = MakeUnit("Burners", Side.Player, Burner(100), Vector3.zero).Soldiers[0];
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.layer = TerrainLayers.Structure;
            wall.transform.position = new Vector3(0, 1.5f, 2);
            wall.transform.localScale = new Vector3(6, 3, 0.3f);
            Physics.SyncTransforms();
            var behindWall = MakeUnit("Foes", Side.AI, trooperT, new Vector3(0, 0, 3.5f)).Soldiers[0];
            holder.Kill();
            Assert.That(behindWall.Health, Is.EqualTo(5));
        }

        [Test]
        public void ExplosionsCanSetOffAChainReaction()
        {
            var burnerT = Burner(100);
            var first = MakeUnit("A", Side.Player, burnerT, Vector3.zero).Soldiers[0];
            var second = MakeUnit("B", Side.Player, burnerT, new Vector3(4, 0, 0)).Soldiers[0];     // 5 health: dies to the blast
            var bystander = MakeUnit("C", Side.AI, trooperT, new Vector3(8, 0, 0)).Soldiers[0];   // 8 m from the first, 4 m from the second
            first.Kill();
            Assert.That(second.IsAlive, Is.False);
            Assert.That(bystander.IsAlive, Is.False, "caught by the second explosion");
        }

        [Test]
        public void OnDeathTriggersFireWhenShotDead()
        {
            var holder = MakeUnit("Burners", Side.AI, Burner(100), new Vector3(0, 0, 10)).Soldiers[0];
            var next = MakeUnit("Next", Side.AI, trooperT, new Vector3(1, 0, 10)).Soldiers[0];
            var shooters = MakeUnit("Shooters", Side.Player, Soldier(5, 1, rifleT), Vector3.zero, new Vector3(2, 0, 0));
            holder.TakeDamage(1);   // 4 left: one rifle hit (4) kills
            UnitShooting.Resolve(shooters, holder.Unit, false, new System.Random(1));
            Assert.That(holder.IsAlive, Is.False);
            Assert.That(next.Health, Is.EqualTo(0).Or.LessThan(5), "the explosion hit the soldier beside them");
        }

        [Test]
        public void AnExplosionThatWipesOutTheLastEnemyWinsTheBattle()
        {
            MakeUnit("Us", Side.Player, trooperT, new Vector3(0, 0, -20));
            var holder = MakeUnit("Burners", Side.AI, Burner(100), Vector3.zero).Soldiers[0];
            MakeUnit("Their others", Side.AI, trooperT, new Vector3(2, 0, 0));
            holder.Kill();
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.PlayerWins));
        }

        // ---- The data ----

        [Test]
        public void TheFlamethrowerIsAsDesigned()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/Flamethrower.asset");
            Assert.That((w.displayName, w.baseDamage, w.PenetrationValue, w.accuracyBonus, w.range),
                Is.EqualTo(("Flamethrower", 10, (int?)null, 0, 10f)));
            Assert.That(w.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Incendiary", "Unstable" }));

            var inc = w.keywords.Single(k => k.Is("Incendiary")).interactions.Single();
            Assert.That((inc.party, inc.condition, inc.keyword.displayName, inc.effect),
                Is.EqualTo((KeywordParty.Target, KeywordCondition.Lacks, "Biological", KeywordEffect.NoEffect)));

            var boom = w.keywords.Single(k => k.Is("Unstable")).triggers.Single();
            Assert.That((boom.when, boom.action, boom.chancePercent, boom.explosionRadius),
                Is.EqualTo((TriggerEvent.Died, TriggerAction.Explode, 10, 5f)));
            Assert.That((boom.explosionWeapon.baseDamage, boom.explosionWeapon.PenetrationValue), Is.EqualTo((5, (int?)null)));
        }
    }
}
