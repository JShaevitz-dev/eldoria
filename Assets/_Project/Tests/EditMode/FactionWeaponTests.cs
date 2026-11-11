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
    /// Factions change the stats of the weapons their soldiers hold (added to the weapon's own) and can give weapons their
    /// own names (aliases of the same stat block).
    /// </summary>
    public class FactionWeaponTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate rifle, pistol, flamer;
        private UnitTemplate infantry;
        private Faction reich, plain;

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
            pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/Pistol.asset");
            flamer = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/Flamethrower.asset");
            infantry = AssetDatabase.LoadAssetAtPath<UnitTemplate>("Assets/_Project/Data/Units/InfantrySquad.asset");

            reich = Make<Faction>();
            reich.displayName = "Test Reich";
            reich.weaponAdjustments.Add(new WeaponAdjustment { weapon = rifle, damage = 1, shots = 1, range = -5f, accuracyBonus = 10 });
            reich.weaponNames.Add(new WeaponName { weapon = rifle, name = "Sturmgewehr" });
            plain = Make<Faction>();
            plain.displayName = "Test Plain";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o is Component c ? c.gameObject : o);
            created.Clear();
        }

        private Unit Spawn(Faction faction, Side side = Side.Player, Vector3 at = default)
        {
            var unit = UnitFactory.Spawn(infantry, side, at, Quaternion.identity);
            created.Add(unit);
            unit.SetFaction(faction);
            return unit;
        }

        [Test]
        public void AFactionsChangesAreAddedToTheWeaponsStats()
        {
            var w = Spawn(reich).Leader.Weapons.First(x => x.Template == rifle);
            Assert.That((w.BaseDamage, w.Shots, w.Range, w.AccuracyBonus, w.PenetrationValue), Is.EqualTo((5, 2, 15f, 10, (int?)1)),
                "Assault Rifle 4 dmg / 1 shot / 20 m / +0 / pen 1, plus +1 / +1 / -5 m / +10");
        }

        [Test]
        public void TheWeaponTemplateItselfIsUnchanged()
        {
            Spawn(reich);
            Assert.That((rifle.baseDamage, rifle.shots, rifle.range, rifle.displayName), Is.EqualTo((4, 1, 20f, "Assault Rifle")));
        }

        [Test]
        public void OtherFactionsAndOtherWeaponsAreUnaffected()
        {
            var plainRifle = Spawn(plain).Leader.Weapons.First(x => x.Template == rifle);
            Assert.That((plainRifle.BaseDamage, plainRifle.Range, plainRifle.Name), Is.EqualTo((4, 20f, "Assault Rifle")));
            var noFaction = Spawn(null).Leader.Weapons.First(x => x.Template == rifle);
            Assert.That((noFaction.BaseDamage, noFaction.Name), Is.EqualTo((4, "Assault Rifle")));
            var reichPistol = Spawn(reich).Leader.Weapons.First(x => x.Template == pistol);
            Assert.That((reichPistol.BaseDamage, reichPistol.Name), Is.EqualTo((2, "Pistol")), "only the weapon it names");
        }

        [Test]
        public void AFactionsNameForAWeaponIsAnAliasOfTheSameStatBlock()
        {
            var w = Spawn(reich).Leader.Weapons.First(x => x.Template == rifle);
            Assert.That(w.Name, Is.EqualTo("Sturmgewehr"));
            Assert.That(w.Template, Is.SameAs(rifle), "still the Assault Rifle underneath");

            // A name only: the stats are the stat block's (with the faction's changes, if any).
            var aliasOnly = Make<Faction>();
            aliasOnly.weaponNames.Add(new WeaponName { weapon = pistol, name = "Sidearm" });
            var p = Spawn(aliasOnly).Leader.Weapons.First(x => x.Template == pistol);
            Assert.That((p.Name, p.BaseDamage, p.Range), Is.EqualTo(("Sidearm", 2, 10f)));
        }

        [Test]
        public void SeveralChangesToOneWeaponAddUp()
        {
            reich.weaponAdjustments.Add(new WeaponAdjustment { weapon = rifle, damage = 2 });
            var w = Spawn(reich).Leader.Weapons.First(x => x.Template == rifle);
            Assert.That(w.BaseDamage, Is.EqualTo(4 + 1 + 2));
        }

        [Test]
        public void StatsStayWithinTheirLimits()
        {
            var harsh = Make<Faction>();
            harsh.weaponAdjustments.Add(new WeaponAdjustment { weapon = rifle, damage = -10, shots = -5, penetration = -3, range = -100f });
            harsh.weaponAdjustments.Add(new WeaponAdjustment { weapon = flamer, penetration = 2, range = 0f });
            var w = WeaponStats.For(rifle, harsh);
            Assert.That((w.BaseDamage, w.Shots, w.PenetrationValue, w.Range), Is.EqualTo((0, 1, (int?)0, 1f)), "never below 0 damage, 1 shot, 0 pen, 1 m");
            var f = WeaponStats.For(flamer, harsh);
            Assert.That(f.PenetrationValue, Is.Null, "a weapon with no penetration value keeps none");
            var grenade = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/FragGrenade.asset");
            harsh.weaponAdjustments.Add(new WeaponAdjustment { weapon = grenade, range = -5f });
            Assert.That(WeaponStats.For(grenade, harsh).Range, Is.EqualTo(0f), "unlimited range stays unlimited");
        }

        [Test]
        public void TheChangesApplyInAVolley()
        {
            var us = Spawn(reich, Side.Player, Vector3.zero);
            var them = Spawn(null, Side.AI, new Vector3(0f, 0f, 18f));   // 16-20 m away: beyond the Reich's 15 m rifles
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots.Where(s => s.Weapon != null && s.Weapon.Template == rifle).All(s => s.Target == null), Is.True,
                "the Reich's rifles reach only 15 m");

            them.transform.position = new Vector3(0f, 0f, 12f);
            Physics.SyncTransforms();
            var r2 = UnitShooting.Resolve(us, them, false, new System.Random(1));
            var rifleShots = r2.Shots.Where(s => s.Weapon != null && s.Weapon.Template == rifle).ToList();
            Assert.That(rifleShots.Max(s => s.Round), Is.EqualTo(2), "2 shots each");
            Assert.That(rifleShots.Where(s => s.Attack != null && s.Attack.Hit).All(s => s.Attack.FinalDamage == 5), Is.True, "5 damage before armor");
        }

        [Test]
        public void TheArmyBuilderShowsTheFactionsStatsAndNames()
        {
            Assert.That(UnitInfo.WeaponLines(infantry, reich).First(),
                Is.EqualTo("Sturmgewehr — Damage 5 · Shots 2 · Penetration 1 · +10% accuracy · Range 15 m"));
            Assert.That(UnitInfo.SoldierLines(infantry, reich).First(), Does.EndWith("Sturmgewehr, Pistol"));
            Assert.That(UnitInfo.WeaponLines(infantry).First(), Does.StartWith("Assault Rifle — Damage 4 · Shots 1"), "no faction: the weapon's own");
        }

        [Test]
        public void TheGamesFactionsNameTheAssaultRifle()
        {
            Faction F(string file) => AssetDatabase.LoadAssetAtPath<Faction>($"Assets/_Project/Data/Factions/{file}.asset");
            var liberteria = WeaponStats.For(rifle, F("UnitedStatesOfLiberteria"));
            Assert.That((liberteria.Name, liberteria.Shots, liberteria.BaseDamage, liberteria.Range), Is.EqualTo(("Semi-auto rifle", 2, 4, 20f)),
                "Liberteria: a semi-auto rifle with 2 shots, otherwise the Assault Rifle");
            foreach (var file in new[] { "UnitedKingdomOfAelfland", "DraksaReich" })
            {
                var bolt = WeaponStats.For(rifle, F(file));
                Assert.That((bolt.Name, bolt.Shots, bolt.BaseDamage, bolt.Range), Is.EqualTo(("Bolt-Action Rifle", 1, 4, 20f)), file);
            }
            foreach (var file in new[] { "UnitedStatesOfLiberteria", "UnitedKingdomOfAelfland", "DraksaReich" })
                Assert.That(WeaponStats.For(pistol, F(file)).Name, Is.EqualTo("Pistol"), "other weapons keep their names");
        }

        [Test]
        public void ThrownAndExplodingWeaponsUseTheUsersFaction()
        {
            var grenade = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/FragGrenade.asset");
            reich.weaponAdjustments.Add(new WeaponAdjustment { weapon = grenade, damage = 3 });
            var us = Spawn(reich, Side.Player, Vector3.zero);
            var w = new Weapon(grenade, us.Leader);
            Assert.That(w.BaseDamage, Is.EqualTo(5 + 3));
        }
    }
}
