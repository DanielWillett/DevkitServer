using DanielWillett.ReflectionTools;
using System.Reflection;

namespace DevkitServer.Core.Cartography;

/// <summary>
/// Contains helpers for writing cartography providers.
/// </summary>
public static class CartographyHelper
{
    private static readonly Action? CallListen = Accessor.GenerateStaticCaller<Action>(
        typeof(Provider).GetMethod("listen", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!,
        throwOnError: false);

#if CLIENT
    /// <summary>
    /// Keeps the client alive when called from the main thread, dequeuing any incomming messages and sending ping requests.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure.</returns>
    public static bool KeepClientAlive()
    {
        if (CallListen == null)
            return false;

        try
        {
            CallListen();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning("Provider.listen", ex, "An error was thrown listening for incoming messages.");
        }
        return true;
    }
#endif
}
