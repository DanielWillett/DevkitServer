namespace DevkitServer.Util;
internal struct FastStructArray<T> : IEnumerable<T> where T : struct
{
    private T[] _array;
    private static T[]? _empty;
    public FastStructArray() : this(8) { }
    public FastStructArray(int capacity)
    {
        _array = new T[capacity];
    }

    public int Length;
    public int Offset;
    public ArraySegment<T> Segment => new ArraySegment<T>(_array ??= Empty(), Offset, Length);
    public T[] Array => _array ??= Empty();

    /// <summary>
    /// This is an unsafe reference and can change whenever you write to this array.
    /// </summary>
    public ref T this[int index]
    {
        get
        {
            if (_array is null)
                throw new IndexOutOfRangeException();
            return ref _array[index + Offset];
        }
    }

    public readonly T At(int index)
    {
        if (_array is null)
            throw new IndexOutOfRangeException();
        return _array[index + Offset];
    }

    /// <summary>
    /// This is an unsafe reference and can change whenever you write to this array.
    /// </summary>
    public ref T Add(T value)
    {
        return ref Insert(Length, value);
    }
    public T RemoveAt(int index) => RemoveAt(ref index);
    public T RemoveAt(ref int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        T valByVal;
        if (index == Length - 1)
        {
            ref T val = ref _array[index + Offset];
            valByVal = val;
            val = default;
            --Length;
            --index;
            return valByVal;
        }

        index += Offset;
        if (index >= _array.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _array ??= Empty();
        ref T val2 = ref _array[index];
        valByVal = val2;
        val2 = default;
        if (Offset >= Length) // move all of segment to beginning of array
        {
            for (int i = 0; i < Length; ++i)
            {
                int oldIndex = i + Offset;
                int newIndex = i;
                if (oldIndex == index)
                    continue;
                if (oldIndex > index)
                    newIndex--;
                _array[newIndex] = _array[oldIndex];
                _array[oldIndex] = default;
            }

            index -= Offset + 1;

            Offset = 0;
        }
        else if (index - Offset > Length / 2) // move segment towards end of array
        {
            int len = index - Offset;
            for (int i = 0; i < len; ++i)
                _array[Offset + i + 1] = _array[Offset + i];

            _array[Offset] = default;

            index = len;

            ++Offset;
        }
        else // move segment towards start of array
        {
            int len = Length + Offset - index - 1;
            for (int i = 0; i < len; ++i)
            {
                _array[index + i] = _array[index + i + 1];
            }

            _array[index + len] = default;

            index -= Offset + 1;
        }
        --Length;
        return valByVal;
    }

    /// <summary>
    /// This is an unsafe reference and can change whenever you write to this array.
    /// </summary>
    public ref T Insert(int index, T value)
    {
        _array ??= Empty();
        index += Offset;
        if (index < _array.Length)
        {
            if (index == Length)
            {
                ref T byRef = ref _array[index];
                byRef = value;
                ++Length;
                return ref byRef;
            }

            int len;
            if (Offset > 0 && index - Offset < Length / 2)
            {
                len = index - Offset + 1;
                for (int i = 0; i < len; ++i)
                    _array[Offset + i - 1] = _array[Offset + i];
                ref T byRef = ref _array[len];
                byRef = value;
                ++Length;
                return ref byRef;
            }

            len = Length + Offset - index;
            for (int i = len; i >= 0; --i)
                _array[i + index + 1] = _array[i + index];
            ref T byRef2 = ref _array[index];
            byRef2 = value;
            ++Length;
            return ref byRef2;
        }

        T[] oldArray = _array;

        int newLength = Math.Max(5, Length + 1);
        if (newLength > _array.Length)
            _array = new T[(int)Math.Ceiling(newLength * 1.5)];

        for (int i = 0; i < Length; ++i)
        {
            int toIndex = i;
            if (i + Offset >= index)
                ++toIndex;
            _array[toIndex] = oldArray[i + Offset];
        }

        ref T val = ref _array[index];
        val = value;

        ++Length;
        Offset = 0;
        return ref val;
    }

    private static T[] Empty()
    {
        if (_empty != null)
            return _empty;

        // ReSharper disable once UseArrayEmptyMethod
#pragma warning disable IDE0300 // Simplify collection initialization
        _empty = new T[0];
#pragma warning restore IDE0300 // Simplify collection initialization

        return _empty;
    }

    public IEnumerator<T> GetEnumerator() => Array.Skip(Offset).Take(Length).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
