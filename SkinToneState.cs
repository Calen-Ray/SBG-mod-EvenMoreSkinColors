using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mirror;
using UnityEngine;

namespace EvenMoreSkinColors
{
    internal static class SkinToneState
    {
        private const int LoadoutCount = 4;
        private static readonly Dictionary<uint, SkinToneSelection> RemoteSelections = new Dictionary<uint, SkinToneSelection>();

        private static ManualLogSource _log;
        private static readonly ConfigEntry<bool>[] EnabledConfigs = new ConfigEntry<bool>[LoadoutCount];
        private static readonly ConfigEntry<string>[] BaseColorConfigs = new ConfigEntry<string>[LoadoutCount];
        private static readonly SkinToneSelection[] LocalSelectionsByLoadout = new SkinToneSelection[LoadoutCount];
        private static SkinToneSelection _localSelection;
        private static int _activeLoadoutIndex;
        private static ConfigEntry<bool> _panelMinimizedConfig;
        [System.ThreadStatic]
        private static bool _isRevertingToVanilla;

        internal static void Initialize(ConfigFile config, ManualLogSource log)
        {
            _log = log;
            for (int i = 0; i < LoadoutCount; i++)
            {
                EnabledConfigs[i] = config.Bind($"CustomSkinTone.Loadout{i}", "Enabled", true, $"Whether the custom skin tone override is enabled for loadout {i + 1}.");
                BaseColorConfigs[i] = config.Bind($"CustomSkinTone.Loadout{i}", "BaseColor", SkinToneSelection.ToHtml(SkinToneSelection.DefaultBaseColor), $"Custom skin tone hex color for loadout {i + 1} (#RRGGBB).");
                LocalSelectionsByLoadout[i] = new SkinToneSelection
                {
                    Enabled = EnabledConfigs[i].Value,
                    BaseColor = ParseColor(BaseColorConfigs[i].Value, SkinToneSelection.DefaultBaseColor)
                };
            }

            _activeLoadoutIndex = 0;
            _localSelection = LocalSelectionsByLoadout[_activeLoadoutIndex];
            SkinToneDebugState.RecordLoadoutActivated(_activeLoadoutIndex, _localSelection);

            _panelMinimizedConfig = config.Bind(
                "Picker.Panel",
                "Minimized",
                false,
                "Whether the custom skin tone picker panel starts minimized to a small launcher button. Toggle in the menu via the \u2013 / \u2922 button.");
        }

        internal static SkinToneSelection LocalSelection => _localSelection;
        internal static int ActiveLoadoutIndex => _activeLoadoutIndex;
        internal static bool IsRevertingToVanilla => _isRevertingToVanilla;

        internal static bool IsPanelMinimized
        {
            get => _panelMinimizedConfig != null && _panelMinimizedConfig.Value;
            set
            {
                if (_panelMinimizedConfig == null) return;
                if (_panelMinimizedConfig.Value == value) return;
                _panelMinimizedConfig.Value = value;
                _panelMinimizedConfig.ConfigFile.Save();
            }
        }

        internal static void SetLocalSelection(SkinToneSelection selection, bool persist, bool broadcast)
        {
            _localSelection = selection;
            LocalSelectionsByLoadout[_activeLoadoutIndex] = selection;
            SkinToneDebugState.RecordSelectionChanged(_activeLoadoutIndex, selection);

            if (persist)
            {
                EnabledConfigs[_activeLoadoutIndex].Value = selection.Enabled;
                BaseColorConfigs[_activeLoadoutIndex].Value = SkinToneSelection.ToHtml(selection.BaseColor);
                EnabledConfigs[_activeLoadoutIndex].ConfigFile.Save();
            }

            ApplyToPreview(PlayerCustomizationMenu.Instance);

            if (broadcast)
            {
                SkinToneNetwork.TryBroadcastLocalSelection(selection);
            }
        }

        internal static void ActivateLoadout(int index, bool applyPreview, bool broadcast)
        {
            _activeLoadoutIndex = NormalizeLoadoutIndex(index);
            _localSelection = LocalSelectionsByLoadout[_activeLoadoutIndex];
            SkinToneDebugState.RecordLoadoutActivated(_activeLoadoutIndex, _localSelection);

            if (applyPreview)
            {
                ApplyToPreview(PlayerCustomizationMenu.Instance);
            }

            if (broadcast)
            {
                SkinToneNetwork.TryBroadcastLocalSelection(_localSelection);
            }
        }

