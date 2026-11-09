using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Eldoira.Core;
using Eldoira.Rules;
using Eldoira.Terrain;
using Eldoira.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Eldoira.Gameplay
{
    /// <summary>
    /// Player control of one squad's movement (test harness for the movement rules).
    ///   0. Each turn starts with no unit selected: click one of your units (or press Tab) to command it.
    ///   1. Click the ground to move the squad leader (gold marker): inside the blue ring = move (10 m),
    ///      inside the orange ring = sprint (20 m). H = hold position.
    ///   2. Click a soldier to select them, then click the ground to place them within
    ///      the green cohesion ring (5 m of the leader). G = gather everyone out of cohesion automatically.
    ///   3. Enter = end move (only once everyone is within 5 m). Backspace = undo the squad's move.
    ///   N = start the next turn. K = (debug) kill the squad leader to test succession.
    /// Camera: WASD / Q E / right-drag / middle-drag / scroll (left mouse is free for orders).
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        /// <summary>The unit being commanded; null until the player picks one (each turn starts with none selected).</summary>
        private Unit squad;
        [SerializeField] private Side playerSide = Side.Player;
        [SerializeField] private Camera cam;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;

        private static readonly Color MoveColor = new Color(0.3f, 0.75f, 1f, 0.9f);
        private static readonly Color SprintColor = new Color(1f, 0.6f, 0.15f, 0.9f);
        private static readonly Color FollowColor = new Color(0.35f, 1f, 0.45f, 0.9f);
        private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color SelectColor = new Color(1f, 0.9f, 0.3f, 1f);

        private UnitMoveActivation activation;
        private Soldier selected;

        private readonly Dictionary<Soldier, Coroutine> walking = new Dictionary<Soldier, Coroutine>();
        private readonly Dictionary<Soldier, Quaternion> startRotations = new Dictionary<Soldier, Quaternion>();

        private RangeRing moveRing, sprintRing, followRing, selectRing, cursorRing, pathLine;
        private readonly List<RangeRing> cohesionRings = new List<RangeRing>();

        private bool hasHover;
        private Vector3 hoverPoint;
        private Soldier hoverSoldier;
        private MoveCheck hoverCheck;
        private (Vector3 point, Soldier mover, MovePhase phase)? lastPreviewKey;

        private string message;
        private float messageUntil;
        private Rect hudRect = new Rect(10, 10, 420, 520);

        // Shooting (steps 3-4: declare shooting, pick an enemy unit; the rest is UnitShooting).
        private bool shootMode;
        private bool actionTaken;
        private ShootingResult lastShooting;
        private readonly List<RangeRing> targetRings = new List<RangeRing>();
        private readonly List<RangeRing> tracers = new List<RangeRing>();
        private float tracersUntil;

        // Active abilities: pick one, then click a target soldier/unit.
        private Ability abilityMode;
        private Soldier abilityUser;
        private RangeRing abilityRing;
        private readonly List<string> recentLog = new List<string>();

        public ShootingResult LastShooting => lastShooting;
        /// <summary>The active unit has taken its action this turn (shot, or used an ability).</summary>
        public bool HasTakenAction => actionTaken;

        public UnitMoveActivation Activation => activation;
        public Soldier Selected => selected;

        // Step 1: the player picks which of their units to activate. Each unit acts once per turn.
        private readonly List<Unit> playerUnits = new List<Unit>();
        private readonly HashSet<Unit> actedThisTurn = new HashSet<Unit>();

        /// <summary>The unit currently being commanded (null when none is selected, e.g. at the start of a turn).</summary>
        public Unit ActiveUnit => squad;
        public IReadOnlyList<Unit> PlayerUnits => playerUnits;
        public bool HasActed(Unit unit) => actedThisTurn.Contains(unit);

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            playerUnits.Clear();
            foreach (var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                if (u.Side == playerSide) playerUnits.Add(u);
            if (playerUnits.Count == 0)
            {
                Debug.LogError("[Eldoira] MovementController needs at least one player unit.");
                enabled = false;
                return;
            }
            // A stable order for Tab: as listed in the scene hierarchy.
            playerUnits.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            moveRing = RangeRing.Create("Ring_Move", transform, 0.12f);
            sprintRing = RangeRing.Create("Ring_Sprint", transform, 0.12f);
            followRing = RangeRing.Create("Ring_Follow", transform, 0.1f);
            selectRing = RangeRing.Create("Ring_Selected", transform, 0.06f);
            cursorRing = RangeRing.Create("Ring_Cursor", transform, 0.06f);
            pathLine = RangeRing.Create("Line_Path", transform, 0.06f);
            abilityRing = RangeRing.Create("Ring_Ability", transform, 0.1f);

            if (BattlefieldNavigation.Measurer != null) RangeRing.SurfaceHeight = TerrainMeasurer.SurfaceHeight;

            foreach (var u in playerUnits) u.LeaderChanged += OnLeaderChanged;

            Turns.ResetCounter();
            NextTurn();
        }

        private void OnDestroy()
        {
            foreach (var u in playerUnits)
                if (u != null) u.LeaderChanged -= OnLeaderChanged;
        }

        /// <summary>
        /// Step 1: make <paramref name="unit"/> the unit being commanded. Allowed before the current unit has started
        /// moving, or once it has finished. A unit that has already acted this turn can't be picked again.
        /// </summary>
        public bool SelectUnit(Unit unit)
        {
            if (unit == null || unit == squad || !playerUnits.Contains(unit) || unit.IsDestroyed) return false;
            if (actedThisTurn.Contains(unit))
            {
                Flash($"{unit.DisplayName} has already acted this turn.");
                return false;
            }
            if (activation != null && activation.Phase == MovePhase.MoveFollowers)
            {
                Flash("Finish this unit's move first.");
                return false;
            }
            if (squad != null && activation != null && activation.Phase == MovePhase.Complete) actedThisTurn.Add(squad);

            squad = unit;
            actionTaken = false;
            shootMode = false;
            abilityMode = null;
            StartActivation();
            Flash($"Commanding {unit.DisplayName}.");
            return true;
        }

        /// <summary>Selects the next of the player's units that hasn't acted yet this turn.</summary>
        public bool SelectNextUnit()
        {
            int start = playerUnits.IndexOf(squad); // -1 when none is selected: start from the first unit
            for (int i = 1; i <= playerUnits.Count; i++)
            {
                var u = playerUnits[(start + i + playerUnits.Count) % playerUnits.Count];
                if (u != squad && !actedThisTurn.Contains(u) && !u.IsDestroyed) return SelectUnit(u);
            }
            Flash("No other unit can act this turn.");
            return false;
        }

        private bool CanSwitchUnit =>
            activation == null || activation.Phase == MovePhase.Complete
            || (activation.Phase == MovePhase.MoveLeader && activation.LeaderMove == MoveKind.None);

        // ------------------------------------------------------------------ Orders (public so AI/tests can drive them)

        public void NextTurn()
        {
            Turns.StartNextTurn(); // removes keywords that wear off now, e.g. "Moved"
            actionTaken = false;
            shootMode = false;
            abilityMode = null;
            actedThisTurn.Clear();
            Deselect();
            Flash($"Turn {Turns.Current}: click one of your units (or press Tab) to command it.");
        }

        /// <summary>No unit is being commanded (the state at the start of every turn).</summary>
        private void Deselect()
        {
            StopAllWalking();
            squad = null;
            activation = null;
            selected = null;
            HideMoveVisuals();
        }

        private void HideMoveVisuals()
        {
            foreach (var r in new[] { moveRing, sprintRing, followRing, selectRing, cursorRing, pathLine, abilityRing }.Concat(cohesionRings))
                if (r != null) r.Hide();
        }

        /// <summary>True (and tells the player) if no unit is selected to give orders to.</summary>
        private bool NoActiveUnit()
        {
            if (activation != null) return false;
            Flash("Select one of your units first (click it or press Tab).");
            return true;
        }

        /// <summary>Begins a fresh move for the squad, led by whoever holds command right now.</summary>
        private void StartActivation()
        {
            StopAllWalking();
            if (squad == null || squad.IsDestroyed || squad.Leader == null)
            {
                string name = squad != null ? squad.DisplayName : "The unit";
                Deselect();
                Flash($"{name} has been wiped out.");
                return;
            }
            activation = new UnitMoveActivation(squad, FindObjectsByType<Soldier>(FindObjectsSortMode.None),
                BattlefieldNavigation.Measurer);
            lastPreviewKey = null;
            startRotations.Clear();
            foreach (var m in squad.LivingSoldiers) startRotations[m] = m.transform.rotation;
            selected = activation.Leader;
        }

        /// <summary>Debug: kills the current squad leader to test succession. Only before or after the squad moves.</summary>
        public bool DebugKillLeader()
        {
            if (NoActiveUnit()) return false;
            if (activation.Phase == MovePhase.MoveFollowers)
            {
                Flash("Finish or undo the move before killing the leader.");
                return false;
            }
            bool wasWaitingToMove = activation.Phase == MovePhase.MoveLeader;
            squad.Leader.Kill(); // raises LeaderChanged
            if (wasWaitingToMove) StartActivation();
            return true;
        }

        private void OnLeaderChanged(Soldier fallen, Rules.SuccessionResult result)
        {
            if (result.NewLeader == null) Flash($"{fallen.name} is down. No one is left to lead.");
            else Flash($"{fallen.name} is down. {result.NewLeader.name} takes command" +
                       (result.WasTie ? $" (closest of {result.TiedCandidates} tied soldiers, decided by roll)." : "."));
        }

        public bool OrderLeaderMove(Vector3 destination)
        {
            if (NoActiveUnit()) return false;
            if (!activation.TryMoveLeader(destination, out var check))
            {
                Flash(check.Reason);
                return false;
            }
            WalkTo(activation.Leader, check.Path, check.Kind == MoveKind.Sprint);
            selected = NextOutOfCohesion();
            string who = activation.Leader.name;
            Flash(check.Kind == MoveKind.Sprint ? $"{who} sprints {check.Distance:0.0} m." : $"{who} moves {check.Distance:0.0} m.");
            return true;
        }

        public bool OrderHold()
        {
            if (NoActiveUnit()) return false;
            if (!activation.HoldLeader()) return false;
            selected = NextOutOfCohesion();
            Flash($"{activation.Leader.name} holds position.");
            return true;
        }

        public bool Select(Soldier soldier)
        {
            if (activation == null || activation.Phase != MovePhase.MoveFollowers || !activation.IsFollower(soldier)) return false;
            selected = soldier;
            return true;
        }

        public bool OrderSelectedMove(Vector3 destination)
        {
            if (activation == null || selected == null || selected == activation.Leader) return false;
            if (!activation.TryMoveFollower(selected, destination, out var check))
            {
                Flash(check.Reason);
                return false;
            }
            WalkTo(selected, check.Path, false);
            selected = NextOutOfCohesion();
            return true;
        }

        public void OrderGather()
        {
            if (activation == null || activation.Phase != MovePhase.MoveFollowers) return;
            var moved = activation.GatherIntoCohesion();
            foreach (var m in moved) WalkTo(m, activation.PathOf(m), false);
            int left = activation.OutOfCohesion().Count;
            Flash(left == 0 ? $"Gathered {moved.Count} soldier(s)." : $"Gathered {moved.Count}; {left} could not find room.");
            selected = NextOutOfCohesion();
        }

        public bool OrderEndMove()
        {
            if (activation == null) return false;
            if (!activation.Complete())
            {
                if (activation.Phase == MovePhase.MoveFollowers)
                    Flash($"{activation.OutOfCohesion().Count} soldier(s) still out of cohesion.");
                return false;
            }
            selected = null;
            Flash("Move complete.");
            return true;
        }

        public void OrderReset()
        {
            if (activation == null || activation.Phase == MovePhase.Complete) return;
            StopAllWalking();
            activation.Reset();
            foreach (var m in squad.LivingSoldiers)
            {
                m.transform.position = activation.PositionOf(m);
                if (startRotations.TryGetValue(m, out var rot)) m.transform.rotation = rot;
            }
            selected = activation.Leader;
            Flash("Move undone.");
        }

        private Soldier NextOutOfCohesion()
        {
            var leaderPos = activation.LeaderPosition;
            return activation.OutOfCohesion()
                .OrderBy(m => Measure.Horizontal(activation.PositionOf(m), leaderPos))
                .FirstOrDefault();
        }

        // ------------------------------------------------------------------ Input

        private void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            UpdateHover(mouse);

            if (activation == null)
            {
                // No unit selected (start of a turn): pick one with Tab or by clicking it.
                if (Time.time > tracersUntil) foreach (var t in tracers) t.Hide();
                if (kb != null && kb.tabKey.wasPressedThisFrame) SelectNextUnit();
                if (kb != null && kb.nKey.wasPressedThisFrame) NextTurn();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverHud(mouse))
                {
                    if (hoverSoldier != null && playerUnits.Contains(hoverSoldier.Unit)) SelectUnit(hoverSoldier.Unit);
                    else Flash("Click one of your units (or press Tab) to command it.");
                }
                return;
            }

            if (kb != null)
            {
                if (kb.hKey.wasPressedThisFrame) OrderHold();
                if (kb.gKey.wasPressedThisFrame) OrderGather();
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) OrderEndMove();
                if (kb.backspaceKey.wasPressedThisFrame) OrderReset();
                if (kb.nKey.wasPressedThisFrame && activation.Phase == MovePhase.Complete) NextTurn();
                if (kb.escapeKey.wasPressedThisFrame && activation.Phase == MovePhase.MoveFollowers) selected = null;
                if (kb.kKey.wasPressedThisFrame) DebugKillLeader();
                if (kb.fKey.wasPressedThisFrame) ToggleShootMode();
                if (kb.tabKey.wasPressedThisFrame && CanSwitchUnit) SelectNextUnit();
                if (kb.escapeKey.wasPressedThisFrame) { shootMode = false; abilityMode = null; }
                if (activation == null) return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverHud(mouse))
                HandleClick();

            UpdateVisuals();
        }

        private void HandleClick()
        {
            if (abilityMode != null)
            {
                if (hoverSoldier == null) { Flash($"Click a {(abilityMode.targets == AbilityTarget.Unit ? "unit" : "soldier")} for {abilityMode.displayName} (Esc to cancel)."); return; }
                UseAbility(abilityMode, abilityUser, hoverSoldier, hoverSoldier.Unit);
                return;
            }

            if (shootMode)
            {
                if (hoverSoldier != null && hoverSoldier.Unit != null && hoverSoldier.Unit.Side != squad.Side)
                    OrderShoot(hoverSoldier.Unit);
                else Flash("Click an enemy soldier to shoot at their unit (Esc to cancel).");
                return;
            }

            // Step 1: clicking one of your other units switches to it (before moving, or after finishing).
            if (hoverSoldier != null && hoverSoldier.Unit != null && hoverSoldier.Unit != squad
                && playerUnits.Contains(hoverSoldier.Unit) && CanSwitchUnit)
            {
                SelectUnit(hoverSoldier.Unit);
                return;
            }

            switch (activation.Phase)
            {
                case MovePhase.MoveLeader:
                    if (hasHover) OrderLeaderMove(hoverPoint);
                    break;

                case MovePhase.MoveFollowers:
                    if (hoverSoldier != null && activation.IsFollower(hoverSoldier)) Select(hoverSoldier);
                    else if (hoverSoldier != null && hoverSoldier == activation.Leader) Flash("The squad leader has already moved this turn.");
                    else if (selected != null && hasHover) OrderSelectedMove(hoverPoint);
                    break;
            }
        }

        private void UpdateHover(Mouse mouse)
        {
            hasHover = false;
            hoverSoldier = null;
            if (mouse == null || cam == null || PointerOverHud(mouse)) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 500f, TerrainLayers.SoldierMask))
                hoverSoldier = hit.collider.GetComponentInParent<Soldier>();

            if (Physics.Raycast(ray, out var ground, 500f, TerrainLayers.TerrainMask, QueryTriggerInteraction.Ignore))
            {
                hasHover = true;
                hoverPoint = ground.point;
            }
            else if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
            {
                hasHover = true;
                hoverPoint = ray.GetPoint(enter);
            }
        }

        private bool PointerOverHud(Mouse mouse)
        {
            Vector2 p = mouse.position.ReadValue();
            p.y = Screen.height - p.y;
            return hudRect.Contains(p);
        }

        // ------------------------------------------------------------------ Visuals

        /// <summary>Shows the cursor preview at a world point (called every frame from mouse input).</summary>
        public void PreviewAt(Vector3 point)
        {
            hasHover = true;
            hoverPoint = point;
            hoverSoldier = null;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (Time.time > tracersUntil) foreach (var t in tracers) t.Hide();
            UpdateTargetRings();
            if (abilityMode != null && abilityUser != null)
            {
                foreach (var r in new[] { moveRing, sprintRing, followRing, cursorRing, pathLine }.Concat(cohesionRings)) r.Hide();
                abilityRing.ShowCircle(abilityUser.Position, abilityMode.range, FollowColor);
                return;
            }
            abilityRing.Hide();
            if (shootMode)
            {
                foreach (var r in new[] { moveRing, sprintRing, followRing, cursorRing, pathLine }.Concat(cohesionRings)) r.Hide();
                return;
            }

            var phase = activation.Phase;
            var leader = activation.Leader;

            // Range rings.
            if (phase == MovePhase.MoveLeader)
            {
                moveRing.ShowCircle(activation.LeaderStart, leader.Move, MoveColor);
                sprintRing.ShowCircle(activation.LeaderStart, leader.Sprint, SprintColor);
                followRing.Hide();
            }
            else
            {
                moveRing.Hide();
                sprintRing.Hide();
                if (phase == MovePhase.MoveFollowers) followRing.ShowCircle(activation.LeaderPosition, MovementRules.CohesionRange, FollowColor);
                else followRing.Hide();
            }

            // OutOfCohesion.
            var outside = activation.OutOfCohesion();
            while (cohesionRings.Count < outside.Count)
                cohesionRings.Add(RangeRing.Create("Ring_OutOfCohesion", transform, 0.05f));
            for (int i = 0; i < cohesionRings.Count; i++)
            {
                if (i < outside.Count) cohesionRings[i].ShowCircle(outside[i].Position, outside[i].Radius + 0.25f, InvalidColor);
                else cohesionRings[i].Hide();
            }

            // Selection.
            if (selected != null && phase != MovePhase.Complete)
                selectRing.ShowCircle(selected.Position, selected.Radius + 0.15f, SelectColor);
            else selectRing.Hide();

            // Cursor preview.
            Soldier mover = phase == MovePhase.MoveLeader ? leader
                : phase == MovePhase.MoveFollowers ? selected : null;
            bool pointingAtSoldier = hoverSoldier != null && phase == MovePhase.MoveFollowers;
            if (mover != null && hasHover && !pointingAtSoldier)
            {
                // Route finding is cheap but not free: only re-check when the cursor or mover changes.
                if (lastPreviewKey == null || Vector3.Distance(lastPreviewKey.Value.point, hoverPoint) > 0.02f
                    || lastPreviewKey.Value.mover != mover || lastPreviewKey.Value.phase != phase)
                {
                    hoverCheck = phase == MovePhase.MoveLeader
                        ? activation.CheckLeaderMove(hoverPoint)
                        : activation.CheckFollowerMove(mover, hoverPoint);
                    lastPreviewKey = (hoverPoint, mover, phase);
                }
                Color c = !hoverCheck.Ok ? InvalidColor
                    : hoverCheck.Kind == MoveKind.Sprint ? SprintColor
                    : phase == MovePhase.MoveLeader ? MoveColor : FollowColor;
                Vector3 from = phase == MovePhase.MoveLeader ? activation.LeaderStart : activation.PositionOf(mover);
                cursorRing.ShowCircle(hoverCheck.Ok ? hoverCheck.Destination : hoverPoint, mover.Radius, c);
                if (hoverCheck.Path != null && hoverCheck.Path.Points.Count > 1) pathLine.ShowPath(hoverCheck.Path.Points, c);
                else pathLine.ShowLine(from, hoverPoint, c);
            }
            else
            {
                hoverCheck = default;
                lastPreviewKey = null;
                cursorRing.Hide();
                pathLine.Hide();
            }
        }

        // ------------------------------------------------------------------ Walking

        private void WalkTo(Soldier m, MovePath path, bool fast)
        {
            if (path == null) return;
            if (walking.TryGetValue(m, out var running) && running != null) StopCoroutine(running);
            walking[m] = StartCoroutine(WalkRoutine(m, path.Points, fast ? sprintSpeed : walkSpeed));
        }

        /// <summary>Walks the soldier along the route, over hills and onto objects.</summary>
        private IEnumerator WalkRoutine(Soldier m, List<Vector3> points, float speed)
        {
            Transform t = m.transform;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 target = points[i];
                Vector3 flatDir = target - t.position;
                flatDir.y = 0f;
                if (flatDir.sqrMagnitude > 0.0025f) t.rotation = Quaternion.LookRotation(flatDir);

                while ((t.position - target).sqrMagnitude > 0.0001f)
                {
                    t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
                    yield return null;
                }
            }
            walking.Remove(m);
        }

        private void StopAllWalking()
        {
            foreach (var c in walking.Values)
                if (c != null) StopCoroutine(c);
            walking.Clear();
        }

        // ------------------------------------------------------------------ HUD

        private void Flash(string text)
        {
            message = text;
            messageUntil = Time.time + 4f;
        }

        private void OnGUI()
        {
            if (!enabled) return;

            // The panel grows to fit its contents (measured each repaint), up to the bottom of the screen.
            float maxHeight = Screen.height - hudRect.y - 10f;
            GUI.Box(hudRect, GUIContent.none);
            GUILayout.BeginArea(new Rect(hudRect.x + 10, hudRect.y + 8, hudRect.width - 20, maxHeight - 16));
            GUILayout.BeginVertical();
            DrawHud();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
                hudRect.height = Mathf.Min(GUILayoutUtility.GetLastRect().height + 16f, maxHeight);
            GUILayout.EndArea();
        }

        private void DrawHud()
        {
            var wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };

            GUILayout.Label($"{(squad != null ? squad.DisplayName : "No unit selected")}  —  Turn {Turns.Current}", title);
            if (playerUnits.Count > 1 || squad == null)
            {
                var units = playerUnits.Where(u => u != null).Select(u =>
                    u == squad ? $"[{u.DisplayName}]" : actedThisTurn.Contains(u) || u.IsDestroyed ? $"{u.DisplayName} (done)" : u.DisplayName);
                GUILayout.Label("Units: " + string.Join(", ", units) + "  — Tab or click to switch", wrap);
            }

            // Every keyword on the selected unit's soldiers.
            if (squad != null) DrawUnitKeywords(squad, wrap);

            // Keywords of the soldier under the cursor (or the selected one).
            var shown = hoverSoldier != null ? hoverSoldier : selected;
            if (shown != null)
                GUILayout.Label($"{shown.name}: {string.Join(", ", shown.Keywords.Select(k => k.displayName))}", wrap);

            if (activation == null)
            {
                GUILayout.Label("Click one of your units, or press Tab, to command it.", wrap);
                if (GUILayout.Button("Next turn (N)")) NextTurn();
                if (!string.IsNullOrEmpty(message) && Time.time < messageUntil) GUILayout.Label(message, wrap);
                return;
            }

            string leaderName = activation.Leader.name;
            switch (activation.Phase)
            {
                case MovePhase.MoveLeader:
                    GUILayout.Label($"1. Move the squad leader ({leaderName}, gold marker). Click inside the blue ring to move (10 m) or the orange ring to sprint (20 m).", wrap);
                    GUILayout.Label(HoverReadout(), wrap);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Hold position (H)")) OrderHold();
                    if (GUILayout.Button("Shoot without moving (F)")) ToggleShootMode();
                    GUILayout.EndHorizontal();
                    if (GUILayout.Button("Kill leader (K, debug)")) DebugKillLeader();
                    break;

                case MovePhase.MoveFollowers:
                    string how = activation.LeaderMove == MoveKind.Sprint ? "sprinted"
                        : activation.LeaderMove == MoveKind.Hold ? "held position" : "moved";
                    int outOfCohesion = activation.OutOfCohesion().Count;
                    GUILayout.Label($"2. {leaderName} {how}. Click a soldier, then click a spot inside the green cohesion ring ({MovementRules.CohesionRange:0} m).", wrap);
                    GUILayout.Label(outOfCohesion == 0 ? "Whole unit is in cohesion." : $"{outOfCohesion} soldier(s) out of cohesion.", wrap);
                    GUILayout.Label(HoverReadout(), wrap);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Gather (G)")) OrderGather();
                    GUI.enabled = activation.CanComplete;
                    if (GUILayout.Button("End move (Enter)")) OrderEndMove();
                    GUI.enabled = true;
                    if (GUILayout.Button("Undo (Bksp)")) OrderReset();
                    GUILayout.EndHorizontal();
                    break;

                case MovePhase.Complete:
                    GUILayout.Label(actionTaken ? "Unit has taken its action; its turn is over." : "Unit has finished moving. It may shoot OR use one ability.", wrap);
                    GUILayout.BeginHorizontal();
                    if (!actionTaken && GUILayout.Button("Shoot (F)")) ToggleShootMode();
                    if (GUILayout.Button("Next turn (N)")) NextTurn();
                    if (GUILayout.Button("Kill leader (K, debug)")) DebugKillLeader();
                    GUILayout.EndHorizontal();
                    break;
            }

            if (CanUseAbilities)
            {
                foreach (var (user, ability) in ActiveAbilitiesOfUnit())
                {
                    int left = ability.usesPerTurn - user.TimesUsedThisTurn(ability);
                    string why = ActiveAbilities.WhyCannotCast(user, ability);
                    string spell = ability.IsSpell ? " [Spell]" : "";
                    // Greyed out with the reason (e.g. a Spell after sprinting); used-up abilities just show 0 left.
                    string label = why == null || left <= 0
                        ? $"{ability.displayName}{spell} ({left} left) — {ability.range:0} m"
                        : $"{ability.displayName}{spell} — {why}";
                    GUI.enabled = why == null;
                    if (GUILayout.Button(label))
                        BeginAbility(user, ability);
                    GUI.enabled = true;
                }
            }
            if (abilityMode != null)
                GUILayout.Label($"{abilityMode.displayName.ToUpperInvariant()}: click a {(abilityMode.targets == AbilityTarget.Unit ? "friendly unit" : "friendly soldier")} within the green ring. Esc to cancel.", title);

            if (shootMode)
                GUILayout.Label("SHOOTING: click an enemy soldier to shoot at their unit. Esc to cancel.", title);

            if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
                GUILayout.Label(message, wrap);

            if (recentLog.Count > 0)
            {
                var small = new GUIStyle(wrap) { fontSize = 11 };
                foreach (var line in recentLog.Skip(Mathf.Max(0, recentLog.Count - 8)))
                    GUILayout.Label(line, small);
            }
        }

        /// <summary>
        /// The unit's keywords: one line per keyword. Keywords every soldier has are listed once; others say how many
        /// soldiers have them and who.
        /// </summary>
        public static List<string> UnitKeywordLines(Unit unit)
        {
            var lines = new List<string>();
            int total = unit.LivingSoldiers.Count();
            foreach (var (keyword, soldiers) in unit.KeywordSummary())
            {
                string tag = keyword.category == KeywordCategory.Status ? " (status)" : "";
                string who = soldiers.Count == total
                    ? (total > 1 ? $"all {total}" : "")
                    : $"{soldiers.Count} of {total}: {string.Join(", ", soldiers.Select(s => s.name))}";
                lines.Add($"{keyword.displayName}{tag}{(who.Length > 0 ? " — " + who : "")}");
            }
            return lines;
        }

        private static void DrawUnitKeywords(Unit unit, GUIStyle wrap)
        {
            var bold = new GUIStyle(wrap) { fontStyle = FontStyle.Bold };
            var small = new GUIStyle(wrap) { fontSize = 11 };
            GUILayout.Label("Unit keywords:", bold);
            var lines = UnitKeywordLines(unit);
            if (lines.Count == 0) GUILayout.Label("  (none)", small);
            foreach (var line in lines) GUILayout.Label("  • " + line, small);
        }

        // ------------------------------------------------------------------ Shooting

        // ------------------------------------------------------------------ Active abilities

        /// <summary>
        /// After moving or instead of moving, a unit takes ONE action: shoot, or use one ability of one of its soldiers.
        /// That ends the unit's turn.
        /// </summary>
        private bool CanUseAbilities => activation != null && !actionTaken && activation.Phase != MovePhase.MoveFollowers;

        private IEnumerable<(Soldier, Ability)> ActiveAbilitiesOfUnit()
        {
            foreach (var s in squad.LivingSoldiers)
                foreach (var a in s.Abilities)
                    if (a != null && a.IsActive) yield return (s, a);
        }

        /// <summary>Picks an ability to use (the HUD button): the next click chooses its target.</summary>
        public void BeginAbility(Soldier user, Ability ability)
        {
            shootMode = false;
            abilityUser = user;
            abilityMode = ability;
        }

        /// <summary>The ability waiting for the player to click a target (null if none).</summary>
        public Ability TargetingAbility => abilityMode;

        /// <summary>Uses an active ability on a soldier (or, for unit abilities, that soldier's unit / the given unit).</summary>
        public AbilityResult UseAbility(Ability ability, Soldier user, Soldier targetSoldier, Unit targetUnit = null)
        {
            if (!CanUseAbilities)
            {
                var refused = new AbilityResult { Reason = actionTaken ? "This unit has already taken its action this turn" : "Finish the move first" };
                Flash(refused.Reason);
                CombatAudio.PlayError();
                return refused;
            }
            if (ability.targets == AbilityTarget.Unit && targetUnit == null && targetSoldier != null) targetUnit = targetSoldier.Unit;
            var result = ActiveAbilities.Use(user, ability, targetSoldier, targetUnit);
            if (!result.Used)
            {
                // Refused (e.g. healing a soldier at full health, or one who isn't Biological): nothing happens.
                // The ability stays selected, so the player can pick another target, or press Esc to do something else.
                Flash(result.Reason);
                CombatAudio.PlayError();
                return result;
            }
            abilityMode = null;
            actionTaken = true;
            actedThisTurn.Add(squad);
            activation.EndActivation(); // the unit's action ends its turn (no moving afterwards)
            recentLog.Clear();
            recentLog.AddRange(result.Log);
            Flash($"{user.name} uses {ability.displayName}. {squad.DisplayName}'s turn is over.");
            return result;
        }

        private void ToggleShootMode()
        {
            abilityMode = null;
            if (shootMode) { shootMode = false; return; }
            if (activation == null) return;
            if (actionTaken) { Flash("This unit has already taken its action this turn."); return; }
            if (activation.Phase == MovePhase.MoveFollowers) { Flash("Finish the move before shooting."); return; }
            shootMode = true;
        }

        /// <summary>Steps 3–4 then the shooting sequence: this unit shoots at <paramref name="enemy"/>. Ends the unit's turn.</summary>
        public ShootingResult OrderShoot(Unit enemy)
        {
            if (enemy == null || enemy == squad || actionTaken || activation == null || activation.Phase == MovePhase.MoveFollowers)
                return null;
            bool sprinted = activation.LeaderMove == MoveKind.Sprint;
            lastShooting = UnitShooting.Resolve(squad, enemy, sprinted, GameRandom.Shared);
            actionTaken = true;
            actedThisTurn.Add(squad);
            recentLog.Clear();
            recentLog.AddRange(lastShooting.Log);
            shootMode = false;
            activation.EndActivation();
            selected = null;
            ShowTracers(lastShooting);
            Flash(lastShooting.HasLineOfSight || lastShooting.ShotsTaken > 0
                ? $"Shot at {enemy.DisplayName}: {lastShooting.Hits}/{lastShooting.ShotsTaken} hits, {lastShooting.Crits} crits, {lastShooting.TotalDamage} damage, {lastShooting.Kills} down."
                : $"Can't see {enemy.DisplayName}.");
            return lastShooting;
        }

        private void ShowTracers(ShootingResult r)
        {
            int i = 0;
            foreach (var shot in r.Shots)
            {
                if (shot.Attack == null || shot.Target == null || shot.Attack.NotTargetable) continue;
                while (tracers.Count <= i) tracers.Add(RangeRing.Create("Tracer", transform, 0.05f));
                Color c = shot.Attack.Crit ? new Color(1f, 0.5f, 0.1f) : shot.Attack.Hit ? new Color(1f, 0.95f, 0.4f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                Vector3 from = shot.Shooter.Position + Vector3.up * shot.Shooter.Height * 0.7f;
                Vector3 to = shot.Target.transform.position + Vector3.up * shot.Target.Height * 0.6f;
                tracers[i++].ShowLine(from, to, c);
            }
            for (; i < tracers.Count; i++) tracers[i].Hide();
            tracersUntil = Time.time + 2.5f;
        }

        /// <summary>In shoot mode, red rings mark the enemy unit under the cursor.</summary>
        private void UpdateTargetRings()
        {
            var enemy = shootMode && hoverSoldier != null && hoverSoldier.Unit != null && hoverSoldier.Unit.Side != squad.Side
                ? hoverSoldier.Unit : null;
            var soldiers = enemy != null ? enemy.LivingSoldiers.ToList() : new List<Soldier>();
            while (targetRings.Count < soldiers.Count) targetRings.Add(RangeRing.Create("Ring_Target", transform, 0.06f));
            for (int i = 0; i < targetRings.Count; i++)
            {
                if (i < soldiers.Count) targetRings[i].ShowCircle(soldiers[i].Position, soldiers[i].Radius + 0.3f, InvalidColor);
                else targetRings[i].Hide();
            }
        }

        private string HoverReadout()
        {
            if (!cursorRing.gameObject.activeSelf) return " ";
            if (!hoverCheck.Ok) return $"{hoverCheck.Distance:0.0} m — {hoverCheck.Reason}";
            return hoverCheck.Kind == MoveKind.Sprint ? $"{hoverCheck.Distance:0.0} m — Sprint" : $"{hoverCheck.Distance:0.0} m";
        }
    }
}
