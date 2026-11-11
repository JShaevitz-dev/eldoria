using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The army builder, shown when the player presses Play. First the player picks a faction, which decides the units
    /// they can take; then they pick up to 6 of those units and Confirm. The list is checked against the army rules
    /// (<see cref="ArmyRules"/>); a valid army goes to the battlefield, an invalid one says why.
    /// </summary>
    public class ArmyBuilderScreen
    {
        public enum Result { None, Back, Confirmed }

        private readonly UnitCatalog catalog;
        private readonly List<UnitTemplate> army = new List<UnitTemplate>();
        private List<UnitTemplate> available = new List<UnitTemplate>();
        private readonly List<Faction> factions;
        private readonly Faction lastFaction;
        private readonly List<UnitTemplate> lastArmy;
        private List<string> problems = new List<string>();
        private Vector2 scroll;
        private GUIStyle title, heading, body, small, good, bad, warn, stat, paragraph, button, bigButton;

        /// <summary>The chosen faction (null while the player is still choosing one).</summary>
        public Faction Faction { get; private set; }
        public IReadOnlyList<Faction> Factions => factions;
        public IReadOnlyList<UnitTemplate> Army => army;
        /// <summary>The units the chosen faction can take (none until a faction is chosen).</summary>
        public IReadOnlyList<UnitTemplate> Available => available;
        /// <summary>Why the last Confirm was refused (empty if it wasn't).</summary>
        public IReadOnlyList<string> Problems => problems;

        /// <param name="previousFaction">The faction of the player's last confirmed army, if any.</param>
        /// <param name="previousArmy">That army: the list starts from it if the same faction is chosen again.</param>
        public ArmyBuilderScreen(UnitCatalog catalog, Faction previousFaction = null, IEnumerable<UnitTemplate> previousArmy = null)
        {
            this.catalog = catalog;
            factions = catalog != null ? catalog.Factions.ToList() : new List<Faction>();
            lastFaction = previousFaction;
            lastArmy = previousArmy?.Where(u => u != null).ToList();
        }

        /// <summary>
        /// Chooses the faction: its units become the ones available, and the list starts from the player's last army
        /// for this faction, or the demo army (keeping only units the faction has).
        /// </summary>
        public void ChooseFaction(Faction faction)
        {
            Faction = faction;
            available = catalog != null ? catalog.UnitsFor(faction).ToList() : new List<UnitTemplate>();
            army.Clear();
            var start = faction == lastFaction && lastArmy != null ? lastArmy : catalog != null ? catalog.defaultArmy : null;
            if (start != null)
                foreach (var u in start)
                    if (ArmyRules.WhyCannotAdd(army, u, faction) == null) army.Add(u);
            problems.Clear();
        }

        /// <summary>Back to choosing a faction (the list is rebuilt when one is chosen).</summary>
        public void ChangeFaction()
        {
            Faction = null;
            available = new List<UnitTemplate>();
            army.Clear();
            problems.Clear();
        }

        /// <summary>Adds a unit to the next empty slot. Returns null if added; otherwise why not.</summary>
        public string Add(UnitTemplate unit)
        {
            if (Faction == null) return "Choose a faction first";
            string why = ArmyRules.WhyCannotAdd(army, unit, Faction);
            if (why == null) army.Add(unit);
            problems.Clear();
            return why;
        }

        public void RemoveAt(int slot)
        {
            if (slot >= 0 && slot < army.Count) army.RemoveAt(slot);
            problems.Clear();
        }

        public void Clear()
        {
            army.Clear();
            problems.Clear();
        }

        public void Randomize(System.Random rng)
        {
            army.Clear();
            army.AddRange(ArmyRules.RandomArmy(available, rng));
            problems.Clear();
        }

        /// <summary>Checks the list. True if it's a valid army; otherwise <see cref="Problems"/> says why.</summary>
        public bool Confirm()
        {
            problems = Faction == null ? new List<string> { "Choose a faction first." } : ArmyRules.Problems(army, Faction);
            return problems.Count == 0;
        }

        // ------------------------------------------------------------------ Drawing

        public Result Draw()
        {
            InitStyles();
            if (Faction == null) return DrawFactionChoice();
            var result = Result.None;
            float w = Mathf.Min(1280f, Screen.width - 40f), h = Mathf.Min(820f, Screen.height - 40f);
            var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(area);

            GUILayout.Label("BUILD YOUR ARMY", title);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(22f), GUILayout.Height(22f));
            Swatch(GUILayoutUtility.GetLastRect(), Faction.color);
            GUILayout.Label(Faction.displayName, heading);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Change faction", button, GUILayout.Width(160f), GUILayout.Height(30f))) ChangeFaction();
            GUILayout.EndHorizontal();
            if (Faction == null) { GUILayout.EndArea(); return Result.None; }
            GUILayout.Label($"Up to {ArmyRules.MaxUnits} units · at least 1 Commander · no duplicate Commander or Specialist units", small);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();

            // --- Available units ---
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(w * 0.62f));
            GUILayout.Label("Available units", heading);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var unit in available) DrawUnitEntry(unit);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // --- The army ---
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Your army ({army.Count}/{ArmyRules.MaxUnits})", heading);
            int remove = -1;   // applied after drawing, so every slot is drawn the same way this frame
            for (int i = 0; i < ArmyRules.MaxUnits; i++)
            {
                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(44f));
                if (i < army.Count)
                {
                    GUILayout.Label($"{i + 1}. {army[i].displayName}\n    {army[i].role.DisplayName()}", body);
                    if (GUILayout.Button("Remove", button, GUILayout.Width(90f))) remove = i;
                }
                else GUILayout.Label($"{i + 1}. — empty —", small);
                GUILayout.EndHorizontal();
            }
            if (remove >= 0) RemoveAt(remove);

            GUILayout.Space(8f);
            var current = ArmyRules.Problems(army);
            if (problems.Count > 0) foreach (var p in problems) GUILayout.Label(p, bad);
            else if (current.Count > 0) foreach (var p in current) GUILayout.Label(p, small);
            else GUILayout.Label("Your army is ready.", good);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", button, GUILayout.Height(36f))) Clear();
            if (GUILayout.Button("Random", button, GUILayout.Height(36f))) Randomize(Core.GameRandom.Shared);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", bigButton, GUILayout.Height(50f))) result = Result.Back;
            if (GUILayout.Button("Confirm army", bigButton, GUILayout.Height(50f)) && Confirm()) result = Result.Confirmed;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            return result;
        }

        /// <summary>
        /// One unit in the list: name, role and size with its Add button, then its stats (soldiers, weapons, abilities,
        /// keywords) and a paragraph on how it plays and what it does in an army.
        /// </summary>
        private void DrawUnitEntry(UnitTemplate unit)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(unit.displayName, heading);
            string unique = ArmyRules.IsUnique(unit.role) ? " · one per army" : "";
            GUILayout.Label($"{unit.role.DisplayName()}{unique} · {unit.Size} soldier{(unit.Size == 1 ? "" : "s")}", body);
            GUILayout.EndVertical();
            string why = ArmyRules.WhyCannotAdd(army, unit, Faction);
            GUI.enabled = why == null;
            if (GUILayout.Button(why == null ? "Add" : "—", button, GUILayout.Width(80f), GUILayout.Height(40f))) Add(unit);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (why != null) GUILayout.Label(why, warn);

            GUILayout.Space(4f);
            foreach (var line in UnitInfo.SoldierLines(unit)) GUILayout.Label(line, stat);
            foreach (var line in UnitInfo.WeaponLines(unit)) GUILayout.Label("<b>Weapon:</b> " + line, stat);
            foreach (var line in UnitInfo.AbilityLines(unit)) GUILayout.Label("<b>Ability:</b> " + line, stat);
            string keywords = UnitInfo.SharedKeywords(unit);
            if (keywords.Length > 0) GUILayout.Label("<b>Keywords:</b> " + keywords, stat);

            if (!string.IsNullOrEmpty(unit.description))
            {
                GUILayout.Space(4f);
                GUILayout.Label(unit.description, paragraph);
            }
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        /// <summary>The first step: choose a faction.</summary>
        private Result DrawFactionChoice()
        {
            var result = Result.None;
            float w = Mathf.Min(700f, Screen.width - 40f), h = Mathf.Min(560f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));
            GUILayout.Label("CHOOSE YOUR FACTION", title);
            GUILayout.Label("Your faction decides which units and commanders you can take.", small);
            GUILayout.Space(16f);
            foreach (var faction in factions)
            {
                int count = catalog != null ? catalog.UnitsFor(faction).Count() : 0;
                if (GUILayout.Button($"{faction.displayName}\n<size=14>{count} units available</size>", bigButton, GUILayout.Height(78f)))
                    ChooseFaction(faction);
                var b = GUILayoutUtility.GetLastRect();
                Swatch(new Rect(b.x + 18f, b.y + 18f, b.height - 36f, b.height - 36f), faction.color);
                GUILayout.Space(8f);
            }
            if (factions.Count == 0) GUILayout.Label("No factions found.", bad);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", bigButton, GUILayout.Height(50f))) result = Result.Back;
            GUILayout.EndArea();
            return result;
        }

        /// <summary>A square of the faction's colour (only drawn on Repaint, so it never disturbs the layout).</summary>
        private static void Swatch(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void InitStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.95f, 0.78f, 0.3f);
            heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            small.normal.textColor = new Color(0.75f, 0.77f, 0.82f);
            good = new GUIStyle(body);
            good.normal.textColor = new Color(0.45f, 0.9f, 0.5f);
            stat = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
            stat.normal.textColor = new Color(0.82f, 0.84f, 0.88f);
            paragraph = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, fontStyle = FontStyle.Italic };
            paragraph.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            warn = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            warn.normal.textColor = new Color(1f, 0.7f, 0.35f);
            bad = new GUIStyle(body) { fontStyle = FontStyle.Bold };
            bad.normal.textColor = new Color(1f, 0.4f, 0.35f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
        }
    }
}
