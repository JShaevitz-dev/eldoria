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
            Assert.That(catalog.playerArmor, Is.Not.Null);
            Assert.That(catalog.enemyArmor, Is.Not.Null);
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

        [Test]
        public void TheArmyBuilderAddsRemovesAndChecks()
        {
            var builder = new ArmyBuilderScreen(UnitCatalog.Load(), new List<UnitTemplate>());
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

        [Test]
        public void SixSlotsFitOnTheUrbanDemoMap()
        {
            Assert.That(ArmyDeployment.PlayerSlots.Length, Is.EqualTo(ArmyRules.MaxUnits));
            for (int i = 0; i < ArmyRules.MaxUnits; i++)
                Assert.That(ArmyDeployment.SlotFor(Side.AI, i), Is.EqualTo(-ArmyDeployment.PlayerSlots[i]), "the AI's slots are the player's turned 180°");
        }
    }
}
