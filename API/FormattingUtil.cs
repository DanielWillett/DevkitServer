using StackCleaner;

namespace DevkitServer.API;
public interface ITerminalFormattable
{
    string Format(ITerminalFormatProvider provider);
}
public interface ITerminalFormatProvider
{
    public StackTraceCleaner StackCleaner { get; }
}
