using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(110)]
public class RelicBarUI : MonoBehaviour
{
    [SerializeField] Transform iconRoot;
    [SerializeField] GameObject relicIconPrefab;
    [SerializeField] float iconSize = 48f;
    [SerializeField] float relicStartPadding = 10f;

    readonly List<GameObject> spawnedIcons = new List<GameObject>();

    void Awake()
    {
        EnsureIconRoot();
    }

    void OnEnable()
    {
        RelicInventory.RelicAdded += OnRelicAdded;
        RelicInventory.RelicCleared += OnRelicCleared;
    }

    void OnDisable()
    {
        RelicInventory.RelicAdded -= OnRelicAdded;
        RelicInventory.RelicCleared -= OnRelicCleared;
    }

    void Start()
    {
        RebuildFromInventory();
    }

    void OnRelicAdded(RelicData relic)
    {
        if (relic == null)
        {
            return;
        }

        SpawnIcon(relic.hudIcon);
    }

    void OnRelicCleared()
    {
        ClearIcons();
    }

    void RebuildFromInventory()
    {
        ClearIcons();

        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        for (int i = 0; i < relics.Count; i++)
        {
            SpawnIcon(relics[i].hudIcon);
        }
    }

    void SpawnIcon(Sprite sprite)
    {
        if (relicIconPrefab == null || sprite == null)
        {
            return;
        }

        Transform root = GetIconRoot();
        GameObject iconObject = Instantiate(relicIconPrefab, root);
        spawnedIcons.Add(iconObject);

        Image image = iconObject.GetComponent<Image>();
        if (image == null)
        {
            image = iconObject.GetComponentInChildren<Image>();
        }

        if (image != null)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
        }

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        HealthManager.ConfigureHudIcon(iconObject, iconSize);
        HealthManager.RefreshHudBarLayout(transform);
    }

    void ClearIcons()
    {
        for (int i = 0; i < spawnedIcons.Count; i++)
        {
            if (spawnedIcons[i] != null)
            {
                Destroy(spawnedIcons[i]);
            }
        }

        spawnedIcons.Clear();
    }

    Transform GetIconRoot()
    {
        EnsureIconRoot();
        return iconRoot != null ? iconRoot : transform;
    }

    void EnsureIconRoot()
    {
        if (iconRoot != null)
        {
            return;
        }

        int leftPadding = relicStartPadding > 0f ? Mathf.RoundToInt(relicStartPadding) : 0;
        iconRoot = HealthManager.GetOrCreateHudContainer(transform, "RelicRoot", 1, leftPadding);
    }
}
