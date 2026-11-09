using UnityEngine;

namespace Eldoira.Terrain
{
    /// <summary>Physics layers used by the rules (defined in Project Settings > Tags and Layers).</summary>
    public static class TerrainLayers
    {
        public const string StructureName = "Structure";
        public const string ObjectName = "TerrainObject";
        public const string SoldierName = "Soldier";

        public static int Structure => LayerMask.NameToLayer(StructureName);
        public static int Object => LayerMask.NameToLayer(ObjectName);
        public static int Soldier => LayerMask.NameToLayer(SoldierName);

        public static int StructureMask => LayerMask.GetMask(StructureName);
        public static int ObjectMask => LayerMask.GetMask(ObjectName);
        public static int SoldierMask => LayerMask.GetMask(SoldierName);
        public static int TerrainMask => LayerMask.GetMask(StructureName, ObjectName);

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
