namespace DevkitServer.Util.Region;
public struct ListRegionsEnumerator<T> : IEnumerable<T>, IEnumerator<T>
{
    private readonly List<T>[,] _regions;

    private SurroundingRegionsIterator _xyIterator;
    private List<T>? _currentRegion;
    private int _index;
    private T? _current;

    public RegionCoord Coordinate = RegionCoord.ZERO;
    public List<T>[,] Regions => _regions;
    public List<T>? Region => _currentRegion;
    public ListRegionsEnumerator(List<T>[,] regions) : this(regions, (byte)(SDG.Unturned.Regions.WORLD_SIZE / 2), (byte)(SDG.Unturned.Regions.WORLD_SIZE / 2), 255) { }
    public ListRegionsEnumerator(List<T>[,] regions, byte centerX, byte centerY, byte maxRegionDistance = 255)
    {
        _regions = regions;
        _xyIterator = new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
        _currentRegion = null;
        _current = default;
    }

    public bool MoveNext()
    {
        ++_index;
        while (_currentRegion == null || _index >= _currentRegion.Count)
        {
            if (_regions == null)
                return false;
            if (!_xyIterator.MoveNext())
            {
                _current = default;
                return false;
            }
            Coordinate = _xyIterator.Current;
            _currentRegion = _regions[Coordinate.x, Coordinate.y];
            if (_currentRegion.Count == 0)
                continue;
            _index = 0;
        }

        _current = _currentRegion[_index];
        return true;
    }

    public void Reset()
    {
        _xyIterator.Reset();
        _currentRegion = null;
        _current = default;
    }

    public T Current => _current!;
    object IEnumerator.Current => _current!;
    public ListRegionsEnumerator<T> GetEnumerator() => new ListRegionsEnumerator<T>(_regions, _xyIterator.StartX, _xyIterator.StartY, _xyIterator.MaxRegionDistance);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new ListRegionsEnumerator<T>(_regions, _xyIterator.StartX, _xyIterator.StartY, _xyIterator.MaxRegionDistance);
    IEnumerator IEnumerable.GetEnumerator() => new ListRegionsEnumerator<T>(_regions, _xyIterator.StartX, _xyIterator.StartY, _xyIterator.MaxRegionDistance);
    void IDisposable.Dispose() { }
}
