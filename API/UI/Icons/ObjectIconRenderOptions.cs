namespace DevkitServer.API.UI.Icons;
/// <summary>
/// Options for <see cref="ObjectIconGenerator"/>.
/// </summary>
public class ObjectIconRenderOptions
{
    /// <summary>
    /// Override the index in a <see cref="MaterialPaletteAsset"/> of the material to use. Defaults to a random index.
    /// </summary>
    public int MaterialIndexOverride { get; set; } = -1;

    /// <summary>
    /// Override a <see cref="MaterialPaletteAsset"/> to use for rendering. Defaults to a the one defined in the dat file.
    /// </summary>
    public AssetReference<MaterialPaletteAsset> MaterialPaletteOverride { get; set; } = AssetReference<MaterialPaletteAsset>.invalid;
}
