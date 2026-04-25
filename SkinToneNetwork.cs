using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace EvenMoreSkinColors
{
    public struct SkinToneUpdateRequestMsg : NetworkMessage
    {
        public bool enabled;
        public byte r;
        public byte g;
        public byte b;
    }

    public struct SkinToneStateMsg : NetworkMessage
    {
        public uint netId;
        public bool enabled;
        public byte r;
        public byte g;
        public byte b;
    }

    public struct SkinToneSnapshotRequestMsg : NetworkMessage
    {
    }

    internal static class SkinToneNetwork
    {
        private static bool _clientHandlersRegistered;
        private static bool _serverHandlersRegistered;
        private static readonly Dictionary<uint, SkinToneSelection> ServerSelections = new Dictionary<uint, SkinToneSelection>();

        internal static void OnClientConnected()
        {
            RegisterClientHandlers();
            RegisterServerHandlers();

            if (NetworkClient.active)
            {
                NetworkClient.Send(new SkinToneSnapshotRequestMsg());
            }
        }

        internal static void TryBroadcastLocalSelection(SkinToneSelection selection)
        {
            if (!NetworkClient.active)
            {
                return;
            }

            RegisterClientHandlers();
            RegisterServerHandlers();

            Color32 color = selection.BaseColor;
            uint netId = NetworkClient.connection != null && NetworkClient.connection.identity != null
                ? NetworkClient.connection.identity.netId
                : 0u;
            SkinToneDebugState.RecordBroadcast(netId, selection);
            NetworkClient.Send(new SkinToneUpdateRequestMsg
            {
                enabled = selection.Enabled,
                r = color.r,
                g = color.g,
                b = color.b
            });
        }

        private static void RegisterClientHandlers()
        {
            if (_clientHandlersRegistered)
            {
                return;
            }

            NetworkClient.ReplaceHandler<SkinToneStateMsg>(OnStateMessage);
            _clientHandlersRegistered = true;
        }

        private static void RegisterServerHandlers()
        {
            if (!NetworkServer.active || _serverHandlersRegistered)
            {
                return;
            }

            NetworkServer.ReplaceHandler<SkinToneUpdateRequestMsg>(OnUpdateRequest);
            NetworkServer.ReplaceHandler<SkinToneSnapshotRequestMsg>(OnSnapshotRequest);
            _serverHandlersRegistered = true;
        }

        private static void OnStateMessage(SkinToneStateMsg msg)
        {
            SkinToneSelection selection = new SkinToneSelection
            {
                Enabled = msg.enabled,
                BaseColor = new Color32(msg.r, msg.g, msg.b, 255)
            };
            SkinToneDebugState.RecordReceive(msg.netId, selection);

            if (selection.Enabled)
            {
                SkinToneState.SetRemoteSelection(msg.netId, selection);
            }
            else
            {
                SkinToneState.ClearRemoteSelection(msg.netId);
            }
        }

        private static void OnUpdateRequest(NetworkConnectionToClient conn, SkinToneUpdateRequestMsg msg)
        {
            if (conn == null || conn.identity == null)
            {
                return;
            }

            PlayerCosmetics cosmetics = conn.identity.GetComponent<PlayerCosmetics>();
            if (cosmetics == null)
            {
                PlayerInfo playerInfo = conn.identity.GetComponent<PlayerInfo>();
                cosmetics = playerInfo != null ? playerInfo.Cosmetics : null;
            }

            if (cosmetics == null)
            {
                return;
            }

            SkinToneSelection selection = new SkinToneSelection
            {
                Enabled = msg.enabled,
                BaseColor = new Color32(msg.r, msg.g, msg.b, 255)
            };

            if (selection.Enabled)
            {
                ServerSelections[cosmetics.netId] = selection;
            }
            else
            {
                ServerSelections.Remove(cosmetics.netId);
            }

            NetworkServer.SendToAll(BuildState(cosmetics.netId, selection));
        }

        private static void OnSnapshotRequest(NetworkConnectionToClient conn, SkinToneSnapshotRequestMsg _)
        {
            if (conn == null)
            {
                return;
            }

            foreach (KeyValuePair<uint, SkinToneSelection> pair in ServerSelections)
            {
                conn.Send(BuildState(pair.Key, pair.Value));
            }
        }

        private static SkinToneStateMsg BuildState(uint netId, SkinToneSelection selection)
        {
            Color32 color = selection.BaseColor;
            return new SkinToneStateMsg
            {
                netId = netId,
                enabled = selection.Enabled,
                r = color.r,
                g = color.g,
                b = color.b
            };
        }
    }
}
