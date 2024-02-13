namespace DevkitServer.Util;
internal struct FastList<T> : IEnumerable<T> where T : struct
{
    private T[]? _array;
    private static T[]? _empty;
    public FastList() : this(8) { }
    public FastList(int capacity)
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
    public ref T Add(in T value)
    {
        return ref Insert(Length, value);
    }
    public void Clear()
    {
        Offset = 0;
        Length = 0;
    }
    public void RemoveAt(int index) => RemoveAt(ref index);
    public void RemoveAt(ref int index)
    {
        if (index < 0 || _array == null)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (index == Length - 1)
        {
            ref T val = ref _array[index + Offset];
            val = default;
            --Length;
            --index;
            return;
        }

        index += Offset;
        if (index >= _array.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _array ??= Empty();
        ref T val2 = ref _array[index];
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
    }

    /// <summary>
    /// This is an unsafe reference and can change whenever you write to this array.
    /// </summary>
    public ref T Insert(int index, in T value)
    {
        _array ??= Empty();

        if (index > Length || index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        index += Offset;
        if (Length + 1 <= _array.Length)
        {
            // last element and there's room at the end of the buffer
            if (index == Length)
            {
                ref T byRef = ref _array[index];
                byRef = value;
                ++Length;
                return ref byRef;
            }

            int len;
            // can shift buffer left, so shift the segment [0, index] left and insert at index
            if (Offset > 0 && index - Offset < Length / 2)
            {
                len = index - Offset + 1;
                for (int i = 0; i < len; ++i)
                    _array[Offset + i - 1] = _array[Offset + i];
                ref T byRef = ref _array[len];
                byRef = value;
                ++Length;
                --Offset;
                return ref byRef;
            }

            // can shift buffer right, so shift the segment (index, length) to the right and insert at index
            len = Length - (index - Offset);
            for (int i = len - 2; i >= 0; --i)
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