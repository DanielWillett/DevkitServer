namespace DevkitServer.Util.Comparers;
public class LevelObjectComparer : IComparer<LevelObject>, IEqualityComparer<LevelObject>
{
    public static readonly LevelObjectComparer Instance = new LevelObjectComparer();
    public int Compare(LevelObject x, LevelObject y) => x.instanceID.CompareTo(y.instanceID);
    public bool Equals(LevelObject x, LevelObject y) => x.instanceID == y.instanceID;
    public int GetHashCode(LevelObject obj) => unchecked( (int)obj.instanceID );
    private LevelObjectComparer() { }
}
