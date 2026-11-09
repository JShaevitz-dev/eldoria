using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using UnityEngine;
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
    /// A unit of models that activate, move and fight together (e.g. 10 infantry with a sergeant).
    /// Models are the UnitModel components among this object's children.
    /// </summary>
    public class Squad : MonoBehaviour
    {
        [SerializeField] private string squadName = "Squad";
        [SerializeField] private Side side = Side.Player;
        [SerializeField] private List<UnitModel> models = new List<UnitModel>();

        public string SquadName => squadName;
        public Side Side => side;
        public IReadOnlyList<UnitModel> Models => models;
        public UnitModel Leader => models.FirstOrDefault(m => m != null && m.IsLeader);

        private void Awake() => RefreshModels();
        private void OnValidate() => RefreshModels();
        private void OnTransformChildrenChanged() => RefreshModels();

        public void RefreshModels()
        {
            models.Clear();
            models.AddRange(GetComponentsInChildren<UnitModel>());
        }

        public bool IsInCoherency() => Coherency.Check(models, out _);

#if UNITY_EDITOR
        // Debug view of unit coherency: green links between models within coherency range,
        // red rings around models that are out of coherency.
        private void OnDrawGizmos()
        {
            if (models.Count == 0) return;
            Coherency.Check(models, out var broken);

            Handles.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            for (int i = 0; i < models.Count; i++)
            for (int j = i + 1; j < models.Count; j++)
            {
                if (models[i] == null || models[j] == null) continue;
                if (Coherency.AreLinked(models[i], models[j]))
                    Handles.DrawLine(models[i].Position + Vector3.up * 0.05f, models[j].Position + Vector3.up * 0.05f);
            }

            Handles.color = Color.red;
            foreach (var m in broken)
                Handles.DrawWireDisc(m.Position + Vector3.up * 0.05f, Vector3.up, m.Radius + 0.2f, 3f);
        }
#endif
    }
}
