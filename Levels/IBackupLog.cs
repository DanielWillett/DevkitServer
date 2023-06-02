#if SERVER
namespace DevkitServer.Levels;
public interface IBackupLog
{
    string RelativeName { get; }
    void Write(TextWriter fileWriter);
}
#endif