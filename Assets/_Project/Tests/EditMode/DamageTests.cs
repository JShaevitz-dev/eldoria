using Eldoria.Rules;
using NUnit.Framework;

namespace Eldoria.Tests
{
    public class DamageTests
    {
        [Test]
        public void NoCritIsJustPenetrationDamage()
        {
            var d = Damage.Resolve(6, penetration: 1, armorLevel: 1, crit: false);
            Assert.That(d.CritBonus, Is.EqualTo(0));
            Assert.That(d.Total, Is.EqualTo(6));
        }

        [Test]
        public void CritAddsTheDamageAfterPenetrationAgain()
        {
            // 6 damage, penetration beats armor → 9; crit adds 100% of 9 → 18.
            var d = Damage.Resolve(6, penetration: 2, armorLevel: 1, crit: true);
            Assert.That(d.Penetration.Damage, Is.EqualTo(9));
            Assert.That(d.CritBonus, Is.EqualTo(9));
            Assert.That(d.Total, Is.EqualTo(18));
        }

        [Test]
        public void CritBonusIsBasedOnReducedDamage()
        {
            // 5 damage, penetration 1 below armor → 2 (rounded down); crit adds 2 → 4.
            var d = Damage.Resolve(5, penetration: 0, armorLevel: 1, crit: true);
            Assert.That(d.Total, Is.EqualTo(4));
        }

        [Test]
        public void ACritCantGetThroughArmorThatBlocksTheAttack()
        {
            var d = Damage.Resolve(10, penetration: 0, armorLevel: 3, crit: true);
            Assert.That(d.Total, Is.EqualTo(0));
            Assert.That(d.BonusEffectsApply, Is.False);
        }
    }
}
