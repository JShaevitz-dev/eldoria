using Eldoria.Units;

namespace Eldoria.Rules
{
    /// <summary>
    /// Whether a soldier can use a weapon right now (for shooting, or an ability that uses a weapon such as Frag Grenade):
    ///  - Close range: only weapons with the Pistol keyword (see <see cref="CloseRange"/>).
    ///  - Deployed: a weapon with this keyword can't be used in a volley if the soldier holding it has moved this turn
    ///    (they have the Moved status: any move this turn, even 0 m; holding position isn't moving).
    /// A soldier who can't use their primary weapon uses another weapon they carry that they can use, if any.
    /// </summary>
    public static class WeaponUse
    {
        /// <summary>Weapon keyword: can't be used if the soldier holding it has moved this turn.</summary>
        public const string DeployedKeyword = "Deployed";

        public static string WhyCannotUse(Soldier soldier, Weapon weapon) =>
            weapon == null ? "No weapon" : WhyCannotUse(soldier, weapon.HasKeyword);

        public static string WhyCannotUse(Soldier soldier, WeaponTemplate weapon) =>
            weapon == null ? "No weapon" : WhyCannotUse(soldier, weapon.HasKeyword);

        public static bool CanUse(Soldier soldier, Weapon weapon) => WhyCannotUse(soldier, weapon) == null;
        public static bool CanUse(Soldier soldier, WeaponTemplate weapon) => WhyCannotUse(soldier, weapon) == null;

        private static string WhyCannotUse(Soldier soldier, System.Func<string, bool> weaponHas)
        {
            if (weaponHas(DeployedKeyword) && soldier != null && soldier.HasKeyword(KeywordLibrary.Moved))
                return "moved this turn with a Deployed weapon";
            if (!weaponHas(CloseRange.PistolKeyword) && CloseRange.IsInCloseRange(soldier))
                return "in close range without a pistol";
            return null;
        }
    }
}
