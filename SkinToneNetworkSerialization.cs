using Mirror;

namespace EvenMoreSkinColors
{
    internal static class SkinToneNetworkSerialization
    {
        internal static void Register()
        {
            Writer<SkinToneUpdateRequestMsg>.write = WriteSkinToneUpdateRequest;
            Reader<SkinToneUpdateRequestMsg>.read = ReadSkinToneUpdateRequest;

            Writer<SkinToneStateMsg>.write = WriteSkinToneState;
            Reader<SkinToneStateMsg>.read = ReadSkinToneState;

            Writer<SkinToneSnapshotRequestMsg>.write = WriteSkinToneSnapshotRequest;
            Reader<SkinToneSnapshotRequestMsg>.read = ReadSkinToneSnapshotRequest;
        }

        private static void WriteSkinToneUpdateRequest(NetworkWriter writer, SkinToneUpdateRequestMsg msg)
        {
            writer.WriteBool(msg.enabled);
            writer.WriteByte(msg.r);
            writer.WriteByte(msg.g);
            writer.WriteByte(msg.b);
        }

        private static SkinToneUpdateRequestMsg ReadSkinToneUpdateRequest(NetworkReader reader)
        {
            return new SkinToneUpdateRequestMsg
            {
                enabled = reader.ReadBool(),
                r = reader.ReadByte(),
                g = reader.ReadByte(),
                b = reader.ReadByte()
            };
        }

        private static void WriteSkinToneState(NetworkWriter writer, SkinToneStateMsg msg)
        {
            writer.WriteUInt(msg.netId);
            writer.WriteBool(msg.enabled);
            writer.WriteByte(msg.r);
            writer.WriteByte(msg.g);
            writer.WriteByte(msg.b);
        }

        private static SkinToneStateMsg ReadSkinToneState(NetworkReader reader)
        {
            return new SkinToneStateMsg
            {
                netId = reader.ReadUInt(),
                enabled = reader.ReadBool(),
                r = reader.ReadByte(),
                g = reader.ReadByte(),
                b = reader.ReadByte()
            };
        }

        private static void WriteSkinToneSnapshotRequest(NetworkWriter writer, SkinToneSnapshotRequestMsg msg)
        {
        }

        private static SkinToneSnapshotRequestMsg ReadSkinToneSnapshotRequest(NetworkReader reader)
        {
            return new SkinToneSnapshotRequestMsg();
        }
    }
}
