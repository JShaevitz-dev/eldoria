using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>
    /// Sniper (Health 5, Armor 0, Accuracy 100%, 15 m; Biological, Solo, Sniper Precision): a solo unit. A Sniper Precision
    /// soldier shoots only the enemy soldier the player picked; they make no choice of their own.
    /// </summary>
    public class SniperTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword precision;
        private WeaponTemplate rifleT, longRifleT;
        private SoldierTemplate sniperT, trooperT, targetT;

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
            precision = Make<Keyword>();
            precision.displayName = "Sniper Precision";

            rifleT = Make<WeaponTemplate>();
            rifleT.baseDamage = 4; rifleT.penetration = 1; rifleT.range = 20f;
            longRifleT = Make<WeaponTemplate>();
            longRifleT.baseDamage = 5; longRifleT.penetration = 1; longRifleT.range = 50f;

            sniperT = Soldier(longRifleT);
            sniperT.keywords.Add(precision);
            trooperT = Soldier(rifleT);
            targetT = Soldier(rifleT);
            targetT.health = 20;
        }

        private SoldierTemplate Soldier(WeaponTemplate weapon)
        {
            var t = Make<SoldierTemplate>();
            t.health = 5;
            t.accuracy = 100;
            t.weapons.Add(weapon);
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit MakeUnit(string name, Side side, params (SoldierTemplate t, Vector3 pos)[] soldiers)
        {
            var go = new GameObject(name);
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, name);
            for (int i = 0; i < soldiers.Length; i++)
            {
                var s = new GameObject($"{name}_{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = soldiers[i].pos;
                s.AddComponent<Soldier>().Configure(soldiers[i].t, i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        [Test]
        public void TheSniperShootsThePickedSoldierAndTheOthersShootTheClosest()
        {
            var us = MakeUnit("Us", Side.Player, (trooperT, Vector3.zero), (sniperT, new Vector3(2, 0, 0)));
            var them = MakeUnit("Them", Side.AI, (targetT, new Vector3(0, 0, 10)), (targetT, new Vector3(6, 0, 18)));
            var picked = them.Soldiers[1];   // the farther one

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1), chosenTarget: picked);
            Assert.That(r.Shots.Single(s => s.Shooter == us.Soldiers[1]).Target, Is.EqualTo(picked), "the sniper takes the picked soldier");
            Assert.That(r.Shots.Single(s => s.Shooter == us.Soldiers[0]).Target, Is.EqualTo(them.Soldiers[0]), "the trooper shoots the closest");
        }

        [Test]
        public void TheSniperMakesNoChoiceOfTheirOwn()
        {
            // With no soldier picked, the sniper doesn't shoot (the AI and the player always pick one).
            var us = MakeUnit("Us", Side.Player, (sniperT, Vector3.zero));
            var them = MakeUnit("Them", Side.AI, (targetT, new Vector3(0, 0, 10)), (targetT, new Vector3(0, 0, 30)));
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Target, Is.Null);
            Assert.That(r.Shots[0].Note, Does.Contain("no soldier picked"));
            Assert.That(UnitShooting.SoldiersAbleToShoot(us, them), Is.EqualTo(0), "so an order with no pick is refused");
        }

        [Test]
        public void IfTheSniperCantTargetThePickedSoldierTheyDontShootAndTheOrderIsRefused()
        {
            var us = MakeUnit("Us", Side.Player, (sniperT, Vector3.zero));
            var them = MakeUnit("Them", Side.AI, (targetT, new Vector3(0, 0, 10)), (targetT, new Vector3(0, 0, 60)));
            var tooFar = them.Soldiers[1];   // 60 m: beyond 50 m
            Assert.That(UnitShooting.SoldiersAbleToShoot(us, them, tooFar), Is.EqualTo(0), "refused: pick another soldier");
            Assert.That(UnitShooting.SoldiersAbleToShoot(us, them, them.Soldiers[0]), Is.EqualTo(1));
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1), chosenTarget: tooFar);
            Assert.That(r.Shots[0].Target, Is.Null, "no fallback to another soldier");
            Assert.That(r.Shots[0].Note, Does.Contain("can't target"));
        }

        [Test]
        public void ThePickMustBeInTheUnitBeingShotAt()
        {
            var us = MakeUnit("Us", Side.Player, (sniperT, Vector3.zero));
            var them = MakeUnit("Them", Side.AI, (targetT, new Vector3(0, 0, 10)));
            var others = MakeUnit("Others", Side.AI, (targetT, new Vector3(0, 0, 20)));
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1), chosenTarget: others.Soldiers[0]);
            Assert.That(r.Shots[0].Target, Is.Null);
        }

        [Test]
        public void WithNoValidTargetsAnywhereTheUnitCantShoot()
        {
            var us = MakeUnit("Us", Side.Player, (sniperT, Vector3.zero));
            var far = MakeUnit("Far", Side.AI, (targetT, new Vector3(0, 0, 70)));   // beyond the 50 m rifle
            Assert.That(UnitShooting.HasAnyTarget(us), Is.False, "it can use an ability, or do nothing");
            far.Soldiers[0].transform.position = new Vector3(0, 0, 40);
            Assert.That(UnitShooting.HasAnyTarget(us), Is.True);
        }

        // ---- The AI's sniper: Solo soldiers and sergeants first, otherwise the closest ----

        private SoldierTemplate Named(string name, bool solo = false)
        {
            var t = Soldier(rifleT);
            t.displayName = name;
            t.health = 20;
            if (solo)
            {
                var k = Make<Keyword>();
                k.displayName = "Solo";
                t.keywords.Add(k);
            }
            return t;
        }

        [Test]
        public void WithNoValuableTargetTheAIsSniperShootsTheClosest()
        {
            var ai = MakeUnit("AI Sniper", Side.AI, (sniperT, Vector3.zero));
            var them = MakeUnit("Them", Side.Player, (targetT, new Vector3(0, 0, 30)), (targetT, new Vector3(0, 0, 12)));
            var action = AI.AIPlanner.ChooseAction(ai);
            Assert.That(action.Kind, Is.EqualTo(AI.AIActionKind.Shoot));
            Assert.That(action.ShootAtSoldier, Is.EqualTo(them.Soldiers[1]));
            Assert.That(action.ShootAt, Is.EqualTo(them));
        }

        [Test]
        public void TheAIsSniperPrefersASoloSoldierOverACloserTrooper()
        {
            var ai = MakeUnit("AI Sniper", Side.AI, (sniperT, Vector3.zero));
            MakeUnit("Squad", Side.Player, (targetT, new Vector3(0, 0, 8)), (targetT, new Vector3(2, 0, 8)));
            var commander = MakeUnit("Commander", Side.Player, (Named("Field Commander", solo: true), new Vector3(0, 0, 35))).Soldiers[0];
            var action = AI.AIPlanner.ChooseAction(ai);
            Assert.That(action.ShootAtSoldier, Is.EqualTo(commander), "from any enemy unit");
            Assert.That(action.ShootAt, Is.EqualTo(commander.Unit));
        }

        [Test]
        public void TheAIsSniperPrefersASergeant()
        {
            var ai = MakeUnit("AI Sniper", Side.AI, (sniperT, Vector3.zero));
            var squad = MakeUnit("Squad", Side.Player, (Named("Sergeant"), new Vector3(0, 0, 25)), (targetT, new Vector3(2, 0, 9)));
            var action = AI.AIPlanner.ChooseAction(ai);
            Assert.That(action.ShootAtSoldier, Is.EqualTo(squad.Soldiers[0]));
        }

        [Test]
        public void AmongValuableTargetsTheClosest()
        {
            var ai = MakeUnit("AI Sniper", Side.AI, (sniperT, Vector3.zero));
            var far = MakeUnit("Far", Side.Player, (Named("Battle Priest", solo: true), new Vector3(0, 0, 40))).Soldiers[0];
            var near = MakeUnit("Near", Side.Player, (Named("Sergeant in Powered Armor"), new Vector3(0, 0, 20))).Soldiers[0];
            Assert.That(AI.AIPlanner.ChooseAction(ai).ShootAtSoldier, Is.EqualTo(near));
        }

        [Test]
        public void AValuableTargetOutOfReachIsSkipped()
        {
            var ai = MakeUnit("AI Sniper", Side.AI, (sniperT, Vector3.zero));
            MakeUnit("Commander", Side.Player, (Named("Field Commander", solo: true), new Vector3(0, 0, 70)));   // beyond 50 m
            var trooper = MakeUnit("Squad", Side.Player, (targetT, new Vector3(0, 0, 15))).Soldiers[0];
            Assert.That(AI.AIPlanner.ChooseAction(ai).ShootAtSoldier, Is.EqualTo(trooper));
        }

        [Test]
        public void ATrooperWhoTookOverCommandIsNotASergeant()
        {
            var squad = MakeUnit("Squad", Side.Player, (Named("Sergeant"), new Vector3(0, 0, 25)), (targetT, new Vector3(2, 0, 25)));
            squad.Soldiers[0].Kill();
            Assert.That(squad.Leader, Is.EqualTo(squad.Soldiers[1]));
            Assert.That(AI.AIPlanner.IsValuableTarget(squad.Soldiers[1]), Is.False);
        }

        [Test]
        public void TheRealSniperUnitShootsThePickedSoldierWithTheSniperRifle()
        {
            var template = AssetDatabase.LoadAssetAtPath<UnitTemplate>("Assets/_Project/Data/Units/Sniper.asset");
            var sniperUnit = UnitFactory.Spawn(template, Side.Player, Vector3.zero, Quaternion.identity);
            created.Add(sniperUnit.gameObject);
            var sniper = sniperUnit.Leader;
            var them = MakeUnit("Them", Side.AI, (targetT, new Vector3(0, 0, 10)), (targetT, new Vector3(3, 0, 45)));
            var picked = them.Soldiers[1];   // 45 m away: only the Sniper Rifle reaches

            var weapon = sniper.WeaponFor(null);
            Assert.That(weapon.Name, Is.EqualTo("Sniper Rifle"));
            var accuracy = Rules.Accuracy.ForAttack(sniper, weapon, sprinted: false);
            Assert.That((accuracy.Final, accuracy.HitChance, accuracy.CritChance), Is.EqualTo((150, 100, 50)), "100% + 50: always hits, 50% crit");

            var r = UnitShooting.Resolve(sniperUnit, them, false, new System.Random(3), chosenTarget: picked);
            Assert.That(r.Shots.Single().Target, Is.EqualTo(picked));
            Assert.That(r.Shots.Single().Attack.Hit, Is.True);
            Assert.That(picked.Health, Is.LessThan(20));

            // Deployed: after moving, the Sniper can't shoot (no other weapon), so there are no valid targets.
            sniper.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(UnitShooting.HasAnyTarget(sniperUnit), Is.False);
        }

        [Test]
        public void TheSniperIsAsDesigned()
        {
            var t = AssetDatabase.LoadAssetAtPath<SoldierTemplate>("Assets/_Project/Data/Soldiers/Sniper.asset");
            Assert.That(t.displayName, Is.EqualTo("Sniper"));
            Assert.That((t.health, t.armorLevel, t.accuracy, t.movementRange), Is.EqualTo((5, 0, 100, 15f)));
            Assert.That(t.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Biological", "Solo", "Sniper Precision" }));
            var unit = AssetDatabase.LoadAssetAtPath<UnitTemplate>("Assets/_Project/Data/Units/Sniper.asset");
            Assert.That((unit.displayName, unit.leader, unit.Size), Is.EqualTo(("Sniper", t, 1)), "a solo unit");
            Assert.That(t.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Sniper Rifle" }));
            Assert.That(t.PrimaryWeapon.displayName, Is.EqualTo("Sniper Rifle"));
            Assert.That(t.prefab, Is.Not.Null);
            var kw = t.keywords.Single(k => k.Is("Sniper Precision"));
            Assert.That((kw.appliesTo, kw.category), Is.EqualTo((KeywordTarget.Soldier, KeywordCategory.Trait)));
        }

        [Test]
        public void ThereIsNoPlainSniperKeyword()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Keyword>("Assets/_Project/Data/Keywords/Sniper.asset"), Is.Null, "redundant with Sniper Precision");
            var library = Resources.Load<KeywordLibrary>(KeywordLibrary.ResourcePath);
            Assert.That(library.keywords.Any(k => k != null && k.Is("Sniper")), Is.False);
        }
    }
}