        internal static void ApplyToPreview(PlayerCustomizationMenu menu)
        {
            if (menu == null || menu.characterPreview == null || menu.characterPreview.cosmeticsSwitcher == null)
            {
                return;
            }

            if (_localSelection.Enabled)
            {
                SkinToneDebugState.RecordApplyPreview(_localSelection);
                SkinToneMaterialApplier.Apply(menu.characterPreview.cosmeticsSwitcher, _localSelection);
            }
            else
            {
                SkinToneDebugState.RecordRevert(0, isPreview: true);
                RevertToVanilla(menu.characterPreview.cosmeticsSwitcher);
            }
        }

        internal static void SetRemoteSelection(uint netId, SkinToneSelection selection)
        {
            RemoteSelections[netId] = selection;
            TryApplyToSpawnedPlayer(netId);
        }

        internal static void ClearRemoteSelection(uint netId)
        {
            RemoteSelections.Remove(netId);
            TryApplyToSpawnedPlayer(netId);
        }

        internal static bool TryGetRemoteSelection(uint netId, out SkinToneSelection selection)
        {
            return RemoteSelections.TryGetValue(netId, out selection);
        }

        internal static void TryApplyToSpawnedPlayer(uint netId)
        {
            if (!NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) || identity == null)
            {
                return;
            }

            PlayerCosmetics cosmetics = identity.GetComponent<PlayerCosmetics>();
            if (cosmetics == null)
            {
                PlayerInfo playerInfo = identity.GetComponent<PlayerInfo>();
                cosmetics = playerInfo != null ? playerInfo.Cosmetics : null;
            }

            if (cosmetics == null)
            {
                return;
            }

            PlayerCosmeticsSwitcher switcher = cosmetics.GetComponent<PlayerCosmeticsSwitcher>();
            if (switcher == null)
            {
                return;
            }

            if (RemoteSelections.TryGetValue(netId, out SkinToneSelection selection) && selection.Enabled)
            {
                SkinToneDebugState.RecordApplyPlayer(netId, selection);
                SkinToneMaterialApplier.Apply(switcher, selection);
            }
            else
            {
                SkinToneDebugState.RecordRevert(netId, isPreview: false);
                RevertToVanilla(switcher);
            }
        }

        internal static void TryApplyFor(PlayerCosmetics cosmetics)
        {
            if (cosmetics == null)
            {
                return;
            }

            PlayerCosmeticsSwitcher switcher = cosmetics.GetComponent<PlayerCosmeticsSwitcher>();
            if (switcher == null)
            {
                return;
            }

            if (cosmetics.isLocalPlayer)
            {
                if (_localSelection.Enabled)
                {
                    SkinToneDebugState.RecordApplyPlayer(cosmetics.netId, _localSelection);
                    SkinToneMaterialApplier.Apply(switcher, _localSelection);
                }
                else
                {
                    SkinToneDebugState.RecordRevert(cosmetics.netId, isPreview: false);
                    RevertToVanilla(switcher);
                }

                return;
            }

            if (RemoteSelections.TryGetValue(cosmetics.netId, out SkinToneSelection selection) && selection.Enabled)
            {
                SkinToneDebugState.RecordApplyPlayer(cosmetics.netId, selection);
                SkinToneMaterialApplier.Apply(switcher, selection);
            }
        }

        internal static void OnLocalPlayerStarted(PlayerCosmetics cosmetics)
        {
            ActivateLoadout(cosmetics.GetEquippedLoadoutIndex(), applyPreview: true, broadcast: false);
            TryApplyFor(cosmetics);
            SkinToneNetwork.TryBroadcastLocalSelection(_localSelection);
        }

        private static Color ParseColor(string raw, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(raw) && ColorUtility.TryParseHtmlString(raw, out Color color))
            {
                color.a = 1f;
                return color;
            }

            _log?.LogWarning($"Invalid custom skin tone '{raw}', using fallback.");
            fallback.a = 1f;
            return fallback;
        }

        internal static void RevertToVanilla(PlayerCosmeticsSwitcher switcher)
        {
            if (switcher == null)
            {
                return;
            }

            bool previous = _isRevertingToVanilla;
            _isRevertingToVanilla = true;
            try
            {
                switcher.SetSkinColor(switcher.CurrentSkinColorIndex);
            }
            finally
            {
                _isRevertingToVanilla = previous;
            }
        }

        private static int NormalizeLoadoutIndex(int index)
        {
            return Mathf.Clamp(index, 0, LoadoutCount - 1);
        }
    }
}
