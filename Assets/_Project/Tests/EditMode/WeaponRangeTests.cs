using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>Weapon range: a soldier can only target enemies within their weapon's range (horizontal).</summary>
    public class WeaponRangeTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate rifleT, pistolT, mortarT;
        private SoldierTemplate rifleman, sergeant, mortarCrew, target;

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
            var biological = Make<Keyword>();
            biological.displayName = "Biological";
            var pistolKw = Make<Keyword>();
            pistolKw.displayName = "Pistol";
            var indirect = Make<Keyword>();
            indirect.displayName = "Indirect fire";
            indirect.interactions.Add(new KeywordInteraction { condition = KeywordCondition.Always, effect = KeywordEffect.IgnoresLineOfSight });

            rifleT = Weapon("Rifle", 4, 1, 20f);
            pistolT = Weapon("Pistol", 2, 0, 10f);
            pistolT.keywords.Add(pistolKw);
            mortarT = Weapon("Mortar", 4, 1, 30f);
            mortarT.keywords.Add(indirect);

            rifleman = Soldier(rifleT);
            sergeant = Soldier(rifleT, pistolT);
            sergeant.primaryWeapon = rifleT;
            mortarCrew = Soldier(mortarT);
            target = Soldier(rifleT);
            foreach (var t in new[] { rifleman, sergeant, mortarCrew, target }) t.keywords.Add(biological);
        }

        private WeaponTemplate Weapon(string name, int damage, int pen, float range)
        {
            var w = Make<WeaponTemplate>();
            w.displayName = name;
            w.baseDamage = damage;
            w.penetration = pen;
            w.range = range;
            return w;
        }

        private SoldierTemplate Soldier(params WeaponTemplate[] weapons)
        {
            var t = Make<SoldierTemplate>();
            t.health = 20;
            t.accuracy = 100;
            t.weapons.AddRange(weapons);
            return t;
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
        public void ARifleReaches20MetresButNoFarther()
        {
            var shooter = MakeUnit("Us", Side.Player, rifleman, Vector3.zero).Soldiers[0];
            var at20 = MakeUnit("A", Side.AI, target, new Vector3(0, 0, 20)).Soldiers[0];
            var at21 = MakeUnit("B", Side.AI, target, new Vector3(0, 0, 20.5f)).Soldiers[0];
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], at20), Is.True, "exactly 20 m is in range");
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], at21), Is.False);
            var r = Attack.Resolve(shooter, shooter.Weapons[0], at21, false, new System.Random(1));
            Assert.That(r.NotTargetableReason, Does.Contain("range"));
        }

        [Test]
        public void EachSoldierPicksTheClosestEnemyInRangeAndOthersDontShoot()
        {
            var us = MakeUnit("Us", Side.Player, rifleman, new Vector3(0, 0, 0), new Vector3(0, 0, -10));
            var them = MakeUnit("Them", Side.AI, target, new Vector3(0, 0, 18), new Vector3(30, 0, 0));
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Target, Is.EqualTo(them.Soldiers[0]), "18 m: in range");
            Assert.That(r.Shots[1].Target, Is.Null, "28 m and 31.6 m: both out of range");
            Assert.That(r.Shots[1].Note, Does.Contain("range"));
            Assert.That(UnitShooting.SoldiersAbleToShoot(us, them), Is.EqualTo(1));
        }

        [Test]
        public void NobodyInRangeMeansTheUnitCantShootAtAll()
        {
            var us = MakeUnit("Us", Side.Player, rifleman, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, target, new Vector3(0, 0, 25));
            Assert.That(UnitShooting.SoldiersAbleToShoot(us, them), Is.EqualTo(0));
        }

        [Test]
        public void InCloseRangeThePistolsShorterRangeApplies()
        {
            var sgt = MakeUnit("Us", Side.Player, sergeant, Vector3.zero).Soldiers[0];
            MakeUnit("Adjacent", Side.AI, target, new Vector3(1, 0, 0));     // puts the sergeant in close range
            var at15 = MakeUnit("Far", Side.AI, target, new Vector3(0, 0, 15)).Soldiers[0];
            var weapon = sgt.WeaponFor(null);
            Assert.That(weapon.Name, Is.EqualTo("Pistol"));
            Assert.That(Attack.CanTarget(sgt, weapon, at15), Is.False, "15 m is beyond the pistol's 10 m");
        }

        [Test]
        public void IndirectFireIgnoresLineOfSightButNotRange()
        {
            var crew = MakeUnit("Us", Side.Player, mortarCrew, Vector3.zero).Soldiers[0];
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.layer = Terrain.TerrainLayers.Structure;
            wall.transform.position = new Vector3(0, 2, 10);
            wall.transform.localScale = new Vector3(20, 4, 0.3f);
            Physics.SyncTransforms();
            var behindWall = MakeUnit("A", Side.AI, target, new Vector3(0, 0, 25)).Soldiers[0];
            var tooFar = MakeUnit("B", Side.AI, target, new Vector3(0, 0, 35)).Soldiers[0];
            Assert.That(Attack.CanTarget(crew, crew.Weapons[0], behindWall), Is.True);
            Assert.That(Attack.CanTarget(crew, crew.Weapons[0], tooFar), Is.False);
        }

        [Test]
        public void AWeaponWithNoRangeDefinedHasUnlimitedRange()
        {
            var noRange = Weapon("Old Rifle", 4, 1, 0f);   // range left undefined
            var shooterT = Soldier(noRange);
            var shooter = MakeUnit("Us", Side.Player, shooterT, Vector3.zero).Soldiers[0];
            var farAway = MakeUnit("Far", Side.AI, target, new Vector3(0, 0, 1000)).Soldiers[0];
            Assert.That(shooter.Weapons[0].HasRangeLimit, Is.False);
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], farAway), Is.True, "1 km away: still in range");
        }

        [Test]
        public void TheWeaponsHaveTheirDesignedRanges()
        {
            float Range(string file) => AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"Assets/_Project/Data/Weapons/{file}.asset").range;
            Assert.That(Range("AssaultRifle"), Is.EqualTo(20f));
            Assert.That(Range("Pistol"), Is.EqualTo(10f));
            Assert.That(Range("SMG"), Is.EqualTo(10f));
            Assert.That(Range("FragGrenade"), Is.EqualTo(0f), "the blast has no range of its own; the throw's 10 m applies");
        }
    }
}
