using System.Collections.Generic;
using System.Linq;
using Eldoria.Network;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The main menu (Scenes/MainMenu, the first scene the game opens):
    ///   Play - choose a map, then build an army; confirming a valid army starts a game on that map against an AI army.
    ///   Multiplayer - play another person over the network: one Hosts and tells the other their IP address, the other
    ///     Joins by typing it in. The host chooses the map, both build an army, and the battle starts when both are ready.
    ///   Quit - closes the program (in the Unity editor, stops Play mode).
    ///   Turn timer - On/Off (off by default) and its length (10 minutes by default); remembered between sessions.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        /// <summary>The scene names (they must be in the build settings).</summary>
        public const string MenuScene = "MainMenu";
        public const string GameScene = "UrbanDemo";

        private GUIStyle titleStyle, subtitleStyle, buttonStyle, optionStyle, optionLabelStyle, mapButtonStyle;

        /// <summary>True while the player is choosing a map (after Play, before the army builder).</summary>
        public bool ChoosingMap { get; private set; }

        /// <summary>The map the next battle is fought on (the Boxed in unless another is chosen).</summary>
        public static Battlefield ChosenMap { get; private set; } = Battlefields.UrbanDemo;

        /// <summary>Play: choose a map first.</summary>
        public void OpenMapChoice()
        {
            Builder = null;
            ChoosingMap = true;
        }

        /// <summary>Chooses the map for the next battle and opens the army builder.</summary>
        public void ChooseMap(Battlefield map)
        {
            ChosenMap = map ?? Battlefields.UrbanDemo;
            ChoosingMap = false;
            // Multiplayer: the host's choice is the map for both players.
            if (MultiplayerSession.Active != null) MultiplayerSession.Active.ChooseMap(ChosenMap.Scene);
            OpenArmyBuilder();
        }

        // ------------------------------------------------------------------ Multiplayer pages

        public enum MultiplayerPage { None, HostOrJoin, Hosting, Joining, Lobby, WaitingForArmies }

        /// <summary>Which multiplayer page is showing (None: not in the multiplayer menus).</summary>
        public MultiplayerPage Page { get; private set; }
        private string joinAddress;
        private const string JoinAddressPref = "Eldoria.JoinAddress";

        public void OpenMultiplayer()
        {
            MultiplayerSession.Leave();
            Builder = null;
            ChoosingMap = false;
            Page = MultiplayerPage.HostOrJoin;
        }

        /// <summary>Host: wait for the other player to join on <see cref="NetConnection.DefaultPort"/>.</summary>
        public MultiplayerSession Host(bool loopbackOnly = false)
        {
            var session = MultiplayerSession.HostGame(NetConnection.DefaultPort, loopbackOnly);
            Page = MultiplayerPage.Hosting;
            return session;
        }

        /// <summary>Join: connect to the host at <paramref name="address"/>.</summary>
        public MultiplayerSession Join(string address)
        {
            address = (address ?? "").Trim();
            joinAddress = address;
            try { PlayerPrefs.SetString(JoinAddressPref, address); PlayerPrefs.Save(); } catch { }
            var session = MultiplayerSession.JoinGame(address);
            Page = MultiplayerPage.Joining;
            return session;
        }

        /// <summary>Leaves the multiplayer menus (and the game with the other player, if any) for the main menu.</summary>
        public void LeaveMultiplayer()
        {
            MultiplayerSession.Leave();
            Builder = null;
            ChoosingMap = false;
            Page = MultiplayerPage.None;
        }

        /// <summary>
        /// Multiplayer: this player's army is ready. It's checked like any army, sent to the other player, and the battle
        /// starts once both are ready. Returns why the army isn't valid (empty if it was sent).
        /// </summary>
        public List<string> SubmitMultiplayerArmy(Faction faction, IReadOnlyList<UnitTemplate> army, IReadOnlyList<UnitLoadout> loadouts = null)
        {
            var problems = faction == null ? new List<string> { "Choose a faction first." } : ArmyRules.Problems(army, faction);
            if (problems.Count > 0 || MultiplayerSession.Active == null) return problems;
            lastArmy = new List<UnitTemplate>(army);
            lastLoadouts = loadouts?.Select(l => l?.Clone()).ToList();
            lastFaction = faction;
            Builder = null;
            Page = MultiplayerPage.WaitingForArmies;
            MultiplayerSession.Active.SubmitArmy(faction, army, loadouts);
            return problems;
        }

        /// <summary>Moves the multiplayer pages along as the connection and the other player progress.</summary>
        private void UpdateMultiplayer()
        {
            var session = MultiplayerSession.Active;
            if (Page == MultiplayerPage.None || Page == MultiplayerPage.HostOrJoin || session == null) return;
            if (session.Lost || session.BattleStarted) return;
            if ((Page == MultiplayerPage.Hosting || Page == MultiplayerPage.Joining) && session.IsConnected)
            {
                Page = MultiplayerPage.Lobby;
                // The host picks the battlefield; the other player waits for it.
                if (session.Role == NetRole.Host) OpenMapChoice();
            }
            if (Page == MultiplayerPage.Lobby && session.Role == NetRole.Client && Builder == null && !string.IsNullOrEmpty(session.MapScene))
            {
                ChosenMap = Battlefields.All.FirstOrDefault(b => b.Scene == session.MapScene) ?? ChosenMap;
                OpenArmyBuilder();
            }
        }

        private static string MapName(string scene) =>
            Battlefields.All.FirstOrDefault(b => b.Scene == scene)?.Name ?? scene;

        private void DrawMultiplayer()
        {
            var session = MultiplayerSession.Active;
            float w = Mathf.Min(640f, Screen.width - 40f), h = Mathf.Min(520f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));
            var heading = new GUIStyle(titleStyle) { fontSize = 36 };
            var text = new GUIStyle(subtitleStyle) { wordWrap = true };
            var big = new GUIStyle(subtitleStyle) { fontSize = 26, fontStyle = FontStyle.Bold };
            big.normal.textColor = new Color(0.95f, 0.78f, 0.3f);
            GUILayout.Label("MULTIPLAYER", heading);
            GUILayout.Space(16f);

            if (session != null && session.Lost)
            {
                GUILayout.Label(session.Error ?? "The connection to the other player was lost.", text);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(50f))) OpenMultiplayer();
                GUILayout.EndArea();
                return;
            }

            switch (Page)
            {
                case MultiplayerPage.HostOrJoin:
                    GUILayout.Label("One player hosts and tells the other their IP address; the other joins by typing it in.", text);
                    GUILayout.Space(16f);
                    if (GUILayout.Button("Host", buttonStyle, GUILayout.Height(60f))) Host();
                    GUILayout.Space(10f);
                    if (GUILayout.Button("Join", buttonStyle, GUILayout.Height(60f)))
                    {
                        Page = MultiplayerPage.Joining;
                        joinAddress = PlayerPrefs.GetString(JoinAddressPref, "");
                    }
                    break;

                case MultiplayerPage.Hosting:
                    if (session != null && session.Error != null) GUILayout.Label(session.Error, text);
                    else
                    {
                        GUILayout.Label("Waiting for the other player to join…", text);
                        GUILayout.Space(10f);
                        GUILayout.Label("Give them your IP address:", text);
                        var addresses = NetConnection.LocalAddresses();
                        if (addresses.Count == 0) GUILayout.Label("(couldn't find this computer's address)", text);
                        foreach (var a in addresses) GUILayout.Label(a, big);
                        GUILayout.Label($"Port {NetConnection.DefaultPort}", text);
                        GUILayout.Space(6f);
                        GUILayout.Label("Over the internet, they need your public IP address, and your router must forward " +
                                        $"TCP port {NetConnection.DefaultPort} to this computer.", text);
                    }
                    break;

                case MultiplayerPage.Joining:
                    GUILayout.Label("The host's IP address:", text);
                    bool connecting = session != null && session.Error == null && !session.IsConnected;
                    GUI.enabled = !connecting;
                    var field = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                    joinAddress = GUILayout.TextField(joinAddress ?? "", 64, field, GUILayout.Height(44f));
                    GUILayout.Space(10f);
                    if (GUILayout.Button("Connect", buttonStyle, GUILayout.Height(54f)) && !string.IsNullOrWhiteSpace(joinAddress)) Join(joinAddress);
                    GUI.enabled = true;
                    if (connecting) GUILayout.Label($"Connecting to {joinAddress}…", text);
                    else if (session != null && session.Error != null) GUILayout.Label(session.Error, text);
                    break;

                case MultiplayerPage.Lobby:
                    GUILayout.Label("Connected!", big);
                    GUILayout.Label("The host is choosing the battlefield…", text);
                    break;

                case MultiplayerPage.WaitingForArmies:
                    GUILayout.Label("Your army is ready.", big);
                    if (session != null && !string.IsNullOrEmpty(session.MapScene)) GUILayout.Label($"Battlefield: {MapName(session.MapScene)}", text);
                    GUILayout.Label(session != null && session.RemoteArmy == null
                        ? "Waiting for your opponent to finish their army…"
                        : "Starting the battle…", text);
                    if (session != null && session.Error != null) GUILayout.Label(session.Error, text);
                    break;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Page == MultiplayerPage.HostOrJoin ? "Back" : "Cancel", buttonStyle, GUILayout.Height(50f)))
            {
                if (Page == MultiplayerPage.HostOrJoin) LeaveMultiplayer();
                else OpenMultiplayer();
            }
            GUILayout.EndArea();
        }

        /// <summary>The army builder, while it's open (null on the main menu itself).</summary>
        public ArmyBuilderScreen Builder { get; private set; }

        /// <summary>The player's last confirmed army and its faction: the builder starts from it when that faction is chosen again.</summary>
        private static List<UnitTemplate> lastArmy;
        private static List<UnitLoadout> lastLoadouts;
        private static Faction lastFaction;

        /// <summary>Forgets the last confirmed army: the builder starts from the demo army again.</summary>
        public static void ForgetLastArmy()
        {
            lastArmy = null;
            lastLoadouts = null;
            lastFaction = null;
            ChosenMap = Battlefields.UrbanDemo;
        }

        /// <summary>Opens the army builder (after the map is chosen).</summary>
        public void OpenArmyBuilder() => Builder = new ArmyBuilderScreen(UnitCatalog.Load(), lastFaction, lastArmy, lastLoadouts);

        /// <summary>
        /// Starts a battle with this faction and army, if it's valid: the AI picks a random faction and builds a random valid
        /// army from it, and both go to the chosen map. Returns why the army isn't valid (empty if the battle started).
        /// </summary>
        /// <param name="loadouts">Each unit's weapon swaps and equipment (same order as <paramref name="army"/>), if any.</param>
        public static List<string> StartBattle(Faction faction, IReadOnlyList<UnitTemplate> army, IReadOnlyList<UnitLoadout> loadouts = null)
        {
            var problems = faction == null ? new List<string> { "Choose a faction first." } : ArmyRules.Problems(army, faction);
            if (problems.Count > 0) return problems;
            var catalog = UnitCatalog.Load();
            var rng = Core.GameRandom.Shared;
            var factions = catalog != null ? catalog.Factions.ToList() : new List<Faction>();
            var aiFaction = factions.Count > 0 ? factions[rng.Next(factions.Count)] : faction;
            var aiArmy = ArmyRules.RandomArmy(catalog != null ? catalog.UnitsFor(aiFaction) : army, rng);
            lastArmy = new List<UnitTemplate>(army);
            lastLoadouts = loadouts?.Select(l => l?.Clone()).ToList();
            lastFaction = faction;
            ArmySelection.Set(army, aiArmy, faction, aiFaction, loadouts);
            SceneManager.LoadScene(ChosenMap.Scene);
            return problems;
        }

        /// <summary>Closes the program (in the editor: stops Play mode).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Back to this menu (e.g. from the end-of-battle banner).</summary>
        public static void BackToMenu()
        {
            Network.MultiplayerSession.Leave();   // leaving a multiplayer battle ends the game for both players
            SceneManager.LoadScene(MenuScene);
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = new Color(0.95f, 0.78f, 0.3f);
                subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                subtitleStyle.normal.textColor = new Color(0.75f, 0.77f, 0.82f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
                optionStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                optionLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                mapButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(20, 20, 12, 12) };
            }

            UpdateMultiplayer();
            bool multiplayer = Page != MultiplayerPage.None;
            var session = MultiplayerSession.Active;
            if (multiplayer && session != null && session.Lost) { Builder = null; ChoosingMap = false; }

            if (Builder != null)
            {
                var result = Builder.Draw();
                if (result == ArmyBuilderScreen.Result.Back)
                {
                    // Multiplayer: the host goes back to the map choice; the other player leaves the game.
                    if (!multiplayer || (session != null && session.Role == NetRole.Host)) OpenMapChoice();
                    else OpenMultiplayer();
                }
                else if (result == ArmyBuilderScreen.Result.Confirmed)
                {
                    if (multiplayer) SubmitMultiplayerArmy(Builder.Faction, Builder.Army, Builder.Loadouts);
                    else StartBattle(Builder.Faction, Builder.Army, Builder.Loadouts);
                }
                return;
            }
            if (ChoosingMap)
            {
                DrawMapChoice();
                return;
            }
            if (multiplayer)
            {
                DrawMultiplayer();
                return;
            }

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.Label(new Rect(cx - 400f, cy - 220f, 800f, 100f), "ELDORIA", titleStyle);
            GUI.Label(new Rect(cx - 400f, cy - 125f, 800f, 30f), "Turn-based tactics", subtitleStyle);

            const float w = 260f, h = 60f;
            if (GUI.Button(new Rect(cx - w / 2f, cy - 40f, w, h), "Play", buttonStyle)) OpenMapChoice();
            if (GUI.Button(new Rect(cx - w / 2f, cy + 35f, w, h), "Multiplayer", buttonStyle)) OpenMultiplayer();
            if (GUI.Button(new Rect(cx - w / 2f, cy + 110f, w, h), "Quit", buttonStyle)) Quit();

            // Options: the turn timer (off by default; 10 minutes by default).
            bool timerOn = TurnTimerSettings.Enabled;
            if (GUI.Button(new Rect(cx - w / 2f, cy + 200f, w, 40f), $"Turn timer: {(timerOn ? "On" : "Off")}", optionStyle))
                TurnTimerSettings.Enabled = !timerOn;
            GUI.enabled = timerOn;
            if (GUI.Button(new Rect(cx - w / 2f, cy + 250f, 50f, 36f), "<", optionStyle)) StepMinutes(-1);
            GUI.Label(new Rect(cx - w / 2f + 50f, cy + 250f, w - 100f, 36f), $"{TurnTimerSettings.Minutes:0.#} min per turn", optionLabelStyle);
            if (GUI.Button(new Rect(cx + w / 2f - 50f, cy + 250f, 50f, 36f), ">", optionStyle)) StepMinutes(+1);
            GUI.enabled = true;
        }

        /// <summary>The map choice: one button per map, with its description.</summary>
        private void DrawMapChoice()
        {
            float w = Mathf.Min(760f, Screen.width - 40f), h = Mathf.Min(560f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));
            var heading = new GUIStyle(titleStyle) { fontSize = 36 };
            GUILayout.Label("CHOOSE A BATTLEFIELD", heading);
            GUILayout.Space(16f);
            foreach (var map in Battlefields.All)
            {
                if (GUILayout.Button($"<size=24><b>{map.Name}</b></size>\n{map.Description}", mapButtonStyle, GUILayout.MinHeight(110f)))
                    ChooseMap(map);
                GUILayout.Space(10f);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Page != MultiplayerPage.None ? "Leave" : "Back", buttonStyle, GUILayout.Height(50f)))
            {
                if (Page != MultiplayerPage.None) OpenMultiplayer();   // the host leaves the game with the other player
                else ChoosingMap = false;
            }
            GUILayout.EndArea();
        }

        /// <summary>Moves the turn timer length to the next shorter or longer option.</summary>
        public static void StepMinutes(int direction)
        {
            var options = TurnTimerSettings.MinuteOptions;
            int i = System.Array.FindIndex(options, m => Mathf.Approximately(m, TurnTimerSettings.Minutes));
            if (i < 0) i = System.Array.IndexOf(options, TurnTimerSettings.DefaultMinutes);
            TurnTimerSettings.Minutes = options[Mathf.Clamp(i + direction, 0, options.Length - 1)];
        }
    }
}
