using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A unit's primary colour (its faction's colour). Soldier models paint their main parts (body, cap...) with one shared
    /// "primary colour" material; this tints just those parts on a given unit through a per-renderer override
    /// (a MaterialPropertyBlock), so every faction shares the same models and materials — no new texture or material
    /// per faction. Other parts (skin, weapons, gold trim, robes, armor plates) keep their own colours.
    /// </summary>
    public static class PrimaryColor
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
        private static readonly int ColorId = Shader.PropertyToID("_Color");           // built-in shaders

        /// <summary>
        /// Tints every part of <paramref name="root"/> drawn with <paramref name="primaryMaterial"/>.
        /// Returns how many parts were tinted.
        /// </summary>
        public static int Apply(GameObject root, Material primaryMaterial, Color color)
        {
            if (root == null || primaryMaterial == null) return 0;
            int tinted = 0;
            var block = new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != primaryMaterial) continue;
                    r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, color);
                    block.SetColor(ColorId, color);
                    r.SetPropertyBlock(block, i);
                    tinted++;
                }
            }
            return tinted;
        }

        /// <summary>The colour a part is drawn in: its override if it has one, otherwise its material's colour.</summary>
        public static Color Of(Renderer renderer, int materialIndex = 0)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, materialIndex);
            if (!block.isEmpty && block.HasColor(BaseColorId)) return block.GetColor(BaseColorId);
            var mat = renderer.sharedMaterials[materialIndex];
            return mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : mat.color;
        }
    }
}
