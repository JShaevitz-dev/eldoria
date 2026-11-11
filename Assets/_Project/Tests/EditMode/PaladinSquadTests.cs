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
    /// Paladin Squad (United Kingdom of Aelfland only, Heavy Support): a Sergeant and 4 Troopers in Powered Armor, each with a
    /// Broadsword (Damage 5, Penetration 1, Range 2 m, +0, Melee).
    /// </summary>
    public class PaladinSquadTests
    {
        private const string Data = "Assets/_Project/Data";
        private static UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/{file}.asset");
        private static SoldierTemplate S(string file) => AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/{file}.asset");
        private static Faction F(string file) => AssetDatabase.LoadAssetAtPath<Faction>($"{Data}/Factions/{file}.asset");

        [Test]
        public void TheSquadIsASergeantAndFourTroopersInPoweredArmorWithBroadswords()
        {
            var squad = U("PaladinSquad");
            Assert.That((squad.displayName, squad.Size, squad.role), Is.EqualTo(("Paladin Squad", 5, UnitRole.HeavySupport)));
            Assert.That(squad.leader.displayName, Is.EqualTo("Sergeant in Powered Armor"));
            Assert.That(squad.members.Select(m => (m.soldier.displayName, m.count)), Is.EqualTo(new[] { ("Trooper in Powered Armor", 4) }));
            foreach (var s in squad.Roster())
            {
                Assert.That(s.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Broadsword" }), s.displayName);
                Assert.That(s.PrimaryWeapon.displayName, Is.EqualTo("Broadsword"));
                Assert.That(s.prefab, Is.Not.Null);
            }
        }

        [Test]
        public void ThePaladinsHaveThePoweredArmorSoldiersStats()
        {
            foreach (var (paladin, original) in new[] { ("PaladinSergeant", "PoweredArmorSergeant"), ("PaladinTrooper", "PoweredArmorTrooper") })
            {
                var p = S(paladin);
                var o = S(original);
                Assert.That((p.health, p.armorLevel, p.accuracy, p.movementRange), Is.EqualTo((o.health, o.armorLevel, o.accuracy, o.movementRange)), paladin);
                Assert.That(p.keywords, Is.EqualTo(o.keywords));
            }
            Assert.That(S("PaladinTrooper").health, Is.EqualTo(5));
            Assert.That(S("PaladinSergeant").health, Is.EqualTo(10));
        }

        [Test]
        public void TheBroadswordIsAsDesigned()
        {
            var sword = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/Broadsword.asset");
            Assert.That((sword.displayName, sword.baseDamage, sword.PenetrationValue, sword.range, sword.accuracyBonus, sword.shots),
                Is.EqualTo(("Broadsword", 5, (int?)1, 2f, 0, 1)));
            Assert.That(sword.keywords.Select(k => k.displayName), Is.EqualTo(new[] { "Melee" }));
        }

        [Test]
        public void OnlyAelflandCanTakeIt()
        {
            Assert.That(F("UnitedKingdomOfAelfland").Has(U("PaladinSquad")), Is.True);
            Assert.That(F("UnitedStatesOfLiberteria").Has(U("PaladinSquad")), Is.False);
            Assert.That(F("DraksaReich").Has(U("PaladinSquad")), Is.False);
        }

        [Test]
        public void PaladinsFightInCloseRangeWithinTwoMetres()
        {
            var paladins = UnitFactory.Spawn(U("PaladinSquad"), Side.Player, Vector3.zero, Quaternion.identity);
            var near = UnitFactory.Spawn(U("InfantrySquad"), Side.AI, new Vector3(0f, 0f, 1.5f), Quaternion.Euler(0f, 180f, 0f));
            try
            {
                var leader = paladins.Leader;
                Assert.That(CloseRange.IsInCloseRange(leader), Is.True, "Biological, an enemy 1.5 m away");
                Assert.That(leader.WeaponFor(null)?.Name, Is.EqualTo("Broadsword"), "Melee: usable in close range, unlike a rifle");

                var r = UnitShooting.Resolve(paladins, near, false, new System.Random(1));
                var shots = r.Shots.Where(s => s.Target != null).ToList();
                Assert.That(shots, Is.Not.Empty);
                Assert.That(shots.All(s => Measure.Between(s.Shooter, s.Target) <= 2f + 0.01f), Is.True, "only within the sword's 2 m");
                foreach (var hit in shots.Where(s => s.Attack.Hit))
                    Assert.That(hit.Attack.DamageDealt, Is.EqualTo(5), "Damage 5, penetration 1 vs Armor 1: 100%");
            }
            finally
            {
                Object.DestroyImmediate(paladins.gameObject);
                Object.DestroyImmediate(near.gameObject);
            }
        }

        [Test]
        public void TheArmyBuilderShowsTheBroadsword()
        {
            Assert.That(UnitInfo.WeaponLines(U("PaladinSquad")).Single(), Is.EqualTo("Broadsword — Damage 5 · Shots 1 · Penetration 1 · Range 2 m · Melee"));
        }
    }
}
