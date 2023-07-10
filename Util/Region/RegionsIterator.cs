namespace DevkitServer.Util.Region;
public struct RegionsIterator : IEnumerator<RegionCoord>, IEnumerable<RegionCoord>
{
    private int _x;
    private int _y;
    private readonly bool _yPrimary;
    private RegionCoord _current;
    public bool MoveNext()
    {
        if (_yPrimary)
        {
            ++_x;
            if (_x >= Regions.WORLD_SIZE)
            {
                _x = 0;
                ++_y;
                if (_y >= Regions.WORLD_SIZE)
                    return false;
            }
        }
        else
        {
            ++_y;
            if (_y >= Regions.WORLD_SIZE)
            {
                _y = 0;
                ++_x;
                if (_x >= Regions.WORLD_SIZE)
                    return false;
            }
        }
        _current = new RegionCoord((byte)_x, (byte)_y);
        return true;
    }

    public RegionsIterator()
    {
        _yPrimary = false;
        _x = 0;
        _y = -1;
    }
    public RegionsIterator(bool yPrimary = false)
    {
        _yPrimary = yPrimary;
        _x = yPrimary ? -1 : 0;
        _y = yPrimary ? 0 : -1;
    }
    public void Reset()
    {
        _x = _yPrimary ? -1 : 0;
        _y = _yPrimary ? 0 : -1;
    }

    public RegionCoord Current => _current;
    object IEnumerator.Current => Current;

    void IDisposable.Dispose() { }
    public RegionsIterator GetEnumerator() => new RegionsIterator(_yPrimary);
    IEnumerator<RegionCoord> IEnumerable<RegionCoord>.GetEnumerator() => new RegionsIterator(_yPrimary);
    IEnumerator IEnumerable.GetEnumerator() => new RegionsIterator(_yPrimary);
}
