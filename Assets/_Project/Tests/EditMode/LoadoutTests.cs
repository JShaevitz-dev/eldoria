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
    /// <summary>
    /// Army builder loadouts: a soldier type can offer weapon swaps and equipment; choices apply to every soldier of that
    /// type in the unit. Equipment gives keywords, abilities, extra weapons and stat changes.
    /// </summary>
    public class LoadoutTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate rifle, smg, pistol;
        private Keyword camo;
        private Ability medkit;
        private Equipment plating;
        private SoldierTemplate trooper, sergeant;
        private UnitTemplate squad;
        private Faction faction;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            rifle = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/AssaultRifle.asset");
            smg = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/SMG.asset");
            pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/Pistol.asset");

            camo = Make<Keyword>();
            camo.displayName = "Camouflaged";
            medkit = Make<Ability>();
            medkit.displayName = "Medkit";
            medkit.kind = AbilityKind.Active;
            plating = Make<Equipment>();
            plating.displayName = "Armor Plating";
            plating.armorLevel = 1;
            plating.health = 2;
            plating.accuracy = 10;
            plating.movement = 2f;
            plating.keywords.Add(camo);
            plating.abilities.Add(medkit);
            plating.weapons.Add(pistol);

            trooper = Make<SoldierTemplate>();
            trooper.displayName = "Test Trooper";
            trooper.health = 5; trooper.armorLevel = 1; trooper.accuracy = 75; trooper.movementRange = 10f;
            trooper.weapons.Add(rifle);
            trooper.primaryWeapon = rifle;
            trooper.weaponOptions.Add(new WeaponOption { weapon = rifle, alternatives = new List<WeaponTemplate> { smg } });
            trooper.equipmentOptions.Add(plating);

            sergeant = Make<SoldierTemplate>();
            sergeant.displayName = "Test Sergeant";
            sergeant.health = 5;
            sergeant.weapons.Add(rifle);
            sergeant.primaryWeapon = rifle;

            squad = Make<UnitTemplate>();
            squad.displayName = "Test Squad";
            squad.role = UnitRole.Infantry;
            squad.leader = sergeant;
            squad.members.Add(new UnitTemplate.Member { soldier = trooper, count = 3 });

            faction = Make<Faction>();
            faction.displayName = "Test Faction";
            faction.units.Add(squad);
            faction.units.Add(AssetDatabase.LoadAssetAtPath<UnitTemplate>("Assets/_Project/Data/Units/FieldCommander.asset"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o is Component c ? c.gameObject : o);
            created.Clear();
        }

        private Unit Spawn(UnitLoadout loadout)
        {
            var unit = ArmyDeployment.Spawn(squad, Side.Player, Vector3.zero, Quaternion.identity, "Test Squad", null, null, loadout);
            created.Add(unit);
            return unit;
        }

        private static UnitLoadout Loadout(SoldierTemplate soldier, WeaponTemplate from = null, WeaponTemplate to = null, Equipment item = null)
        {
            var l = new UnitLoadout();
            if (from != null) l.For(soldier).Swaps[from] = to;
            if (item != null) l.For(soldier).Equipment.Add(item);
            return l;
        }

        [Test]
        public void ASoldierTypeWithSwapsOrEquipmentHasOptions()
        {
            Assert.That(trooper.HasOptions, Is.True);
            Assert.That(sergeant.HasOptions, Is.False);
        }

        [Test]
        public void ASwappedWeaponReplacesTheOriginalForEverySoldierOfThatType()
        {
            var unit = Spawn(Loadout(trooper, rifle, smg));
            foreach (var s in unit.Soldiers.Where(s => s.Template == trooper))
            {
                Assert.That(s.Weapons.Select(w => w.Name), Is.EqualTo(new[] { "SMG" }));
                Assert.That(s.PrimaryWeapon.Name, Is.EqualTo("SMG"), "the swapped primary is the new primary");
                Assert.That(s.WeaponFor(null).Name, Is.EqualTo("SMG"));
            }
            Assert.That(unit.Leader.Weapons.Single().Name, Is.EqualTo("Assault Rifle"), "the Sergeant is set separately");
        }

        [Test]
        public void EquipmentGivesStatsKeywordsAbilitiesAndWeapons()
        {
            var unit = Spawn(Loadout(trooper, item: plating));
            var s = unit.Soldiers.First(x => x.Template == trooper);
            Assert.That((s.MaxHealth, s.Health, s.ArmorLevel, s.Accuracy, s.Move), Is.EqualTo((7, 7, 2, 85, 12f)), "+2 health, +1 armor, +10%, +2 m");
            Assert.That(s.Sprint, Is.EqualTo(24f));
            Assert.That(s.HasKeyword("Camouflaged"), Is.True);
            Assert.That(s.Abilities, Does.Contain(medkit));
            Assert.That(s.Weapons.Select(w => w.Name), Is.EqualTo(new[] { "Assault Rifle", "Pistol" }), "an extra weapon, not the primary");
            Assert.That(s.PrimaryWeapon.Name, Is.EqualTo("Assault Rifle"));

            var sgt = unit.Leader;
            Assert.That((sgt.ArmorLevel, sgt.HasKeyword("Camouflaged")), Is.EqualTo((0, false)), "only that soldier type");
        }

        [Test]
        public void WithoutALoadoutNothingChanges()
        {
            var s = Spawn(null).Soldiers.First(x => x.Template == trooper);
            Assert.That((s.MaxHealth, s.ArmorLevel, s.Accuracy, s.Move), Is.EqualTo((5, 1, 75, 10f)));
            Assert.That(s.Weapons.Single().Name, Is.EqualTo("Assault Rifle"));
            Assert.That(s.Abilities, Is.Empty);
        }

        [Test]
        public void TheArmyBuilderCustomizesEachSlotsSoldierTypes()
        {
            var builder = new ArmyBuilderScreen(UnitCatalog.Load());
            builder.ChooseFaction(faction);
            builder.Clear();
            builder.Add(faction.units[1]);   // the Field Commander: nothing to customize
            Assert.That(builder.Add(squad), Is.Null);
            Assert.That(builder.Add(squad), Is.Null, "two of the same unit");
            Assert.That(builder.CanCustomize(0), Is.False);
            Assert.That(builder.CustomizableSoldiers(1), Is.EqualTo(new[] { trooper }), "only soldier types with options");

            Assert.That(builder.SetWeapon(1, trooper, rifle, smg), Is.Null);
            Assert.That(builder.SetEquipment(2, trooper, plating, true), Is.Null);
            Assert.That(builder.Loadouts[1].Get(trooper).Swap(rifle), Is.SameAs(smg));
            Assert.That(builder.Loadouts[2].Get(trooper).Swap(rifle), Is.SameAs(rifle), "each slot has its own choices");
            Assert.That(builder.Loadouts[1].Describe(), Is.EqualTo("Test Trooper: SMG"));
            Assert.That(builder.Loadouts[2].Describe(), Is.EqualTo("Test Trooper: Armor Plating"));

            Assert.That(builder.SetWeapon(1, trooper, rifle, pistol), Does.Contain("can't be swapped for Pistol"), "only the listed choices");
            Assert.That(builder.SetWeapon(1, sergeant, rifle, smg), Does.Contain("can't be swapped"), "the Sergeant has no options");
            Assert.That(builder.SetWeapon(1, trooper, rifle, rifle), Is.Null, "choosing the original undoes the swap");
            Assert.That(builder.Loadouts[1].IsEmpty, Is.True);

            builder.RemoveAt(1);
            Assert.That(builder.Loadouts.Count, Is.EqualTo(2));
            Assert.That(builder.Loadouts[1].Describe(), Is.EqualTo("Test Trooper: Armor Plating"), "the choices move with their unit");
            Assert.That(builder.Confirm(), Is.True);
        }
    }
}
