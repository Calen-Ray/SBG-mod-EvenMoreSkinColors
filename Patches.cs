using HarmonyLib;
using UnityEngine;

namespace EvenMoreSkinColors
{
    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyPatch(typeof(PlayerCosmetics), "Awake")]
        [HarmonyPostfix]
        private static void PlayerCosmeticsAwakePostfix(PlayerCosmetics __instance)
        {
            SkinToneFollower follower = __instance.GetComponent<SkinToneFollower>();
            if (follower == null)
            {
                follower = __instance.gameObject.AddComponent<SkinToneFollower>();
            }

            follower.Initialize(__instance);
        }

        [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.OnStartLocalPlayer))]
        [HarmonyPostfix]
        private static void PlayerCosmeticsOnStartLocalPlayerPostfix(PlayerCosmetics __instance)
        {
            SkinToneState.OnLocalPlayerStarted(__instance);
        }

        [HarmonyPatch(typeof(PlayerCosmeticsSwitcher), nameof(PlayerCosmeticsSwitcher.SetSkinColor))]
        [HarmonyPostfix]
        private static void PlayerCosmeticsSwitcherSetSkinColorPostfix(PlayerCosmeticsSwitcher __instance)
        {
            if (SkinToneState.IsRevertingToVanilla)
            {
                return;
            }

            PlayerCosmetics cosmetics = __instance.GetComponent<PlayerCosmetics>();
            if (cosmetics != null)
            {
                SkinToneState.TryApplyFor(cosmetics);
                return;
            }

            if (PlayerCustomizationMenu.Instance != null &&
                PlayerCustomizationMenu.Instance.characterPreview != null &&
                ReferenceEquals(PlayerCustomizationMenu.Instance.characterPreview.cosmeticsSwitcher, __instance))
            {
                SkinToneState.ApplyToPreview(PlayerCustomizationMenu.Instance);
            }
        }

        [HarmonyPatch(typeof(PlayerCustomizationMenu), "Start")]
        [HarmonyPostfix]
        private static void PlayerCustomizationMenuStartPostfix(PlayerCustomizationMenu __instance)
        {
            if (__instance.skinColorTemplate == null || __instance.skinColorTemplate.transform.parent == null)
            {
                return;
            }

            // We must land outside the Skincolors GridLayoutGroup: once inside, its rebuild
            // squashes our panel into a ~50px grid cell, injects VerticalLayoutGroup +
            // ContentSizeFitter + a "Content" wrapper around our children, and ends up hiding
            // AdvancedArea — which is why the wheel panel was unresponsive. characterPreview
            // sits on the "Preview" panel (Skincolors' sibling container), so go one level up
            // to Preview via the CharacterPreview component; fall back to skin-color container
            // walk only if the preview reference is missing.
            Transform panelParent = null;
            if (__instance.characterPreview != null)
            {
                panelParent = __instance.characterPreview.transform;
            }
            if (panelParent == null)
            {
                Transform skinColorsContainer = __instance.skinColorTemplate.transform.parent;
                panelParent = skinColorsContainer.parent != null ? skinColorsContainer.parent : skinColorsContainer;
            }

            Transform existing = panelParent.Find("EvenMoreSkinColors_CustomSkinTonePanel");
            if (existing != null)
            {
                return;
            }

            // If a prior Start left a stale panel under the wrong parent (Skincolors), clean it
            // up before creating a new one. Searching siblings of panelParent is cheap and keeps
            // us resilient to re-parents forced by vanilla layout code.
            foreach (Transform sibling in panelParent.parent != null ? panelParent.parent : panelParent)
            {
                Transform stale = sibling.Find("EvenMoreSkinColors_CustomSkinTonePanel");
                if (stale != null && stale.parent != panelParent)
                {
                    UnityEngine.Object.Destroy(stale.gameObject);
                }
            }

            GameObject panelObject = new GameObject("EvenMoreSkinColors_CustomSkinTonePanel");
            panelObject.transform.SetParent(panelParent, false);
            panelObject.transform.SetAsLastSibling();
            SkinTonePickerPanel panel = panelObject.AddComponent<SkinTonePickerPanel>();
            panel.Initialize(__instance);
            SkinToneState.ApplyToPreview(__instance);
        }

        [HarmonyPatch(typeof(PlayerCustomizationMenu), "SetLoadout")]
        [HarmonyPostfix]
        private static void PlayerCustomizationMenuSetLoadoutPostfix(int index)
        {
            SkinToneState.ActivateLoadout(index, applyPreview: true, broadcast: false);
        }

        [HarmonyPatch(typeof(PlayerCustomizationMenu), "SetSkinColorIndex")]
        [HarmonyPostfix]
        private static void PlayerCustomizationMenuSetSkinColorIndexPostfix(PlayerCustomizationMenu __instance)
        {
            SkinToneState.ApplyToPreview(__instance);
        }

        [HarmonyPatch(typeof(PlayerCustomizationMenu), "SaveLoadout")]
        [HarmonyPostfix]
        private static void PlayerCustomizationMenuSaveLoadoutPostfix()
        {
            SkinToneState.ActivateLoadout(SkinToneState.ActiveLoadoutIndex, applyPreview: true, broadcast: true);
        }
    }
}
