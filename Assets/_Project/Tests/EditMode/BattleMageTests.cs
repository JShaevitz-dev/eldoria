using System.Collections.Generic;
using System.Linq;
using Eldoria.AI;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>
    /// Battle Mage (United States of Liberteria only, Specialist): Health 5, Armor 0, Accuracy 75%, Move 20 m; Biological,
    /// Solo, Officer, Magic; Pistol. Spells: Firestorm (an enemy unit within 10 m: 3 damage to every Biological soldier,
    /// always hits, ignores armor) and Fireball (aimed attack: Damage 10, Penetration X, Range 20 m, +0).
    /// </summary>
    public class BattleMageTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();

        private static UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/{file}.asset");
        private static Ability A(string file) => AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/{file}.asset");
        private static Faction F(string file) => AssetDatabase.LoadAssetAtPath<Faction>($"{Data}/Factions/{file}.asset");

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

        private Unit Spawn(string file, Side side, Vector3 at)
        {
            var unit = UnitFactory.Spawn(U(file), side, at, side == Side.Player ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
            created.Add(unit);
            return unit;
        }

        [Test]
        public void TheBattleMageIsAsDesigned()
        {
            var unit = U("BattleMage");
            Assert.That((unit.displayName, unit.Size, unit.role), Is.EqualTo(("Battle Mage", 1, UnitRole.Specialist)));
            var m = unit.leader;
            Assert.That(m.displayName, Is.EqualTo("Battle Mage"));
            Assert.That((m.health, m.armorLevel, m.accuracy, m.movementRange), Is.EqualTo((5, 0, 75, 20f)));
            Assert.That(m.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Biological", "Solo", "Officer", "Magic" }));
            Assert.That(m.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Pistol" }));
            Assert.That(m.abilities, Is.EqualTo(new[] { A("Firestorm"), A("Fireball") }));
            Assert.That(m.abilities.All(a => a.IsSpell), Is.True, "both are spells");
            Assert.That(m.prefab, Is.Not.Null);
        }

        [Test]
        public void OnlyLiberteriaCanTakeTheBattleMage()
        {
            Assert.That(F("UnitedStatesOfLiberteria").Has(U("BattleMage")), Is.True);
            Assert.That(F("UnitedKingdomOfAelfland").Has(U("BattleMage")), Is.False);
            Assert.That(F("DraksaReich").Has(U("BattleMage")), Is.False);
            var commander = U("FieldCommander");
            Assert.That(ArmyRules.Problems(new List<UnitTemplate> { commander, U("BattleMage") }, F("DraksaReich")).Single(),
                Does.Contain("Battle Mage isn't available to The Draksa Reich"));
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { commander, U("BattleMage") }, F("UnitedStatesOfLiberteria")), Is.True);
        }

        [Test]
        public void TheSpellsAreAsDesigned()
        {
            var firestorm = A("Firestorm");
            Assert.That((firestorm.targets, firestorm.affects, firestorm.range, firestorm.targetMustHave.displayName),
                Is.EqualTo((AbilityTarget.Unit, AuraAffects.Enemies, 10f, "Biological")));
            Assert.That(firestorm.IsUnitAttack, Is.True);
            Assert.That((firestorm.attackWeapon.baseDamage, firestorm.attackWeapon.PenetrationValue), Is.EqualTo((3, (int?)null)));

            var fireball = A("Fireball");
            Assert.That((fireball.targets, fireball.IsAimedAttack), Is.EqualTo((AbilityTarget.Soldier, true)));
            var w = fireball.attackWeapon;
            Assert.That((w.displayName, w.baseDamage, w.PenetrationValue, w.range, w.accuracyBonus, w.shots),
                Is.EqualTo(("Fireball", 10, (int?)null, 20f, 0, 1)));
        }

        [Test]
        public void FirestormHitsEveryBiologicalSoldierForThreeAlwaysAndIgnoringArmor()
        {
            var mage = Spawn("BattleMage", Side.Player, Vector3.zero).Leader;
            var squad = Spawn("PoweredArmorSquad", Side.AI, new Vector3(0f, 0f, 8f));   // Armor 2: armor would halve or stop most damage
            var r = ActiveAbilities.Use(mage, A("Firestorm"), targetUnit: squad);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(r.AreaHits.Count, Is.EqualTo(5));
            Assert.That(squad.Soldiers.All(s => s.Health == s.MaxHealth - 3), Is.True, "every one takes 3: no roll, armor ignored");
        }

        [Test]
        public void FirestormOnlyAffectsBiologicalSoldiersAndNeedsOne()
        {
            var mage = Spawn("BattleMage", Side.Player, Vector3.zero).Leader;
            var tank = Spawn("MediumTank", Side.AI, new Vector3(0f, 0f, 8f));
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Firestorm"), targetUnit: tank), Does.Contain("no Biological soldiers"));
        }

        [Test]
        public void FirestormReachesTenMetresToTheUnitsClosestSoldier()
        {
            var mage = Spawn("BattleMage", Side.Player, Vector3.zero).Leader;
            var near = Spawn("InfantrySquad", Side.AI, new Vector3(0f, 0f, 9.5f));
            var far = Spawn("InfantrySquad", Side.AI, new Vector3(30f, 0f, 12f));
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Firestorm"), targetUnit: near), Is.Null);
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Firestorm"), targetUnit: far), Does.Contain("Out of range (10 m)"));
        }

        [Test]
        public void FireballIsAnAimedShotForTenIgnoringArmor()
        {
            var mage = Spawn("BattleMage", Side.Player, Vector3.zero).Leader;
            var tank = Spawn("MediumTank", Side.AI, new Vector3(0f, 0f, 15f)).Leader;
            var far = Spawn("MediumTank", Side.AI, new Vector3(12f, 0f, 30f)).Leader;
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Fireball"), far), Does.Contain("Out of range (20 m)"));

            var r = ActiveAbilities.Use(mage, A("Fireball"), targetSoldier: tank);
            Assert.That(r.Used, Is.True, r.Reason);
            var shot = r.Attacks.Single();
            Assert.That(shot.Accuracy.Final, Is.EqualTo(75), "a hit roll at the mage's 75%");
            Assert.That(tank.Health, Is.EqualTo(shot.Hit ? 20 - 10 : 20), "10 damage, armor 4 ignored");
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Fireball"), tank), Does.Contain("already been used this turn"), "once a turn");
        }

        [Test]
        public void SpellsCantBeCastAfterSprintingButWorkInCloseRange()
        {
            var unit = Spawn("BattleMage", Side.Player, Vector3.zero);
            var mage = unit.Leader;
            var enemies = Spawn("InfantrySquad", Side.AI, new Vector3(0f, 0f, 1.5f));
            Assert.That(CloseRange.IsInCloseRange(mage), Is.True);
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Firestorm"), targetUnit: enemies), Is.Null, "magic works in close range");

            unit.SetSprintedThisTurn(true);
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Firestorm"), targetUnit: enemies), Does.Contain("can't be cast after sprinting"));
            Assert.That(ActiveAbilities.WhyCannotUse(mage, A("Fireball"), enemies.Leader), Does.Contain("can't be cast after sprinting"));
        }

        [Test]
        public void TheAICastsFirestormOnASquadAndFireballOtherwise()
        {
            var mage = Spawn("BattleMage", Side.AI, new Vector3(0f, 0f, 8f));
            var squad = Spawn("InfantrySquad", Side.Player, Vector3.zero);
            var action = AIPlanner.ChooseAction(mage);
            Assert.That((action.Kind, action.Ability, action.TargetUnit), Is.EqualTo((AIActionKind.Ability, A("Firestorm"), squad)));

            Object.DestroyImmediate(squad.gameObject);
            var sniper = Spawn("Sniper", Side.Player, new Vector3(0f, 0f, -6f));
            var single = AIPlanner.ChooseAction(mage);
            Assert.That((single.Kind, single.Ability, single.TargetSoldier), Is.EqualTo((AIActionKind.Ability, A("Fireball"), sniper.Leader)),
                "one soldier: a Fireball, not a Firestorm");
        }

        [Test]
        public void TheArmyBuilderShowsTheSpells()
        {
            var lines = UnitInfo.WeaponLines(U("BattleMage"));
            Assert.That(lines, Does.Contain("Firestorm — Damage 3 · Shots 1 · Penetration X · Range 10 m"));
            Assert.That(lines, Does.Contain("Fireball — Damage 10 · Shots 1 · Penetration X · Range 20 m"));
            Assert.That(UnitInfo.AbilityLines(U("BattleMage")), Is.EqualTo(new[] { "Firestorm (active, spell)", "Fireball (active, spell)" }));
        }
    }
}
