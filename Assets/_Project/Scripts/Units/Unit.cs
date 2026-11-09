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

        /// <summary>Re-reads the Soldier children. The leader defaults to the first sergeant.</summary>
        public void RefreshSoldiers()
        {
            soldiers.Clear();
            soldiers.AddRange(GetComponentsInChildren<Soldier>(true));
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
