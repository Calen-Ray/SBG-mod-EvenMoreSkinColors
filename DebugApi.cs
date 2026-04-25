using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace EvenMoreSkinColors
{
    public static class DebugApi
    {
        public sealed class LocalState
        {
            public bool hasLocalPlayer;
            public uint localNetId;
            public int activeLoadoutIndex;
            public int vanillaSkinColorIndex;
            public bool customToneEnabled;
            public string customColorHex;
            public bool previewOverrideApplied;
            public bool playerOverrideApplied;
            public string lastEvent;
        }

        public sealed class RemoteState
        {
            public uint netId;
            public bool isLocalPlayer;
            public int vanillaSkinColorIndex;
            public bool hasCustomTone;
            public string customColorHex;
            public bool overrideApplied;
        }

        public sealed class Snapshot
        {
            public LocalState local;
            public List<RemoteState> players;
        }

        public static LocalState GetLocalState()
        {
            PlayerInfo localPlayer = GameManager.LocalPlayerInfo;
            PlayerCosmetics cosmetics = localPlayer != null ? localPlayer.Cosmetics : null;
            PlayerCosmeticsSwitcher previewSwitcher = PlayerCustomizationMenu.Instance != null &&
                                                     PlayerCustomizationMenu.Instance.characterPreview != null
                ? PlayerCustomizationMenu.Instance.characterPreview.cosmeticsSwitcher
                : null;

            return new LocalState
            {
                hasLocalPlayer = localPlayer != null,
                localNetId = cosmetics != null ? cosmetics.netId : 0u,
                activeLoadoutIndex = SkinToneState.ActiveLoadoutIndex,
                vanillaSkinColorIndex = cosmetics != null && cosmetics.GetComponent<PlayerCosmeticsSwitcher>() != null
                    ? cosmetics.GetComponent<PlayerCosmeticsSwitcher>().CurrentSkinColorIndex
                    : -1,
                customToneEnabled = SkinToneState.LocalSelection.Enabled,
                customColorHex = SkinToneSelection.ToHtml(SkinToneState.LocalSelection.BaseColor),
                previewOverrideApplied = previewSwitcher != null && SkinToneState.LocalSelection.Enabled,
                playerOverrideApplied = cosmetics != null && SkinToneState.LocalSelection.Enabled,
                lastEvent = SkinToneDebugState.Current.LastEvent
            };
        }

        public static List<RemoteState> GetRemoteStates()
        {
            var result = new List<RemoteState>();
            foreach (KeyValuePair<uint, NetworkIdentity> pair in NetworkClient.spawned)
            {
                NetworkIdentity identity = pair.Value;
                if (identity == null)
                {
                    continue;
                }

                PlayerCosmetics cosmetics = identity.GetComponent<PlayerCosmetics>();
                if (cosmetics == null)
                {
                    PlayerInfo playerInfo = identity.GetComponent<PlayerInfo>();
                    cosmetics = playerInfo != null ? playerInfo.Cosmetics : null;
                }

                if (cosmetics == null)
                {
                    continue;
                }

                PlayerCosmeticsSwitcher switcher = cosmetics.GetComponent<PlayerCosmeticsSwitcher>();
                bool hasRemote = SkinToneState.TryGetRemoteSelection(cosmetics.netId, out SkinToneSelection remoteSelection);
                result.Add(new RemoteState
                {
                    netId = cosmetics.netId,
                    isLocalPlayer = cosmetics.isLocalPlayer,
                    vanillaSkinColorIndex = switcher != null ? switcher.CurrentSkinColorIndex : -1,
                    hasCustomTone = cosmetics.isLocalPlayer ? SkinToneState.LocalSelection.Enabled : hasRemote && remoteSelection.Enabled,
                    customColorHex = cosmetics.isLocalPlayer
                        ? SkinToneSelection.ToHtml(SkinToneState.LocalSelection.BaseColor)
                        : (hasRemote ? SkinToneSelection.ToHtml(remoteSelection.BaseColor) : string.Empty),
                    overrideApplied = cosmetics.isLocalPlayer
                        ? SkinToneState.LocalSelection.Enabled
                        : hasRemote && remoteSelection.Enabled
                });
            }

            return result;
        }

        public static Snapshot GetSnapshot()
        {
            return new Snapshot
            {
                local = GetLocalState(),
                players = GetRemoteStates()
            };
        }
    }
}

