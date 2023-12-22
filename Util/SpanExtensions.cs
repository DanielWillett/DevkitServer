namespace DevkitServer.Util;
public static class SpanExtensions
{
    public static int IndexOf<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value, int startIndex) where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        if (index < 0)
            return -1;
        return index + startIndex;
    }
    public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex) where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        if (index < 0)
            return -1;
        return index + startIndex;
    }
    public static int Count<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value) where T : IEquatable<T>
    {
        int amt = 0;
        int lastIndex = -value.Length;
        while ((lastIndex = span.IndexOf(value, lastIndex + value.Length)) >= 0)
        {
            ++amt;
            if (lastIndex + value.Length >= span.Length)
                break;
        }

        return amt;
    }
}
