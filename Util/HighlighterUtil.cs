#if CLIENT
using HighlightingSystem;
using SDG.Framework.Utilities;

namespace DevkitServer.Util;
public class HighlighterUtil
{
    public static void Highlight(Transform transform, Color color, float fade = 0.25f)
    {
        ThreadUtil.assertIsGameThread();

        Highlighter highlighter = transform.gameObject.GetOrAddComponent<Highlighter>();
        highlighter.ConstantOn(color, Mathf.Max(0, fade));
    }
    
    public static void Unhighlight(Transform transform, float fade = 0.25f)
    {
        ThreadUtil.assertIsGameThread();

        if (fade <= 0f)
            HighlighterTool.unhighlight(transform);
        else if (transform.gameObject.TryGetComponent(out Highlighter highlighter))
        {
            highlighter.ConstantOff(fade);
            TimeUtility.InvokeAfterDelay(() =>
            {
                if (transform.gameObject.TryGetComponent(out Highlighter highlighter) && !highlighter.constant)
                    Object.DestroyImmediate(highlighter);
            }, fade);
        }
    }
}
#endif