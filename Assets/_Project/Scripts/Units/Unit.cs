using System;
using System.Collections.Generic;
using System.Linq;
using Eldoira.Core;
using Eldoira.Rules;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Eldoira.Units
{
    public enum Side
    {
        Player,
        AI
    }

    /// <summary>
    /// A unit of soldiers that activate, move and fight together (e.g. 10 infantry with a sergeant),
    /// usually spawned from a <see cref="UnitTemplate"/>. Soldiers are the Soldier components among this object's children.
    /// One living soldier holds the unit-leader role; it starts with the sergeant and passes to the
    /// nearest soldier if the leader is killed (<see cref="LeaderSuccession"/>).
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [FormerlySerializedAs("squadName")]
        [SerializeField] private string displayName = "Unit";
        [SerializeField] private UnitTemplate template;
        [SerializeField] private Side side = Side.Player;
        [FormerlySerializedAs("models")]
        [SerializeField] private List<Soldier> soldiers = new List<Soldier>();
        [SerializeField] private Soldier leader;

        /// <summary>The turn number in which this unit sprinted (0 = hasn't sprinted).</summary>
        private int sprintedOnTurn;

        /// <summary>The unit's leader sprinted this turn, so the whole unit counts as having sprinted.</summary>
        public bool SprintedThisTurn => sprintedOnTurn != 0 && sprintedOnTurn == Turns.Current;

        /// <summary>Records whether the unit sprinted this turn (set when its leader sprints; cleared if the move is undone).</summary>
        public void SetSprintedThisTurn(bool sprinted) => sprintedOnTurn = sprinted ? Mathf.Max(1, Turns.Current) : 0;

        public string DisplayName => displayName;
        public UnitTemplate Template => template;
        public Side Side => side;

        /// <summary>Every soldier in the unit, living or dead.</summary>
        public IReadOnlyList<Soldier> Soldiers => soldiers;
        public IEnumerable<Soldier> LivingSoldiers => soldiers.Where(m => m != null && m.IsAlive);
        public Soldier Leader => leader;
        public bool IsDestroyed => !LivingSoldiers.Any();

        /// <summary>Raised when command passes to a new soldier (old leader, result).</summary>
        public event Action<Soldier, SuccessionResult> LeaderChanged;

        private void Awake() => RefreshSoldiers();
        private void OnValidate() => RefreshSoldiers();
        private void OnTransformChildrenChanged() => RefreshSoldiers();

        /// <summary>Sets up a unit spawned from a template (see <see cref="UnitFactory"/>).</summary>
        public void Configure(UnitTemplate newTemplate, Side newSide, string newName)
        {
            template = newTemplate;
            side = newSide;
            displayName = string.IsNullOrEmpty(newName) ? (newTemplate != null ? newTemplate.displayName : "Unit") : newName;
        }

        /// <summary>
        /// Re-reads the Soldier children. The leader defaults to the first sergeant.
        /// Only this unit's own soldiers count: a soldier belongs to the nearest unit above it, so a unit placed
        /// inside another never shares soldiers with it (and command can never pass between units).
        /// </summary>
        public void RefreshSoldiers()
        {
            soldiers.Clear();
            soldiers.AddRange(GetComponentsInChildren<Soldier>(true).Where(s => s.GetComponentInParent<Unit>(true) == this));
            if (leader == null || !soldiers.Contains(leader))
                leader = soldiers.FirstOrDefault(m => m.IsSergeant) ?? soldiers.FirstOrDefault();
        }

        /// <summary>Called by <see cref="Soldier.Kill"/> before the soldier is removed from play.</summary>
        public void OnSoldierKilled(Soldier soldier, System.Random rng = null)
        {
            if (soldier != leader) return;
            var result = LeaderSuccession.ChooseSuccessor(soldier.Position,
                LivingSoldiers.Where(m => m != soldier), rng ?? GameRandom.Shared);
            var old = leader;
            leader = result.NewLeader;
            LeaderChanged?.Invoke(old, result);
        }

        /// <summary>Living soldiers (other than the leader) standing farther than cohesion range from the leader.</summary>
        public List<Soldier> OutOfCohesion()
        {
            if (leader == null) return new List<Soldier>();
            return LivingSoldiers.Where(m => m != leader
                                           && !MovementRules.IsWithinCohesion(leader.Position, m.Position)).ToList();
        }

        public bool IsInFormation() => OutOfCohesion().Count == 0;

        /// <summary>
        /// Every keyword held by the unit's living soldiers (inherent, gained, and granted by nearby terrain or auras),
        /// with the soldiers who have it. Ordered by how many soldiers have it, then by name.
        /// </summary>
        public List<(Keyword keyword, List<Soldier> soldiers)> KeywordSummary()
        {
            var byKeyword = new List<(Keyword keyword, List<Soldier> soldiers)>();
            foreach (var s in LivingSoldiers)
            foreach (var k in s.Keywords)
            {
                if (k == null) continue;
                int i = byKeyword.FindIndex(e => e.keyword.SameAs(k));
                if (i < 0) byKeyword.Add((k, new List<Soldier> { s }));
                else if (!byKeyword[i].soldiers.Contains(s)) byKeyword[i].soldiers.Add(s);
            }
            return byKeyword.OrderByDescending(e => e.soldiers.Count).ThenBy(e => e.keyword.displayName).ToList();
        }

#if UNITY_EDITOR
        // Debug view: cohesion range around the leader, and red rings on soldiers outside it.
        private void OnDrawGizmos()
        {
            if (leader == null || !leader.IsAlive) return;

            Handles.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Handles.DrawWireDisc(leader.Position + Vector3.up * 0.05f, Vector3.up, MovementRules.CohesionRange);

            Handles.color = Color.red;
            foreach (var m in OutOfCohesion())
                Handles.DrawWireDisc(m.Position + Vector3.up * 0.05f, Vector3.up, m.Radius + 0.2f, 3f);
        }
#endif
    }
}
