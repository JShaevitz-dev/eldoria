using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Army lists: up to 6 units, at least 1 Commander, no duplicate Commander or Specialist units.</summary>
    public class ArmyRulesTests
    {
        private static UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"Assets/_Project/Data/Units/{file}.asset");
        private static UnitTemplate Infantry => U("InfantrySquad");
        private static UnitTemplate Commander => U("FieldCommander");
        private static UnitTemplate Priest => U("BattlefieldPriest");
        private static UnitTemplate MG => U("MachineGunTeam");
        private static UnitTemplate PA => U("PoweredArmorSquad");
        private static UnitTemplate Sniper => U("Sniper");

        private static List<UnitTemplate> List(params UnitTemplate[] units) => units.ToList();

        [Test]
        public void TheDemoArmyIsValid()
        {
            Assert.That(ArmyRules.Problems(UnitCatalog.Load().defaultArmy), Is.Empty);
        }

        [Test]
        public void AnArmyNeedsAtLeastOneCommander()
        {
            Assert.That(ArmyRules.Problems(List()).Single(), Does.Contain("at least 1 Commander"));
            Assert.That(ArmyRules.Problems(List(Infantry, MG, Sniper)).Single(), Does.Contain("at least 1 Commander"));
            Assert.That(ArmyRules.IsValid(List(Commander)), Is.True, "a single Commander is a valid army");
            Assert.That(ArmyRules.IsValid(List(Priest, Infantry)), Is.True, "the Priest is a Commander too");
        }

        [Test]
        public void AnArmyHasSixSlots()
        {
            Assert.That(ArmyRules.IsValid(List(Commander, Infantry, Infantry, Infantry, Infantry, Infantry)), Is.True);
            Assert.That(ArmyRules.Problems(List(Commander, Infantry, Infantry, Infantry, Infantry, Infantry, Infantry)).Single(),
                Does.Contain("Too many units"));
        }

        [Test]
        public void CommandersAndSpecialistsCantBeDuplicated()
        {
            Assert.That(ArmyRules.Problems(List(Commander, Commander)).Single(), Does.Contain("Only one Field Commander"));
            Assert.That(ArmyRules.Problems(List(Commander, Sniper, Sniper)).Single(), Does.Contain("Only one Sniper"));
            Assert.That(ArmyRules.Problems(List(Commander, PA, PA)).Single(), Does.Contain("Only one Powered Armor Squad"));
            Assert.That(ArmyRules.IsValid(List(Commander, Priest, Sniper, PA)), Is.True, "different ones are fine");
        }

        [Test]
        public void InfantryAndHeavySupportCanBeTakenMoreThanOnce()
        {
            Assert.That(ArmyRules.IsValid(List(Commander, Infantry, Infantry, Infantry, MG, MG)), Is.True);
        }

        [Test]
        public void WhyAUnitCantBeAdded()
        {
            Assert.That(ArmyRules.WhyCannotAdd(List(Commander), Infantry), Is.Null);
            Assert.That(ArmyRules.WhyCannotAdd(List(Commander), Commander), Does.Contain("Already in the army"));
            Assert.That(ArmyRules.WhyCannotAdd(List(Sniper), Sniper), Does.Contain("Already in the army"));
            Assert.That(ArmyRules.WhyCannotAdd(List(Commander, Infantry), Infantry), Is.Null);
            Assert.That(ArmyRules.WhyCannotAdd(List(Commander, Infantry, Infantry, Infantry, Infantry, Infantry), MG), Does.Contain("slots are full"));
        }

        [Test]
        public void TheAIsRandomArmiesAreAlwaysValidAndFull()
        {
            var available = UnitCatalog.Load().Available.ToList();
            var used = new HashSet<UnitTemplate>();
            for (int seed = 0; seed < 300; seed++)
            {
                var army = ArmyRules.RandomArmy(available, new System.Random(seed));
                Assert.That(ArmyRules.Problems(army), Is.Empty, $"seed {seed}");
                Assert.That(army.Count, Is.EqualTo(6), "every slot filled");
                Assert.That(army.Count(u => u.role == UnitRole.Commander), Is.InRange(1, 2));
                used.UnionWith(army);
            }
            Assert.That(used, Is.EquivalentTo(available), "every unit turns up sometimes");
        }

        [Test]
        public void TheCatalogListsEveryUnitCommandersFirst()
        {
            var catalog = UnitCatalog.Load();
            Assert.That(catalog.Available.Count(), Is.EqualTo(7));
            Assert.That(catalog.Available.Select(u => u.role.ListOrder()), Is.Ordered);
            Assert.That(catalog.Available.First().role, Is.EqualTo(UnitRole.Commander));
            Assert.That(catalog.primaryColorMaterial, Is.Not.Null);
            Assert.That(catalog.leaderMarker, Is.Not.Null);
        }

        [Test]
        public void UnitsTakenMoreThanOnceAreNumbered()
        {
            var names = ArmyDeployment.UnitNames(List(Infantry, Commander, Infantry), Side.Player);
            Assert.That(names, Is.EqualTo(new[] { "Infantry Squad 1", "Field Commander", "Infantry Squad 2" }));
            Assert.That(ArmyDeployment.UnitNames(List(Commander), Side.AI).Single(), Is.EqualTo("Field Commander (Enemy)"));
        }

        [Test]
        public void TheBuilderShowsWhatAUnitIsMadeOf()
        {
            Assert.That(ArmyRules.Composition(Infantry), Is.EqualTo("Sergeant + 9× Trooper"));
            Assert.That(ArmyRules.Composition(Sniper), Is.EqualTo("Sniper"));
        }

        // ---- Unit stats and descriptions in the army builder ----

        [Test]
        public void EveryUnitHasADescriptionOfHowItPlays()
        {
            foreach (var unit in UnitCatalog.Load().Available)
            {
                Assert.That(unit.description, Is.Not.Null.And.Not.Empty, unit.displayName);
                Assert.That(unit.description.Length, Is.InRange(200, 600), $"{unit.displayName}: a brief paragraph");
            }
        }

        [Test]
        public void TheBuilderListsEachSoldierTypesStats()
        {
            Assert.That(UnitInfo.SoldierLines(Infantry), Is.EqualTo(new[]
            {
                "Sergeant — Health 5 · Armor 1 · Accuracy 75% · Move 10 m (sprint 20 m) · Assault Rifle, Pistol",
                "9× Trooper — Health 5 · Armor 1 · Accuracy 75% · Move 10 m (sprint 20 m) · Assault Rifle",
            }));
            Assert.That(UnitInfo.SoldierLines(PA).Last(), Does.StartWith("4× Trooper in Powered Armor — Health 10 · Armor 2 · Accuracy 75% · Move 6 m (sprint 12 m)"));
            Assert.That(UnitInfo.SoldierLines(Sniper).Single(), Does.Contain("Accuracy 100% · Move 15 m (sprint 30 m) · Sniper Rifle"));
        }

        [Test]
        public void TheBuilderListsEachWeaponOnce()
        {
            Assert.That(UnitInfo.WeaponLines(Infantry), Is.EqualTo(new[]
            {
                "Assault Rifle — Damage 4 · Penetration 1 · Range 20 m",
                "Pistol — Damage 2 · Penetration 0 · Range 10 m · Pistol",
            }));
            Assert.That(UnitInfo.WeaponLines(Sniper).Single(), Is.EqualTo("Sniper Rifle — Damage 5 · Penetration 1 · +50% accuracy · Range 50 m · Deployed"));
            Assert.That(UnitInfo.WeaponLines(U("FlamethrowerTeam")).First(), Does.StartWith("Flamethrower — Damage 10 · Penetration X · Range 10 m · Incendiary"));
            Assert.That(UnitInfo.WeaponLines(MG).Select(l => l.Split(' ')[0]), Is.EqualTo(new[] { "Pistol", "SMG", "Machine" }));
        }

        [Test]
        public void TheBuilderListsAbilitiesAndKeywords()
        {
            Assert.That(UnitInfo.AbilityLines(Infantry).Single(), Is.EqualTo("Frag Grenade (active, once per battle)"));
            Assert.That(UnitInfo.AbilityLines(Priest), Has.Some.EqualTo("Divine Healing (active, spell)"));
            Assert.That(UnitInfo.AbilityLines(Commander).Single(), Does.Contain("(passive)"));
            Assert.That(UnitInfo.SharedKeywords(Commander), Does.Contain("Officer").And.Contain("Solo"));
            Assert.That(UnitInfo.SharedKeywords(Sniper), Does.Contain("Sniper Precision"));
        }

        [Test]
        public void TheArmyBuilderAddsRemovesAndChecks()
        {
            var catalog = UnitCatalog.Load();
            var builder = new ArmyBuilderScreen(catalog);
            builder.ChooseFaction(catalog.Factions.First());
            builder.Clear();
            Assert.That(builder.Army, Is.Empty);
            Assert.That(builder.Confirm(), Is.False, "an empty army has no Commander");
            Assert.That(builder.Add(Commander), Is.Null);
            for (int i = 0; i < 5; i++) Assert.That(builder.Add(Infantry), Is.Null);
            Assert.That(builder.Add(Infantry), Does.Contain("slots are full"));
            Assert.That(builder.Confirm(), Is.True);
            builder.RemoveAt(0);
            Assert.That(builder.Army.Count, Is.EqualTo(5));
            Assert.That(builder.Confirm(), Is.False);
            Assert.That(builder.Problems.Single(), Does.Contain("Commander"));
        }

        // ---- Factions ----

        private static Faction F(string file) => AssetDatabase.LoadAssetAtPath<Faction>($"Assets/_Project/Data/Factions/{file}.asset");

        [Test]
        public void ThereAreThreeFactionsAndEachHasEveryUnitForNow()
        {
            var catalog = UnitCatalog.Load();
            Assert.That(catalog.Factions.Select(f => f.displayName), Is.EqualTo(new[]
                { "United States of Columbia", "United Kingdom of Aelfland", "The Draksa Reich" }));
            Assert.That(F("UnitedStatesOfColumbia"), Is.Not.Null);
            Assert.That(F("UnitedKingdomOfAelfland"), Is.Not.Null);
            Assert.That(F("DraksaReich"), Is.Not.Null);
            foreach (var f in catalog.Factions)
                Assert.That(catalog.UnitsFor(f), Is.EquivalentTo(catalog.Available), f.displayName);
        }

        [Test]
        public void AFactionsArmyCanOnlyHaveItsOwnUnits()
        {
            var limited = ScriptableObject.CreateInstance<Faction>();
            try
            {
                limited.displayName = "Test Faction";
                limited.units = List(Commander, Infantry);
                Assert.That(ArmyRules.IsValid(List(Commander, Infantry, Infantry), limited), Is.True);
                Assert.That(ArmyRules.Problems(List(Commander, Sniper), limited).Single(), Does.Contain("Sniper isn't available to Test Faction"));
                Assert.That(ArmyRules.WhyCannotAdd(List(Commander), Sniper, limited), Does.Contain("Not available to Test Faction"));
                Assert.That(ArmyRules.Problems(List(Priest), limited), Has.Some.Contains("Battlefield Priest isn't available"),
                    "the faction decides the commanders too");
            }
            finally
            {
                Object.DestroyImmediate(limited);
            }
        }

        [Test]
        public void EachFactionHasItsColour()
        {
            Assert.That(F("UnitedStatesOfColumbia").color, Is.EqualTo(new Color(0.16f, 0.30f, 0.15f)), "dark green");
            Assert.That(F("UnitedKingdomOfAelfland").color, Is.EqualTo(new Color(0.50f, 0.74f, 0.36f)), "light green");
            Assert.That(F("DraksaReich").color, Is.EqualTo(new Color(0.46f, 0.47f, 0.49f)), "gray");
            var usc = F("UnitedStatesOfColumbia").color;
            Assert.That(usc.g > usc.r && usc.g > usc.b, Is.True, "green");
            var aelf = F("UnitedKingdomOfAelfland").color;
            Assert.That(aelf.g > aelf.r && aelf.g > aelf.b && aelf.g > usc.g, Is.True, "a lighter green");
        }

        [Test]
        public void ThePrimaryColourTintsOnlyTheUnitsPrimaryParts()
        {
            var catalog = UnitCatalog.Load();
            var unit = UnitFactory.Spawn(Infantry, Side.Player, Vector3.zero, Quaternion.identity);
            try
            {
                var gray = F("DraksaReich").color;
                int tinted = PrimaryColor.Apply(unit.gameObject, catalog.primaryColorMaterial, gray);
                Assert.That(tinted, Is.GreaterThanOrEqualTo(10), "every soldier's body");
                foreach (var r in unit.GetComponentsInChildren<Renderer>())
                {
                    bool isGray = Vector4.Distance(PrimaryColor.Of(r), gray) < 0.002f;
                    if (r.sharedMaterial == catalog.primaryColorMaterial) Assert.That(isGray, Is.True, r.name);
                    else Assert.That(isGray, Is.False, $"{r.name} keeps its own colour");
                }
                Assert.That(unit.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).Distinct().Count(),
                    Is.LessThan(8), "no new materials were made");
            }
            finally
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }

        [Test]
        public void TheBuilderShowsOnlyTheFactionsUnits()
        {
            var catalog = UnitCatalog.Load();
            var faction = catalog.Factions.Last();
            var builder = new ArmyBuilderScreen(catalog);
            Assert.That(builder.Faction, Is.Null);
            builder.ChooseFaction(faction);
            Assert.That(builder.Available, Is.EquivalentTo(catalog.UnitsFor(faction)));
            Assert.That(builder.Confirm(), Is.True, "starts from the demo army");
            builder.ChangeFaction();
            Assert.That((builder.Faction, builder.Army.Count, builder.Available.Count), Is.EqualTo(((Faction)null, 0, 0)));
        }

        [Test]
        public void TheBuilderRemembersTheLastArmyForItsFaction()
        {
            var catalog = UnitCatalog.Load();
            var usc = catalog.Factions.First();
            var builder = new ArmyBuilderScreen(catalog, usc, List(Commander, Sniper));
            builder.ChooseFaction(usc);
            Assert.That(builder.Army, Is.EqualTo(List(Commander, Sniper)));
            builder.ChooseFaction(catalog.Factions.Last());
            Assert.That(builder.Army, Is.EqualTo(catalog.defaultArmy), "another faction starts from the demo army");
        }

        [Test]
        public void SixSlotsFitOnTheUrbanDemoMap()
        {
            Assert.That(ArmyDeployment.PlayerSlots.Length, Is.EqualTo(ArmyRules.MaxUnits));
            for (int i = 0; i < ArmyRules.MaxUnits; i++)
                Assert.That(ArmyDeployment.SlotFor(Side.AI, i), Is.EqualTo(-ArmyDeployment.PlayerSlots[i]), "the AI's slots are the player's turned 180°");
        }
    }
}
