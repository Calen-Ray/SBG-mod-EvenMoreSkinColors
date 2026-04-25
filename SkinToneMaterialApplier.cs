using HarmonyLib;
using UnityEngine;

namespace EvenMoreSkinColors
{
    internal static class SkinToneMaterialApplier
    {
        internal static void Apply(PlayerCosmeticsSwitcher switcher, SkinToneSelection selection)
        {
            if (switcher == null || !selection.Enabled)
            {
                return;
            }

            var traverse = Traverse.Create(switcher);
            var propertyBlock = traverse.Field<MaterialPropertyBlock>("skinColorProps").Value ?? new MaterialPropertyBlock();
            traverse.Field("skinColorProps").SetValue(propertyBlock);

            ApplyColor(traverse.Field<Renderer>("headRenderer").Value, propertyBlock, selection.BaseColor);
            ApplyColor(traverse.Field<Renderer>("bodyRenderer").Value, propertyBlock, selection.BaseColor);
            ApplyColor(traverse.Field<Renderer>("mouthRenderer").Value, propertyBlock, selection.MouthColor);

            object currentHeadModel = traverse.Field("currentHeadModel").GetValue();
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
            int tintMaterialIndex = Traverse.Create(cosmeticObject).Field("skinColorTintMaterialIndex").GetValue<int>();
            if (!requireSkinColorTint)
            {
                return;
            }

            var cosmeticComponent = cosmeticObject as Component;
            if (cosmeticComponent == null)
            {
                return;
            }

            foreach (Renderer renderer in cosmeticComponent.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", selection.BaseColor);
                if (tintMaterialIndex < 0)
                {
                    renderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    renderer.SetPropertyBlock(propertyBlock, tintMaterialIndex);
                }
            }
        }

        private static void ApplyColor(Renderer renderer, MaterialPropertyBlock propertyBlock, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
