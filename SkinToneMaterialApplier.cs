using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace EvenMoreSkinColors
{
    internal static class SkinToneMaterialApplier
    {
        // Vanilla moved skin-color application off a shared MaterialPropertyBlock (the
        // PlayerCosmeticsSwitcher.skinColorProps field this used to reflect into — removed
        // this update) onto direct Renderer.material mutation instead (see
        // PlayerCosmeticsSwitcher.ApplyCurrentSkinColorToMaterial). We run as a Harmony
        // postfix on SetSkinColor, so vanilla has already written its own base color into
        // these same material instances by the time we get here — we just overwrite them
        // again with the custom color, matching the new architecture instead of layering a
        // property-block override the new pipeline no longer reads.
        internal static void Apply(PlayerCosmeticsSwitcher switcher, SkinToneSelection selection)
        {
            if (switcher == null || !selection.Enabled)
            {
                return;
            }

            ApplyColorToRenderer(switcher.headRenderer, selection.BaseColor);
            ApplyColorToRenderer(switcher.bodyRenderer, selection.BaseColor);
            ApplyColorToRenderer(switcher.mouthRenderer, selection.MouthColor);

            object currentHeadModel = Traverse.Create(switcher).Field("currentHeadModel").GetValue();
            if (currentHeadModel == null)
            {
                return;
            }

            object cosmeticObject = Traverse.Create(currentHeadModel).Field("cosmetic").GetValue();
            if (cosmeticObject == null)
            {
                return;
            }

            bool requireSkinColorTint = Traverse.Create(cosmeticObject).Field("requireSkinColorTint").GetValue<bool>();
            if (!requireSkinColorTint)
            {
                return;
            }

            int tintMaterialIndex = Traverse.Create(cosmeticObject).Field("skinColorTintMaterialIndex").GetValue<int>();
            var cosmeticComponent = cosmeticObject as Component;
            if (cosmeticComponent == null)
            {
                return;
            }

            var materials = new List<Material>();
            foreach (Renderer renderer in cosmeticComponent.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                materials.Clear();
                renderer.GetMaterials(materials);
                if (tintMaterialIndex < 0)
                {
                    foreach (Material material in materials)
                    {
                        material.SetColor("_Color", selection.BaseColor);
                    }
                }
                else if (tintMaterialIndex < materials.Count)
                {
                    materials[tintMaterialIndex].SetColor("_Color", selection.BaseColor);
                }
            }
        }

        private static void ApplyColorToRenderer(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var materials = new List<Material>();
            renderer.GetMaterials(materials);
            foreach (Material material in materials)
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
