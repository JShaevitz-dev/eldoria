using System;
using System.Collections;
using System.Linq;
using Eldoria.AI;
using Eldoria.CameraControl;
using Eldoria.Core;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// Plays the AI's turn, one unit at a time, so the player can watch: the camera pans to the unit, it moves (its
    /// leader first, then the rest gather in cohesion), then it takes its action (shoot, or one ability). Decisions come
    /// from <see cref="AIPlanner"/>; the moves and actions go through the same rules as the player's.
    /// <see cref="Skip"/> resolves the rest of the turn instantly. Stops as soon as the battle is decided.
    /// </summary>
    public class AITurnController : MonoBehaviour
    {
        [SerializeField] private float pauseBeforeMove = 0.6f;
        [SerializeField] private float pauseAfterAction = 1.2f;
        [SerializeField] private float maxWalkWait = 8f;

        private bool skip;

        /// <summary>What the AI is doing right now, for the HUD.</summary>
        public string Status { get; private set; } = "";

        /// <summary>Resolve the rest of the AI's turn without pauses or walking.</summary>
        public void Skip() => skip = true;

        /// <param name="instant">Resolve the whole turn at once (no pauses, soldiers jump to their spots).</param>
        /// <param name="onDone">Called when every unit has acted (or the battle is over).</param>
        public IEnumerator Run(MovementController view, Side side, bool instant, Action onDone)
        {
            skip = instant;
            // The AI's side turn begins: what the AI gained on its last turn ("until its next turn") wears off now.
            Turns.StartSideTurn(side);
            var cam = Camera.main != null ? Camera.main.GetComponentInParent<TacticalCameraController>() : null;

            foreach (var unit in AIPlanner.ActivationOrder(side))
            {
                if (Victory.IsOver) break;
                if (unit == null || unit.IsDestroyed || unit.Leader == null) continue;
                var leader = unit.Leader;

                // --- Move ---
                Status = $"{unit.DisplayName} is moving…";
                if (!skip && cam != null) cam.FocusOn(leader.Position);
                if (!skip) yield return Wait(pauseBeforeMove);

                var activation = new UnitMoveActivation(unit, FindObjectsByType<Soldier>(FindObjectsSortMode.None), BattlefieldNavigation.Measurer);
                var destination = AIPlanner.ChooseLeaderMove(unit, activation);
                if (destination.HasValue && activation.TryMoveLeader(destination.Value, out var check))
                    view.AddLog($"{unit.DisplayName}: {leader.name} {(check.Kind == MoveKind.Sprint ? "sprints" : "moves")} {check.Distance:0.0} m.");
                else
                {
                    activation.HoldLeader();
                    view.AddLog($"{unit.DisplayName} holds position.");
                }
                activation.GatherIntoCohesion();
                if (!activation.Complete()) activation.EndActivation();

                if (!skip)
                {
                    foreach (var s in unit.LivingSoldiers)
                    {
                        var path = activation.PathOf(s);
                        if (path != null) view.WalkAlong(s, path, s == leader && activation.LeaderMove == MoveKind.Sprint);
                    }
                    float waited = 0f;
                    while (view.AnyoneWalking && waited < maxWalkWait && !skip)
                    {
                        waited += Time.deltaTime;
                        if (cam != null && unit.Leader != null) cam.FocusOn(unit.Leader.Position);
                        yield return null;
                    }
                }
                view.StopWalkingNow();
                AIPlanner.ApplyPositions(activation);   // everyone exactly where the rules placed them
                NearbyKeywords.RefreshAll();
                if (Victory.IsOver) break;

                // --- Act ---
                var action = AIPlanner.ChooseAction(unit);
                switch (action.Kind)
                {
                    case AIActionKind.Shoot:
                        Status = $"{unit.DisplayName} shoots at {action.ShootAt.DisplayName}.";
                        var shooting = UnitShooting.Resolve(unit, action.ShootAt, activation.LeaderMove == MoveKind.Sprint, GameRandom.Shared,
                            action.ShootAtSoldier);
                        view.PresentShooting(unit, action.ShootAt, shooting);
                        break;
                    case AIActionKind.Ability:
                        Status = $"{unit.DisplayName} uses {action.Ability.displayName}.";
                        var used = ActiveAbilities.Use(action.User, action.Ability, action.TargetSoldier, action.TargetUnit, action.TargetPoint);
                        view.PresentAbility(unit, action.User, action.Ability, used);
                        break;
                    default:
                        Status = $"{unit.DisplayName}: {action.Reason}.";
                        view.AddLog($"{unit.DisplayName} doesn't act ({action.Reason}).");
                        break;
                }
                if (!skip) yield return Wait(pauseAfterAction);
            }

            Status = "";
            onDone?.Invoke();
        }

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skip) yield return null;
        }
    }
}
