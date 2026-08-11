using UnityEngine;

/// <summary>
/// Shared UI font with CJK coverage for editor + WebGL builds.
/// </summary>
public static class UiFonts
{
    static Font cached;

    public static Font Get()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = Resources.Load<Font>("Fonts/SimHei");
        if (cached != null)
        {
            return cached;
        }

        cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cached == null)
        {
            cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return cached;
    }
}
