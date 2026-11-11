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
    /// Stahl Colossus (Draksa Reich only, Heavy Support): Health 15, Armor 3, Accuracy 75%, Move 15 m; Solo, Mechanical;
    /// Machine Gun and Iron Claw (Damage 10, Pen 0, Range 2 m, +25, Melee: usable in close range). Twice a person's size.
    /// </summary>
    public class StahlColossusTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();

        private static UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/{file}.asset");
        private static WeaponTemplate W(string file) => AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/{file}.asset");
        private static Faction F(string file) => AssetDatabase.LoadAssetAtPath<Faction>($"{Data}/Factions/{file}.asset");
        private static Ability A(string file) => AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/{file}.asset");

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
        public void TheStahlColossusIsAsDesigned()
        {
            var unit = U("StahlColossus");
            Assert.That((unit.displayName, unit.Size, unit.role), Is.EqualTo(("Stahl Colossus", 1, UnitRole.HeavySupport)));
            var c = unit.leader;
            Assert.That(c.displayName, Is.EqualTo("Stahl Colossus"));
            Assert.That((c.health, c.armorLevel, c.accuracy, c.movementRange), Is.EqualTo((15, 3, 75, 15f)));
            Assert.That(c.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Solo", "Mechanical" }));
            Assert.That(c.weapons, Is.EqualTo(new[] { W("MachineGun"), W("IronClaw") }));
            Assert.That(c.PrimaryWeapon, Is.SameAs(W("MachineGun")));
            Assert.That(c.abilities, Is.EqualTo(new[] { A("IronClaw"), A("Stabilizers") }));
            Assert.That(c.height, Is.EqualTo(3.6f), "twice a person's 1.8 m");
            Assert.That(c.prefab, Is.Not.Null);
        }

        [Test]
        public void TheIronClawIsAsDesigned()
        {
            var claw = W("IronClaw");
            Assert.That(claw.displayName, Is.EqualTo("Iron Claw"));
            Assert.That((claw.baseDamage, claw.PenetrationValue, claw.range, claw.accuracyBonus, claw.shots), Is.EqualTo((10, (int?)0, 2f, 25, 1)));
            Assert.That(claw.keywords.Select(k => k.displayName), Is.EqualTo(new[] { "Melee" }));
        }

        [Test]
        public void OnlyTheDraksaReichCanTakeIt()
        {
            Assert.That(F("DraksaReich").Has(U("StahlColossus")), Is.True);
            Assert.That(F("UnitedStatesOfLiberteria").Has(U("StahlColossus")), Is.False);
            Assert.That(F("UnitedKingdomOfAelfland").Has(U("StahlColossus")), Is.False);
            var commander = U("FieldCommander");
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { commander, U("StahlColossus"), U("StahlColossus") }, F("DraksaReich")), Is.True,
                "Heavy Support: more than one");
        }

        [Test]
        public void MeleeWeaponsCanBeUsedInCloseRange()
        {
            // A trooper in close range, given an Iron Claw: the claw works, the rifle doesn't.
            var us = Spawn("InfantrySquad", Side.Player, Vector3.zero);
            Spawn("InfantrySquad", Side.AI, new Vector3(0f, 0f, 1.5f));
            var trooper = us.Leader;
            Assert.That(CloseRange.IsInCloseRange(trooper), Is.True);
            Assert.That(WeaponUse.CanUse(trooper, W("IronClaw")), Is.True, "Melee");
            Assert.That(WeaponUse.CanUse(trooper, W("AssaultRifle")), Is.False);
            Assert.That(WeaponUse.CanUse(trooper, W("Pistol")), Is.True, "Pistol, as before");
        }

        [Test]
        public void StabilizersLetTheMachineGunFireAfterMoving()
        {
            var colossus = Spawn("StahlColossus", Side.Player, Vector3.zero).Leader;
            Assert.That(colossus.CanSprint, Is.False, "Mechanical");
            Assert.That(CloseRange.IsInCloseRange(colossus), Is.False, "Mechanical: never in close range");
            Assert.That(A("Stabilizers").kind, Is.EqualTo(AbilityKind.Passive));
            Assert.That(colossus.Nullifies("Deployed"), Is.True);

            Assert.That(colossus.WeaponFor(null).Name, Is.EqualTo("Machine Gun"));
            colossus.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(colossus.WeaponFor(null).Name, Is.EqualTo("Machine Gun"), "Deployed is nullified: it still fires after moving");
        }

        [Test]
        public void DeployedStillAppliesToEveryoneElse()
        {
            var team = Spawn("MachineGunTeam", Side.Player, Vector3.zero);
            var gunner = team.Soldiers.Single(s => s.Template.displayName == "Machine Gunner");
            Assert.That(gunner.Nullifies("Deployed"), Is.False);
            gunner.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(WeaponUse.WhyCannotUse(gunner, gunner.Weapons.Single()), Does.Contain("Deployed"));
        }

        [Test]
        public void TheIronClawCanBeUsedAsAnAimedStrikeEvenWithoutMoving()
        {
            var colossus = Spawn("StahlColossus", Side.Player, Vector3.zero).Leader;
            var them = Spawn("InfantrySquad", Side.AI, new Vector3(0f, 0f, 1.8f));
            var strike = A("IronClaw");
            Assert.That((strike.IsAimedAttack, strike.range, strike.attackWeapon), Is.EqualTo((true, 2f, W("IronClaw"))));
            Assert.That(colossus.HasKeyword(KeywordLibrary.Moved), Is.False, "it hasn't moved: the machine gun could fire too");

            var target = them.Leader;   // (if it dies, leadership passes on, so keep hold of this soldier)
            var r = ActiveAbilities.Use(colossus, strike, targetSoldier: target);
            Assert.That(r.Used, Is.True, r.Reason);
            var hit = r.Attacks.Single();
            Assert.That(hit.Accuracy.Final, Is.EqualTo(100), "75% + 25");
            Assert.That(target.Health, Is.EqualTo(hit.Hit ? 5 - 5 : 5), "Damage 10, penetration 0 vs Armor 1: 50%");
        }

        [Test]
        public void TheIronClawReachesOnlyTwoMetres()
        {
            var colossus = Spawn("StahlColossus", Side.Player, Vector3.zero).Leader;
            var them = Spawn("InfantrySquad", Side.AI, new Vector3(0f, 0f, 3f));
            Assert.That(ActiveAbilities.WhyCannotUse(colossus, A("IronClaw"), them.Leader), Does.Contain("Out of range (2 m)"));
        }

        [Test]
        public void TheAIDoesntClawWhatItCantHurt()
        {
            // An enemy tank right beside it: the claw (penetration 0) can't hurt Armor 4, so the AI doesn't use it.
            var us = Spawn("StahlColossus", Side.AI, new Vector3(0f, 0f, 3f));
            Spawn("MediumTank", Side.Player, Vector3.zero);
            var action = AIPlanner.ChooseAction(us);
            Assert.That(action.Ability, Is.Not.SameAs(A("IronClaw")));
        }

        [Test]
        public void ItNeedsRoomForItsBodyAndShowsInTheArmyBuilder()
        {
            Assert.That(U("StahlColossus").leader.radius, Is.GreaterThan(0.5f), "wider than a person: it gets a walkable-area map for its size");
            Assert.That(UnitInfo.SoldierLines(U("StahlColossus")).Single(),
                Is.EqualTo("Stahl Colossus — Health 15 · Armor 3 · Accuracy 75% · Move 15 m (can't sprint) · Machine Gun, Iron Claw"));
            Assert.That(UnitInfo.WeaponLines(U("StahlColossus")).Last(),
                Is.EqualTo("Iron Claw — Damage 10 · Shots 1 · Penetration 0 · +25% accuracy · Range 2 m · Melee"));
            Assert.That(UnitInfo.AbilityLines(U("StahlColossus")), Is.EqualTo(new[] { "Iron Claw (active)", "Stabilizers (passive)" }));
        }
    }
}
