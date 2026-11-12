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
    /// The Medium Tank (Heavy Support, every faction): one soldier, Health 20, Armor 4, Accuracy 75%, Move 30 m; Mechanical
    /// (can't sprint), Tank, Unstable; a 100mm Cannon (Damage 10, Pen 4, Range 50 m, Shots 1, Manual Load: once a turn).
    /// </summary>
    public class MediumTankTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();

        private static UnitTemplate Unit() => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/MediumTank.asset");
        private static SoldierTemplate Tank() => AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/MediumTank.asset");
        private static WeaponTemplate Cannon() => AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/Cannon100mm.asset");

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o is Component c ? c.gameObject : o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit Spawn(UnitTemplate template, Side side, Vector3 at)
        {
            var unit = UnitFactory.Spawn(template, side, at, side == Side.Player ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
            created.Add(unit);
            return unit;
        }

        [Test]
        public void TheMediumTankIsAsDesigned()
        {
            var unit = Unit();
            Assert.That((unit.displayName, unit.Size, unit.role), Is.EqualTo(("Medium Tank", 1, UnitRole.HeavySupport)));
            Assert.That(unit.leader, Is.SameAs(Tank()));
            var t = Tank();
            Assert.That(t.displayName, Is.EqualTo("Medium Tank"));
            Assert.That((t.health, t.armorLevel, t.accuracy, t.movementRange), Is.EqualTo((20, 4, 75, 30f)));
            Assert.That(t.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Mechanical", "Tank", "Unstable" }));
            Assert.That(t.HasKeyword("Biological"), Is.False, "a machine");
            Assert.That(t.weapons.Single(), Is.SameAs(Cannon()));
            Assert.That(t.PrimaryWeapon, Is.SameAs(Cannon()));
            Assert.That(t.prefab, Is.Not.Null);
        }

        [Test]
        public void The100mmCannonIsAsDesigned()
        {
            var c = Cannon();
            Assert.That(c.displayName, Is.EqualTo("100mm Cannon"));
            Assert.That((c.baseDamage, c.PenetrationValue, c.range, c.shots, c.accuracyBonus), Is.EqualTo((10, (int?)4, 50f, 1, 0)));
            Assert.That(c.keywords.Select(k => k.displayName), Is.EqualTo(new[] { "Manual Load" }));
        }

        [Test]
        public void EveryFactionCanTakeMediumTanksAndMoreThanOne()
        {
            foreach (var f in UnitCatalog.Load().Factions)
                Assert.That(f.Has(Unit()), Is.True, f.displayName);
            var commander = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/FieldCommander.asset");
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { commander, Unit(), Unit(), Unit() }), Is.True, "Heavy Support isn't unique");
        }

        [Test]
        public void MechanicalSoldiersCantSprint()
        {
            var tank = Spawn(Unit(), Side.Player, Vector3.zero).Leader;
            Assert.That(tank.CanSprint, Is.False);
            Assert.That((tank.Move, tank.Sprint), Is.EqualTo((30f, 30f)), "a normal move only");

            var none = new Dictionary<Soldier, Vector3>();
            var from = new Vector3(0f, 0f, -20f);
            var near = MovementRules.CheckLeaderMove(tank, from, new Vector3(0f, 0f, 9f), none, StraightLineMeasurer.Instance);
            Assert.That((near.Ok, near.Kind), Is.EqualTo((true, MoveKind.Normal)), "29 m: a normal move");
            var tooFar = MovementRules.CheckLeaderMove(tank, from, new Vector3(0f, 0f, 15f), none, StraightLineMeasurer.Instance);
            Assert.That(tooFar.Ok, Is.False, "35 m would be a sprint");
            Assert.That(tooFar.Reason, Does.Contain("can't sprint"));

            var infantry = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/InfantrySquad.asset");
            var sergeant = Spawn(infantry, Side.Player, new Vector3(20f, 0f, 0f)).Leader;
            Assert.That((sergeant.CanSprint, sergeant.Sprint), Is.EqualTo((true, 20f)), "others still can");
        }

        [Test]
        public void ManualLoadFiresOnceATurn()
        {
            var infantry = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/InfantrySquad.asset");
            var tank = Spawn(Unit(), Side.Player, Vector3.zero);
            var enemy = Spawn(infantry, Side.AI, new Vector3(0f, 0f, 20f));
            var cannon = tank.Leader.Weapons.Single();

            var first = UnitShooting.Resolve(tank, enemy, false, new System.Random(1));
            Assert.That(first.Shots.Single().Attack, Is.Not.Null, "the first volley fires");
            Assert.That(WeaponUse.WhyCannotUse(tank.Leader, cannon), Does.Contain("Manual Load"));

            var second = UnitShooting.Resolve(tank, enemy, false, new System.Random(2));
            Assert.That(second.Shots.Single().Attack, Is.Null, "not a second time this turn");
            Assert.That(second.Shots.Single().Note, Does.Contain("Manual Load"));

            Turns.StartNextTurn();
            Assert.That(WeaponUse.CanUse(tank.Leader, cannon), Is.True, "loaded again next turn");
        }

        [Test]
        public void MechanicalSoldiersCantBeInCloseRange()
        {
            var infantry = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/InfantrySquad.asset");
            var tank = Spawn(Unit(), Side.Player, Vector3.zero).Leader;
            var enemy = Spawn(infantry, Side.AI, new Vector3(0f, 0f, 1.5f));
            Assert.That(CloseRange.IsInCloseRange(tank), Is.False, "an enemy 1.5 m away");
            Assert.That(WeaponUse.CanUse(tank, tank.Weapons.Single()), Is.True, "so the cannon can still fire");

            // Even a Biological soldier with the Mechanical keyword is never in close range.
            var friend = Spawn(infantry, Side.Player, new Vector3(0f, 0f, 3f)).Leader;
            Assert.That(CloseRange.IsInCloseRange(friend), Is.True, "a normal soldier would be");
            friend.GainKeyword(KeywordLibrary.Get(MovementRules.MechanicalKeyword));
            Assert.That(CloseRange.IsInCloseRange(friend), Is.False);
        }

        [Test]
        public void OnlyHeavyArmorPiercingWeaponsHurtIt()
        {
            int Damage(int dmg, int? pen) => ArmorPenetration.Resolve(dmg, pen, 4).Damage;
            Assert.That(Damage(4, 1), Is.EqualTo(0), "Assault Rifle");
            Assert.That(Damage(5, 1), Is.EqualTo(0), "Machine Gun");
            Assert.That(Damage(10, 4), Is.EqualTo(10), "another tank's 100mm Cannon: full damage");
        }

        [Test]
        public void TheArmyBuilderShowsItCantSprint()
        {
            Assert.That(UnitInfo.SoldierLines(Unit()).Single(),
                Is.EqualTo("Medium Tank — Health 20 · Armor 4 · Accuracy 75% · Move 30 m (can't sprint) · 100mm Cannon"));
            Assert.That(UnitInfo.WeaponLines(Unit()).Single(),
                Is.EqualTo("100mm Cannon — Damage 10 · Shots 1 · Penetration 4 · Range 50 m · Manual Load"));
            Assert.That(UnitInfo.AbilityLines(Unit()).Single(), Is.EqualTo("Frag Grenade (active)"), "no once per battle");
        }

        // ---- Frag Grenade: the infantry's grenade, but with no once-per-battle limit ----

        private static Ability TankGrenade() => AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/TankFragGrenade.asset");

        [Test]
        public void TheTankHasAFragGrenadeWithNoLimitPerBattle()
        {
            var infantry = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/FragGrenade.asset");
            var g = TankGrenade();
            Assert.That(Tank().abilities, Is.EqualTo(new[] { g }));
            Assert.That(g.displayName, Is.EqualTo("Frag Grenade"));
            Assert.That((g.kind, g.targets, g.range, g.areaRadius, g.areaWeapon, g.usesPerTurn),
                Is.EqualTo((infantry.kind, infantry.targets, infantry.range, infantry.areaRadius, infantry.areaWeapon, infantry.usesPerTurn)), "the same grenade");
            Assert.That(g.usesPerBattle, Is.EqualTo(0), "no limit per battle");
            Assert.That(infantry.usesPerBattle, Is.EqualTo(1), "the infantry's is still once per battle");
        }

        [Test]
        public void TheTankCanThrowAGrenadeEveryTurn()
        {
            var tank = Spawn(Unit(), Side.Player, Vector3.zero);
            var g = TankGrenade();
            for (int turn = 0; turn < 4; turn++)
            {
                Assert.That(ActiveAbilities.Use(tank.Leader, g, targetPoint: new Vector3(0f, 0f, 8f)).Used, Is.True, $"turn {turn + 1}");
                Assert.That(ActiveAbilities.Use(tank.Leader, g, targetPoint: new Vector3(0f, 0f, 8f)).Used, Is.False, "once a turn");
                Turns.StartNextTurn();
            }
            Assert.That(tank.UsesLeftThisBattle(g), Is.EqualTo(int.MaxValue));
            Assert.That(Eldoria.Gameplay.MovementController.AbilityButtonLabel(tank.Leader, g), Is.EqualTo("Frag Grenade — 10 m"));
        }
    }
}
