using System;
using UnityEngine;

// Shared boss target for HUD and room generation.
public static class RunBossTarget
{
    public static GameObject Prefab { get; private set; }
    public static Sprite Icon { get; private set; }
    public static string DisplayName { get; private set; }

    public static event Action Changed;

    public static void Set(GameObject bossPrefab)
    {
        Prefab = bossPrefab;
        Icon = ResolveIcon(bossPrefab);
        DisplayName = bossPrefab != null ? bossPrefab.name.Trim() : string.Empty;
        Changed?.Invoke();
    }

    public static void Clear()
    {
        Prefab = null;
        Icon = null;
        DisplayName = string.Empty;
        Changed?.Invoke();
    }

    static Sprite ResolveIcon(GameObject bossPrefab)
    {
        if (bossPrefab == null)
        {
            return null;
        }

        SpriteRenderer renderer = bossPrefab.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }
}
