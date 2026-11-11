using Eldoria.Units;

namespace Eldoria.Rules
{
    /// <summary>
    /// Whether a soldier can use a weapon right now (for shooting, or an ability that uses a weapon such as Frag Grenade):
    ///  - Close range: only weapons with the Pistol or Melee keyword (see <see cref="CloseRange"/>).
    ///  - Deployed: a weapon with this keyword can't be used in a volley if the soldier holding it has moved this turn
    ///    (they have the Moved status: any move this turn, even 0 m; holding position isn't moving) - unless one of the
    ///    soldier's abilities nullifies Deployed (e.g. the Stahl Colossus's Stabilizers).
    ///  - Manual Load: a weapon with this keyword can't be fired more than once a turn (one volley; all of that volley's
    ///    shots count as the one firing), even if its soldier has a way to shoot more than one volley in a turn.
    /// A soldier who can't use their primary weapon uses another weapon they carry that they can use, if any.
    /// </summary>
    public static class WeaponUse
    {
        /// <summary>Weapon keyword: can't be used if the soldier holding it has moved this turn.</summary>
        public const string DeployedKeyword = "Deployed";

        /// <summary>Weapon keyword: can't be fired more than once a turn.</summary>
        public const string ManualLoadKeyword = "Manual Load";

        public static string WhyCannotUse(Soldier soldier, Weapon weapon)
        {
            if (weapon == null) return "No weapon";
            if (weapon.HasKeyword(ManualLoadKeyword) && weapon.FiredOnTurn == Turns.Current)
                return "already fired this turn (Manual Load)";
            return WhyCannotUse(soldier, weapon.HasKeyword);
        }

        /// <summary>Records that the weapon fired this turn (after a volley it shot in).</summary>
        public static void MarkFired(Weapon weapon)
        {
            if (weapon != null) weapon.FiredOnTurn = Turns.Current;
        }

        public static string WhyCannotUse(Soldier soldier, WeaponTemplate weapon) =>
            weapon == null ? "No weapon" : WhyCannotUse(soldier, weapon.HasKeyword);

        public static bool CanUse(Soldier soldier, Weapon weapon) => WhyCannotUse(soldier, weapon) == null;
        public static bool CanUse(Soldier soldier, WeaponTemplate weapon) => WhyCannotUse(soldier, weapon) == null;

        private static string WhyCannotUse(Soldier soldier, System.Func<string, bool> weaponHas)
        {
            if (weaponHas(DeployedKeyword) && soldier != null && soldier.HasKeyword(KeywordLibrary.Moved) && !soldier.Nullifies(DeployedKeyword))
                return "moved this turn with a Deployed weapon";
            if (!CloseRange.UsableInCloseRange(weaponHas) && CloseRange.IsInCloseRange(soldier))
                return "in close range without a pistol or melee weapon";
            return null;
        }
    }
}
