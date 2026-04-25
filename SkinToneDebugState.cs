using UnityEngine;

namespace EvenMoreSkinColors
{
    internal static class SkinToneDebugState
    {
        internal struct Snapshot
        {
            public int ActiveLoadoutIndex;
            public bool LocalCustomToneEnabled;
            public string LocalColorHex;
            public uint LastAppliedNetId;
            public bool LastAppliedWasPreview;
            public string LastAppliedColorHex;
            public uint LastBroadcastNetId;
            public string LastBroadcastColorHex;
            public uint LastReceivedNetId;
            public string LastReceivedColorHex;
            public string LastEvent;
        }

        private static Snapshot _snapshot;

        internal static Snapshot Current => _snapshot;

        internal static void RecordLoadoutActivated(int loadoutIndex, SkinToneSelection selection)
        {
            _snapshot.ActiveLoadoutIndex = loadoutIndex;
            _snapshot.LocalCustomToneEnabled = selection.Enabled;
            _snapshot.LocalColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = $"loadout:{loadoutIndex}";
        }

        internal static void RecordSelectionChanged(int loadoutIndex, SkinToneSelection selection)
        {
            _snapshot.ActiveLoadoutIndex = loadoutIndex;
            _snapshot.LocalCustomToneEnabled = selection.Enabled;
            _snapshot.LocalColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = $"selection:{loadoutIndex}";
        }

        internal static void RecordBroadcast(uint netId, SkinToneSelection selection)
        {
            _snapshot.LastBroadcastNetId = netId;
            _snapshot.LastBroadcastColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = $"broadcast:{netId}";
            Plugin.Log.LogInfo($"EMSC_DEBUG broadcast netId={netId} enabled={selection.Enabled} color={_snapshot.LastBroadcastColorHex}");
        }

        internal static void RecordReceive(uint netId, SkinToneSelection selection)
        {
            _snapshot.LastReceivedNetId = netId;
            _snapshot.LastReceivedColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = $"receive:{netId}";
            Plugin.Log.LogInfo($"EMSC_DEBUG receive netId={netId} enabled={selection.Enabled} color={_snapshot.LastReceivedColorHex}");
        }

        internal static void RecordApplyPreview(SkinToneSelection selection)
        {
            _snapshot.LastAppliedNetId = 0;
            _snapshot.LastAppliedWasPreview = true;
            _snapshot.LastAppliedColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = "apply-preview";
            Plugin.Log.LogInfo($"EMSC_DEBUG apply preview enabled={selection.Enabled} color={_snapshot.LastAppliedColorHex}");
        }

        internal static void RecordApplyPlayer(uint netId, SkinToneSelection selection)
        {
            _snapshot.LastAppliedNetId = netId;
            _snapshot.LastAppliedWasPreview = false;
            _snapshot.LastAppliedColorHex = SkinToneSelection.ToHtml(selection.BaseColor);
            _snapshot.LastEvent = $"apply-player:{netId}";
            Plugin.Log.LogInfo($"EMSC_DEBUG apply player netId={netId} enabled={selection.Enabled} color={_snapshot.LastAppliedColorHex}");
        }

        internal static void RecordRevert(uint netId, bool isPreview)
        {
            _snapshot.LastAppliedNetId = netId;
            _snapshot.LastAppliedWasPreview = isPreview;
            _snapshot.LastEvent = isPreview ? "revert-preview" : $"revert-player:{netId}";
            Plugin.Log.LogInfo($"EMSC_DEBUG revert {(isPreview ? "preview" : $"player netId={netId}")}");
        }
    }
}

