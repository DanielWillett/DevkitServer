using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.API;
public interface IDevkitServerPlugin<out TConfig> : IDevkitServerPlugin, IConfigProvider<TConfig> where TConfig : class, new() { }
public interface IDevkitServerPlugin
{
    string Name { get; }
    Assembly Assembly { get; }
    void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray);
    void LogInfo(string message, ConsoleColor color = ConsoleColor.Gray);
    void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow);
    void LogError(string message, ConsoleColor color = ConsoleColor.Red);
    void LogError(Exception ex);
    void Load();
    void Unload();
}
