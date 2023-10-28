using DevkitServer.API;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;

namespace DevkitServer.Util.Region;

[EarlyTypeInit]
public struct SurroundingTilesIterator 
    : IEnumerator<LandscapeCoord>, IEnumerable<LandscapeCoord>
    , IEnumerator<HeightmapCoord>, IEnumerable<HeightmapCoord>
    , IEnumerator<SplatmapCoord>, IEnumerable<SplatmapCoord>
    , IEnumerator<FoliageCoord>, IEnumerable<FoliageCoord>
{
    private static int[] _layerIndices = null!;
    static SurroundingTilesIterator()
    {
        GetLayerIndex(Landscape.TILE_SIZE_INT / FoliageSystem.TILE_SIZE_INT * 8);
    }
    private static int GetLayerIndex(int layer)
    {
        if (_layerIndices != null && _layerIndices.Length > layer)
            return _layerIndices[layer];
        int[]? old = _layerIndices;
        _layerIndices = new int[layer + 1];
        if (old == null || old.Length < 2)
        {
            if (layer > 1)
                _layerIndices[1] = 1;
            for (int i = 2; i < _layerIndices.Length; i++)
                _layerIndices[i] = _layerIndices[i - 1] + (i - 1) * 8;
        }
        else
        {
            Buffer.BlockCopy(old, 0, _layerIndices, 0, old.Length * sizeof(int));
            for (int i = old.Length; i < _layerIndices.Length; i++)
                _layerIndices[i] = _layerIndices[i - 1] + (i - 1) * 8;
        }

        return _layerIndices[layer];
    }
    private int _x;
    private int _y;
    private readonly int _startX;
    private readonly int _startY;
    private readonly TileIteratorMode _mode;
    private readonly int _upper;
    private readonly int _lower;
    private int _index;
    private int _layer;
    private int _nextLayerIndex;

    public SurroundingTilesIterator(int x, int y, TileIteratorMode mode, int upperExclusive, int lowerExclusive)
    {
        _upper = Math.Max(upperExclusive, lowerExclusive);
        _lower = Math.Min(upperExclusive, lowerExclusive);
        _startX = x;
        _startY = y;
        _index = -1;
        _nextLayerIndex = 1;
        _mode = mode;
    }
    public SurroundingTilesIterator(int x, int y, TileIteratorMode mode)
    {
        if (mode == TileIteratorMode.Other)
            throw new ArgumentException("Can not use Other unless upper and lower limits are provided.", nameof(mode));
        _startX = x;
        _startY = y;
        _index = -1;
        _nextLayerIndex = 1;
        _mode = mode;
        switch (mode)
        {
            case TileIteratorMode.FoliageCoord:
                _upper = FoliageUtil.Tiles.Max(x => Math.Max(x.coord.x, x.coord.y)) + 1;
                _lower = FoliageUtil.Tiles.Min(x => Math.Min(x.coord.x, x.coord.y)) - 1;
                break;
            case TileIteratorMode.LandscapeCoord:
            default:
                _upper = LandscapeUtil.Tiles.Max(x => Math.Max(x.coord.x, x.coord.y)) + 1;
                _lower = LandscapeUtil.Tiles.Min(x => Math.Min(x.coord.x, x.coord.y)) - 1;
                break;
            case TileIteratorMode.SplatmapCoord:
                _upper = Landscape.SPLATMAP_RESOLUTION + 1;
                _lower = -1;
                break;
            case TileIteratorMode.HeightmapCoord:
                _upper = Landscape.HEIGHTMAP_RESOLUTION + 1;
                _lower = -1;
                break;
        }
    }
    void IDisposable.Dispose() { }
    public bool MoveNext()
    {
        while (MoveNextIntl(out int x, out int y))
        {
            if (x <= _lower || x >= _upper || y <= _lower || y >= _upper)
                continue;
            switch (_mode)
            {
                case TileIteratorMode.FoliageCoord:
                    if (FoliageSystem.getTile(new FoliageCoord(x, y)) == null)
                        continue;
                    break;
                case TileIteratorMode.LandscapeCoord:
                    if (Landscape.getTile(new LandscapeCoord(x, y)) == null)
                        continue;
                    break;
            }
            _x = x;
            _y = y;
            return true;
        }

        return false;
    }
    private bool MoveNextIntl(out int x, out int y)
    {
        ++_index;
        if (_index == 0)
        {
            x = _startX;
            y = _startY;
            return true;
        }

        if (_index >= _nextLayerIndex)
        {
            ++_layer;
            if (_layer >= (_lower < 0 ? Math.Max(-_lower, _upper) : (_upper - _lower)))
            {
                x = 0;
                y = 0;
                return false;
            }

            _nextLayerIndex = GetLayerIndex(_layer + 1);
        }

        int layerIndex = _index - GetLayerIndex(_layer);
        int side = layerIndex % 4;
        int magnitude = layerIndex / 4;
        bool otherSide = magnitude % 2 == 1;
        magnitude = Mathf.CeilToInt(magnitude / 2f);
        switch (side)
        {
            case 0:
                y = _layer;
                if (otherSide)
                    x = magnitude;
                else
                    x = -magnitude;
                break;
            case 1:
                y = -_layer;
                if (otherSide)
                    x = -magnitude;
                else
                    x = magnitude;
                break;
            case 2:
                x = _layer;
                if (otherSide)
                    y = -magnitude;
                else
                    y = magnitude;
                break;
            default:
                x = -_layer;
                if (otherSide)
                    y = magnitude;
                else
                    y = -magnitude;
                break;
        }

        x += _startX;
        y += _startY;

        return true;
    }

    public void Reset()
    {
        _x = 0;
        _y = 0;
        _index = -1;
        _layer = 0;
        _nextLayerIndex = 1;
    }

    public int CurrentX => _x;
    public int CurrentY => _y;
    public LandscapeCoord Current => new LandscapeCoord(_x, _y);
    SplatmapCoord IEnumerator<SplatmapCoord>.Current => new SplatmapCoord(_x, _y);
    HeightmapCoord IEnumerator<HeightmapCoord>.Current => new HeightmapCoord(_x, _y);
    FoliageCoord IEnumerator<FoliageCoord>.Current => new FoliageCoord(_x, _y);
    object IEnumerator.Current => Current;
    public SurroundingTilesIterator GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
    IEnumerator<LandscapeCoord> IEnumerable<LandscapeCoord>.GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
    IEnumerator<SplatmapCoord> IEnumerable<SplatmapCoord>.GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
    IEnumerator<HeightmapCoord> IEnumerable<HeightmapCoord>.GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
    IEnumerator<FoliageCoord> IEnumerable<FoliageCoord>.GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
    IEnumerator IEnumerable.GetEnumerator() => new SurroundingTilesIterator(_startX, _startY, _mode, _upper, _lower);
}

public enum TileIteratorMode
{
    LandscapeCoord,
    SplatmapCoord,
    HeightmapCoord,
    FoliageCoord,
    Other
}
