using HighlightingSystem;

namespace DevkitServer.API.Devkit;
public interface IDevkitHighlightHandler
{
    void OnHighlight(Highlighter highlighter);
}
