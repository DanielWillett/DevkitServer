namespace DevkitServer.Util.Comparers;
public sealed class AssetComparer : IComparer<Asset>, IEqualityComparer<Asset>
{
    public static AssetComparer Instance { get; } = new AssetComparer();
    private AssetComparer() { }
    public int Compare(Asset x, Asset y) => x.GUID.CompareTo(y.GUID);
    public bool Equals(Asset x, Asset y) => x.GUID == y.GUID;
    public int GetHashCode(Asset obj) => obj.GUID.GetHashCode();
}
