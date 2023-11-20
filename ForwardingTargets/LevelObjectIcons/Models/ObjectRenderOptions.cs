using SDG.Unturned;

namespace DanielWillett.LevelObjectIcons.Models;
public class ObjectRenderOptions
{
    public int MaterialIndexOverride { get; set; } = -1;
    public AssetReference<MaterialPaletteAsset> MaterialPaletteOverride { get; set; } = AssetReference<MaterialPaletteAsset>.invalid;
}

