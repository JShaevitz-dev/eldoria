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
