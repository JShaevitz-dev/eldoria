using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The army builder, shown when the player presses Play: pick up to 6 units from the catalog, then Confirm. The list is
    /// checked against the army rules (<see cref="ArmyRules"/>); a valid army goes to the battlefield, an invalid one says why.
    /// </summary>
    public class ArmyBuilderScreen
    {
        public enum Result { None, Back, Confirmed }

        private readonly List<UnitTemplate> army = new List<UnitTemplate>();
        private readonly IReadOnlyList<UnitTemplate> available;
        private List<string> problems = new List<string>();
        private Vector2 scroll;
        private GUIStyle title, heading, body, small, good, bad, button, bigButton;

        public IReadOnlyList<UnitTemplate> Army => army;
        public IReadOnlyList<UnitTemplate> Available => available;
        /// <summary>Why the last Confirm was refused (empty if it wasn't).</summary>
        public IReadOnlyList<string> Problems => problems;

        public ArmyBuilderScreen(UnitCatalog catalog, IEnumerable<UnitTemplate> startingArmy = null)
        {
            available = catalog != null ? catalog.Available.ToList() : new List<UnitTemplate>();
            var start = startingArmy ?? (catalog != null ? catalog.defaultArmy : null);
            if (start != null) army.AddRange(start.Where(u => u != null).Take(ArmyRules.MaxUnits));
        }

        /// <summary>Adds a unit to the next empty slot. Returns null if added; otherwise why not.</summary>
        public string Add(UnitTemplate unit)
        {
            string why = ArmyRules.WhyCannotAdd(army, unit);
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
            problems = ArmyRules.Problems(army);
            return problems.Count == 0;
        }

        // ------------------------------------------------------------------ Drawing

        public Result Draw()
        {
            InitStyles();
            var result = Result.None;
            float w = Mathf.Min(1100f, Screen.width - 40f), h = Mathf.Min(720f, Screen.height - 40f);
            var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(area);

            GUILayout.Label("BUILD YOUR ARMY", title);
            GUILayout.Label($"Up to {ArmyRules.MaxUnits} units · at least 1 Commander · no duplicate Commander or Specialist units", small);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();

            // --- Available units ---
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(w * 0.56f));
            GUILayout.Label("Available units", heading);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var unit in available)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(unit.displayName, heading);
                string unique = ArmyRules.IsUnique(unit.role) ? " · one per army" : "";
                GUILayout.Label($"{unit.role.DisplayName()}{unique} · {unit.Size} soldier{(unit.Size == 1 ? "" : "s")}", body);
                if (unit.Size > 1) GUILayout.Label(ArmyRules.Composition(unit), small);
                GUILayout.EndVertical();
                string why = ArmyRules.WhyCannotAdd(army, unit);
                GUI.enabled = why == null;
                if (GUILayout.Button(why == null ? "Add" : "—", button, GUILayout.Width(80f), GUILayout.Height(40f))) Add(unit);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (why != null) GUILayout.Label(why, small);
            }
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
            bad = new GUIStyle(body) { fontStyle = FontStyle.Bold };
            bad.normal.textColor = new Color(1f, 0.4f, 0.35f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        }
    }
}
