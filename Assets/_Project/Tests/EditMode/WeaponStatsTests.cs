using System.Linq;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;

namespace Eldoria.Tests
{
    /// <summary>The weapon templates as designed (Docs/Rules.md, weapon table).</summary>
    public class WeaponStatsTests
    {
        private static WeaponTemplate Load(string file) =>
            AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"Assets/_Project/Data/Weapons/{file}.asset");

        [Test]
        public void TheSmgHasTheRiflesDamagePenetration0NoBonusAndNoKeywords()
        {
            var smg = Load("SMG");
            var rifle = Load("AssaultRifle");
            Assert.That(smg.displayName, Is.EqualTo("SMG"));
            Assert.That(smg.baseDamage, Is.EqualTo(rifle.baseDamage), "same damage as the rifle");
            Assert.That(smg.PenetrationValue, Is.EqualTo(0));
            Assert.That(smg.accuracyBonus, Is.EqualTo(0));
            Assert.That(smg.keywords, Is.Empty);
        }

        [Test]
        public void TheSniperRifleIsAsDesigned()
        {
            var sniper = Load("SniperRifle");
            Assert.That(sniper.displayName, Is.EqualTo("Sniper Rifle"));
            Assert.That((sniper.baseDamage, sniper.PenetrationValue, sniper.accuracyBonus, sniper.range), Is.EqualTo((5, (int?)1, 50, 50f)));
            Assert.That(sniper.keywords.Count, Is.EqualTo(1));
            Assert.That(sniper.HasKeyword("Deployed"), Is.True);
        }

        [Test]
        public void ASoldierWith75AccuracyHas125PercentWithTheSniperRifleSoA25PercentCrit()
        {
            var sniper = Load("SniperRifle");
            var template = UnityEngine.ScriptableObject.CreateInstance<SoldierTemplate>();
            var go = new UnityEngine.GameObject("Sniper");
            try
            {
                template.accuracy = 75;
                template.weapons.Add(sniper);
                var soldier = go.AddComponent<Soldier>();
                soldier.Configure(template, false);
                var acc = Rules.Accuracy.ForAttack(soldier, soldier.Weapons[0], sprinted: false);
                Assert.That(acc.Final, Is.EqualTo(125));
                Assert.That((acc.HitChance, acc.CritChance), Is.EqualTo((100, 25)));

                // Deployed: not after moving.
                soldier.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Rules.Turns.NextTurn);
                Assert.That(soldier.WeaponFor(null), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void EveryWeaponHasShots()
        {
            var weapons = AssetDatabase.FindAssets("t:WeaponTemplate", new[] { "Assets/_Project/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponTemplate>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.That(weapons.Count, Is.GreaterThanOrEqualTo(8));
            foreach (var w in weapons)
            {
                int expected = w.displayName == "Machine Gun" ? 5 : w.displayName == "Flamethrower" ? 3 : 1;
                Assert.That(w.shots, Is.EqualTo(expected), w.displayName);
            }
            Assert.That(weapons.Select(w => w.displayName), Does.Contain("Machine Gun").And.Contain("Flamethrower"));
            var fresh = UnityEngine.ScriptableObject.CreateInstance<WeaponTemplate>();
            Assert.That(fresh.shots, Is.EqualTo(1), "new weapons default to 1");
            UnityEngine.Object.DestroyImmediate(fresh);
        }

        [Test]
        public void TheRocketLauncherIsAsDesigned()
        {
            var rocket = Load("RocketLauncher");
            Assert.That(rocket.displayName, Is.EqualTo("Rocket Launcher"));
            Assert.That((rocket.baseDamage, rocket.PenetrationValue, rocket.accuracyBonus, rocket.range, rocket.shots),
                Is.EqualTo((5, (int?)5, 0, 50f, 1)));
            Assert.That(rocket.keywords.Select(k => k.displayName), Is.EqualTo(new[] { "Manual Load" }));
            // Penetration 5 beats a Medium Tank's Armor Level 4: 150% damage (7).
            Assert.That(Eldoria.Rules.ArmorPenetration.Resolve(5, 5, 4).Damage, Is.EqualTo(7));
        }

        [Test]
        public void ThePistolAndAssaultRifleAreAsDesigned()
        {
            var pistol = Load("Pistol");
            Assert.That((pistol.baseDamage, pistol.PenetrationValue, pistol.accuracyBonus), Is.EqualTo((2, (int?)0, 0)));
            Assert.That(pistol.HasKeyword("Pistol"), Is.True);
            var rifle = Load("AssaultRifle");
            Assert.That((rifle.baseDamage, rifle.PenetrationValue, rifle.accuracyBonus), Is.EqualTo((4, (int?)1, 0)));
            Assert.That(rifle.keywords, Is.Empty);
        }
    }
}
