using System.Linq;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Gameplay
{
    /// <summary>
    /// Visual only: every living soldier on the battlefield shows their name and health ("Trooper_03  4/5") floating over
    /// their head, at all times. When a unit's soldiers are too close together on screen for full names (zoomed out),
    /// a short name is used instead ("T3 4/5"); the full name is on the HUD for the soldier under the cursor. Names are coloured by side (blue: player, red: AI); health turns red once wounded.
    /// Labels sit above the leader and close-range markers, and shrink with distance. Starts itself when play begins.
    /// </summary>
    public class SoldierLabels : MonoBehaviour
    {
        private const float HeightAboveHead = 1.4f;
        private const float RefreshInterval = 0.5f;

        private static SoldierLabels instance;

        private Soldier[] soldiers = new Soldier[0];
        private float nextRefresh;
        private GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("SoldierLabels");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SoldierLabels>();
            // A new scene (e.g. Play from the menu): look for its soldiers straight away.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => { if (instance != null) instance.nextRefresh = 0f; };
        }

        /// <summary>The label text for a soldier: their name and current / starting health.</summary>
        public static string LabelFor(Soldier soldier) => $"{soldier.name}  {soldier.Health}/{soldier.MaxHealth}";

        /// <summary>The compact label used when soldiers are too close together on screen for full names: "T3 5/5".</summary>
        public static string ShortLabelFor(Soldier soldier) => $"{ShortName(soldier.name)} {soldier.Health}/{soldier.MaxHealth}";

        /// <summary>Trooper_03 → T3, Sergeant → Sgt, Field Commander → Cmdr, Battlefield Priest → Priest; others: first 4 letters.</summary>
        public static string ShortName(string name)
        {
            string baseName = name, number = "";
            int underscore = name.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(name.Substring(underscore + 1), out int n))
            {
                baseName = name.Substring(0, underscore);
                number = n.ToString();
            }
            string shortBase = baseName switch
            {
                "Sergeant" => "Sgt",
                "Field Commander" => "Cmdr",
                "Battlefield Priest" => "Priest",
                _ => number.Length > 0 ? baseName.Substring(0, 1) : (baseName.Length <= 4 ? baseName : baseName.Substring(0, 4)),
            };
            return shortBase + number;
        }

        private void Update()
        {
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + RefreshInterval;
            soldiers = FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null || soldiers.Length == 0 || Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            foreach (var s in soldiers.Where(s => s != null && s.IsAlive && s.gameObject.activeInHierarchy))
            {
                Vector3 world = s.Position + Vector3.up * (s.Height + HeightAboveHead);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f || screen.x < -50f || screen.x > Screen.width + 50f || screen.y < -20f || screen.y > Screen.height + 20f)
                    continue;

                // Smaller when far away, so a zoomed-out view stays readable.
                style.fontSize = Mathf.RoundToInt(Mathf.Clamp(14f * 25f / screen.z, 10f, 14f));

                // Full name if it fits between this soldier and the next one in their unit (~2 m apart); otherwise the short name.
                float pixelsPerMeter = Screen.height / (2f * screen.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                string plainFull = LabelFor(s);
                bool roomy = s.Unit == null || s.Unit.LivingSoldiers.Count() == 1
                             || style.CalcSize(new GUIContent(plainFull)).x < pixelsPerMeter * 1.9f;
                string shownName = roomy ? s.name : ShortName(s.name);
                string plain = roomy ? plainFull : ShortLabelFor(s);

                bool player = s.Unit != null && s.Unit.Side == Side.Player;
                string nameColour = player ? "#9cc7ff" : "#ff9c92";
                string healthColour = s.Health < s.MaxHealth ? "#ff4a3d" : "#ffffff";
                string text = $"<color={nameColour}>{shownName}</color>{(roomy ? "  " : " ")}<color={healthColour}>{s.Health}/{s.MaxHealth}</color>";

                var rect = new Rect(screen.x - 90f, Screen.height - screen.y - 10f, 180f, 20f);
                // A dark offset copy behind the text keeps it readable over any background.
                var shadow = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
                GUI.Label(shadow, $"<color=#000000cc>{plain}</color>", style);
                GUI.Label(rect, text, style);
            }
        }
    }
}
