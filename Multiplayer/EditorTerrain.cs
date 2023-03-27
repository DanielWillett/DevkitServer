using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Multiplayer;
public class EditorTerrain
{
    public static readonly NetCallCustom FlushEditBuffer = new NetCallCustom((int)NetCalls.FlushEditBuffer, short.MaxValue);

    private readonly Queue<ITerrainTransaction> _editBuffer = new Queue<ITerrainTransaction>();

    [NetCall(NetCallSource.FromEither, (int)NetCalls.FlushEditBuffer)]
    private static void ReceiveEditBuffer(MessageContext ctx, ByteReader reader)
    {

    }
#if CLIENT
    internal void FlushEdits()
    {
        MessageOverhead overhead = new MessageOverhead(MessageFlags.LayeredRequest, (int)NetCalls.FlushEditBuffer, 0);
        FlushEditBuffer.Invoke(ref overhead, WriteEditBuffer);
    }
#endif
    private void WriteEditBuffer(ByteWriter writer)
    {

    }

    public void Init()
    {
#if CLIENT
        ClientEvents.OnRampConfirmed += OnRampConfirmed;
#endif
    }

    private void OnRampConfirmed(Bounds bounds)
    {

    }

    public enum TerrainTransactionType
    {
        HeightmapRamp,
        HeightmapAdjust,
        HeightmapFlatten,
        HeightmapSmooth,
        SplatmapPaint,
        SplatmapAutoPaint,
        SplatmapSmooth,

    }

    public interface ITerrainTransaction
    {
        TerrainTransactionType Type { get; }
        void Apply();
        void Write(ByteWriter writer);
        void Read(ByteReader reader);
    }
    private class HeightmapRampTransaction : ITerrainTransaction
    {
        public TerrainTransactionType Type => TerrainTransactionType.HeightmapRamp;
        public void Apply()
        {

        }
        public void Read(ByteReader reader)
        {
            throw new NotImplementedException();
        }
        public void Write(ByteWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
