using System.Collections.Generic;

namespace Eldoria.Gameplay
{
    /// <summary>A map the player can fight on: its name, the scene it's built in, and a short description.</summary>
    public class Battlefield
    {
        public string Name;
        public string Scene;
        public string Description;
    }

    /// <summary>The maps the player can choose from after pressing Play (each scene must be in the build settings).</summary>
    public static class Battlefields
    {
        public static readonly Battlefield UrbanDemo = new Battlefield
        {
            Name = "Boxed in",
            Scene = "UrbanDemo",
            Description = "80 × 60 m city block around a central plaza, laid out with 180° symmetry. Roofless buildings with " +
                          "doorways and climbable windows, streets full of cars, sandbags and crates.",
        };

        public static readonly Battlefield EnglishCountryside = new Battlefield
        {
            Name = "Open Season",
            Scene = "Countryside",
            Description = "100 × 100 m of open, rolling fields. A knoll, a long ridge and a low rise block line of sight; a river " +
                          "crosses the map (wading costs double, or use the stone bridge or the ford); a farmhouse, a barn, a " +
                          "stone shed, and hay bales, carts and fences for cover.",
        };

        public static readonly IReadOnlyList<Battlefield> All = new[] { UrbanDemo, EnglishCountryside };
    }
}
