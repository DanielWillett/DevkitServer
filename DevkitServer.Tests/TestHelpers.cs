using DevkitServer.API;
using StackCleaner;
using System.Reflection;
using System.Threading;

namespace DevkitServer.Tests;
internal class TestHelpers
{
    public static void SetupMainThread()
    {
        typeof(ThreadUtil).GetProperty("gameThread", BindingFlags.Static | BindingFlags.Public)!
            .GetSetMethod(true)!.Invoke(null, [ Thread.CurrentThread ]);
    }
    public static void SetupFormatProvider()
    {
        StackTraceCleaner cleaner = new StackTraceCleaner(new StackCleanerConfiguration
        {
            ColorFormatting = StackColorFormatType.None,
            Colors = Color4Config.Default
        });
        FormattingUtil.FormatProvider = new TestFormatProvider(cleaner);
    }
}

file class TestFormatProvider(StackTraceCleaner cleaner) : ITerminalFormatProvider
{
    public StackTraceCleaner StackCleaner { get; } = cleaner;
}