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
