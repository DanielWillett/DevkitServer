namespace DevkitServer.Util.Encoding;

/// <summary>
/// Defines the way large amounts of binary data is formatted to a string. Not all flags can be combined.
/// </summary>
[Flags]
public enum BinaryStringFormat : long
{
    /// <summary>
    /// Do not log anything. Doesn't affect the behavior of <see cref="FormattingUtil.FormatBinary(ReadOnlySpan{byte},BinaryStringFormat)"/>
    /// </summary>
    NoLogging,

    /// <summary>
    /// Add a new line before the first byte.
    /// </summary>
    NewLineAtBeginning = 1 << 0,

    /// <summary>
    /// Just log the amount of bytes as a number of bytes.
    /// </summary>
    ByteCountAbsolute = 1 << 1,

    /// <summary>
    /// Just log the amount of bytes as a measurement formatted to the nearest unit.
    /// </summary>
    ByteCountUnits = 1 << 2,

    /// <summary>
    /// Hexadecimal (base 16) logging.
    /// </summary>
    Base16 = 1 << 3,

    /// <summary>
    /// Decimal (base 10) logging.
    /// </summary>
    Base10 = 1 << 4,

    /// <summary>
    /// Binary (base 2) logging.
    /// </summary>
    Base2 = 1 << 5,

    /// <summary>
    /// Log with 8 columns.
    /// </summary>
    Columns8 = 1 << 6,

    /// <summary>
    /// Log with 16 columns.
    /// </summary>
    Columns16 = 1 << 7,

    /// <summary>
    /// Log with 32 columns.
    /// </summary>
    Columns32 = 1 << 8,

    /// <summary>
    /// Log with 64 columns. Default if no others are specified.
    /// </summary>
    Columns64 = 1 << 9,

    /// <summary>
    /// Only log the first 8 bytes. Will also append the last X bytes if that flag is specified.
    /// </summary>
    First8 = 1 << 10,

    /// <summary>
    /// Only log the first 16 bytes. Will also append the last X bytes if that flag is specified.
    /// </summary>
    First16 = 1 << 11,

    /// <summary>
    /// Only log the first 32 bytes. Will also append the last X bytes if that flag is specified.
    /// </summary>
    First32 = 1 << 12,

    /// <summary>
    /// Only log the first 64 bytes. Will also append the last X bytes if that flag is specified.
    /// </summary>
    First64 = 1 << 13,

    /// <summary>
    /// Only log the first 128 bytes. Will also append the last X bytes if that flag is specified.
    /// </summary>
    First128 = 1 << 14,

    /// <summary>
    /// Only log the last 8 bytes. Will also prepend the first X bytes if that flag is specified.
    /// </summary>
    Last8 = 1 << 15,

    /// <summary>
    /// Only log the last 16 bytes. Will also prepend the first X bytes if that flag is specified.
    /// </summary>
    Last16 = 1 << 16,

    /// <summary>
    /// Only log the last 32 bytes. Will also prepend the first X bytes if that flag is specified.
    /// </summary>
    Last32 = 1 << 17,

    /// <summary>
    /// Only log the last 64 bytes. Will also prepend the first X bytes if that flag is specified.
    /// </summary>
    Last64 = 1 << 18,

    /// <summary>
    /// Only log the last 128 bytes. Will also prepend the first X bytes if that flag is specified.
    /// </summary>
    Last128 = 1 << 19,

    /// <summary>
    /// Add labels above each column.
    /// </summary>
    ColumnLabels = 1 << 20,

    /// <summary>
    /// Add offset labels next to each row.
    /// </summary>
    RowLabels = 1 << 21
}
