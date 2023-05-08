using DevkitServer.Util.Encoding;
using System.Text;

namespace DevkitServer.Multiplayer.Actions;
public class ActionSettingsCollection : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IBrushTarget, ICoordinates, IAsset, IAutoFoundation, IAutoSlope
{
    public int StartIndex { get; internal set; }
    public ActionSetting Flags { get; private set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float BrushSensitivity { get; set; }
    public float BrushTarget { get; set; }
    public int CoordinateX { get; set; }
    public int CoordinateY { get; set; }
    public Guid Asset { get; set; }
    public float AutoFoundationRayLength { get; set; }
    public float AutoFoundationRayRadius { get; set; }
    public float AutoSlopeMinAngleBegin { get; set; }
    public float AutoSlopeMinAngleEnd { get; set; }
    public float AutoSlopeMaxAngleBegin { get; set; }
    public float AutoSlopeMaxAngleEnd { get; set; }
    public ERayMask AutoFoundationRayMask { get; set; }
    public void Reset()
    {
        Flags = ActionSetting.None;
    }

    public void Write(ByteWriter writer)
    {
        if (!EditorActionsCodeGeneration.Init)
        {
            writer.Write(ActionSetting.None);
            writer.Write((byte)0);
            return;
        }
        writer.Write(Flags);
        writer.Write((byte)StartIndex);
        EditorActionsCodeGeneration.WriteSettingsCollection!(this, writer);
    }

    public void Read(ByteReader reader)
    {
        Reset();
        Flags = reader.ReadEnum<ActionSetting>();
        StartIndex = reader.ReadUInt8();
        if (EditorActionsCodeGeneration.Init)
            EditorActionsCodeGeneration.ReadSettingsCollection!(this, reader);
    }

    public override string ToString()
    {
        if (!EditorActionsCodeGeneration.Init)
            return "FAILED TO INITIALIZE EditorActionsCodeGeneration";
        StringBuilder sb = new StringBuilder();
        EditorActionsCodeGeneration.AppendSettingsCollection!(this, sb);
        return sb.ToString();
    }
}
