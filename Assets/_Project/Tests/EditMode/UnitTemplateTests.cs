using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class UnitTemplateTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate rifle, pistol;
        private SoldierTemplate trooper, sergeant;
        private UnitTemplate squad;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            rifle = Make<WeaponTemplate>();
            rifle.displayName = "Rifle";
            rifle.baseDamage = 4;
            rifle.penetration = 1;
            pistol = Make<WeaponTemplate>();
            pistol.displayName = "Pistol";
            pistol.baseDamage = 2;
            pistol.accuracyBonus = 5;

            trooper = Make<SoldierTemplate>();
            trooper.displayName = "Trooper";
            trooper.health = 4;
            trooper.accuracy = 65;
            trooper.weapons = new List<WeaponTemplate> { rifle };

            sergeant = Make<SoldierTemplate>();
            sergeant.displayName = "Sergeant";
            sergeant.health = 5;
            sergeant.armorLevel = 1;
            sergeant.accuracy = 70;
            sergeant.movementRange = 12f;
            sergeant.weapons = new List<WeaponTemplate> { rifle, pistol };

            squad = Make<UnitTemplate>();
            squad.displayName = "Infantry Squad";
            squad.leader = sergeant;
            squad.members = new List<UnitTemplate.Member> { new UnitTemplate.Member { soldier = trooper, count = 9 } };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private Unit Spawn()
        {
            var unit = UnitFactory.Spawn(squad, Side.Player, new Vector3(0, 0, -16), Quaternion.identity);
            created.Add(unit.gameObject);
            return unit;
        }

        [Test]
        public void TemplateListsLeaderThenMembers()
        {
            Assert.That(squad.Size, Is.EqualTo(10));
            var roster = squad.Roster().ToList();
            Assert.That(roster[0], Is.EqualTo(sergeant));
            Assert.That(roster.Skip(1), Is.All.EqualTo(trooper));
        }

        [Test]
        public void UnitIsBuiltFromItsTemplate()
        {
            var unit = Spawn();
            Assert.That(unit.Template, Is.EqualTo(squad));
            Assert.That(unit.DisplayName, Is.EqualTo("Infantry Squad"));
            Assert.That(unit.Soldiers.Count, Is.EqualTo(10));
            Assert.That(unit.Soldiers.Count(s => s.Template == trooper), Is.EqualTo(9));
            Assert.That(unit.Soldiers.Count(s => s.Template == sergeant), Is.EqualTo(1));
        }

        [Test]
        public void TemplateLeaderLeadsTheUnit()
        {
            var unit = Spawn();
            Assert.That(unit.Leader.Template, Is.EqualTo(sergeant));
            Assert.That(unit.Leader.IsSergeant, Is.True);
            Assert.That(unit.Leader.transform.localPosition, Is.EqualTo(Vector3.zero), "front and centre");
        }

        [Test]
        public void SoldiersTakeTheirStatsFromTheirTemplate()
        {
            var unit = Spawn();
            var sgt = unit.Leader;
            var t = unit.Soldiers.First(s => s.Template == trooper);
            Assert.That(sgt.MaxHealth, Is.EqualTo(5));
            Assert.That(sgt.Health, Is.EqualTo(5));
            Assert.That(sgt.ArmorLevel, Is.EqualTo(1));
            Assert.That(sgt.Accuracy, Is.EqualTo(70));
            Assert.That(sgt.Move, Is.EqualTo(12f));
            Assert.That(sgt.Sprint, Is.EqualTo(24f), "sprint is double the movement range");
            Assert.That(t.MaxHealth, Is.EqualTo(4));
            Assert.That(t.Accuracy, Is.EqualTo(65));
        }

        [Test]
        public void SoldiersCarryTheirTemplatesWeapons()
        {
            var unit = Spawn();
            Assert.That(unit.Leader.Weapons.Select(w => w.Template), Is.EqualTo(new[] { rifle, pistol }));
            var t = unit.Soldiers.First(s => s.Template == trooper);
            Assert.That(t.Weapons.Select(w => w.Template), Is.EqualTo(new[] { rifle }));
            Assert.That(t.Weapons[0].BaseDamage, Is.EqualTo(4));
            Assert.That(t.Weapons[0].Penetration, Is.EqualTo(1));
            Assert.That(unit.Leader.Weapons[1].AccuracyBonus, Is.EqualTo(5));
        }

        [Test]
        public void WeaponsAndSoldiersCarryKeywords()
        {
            var explosive = Make<Keyword>();
            explosive.displayName = "Explosive";
            explosive.appliesTo = KeywordTarget.Weapon;
            var robotic = Make<Keyword>();
            robotic.displayName = "Robotic";
            robotic.appliesTo = KeywordTarget.Soldier;

            pistol.keywords.Add(explosive);
            trooper.keywords.Add(robotic);
            var unit = Spawn();
            var t = unit.Soldiers.First(s => s.Template == trooper);

            Assert.That(unit.Leader.Weapons[1].HasKeyword("explosive"), Is.True, "case-insensitive");
            Assert.That(unit.Leader.Weapons[0].HasKeyword("Explosive"), Is.False);
            Assert.That(t.HasKeyword("Robotic"), Is.True);
            Assert.That(unit.Leader.HasKeyword("Robotic"), Is.False);
            Assert.That(explosive.CanApplyTo(KeywordTarget.Soldier), Is.False);
        }

        [Test]
        public void UnitDeploysInRanksOfFiveSpacedApart()
        {
            var slots = UnitFactory.FormationSlots(10, 2f);
            Assert.That(slots.Count, Is.EqualTo(10));
            Assert.That(slots.Count(s => s.z == 0f), Is.EqualTo(5));
            Assert.That(slots.Count(s => s.z == -2f), Is.EqualTo(5));
            for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
                Assert.That(Vector3.Distance(slots[i], slots[j]), Is.GreaterThanOrEqualTo(2f - 0.001f));
        }
    }
}
