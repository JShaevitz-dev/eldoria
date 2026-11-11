using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Unit roles: Infantry, Commander, Heavy Support, Specialist (Docs/Rules.md, Roles).</summary>
    public class UnitRoleTests
    {
        private static UnitTemplate Load(string file) =>
            AssetDatabase.LoadAssetAtPath<UnitTemplate>($"Assets/_Project/Data/Units/{file}.asset");

        [TestCase("InfantrySquad", UnitRole.Infantry)]
        [TestCase("FieldCommander", UnitRole.Commander)]
        [TestCase("BattlefieldPriest", UnitRole.Specialist)]
        [TestCase("MachineGunTeam", UnitRole.HeavySupport)]
        [TestCase("PoweredArmorSquad", UnitRole.Specialist)]
        [TestCase("Sniper", UnitRole.Specialist)]
        [TestCase("FlamethrowerTeam", UnitRole.Specialist)]
        [TestCase("MediumTank", UnitRole.HeavySupport)]
        [TestCase("TankHunterTeam", UnitRole.HeavySupport)]
        [TestCase("BattleMage", UnitRole.Specialist)]
        [TestCase("StahlColossus", UnitRole.HeavySupport)]
        [TestCase("FieldCommanderPoweredArmor", UnitRole.Commander)]
        public void EachUnitHasItsRole(string file, UnitRole role)
        {
            Assert.That(Load(file).role, Is.EqualTo(role));
        }

        [Test]
        public void EveryUnitInTheGameHasARoleFromTheList()
        {
            var units = AssetDatabase.FindAssets("t:UnitTemplate", new[] { "Assets/_Project/Data/Units" })
                .Select(g => AssetDatabase.LoadAssetAtPath<UnitTemplate>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.That(units.Count, Is.EqualTo(12));
            Assert.That(units.Count(u => u.role == UnitRole.Specialist), Is.EqualTo(5), "everything else is a Specialist (and the Battle Priest)");
        }

        [Test]
        public void RolesAreWrittenAsInTheRules()
        {
            Assert.That(UnitRole.HeavySupport.DisplayName(), Is.EqualTo("Heavy Support"));
            Assert.That(UnitRole.Infantry.DisplayName(), Is.EqualTo("Infantry"));
        }

        [Test]
        public void ADeployedUnitHasItsTemplatesRole()
        {
            var unit = UnitFactory.Spawn(Load("MachineGunTeam"), Side.Player, Vector3.zero, Quaternion.identity);
            try
            {
                Assert.That(unit.Role, Is.EqualTo(UnitRole.HeavySupport));
            }
            finally
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }
    }
}
