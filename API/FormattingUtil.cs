using StackCleaner;
using System.Diagnostics;

namespace DevkitServer.API;

/// <summary>
/// Implement custom format handling for <see cref="FormattingUtil.Format(object?, string?)"/>.
/// </summary>
public interface ITerminalFormattable
{
    /// <summary>
    /// Format handling for <see cref="FormattingUtil.Format(object?, string?)"/>.
    /// </summary>
    string Format(ITerminalFormatProvider provider);
}

/// <summary>
/// Provides a <see cref="StackTraceCleaner"/> instance for formatting stuff in <see cref="FormattingUtil"/>.
/// </summary>
public interface ITerminalFormatProvider
{
    /// <summary>
    /// Provides a configuration for cleaning <see cref="StackTrace"/>s and formatting members.
    /// </summary>
    public StackTraceCleaner StackCleaner { get; }
}
