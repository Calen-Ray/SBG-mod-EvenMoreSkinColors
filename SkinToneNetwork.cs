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
        private static bool _serverDisconnectHooked;
        private static readonly Dictionary<uint, SkinToneSelection> ServerSelections = new Dictionary<uint, SkinToneSelection>();
        // Connections that have proven they speak our protocol by sending a snapshot or update
        // request. Server-only state. We broadcast state messages exclusively to these so
        // vanilla clients in the same lobby don't receive an unknown msg id and disconnect.
        private static readonly HashSet<int> ModdedConnections = new HashSet<int>();

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
            if (!_serverDisconnectHooked)
            {
                NetworkServer.OnDisconnectedEvent += OnServerConnectionDisconnected;
                _serverDisconnectHooked = true;
            }
            _serverHandlersRegistered = true;
        }

        private static void OnServerConnectionDisconnected(NetworkConnectionToClient conn)
        {
            if (conn != null)
            {
                ModdedConnections.Remove(conn.connectionId);
            }
        }

        private static void SendToModdedConnections(SkinToneStateMsg state)
        {
            // Iterate all known connections and target only those that have demonstrated they
            // speak our protocol. Mirror disconnects clients that receive unknown message ids,
            // so a blanket SendToAll would kick vanilla peers in mixed lobbies.
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection == null)
                {
                    continue;
                }
                if (!ModdedConnections.Contains(connection.connectionId))
                {
                    continue;
                }
                connection.Send(state);
            }
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

            // Sender is by definition modded — they sent us a custom message type. Track them
            // so subsequent SendToModdedConnections fan-outs reach this client.
            ModdedConnections.Add(conn.connectionId);

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

            SendToModdedConnections(BuildState(cosmetics.netId, selection));
        }

        private static void OnSnapshotRequest(NetworkConnectionToClient conn, SkinToneSnapshotRequestMsg _)
        {
            if (conn == null)
            {
                return;
            }

            // Snapshot request is the canonical "I'm modded" signal — only modded clients have
            // the serializer for this message id. Mark the connection before replying so the
            // first state msg fan-out reaches them.
            ModdedConnections.Add(conn.connectionId);

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
