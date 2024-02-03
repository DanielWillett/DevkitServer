using System.Reflection;

namespace DevkitServer.API.Abstractions;
public interface IOptionalPatch
{
    MethodBase? Method { get; }
    MethodInfo? Patch { get; }
    bool Unpatch();
}
