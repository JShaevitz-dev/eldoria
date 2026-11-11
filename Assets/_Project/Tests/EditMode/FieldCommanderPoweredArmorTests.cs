using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Field Commander in Powered Armor: the Field Commander with Armor Level 2 and Movement 6 m (Commander, every faction).</summary>
    public class FieldCommanderPoweredArmorTests
    {
        private const string Data = "Assets/_Project/Data";
        private static UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/{file}.asset");

        [Test]
        public void ItsTheFieldCommanderWithArmor2AndMove6()
        {
            var unit = U("FieldCommanderPoweredArmor");
            Assert.That((unit.displayName, unit.Size, unit.role), Is.EqualTo(("Field Commander in Powered Armor", 1, UnitRole.Commander)));
            var pa = unit.leader;
            var fc = U("FieldCommander").leader;
            Assert.That(pa.displayName, Is.EqualTo("Field Commander in Powered Armor"));
            Assert.That((pa.armorLevel, pa.movementRange), Is.EqualTo((2, 6f)), "the changes");
            Assert.That((fc.armorLevel, fc.movementRange), Is.EqualTo((1, 10f)), "the original is unchanged");
            Assert.That((pa.health, pa.accuracy), Is.EqualTo((fc.health, fc.accuracy)), "everything else is the Field Commander's");
            Assert.That(pa.keywords, Is.EqualTo(fc.keywords));
            Assert.That(pa.abilities, Is.EqualTo(fc.abilities), "Lead from the front");
            Assert.That(pa.abilities.Single().displayName, Is.EqualTo("Lead from the front"));
            Assert.That(pa.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Pistol" }));
            Assert.That(pa.prefab, Is.Not.Null.And.Not.EqualTo(fc.prefab), "its own powered-armor model");
        }

        [Test]
        public void EveryFactionCanTakeItAndItCountsAsACommander()
        {
            foreach (var f in UnitCatalog.Load().Factions) Assert.That(f.Has(U("FieldCommanderPoweredArmor")), Is.True, f.displayName);
            var infantry = U("InfantrySquad");
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { U("FieldCommanderPoweredArmor"), infantry }), Is.True, "fills the Commander slot");
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { U("FieldCommanderPoweredArmor"), U("FieldCommander") }), Is.False,
                "only 1 Commander per army, of either kind");
            Assert.That(ArmyRules.IsValid(new List<UnitTemplate> { U("FieldCommanderPoweredArmor"), U("FieldCommanderPoweredArmor") }), Is.False,
                "one per army");
        }

        [Test]
        public void ItsAuraWorksLikeTheFieldCommanders()
        {
            var commander = UnitFactory.Spawn(U("FieldCommanderPoweredArmor"), Side.Player, Vector3.zero, Quaternion.identity);
            var squad = UnitFactory.Spawn(U("InfantrySquad"), Side.Player, new Vector3(0f, 0f, 8f), Quaternion.identity);
            try
            {
                Terrain.NearbyKeywords.RefreshAll();
                Assert.That(squad.Leader.HasKeyword("Led from the front"), Is.True);
                Assert.That(commander.Leader.HasKeyword("Led from the front"), Is.False, "not the Commander itself");
                Assert.That((commander.Leader.Move, commander.Leader.Sprint), Is.EqualTo((6f, 12f)));
            }
            finally
            {
                Object.DestroyImmediate(commander.gameObject);
                Object.DestroyImmediate(squad.gameObject);
            }
        }
    }
}
