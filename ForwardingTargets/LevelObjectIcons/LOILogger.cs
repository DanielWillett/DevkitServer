using DevkitServer.API.Logging;
using DevkitServer.Core.Logging.Loggers;

namespace DanielWillett.LevelObjectIcons;

internal static class LOILogger
{
    public static IDevkitServerLogger Logger { get; } = new CoreLogger("LEVEL OBJ ICONS");
}
