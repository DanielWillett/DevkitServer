namespace DevkitServer.API.Iterators;
public struct DistanceListIterator<T> : IEnumerator<T>, IEnumerable<T>
{
    private readonly List<T> _list;
    private readonly Func<T, Vector3> _positionFunction;
    private readonly Vector3 _center;
    private readonly bool _xzAxisOnly;
    private readonly bool _asc;
    private float _lastDist;
    private int _lastDuplicate;
    // ReSharper disable once InconsistentNaming
    public DistanceListIterator(List<T> list, Func<T, Vector3> selectPosition, Vector3 center, bool descending = false, bool useXZAxisOnly = false)
    {
        _positionFunction = selectPosition;
        _list = list;
        _center = center;
        _xzAxisOnly = useXZAxisOnly;
        _lastDist = -1f;
        _lastDuplicate = -1;
        _asc = !descending;
    }

    public bool MoveNext()
    {
        Vector3 center = _center;
        Func<T, Vector3> posFunc = _positionFunction;
        bool is2d = _xzAxisOnly;
        bool asc = _asc;
        List<T> list = _list;
        float targetDistance = _lastDist;
        float extremeDistance = 0f;
        int extremeIndex = -1;
        bool lowestValueHasADuplicate = false;
        bool excludeTargetDistance = _lastDuplicate == 0;

        redoAfterRunningOutOfDuplicates:
        for (int i = 0; i < list.Count; ++i)
        {
            Vector3 pos = posFunc(list[i]);
            float dist = GetDistance(in pos, in center, is2d, asc);
            if (extremeIndex < 0)
            {
                if (targetDistance == -1f ||
                    (asc ?
                        (excludeTargetDistance ? targetDistance < dist : targetDistance <= dist) :
                        (excludeTargetDistance ? dist < targetDistance : dist <= targetDistance)))
                {
                    extremeIndex = i;
                    extremeDistance = dist;
                }
            }
            else if (asc ?
                         ((targetDistance == -1f || (excludeTargetDistance ? targetDistance < dist : targetDistance <= dist)) && extremeDistance >= dist) :
                         ((targetDistance == -1f || (excludeTargetDistance ? dist < targetDistance : dist <= targetDistance)) && dist >= extremeDistance))
            {
                lowestValueHasADuplicate = dist == extremeDistance;
                extremeIndex = i;
                extremeDistance = dist;
            }
        }

        if (extremeIndex == -1)
        {
            Current = default!;
            return false;
        }

        if (lowestValueHasADuplicate)
        {
            int dupeCt = 0;
            int end = extremeIndex;
            for (int i = 0; i <= end; ++i)
            {
                Vector3 pos = posFunc(list[i]);
                float dist = GetDistance(in pos, in center, is2d, asc);
                if (dist != extremeDistance)
                    continue;

                extremeIndex = i;
                if (++dupeCt > _lastDuplicate)
                    break;
            }

            if (dupeCt == _lastDuplicate)
            {
                excludeTargetDistance = true;
                targetDistance = extremeDistance;
                extremeIndex = -1;
                _lastDuplicate = 0;
                lowestValueHasADuplicate = false;
                goto redoAfterRunningOutOfDuplicates;
            }
            _lastDuplicate = dupeCt;
        }
        else _lastDuplicate = 0;

        _lastDist = extremeDistance;
        Current = list[extremeIndex];
        return true;
    }
    private static float GetDistance(in Vector3 v1, in Vector3 v2, bool is2d, bool asc)
    {
        float dist = v1.x - v2.x;
        dist *= dist;
        float dist2 = v1.y - v2.y;
        dist2 *= dist2;
        dist += dist2;
        if (!is2d)
        {
            dist2 = v1.z - v2.z;
            dist2 *= dist2;
            dist += dist2;
        }

        if (float.IsNaN(dist))
            dist = asc ? float.PositiveInfinity : float.NegativeInfinity;

        return dist;
    }
    public void Reset()
    {
        _lastDist = -1f;
        _lastDuplicate = -1;
        Current = default!;
    }

    public T Current { get; private set; } = default!;
    object IEnumerator.Current => Current!;
    void IDisposable.Dispose() { }
    public DistanceListIterator<T> GetEnumerator() => this with { _lastDist = -1f, _lastDuplicate = -1, Current = default! };
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => this with { _lastDist = -1f, _lastDuplicate = -1, Current = default! };
    IEnumerator IEnumerable.GetEnumerator() => this with { _lastDist = -1f, _lastDuplicate = -1, Current = default! };
}
