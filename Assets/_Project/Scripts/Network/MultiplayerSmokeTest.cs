using System.Collections;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Network
{
    /// <summary>
    /// An end-to-end check of multiplayer with two copies of the built game on one computer, off unless started with
    /// <c>-mpSmoke host</c> or <c>-mpSmoke join</c>. Both go through the multiplayer menu (the host on this computer only,
    /// the other joining 127.0.0.1), the host picks Open Season, both submit an army, and they play scripted turns against
    /// each other (move toward the enemy, shoot what they can, end the turn) until turn 4 or the end of the battle.
    /// Passes (exit code 0) if nothing went wrong and every replayed order gave the same state in both games.
    /// Usage (start the host first):
    ///   Eldoria.exe -batchmode -nographics -mpSmoke host -logFile host.log
    ///   Eldoria.exe -batchmode -nographics -mpSmoke join -logFile join.log
    /// </summary>
    public class MultiplayerSmokeTest : MonoBehaviour
    {
        private const int LastTurn = 4;
        private int problems;
        private bool hosting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-mpSmoke");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("MultiplayerSmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerSmokeTest>().hosting = args[i + 1] == "host";
        }

        private void Check(bool ok, string what)
        {
            if (ok) Debug.Log($"[Eldoria] MP smoke ok: {what}");
            else
            {
                problems++;
                Debug.LogWarning($"[Eldoria] MP smoke FAILED: {what}");
            }
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) problems++;
            };
            yield return Run();
            var session = MultiplayerSession.Active;
            if (session != null) Check(session.Desyncs == 0, $"no orders out of sync ({session.Desyncs})");
            Debug.Log(problems == 0 ? "[Eldoria] MP smoke test PASSED" : $"[Eldoria] MP smoke test FAILED ({problems} problem(s))");
            // Give the last orders time to reach the other game before leaving.
            yield return new WaitForSecondsRealtime(2f);
            MultiplayerSession.Leave();
            Application.Quit(problems == 0 ? 0 : 1);
        }

        private IEnumerator Run()
        {
            yield return Until(() => FindFirstObjectByType<MainMenu>() != null, 10f);
            var menu = FindFirstObjectByType<MainMenu>();
            Check(menu != null, "main menu");
            if (menu == null) yield break;

            menu.OpenMultiplayer();
            var session = hosting ? menu.Host(loopbackOnly: true) : menu.Join("127.0.0.1");
            yield return Until(() => session.IsConnected || session.Error != null, 60f);
            Check(session.IsConnected, $"connected as {(hosting ? "host" : "guest")}" + (session.Error != null ? $" ({session.Error})" : ""));
            if (!session.IsConnected) yield break;

            var catalog = UnitCatalog.Load();
            var faction = catalog.Factions.ElementAt(hosting ? 0 : catalog.Factions.Count() - 1);
            var army = ArmyRules.RandomArmy(catalog.UnitsFor(faction), new System.Random(hosting ? 11 : 22));
            if (hosting) menu.ChooseMap(Battlefields.EnglishCountryside);
            else
            {
                yield return Until(() => !string.IsNullOrEmpty(session.MapScene), 30f);
                Check(session.MapScene == Battlefields.EnglishCountryside.Scene, $"the host's map arrived ({session.MapScene})");
            }
            var refused = menu.SubmitMultiplayerArmy(faction, army);
            Check(refused.Count == 0, "army submitted " + string.Join("; ", refused));

            yield return Until(() => FindFirstObjectByType<MovementController>() != null, 60f);
            var c = FindFirstObjectByType<MovementController>();
            Check(c != null && c.IsMultiplayer, "the battle started");
            if (c == null) yield break;
            yield return null;
            Debug.Log($"[Eldoria] MP smoke: commanding {(c.LocalSide == Side.Player ? "south (moves first)" : "north")}, {c.PlayerUnits.Count} units.");

            float deadline = Time.realtimeSinceStartup + 300f;
            while (!c.BattleOver && Turns.Current < LastTurn && !session.Lost && Time.realtimeSinceStartup < deadline)
            {
                if (c.IsRemoteTurn) { yield return null; continue; }
                yield return PlayTurn(c);
            }
            Check(c.BattleOver || Turns.Current >= LastTurn, $"played to turn {Turns.Current}{(c.BattleOver ? " (battle over)" : "")}");
            Check(!session.Lost, "still connected");
            Debug.Log($"[Eldoria] MP smoke checksum turn {Turns.Current}: {c.StateChecksum()}");
        }

        /// <summary>This player's turn: every unit moves toward the enemy, shoots if it can, then the turn ends.</summary>
        private IEnumerator PlayTurn(MovementController c)
        {
            var forward = c.LocalSide == Side.Player ? Vector3.forward : Vector3.back;
            foreach (var unit in c.PlayerUnits.ToList())
            {
                if (c.BattleOver) yield break;
                if (unit == null || unit.IsDestroyed || !c.SelectUnit(unit)) continue;
                var leader = unit.Leader;
                if (!c.OrderLeaderMove(leader.Position + forward * leader.Move * 0.9f)) c.OrderHold();
                yield return new WaitForSecondsRealtime(0.1f);
                c.OrderGather();
                if (!c.OrderEndMove()) { c.OrderReset(); c.OrderHold(); c.OrderEndMove(); }
                foreach (var enemy in FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side != c.LocalSide && !u.IsDestroyed).OrderBy(u => u.NetId))
                    if (c.OrderShoot(enemy) != null)
                    {
                        Debug.Log($"[Eldoria] MP smoke: {unit.DisplayName} shot at {enemy.DisplayName}: {c.LastShooting.Hits} hits.");
                        break;
                    }
                yield return new WaitForSecondsRealtime(0.1f);
            }
            if (!c.BattleOver) c.EndTurn();
            yield return null;
        }
    }
}
