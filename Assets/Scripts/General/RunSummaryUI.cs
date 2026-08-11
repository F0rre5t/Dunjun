using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared victory / game-over summary on a solid black fullscreen overlay.
/// </summary>
[DefaultExecutionOrder(120)]
public class RunSummaryUI : MonoBehaviour
{
    public static RunSummaryUI Instance { get; private set; }

    [SerializeField] Canvas targetCanvas;
    [SerializeField] GameObject relicIconPrefab;
    [SerializeField] int titleFontSize = 56;
    [SerializeField] int bodyFontSize = 34;
    [SerializeField] float iconSize = 48f;
    [SerializeField] int overlaySortOrder = 500;

    Canvas overlayCanvas;
    GameObject panelRoot;
    Text titleText;
    Text timeText;
    Text damageText;
    Transform relicRoot;
    Transform killRoot;
    Font uiFont;
    Sprite whiteSprite;
    GameObject[] hidHudRoots;
    bool[] hidHudWasActive;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        EnsureBuilt();
        Hide();
    }

    public void Show(bool victory)
    {
        EnsureBuilt();
        if (panelRoot == null)
        {
            return;
        }

        ClearDynamic();
        titleText.text = victory ? "VICTORY" : "GAME OVER";
        titleText.color = victory
            ? new Color(1f, 0.92f, 0.45f, 1f)
            : new Color(1f, 0.45f, 0.45f, 1f);

        timeText.text = $"Time  {RunStats.FormatElapsed()}";
        damageText.text = $"Damage Taken  {RunStats.DamageTaken}";

        PopulateRelics();
        PopulateKills();

        SetGameplayHudVisible(false);
        panelRoot.SetActive(true);
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = true;
            overlayCanvas.sortingOrder = overlaySortOrder;
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }

        RestoreGameplayHud();
    }

    void EnsureBuilt()
    {
        if (panelRoot != null)
        {
            return;
        }

        uiFont = UiFonts.Get();

        whiteSprite = CreateWhiteSprite();
        BuildPanel();
    }

    void BuildPanel()
    {
        GameObject canvasObj = new GameObject(
            "RunSummaryCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        overlayCanvas = canvasObj.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = overlaySortOrder;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot = new GameObject("RunSummaryOverlay", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvasObj.transform, false);

        RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
        StretchFull(rootRect);

        Image dim = panelRoot.GetComponent<Image>();
        dim.sprite = whiteSprite;
        dim.type = Image.Type.Simple;
        dim.color = Color.black;
        dim.raycastTarget = true;

        // Content column centered
        GameObject content = CreateUiObject("Content", panelRoot.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = new Vector2(0f, 40f);
        contentRect.sizeDelta = new Vector2(780f, 520f);

        VerticalLayoutGroup v = content.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 12, 12);
        v.spacing = 14f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        titleText = CreateLabel(content.transform, "Title", titleFontSize, TextAnchor.MiddleCenter);
        timeText = CreateLabel(content.transform, "Time", bodyFontSize, TextAnchor.MiddleCenter);
        damageText = CreateLabel(content.transform, "Damage", bodyFontSize, TextAnchor.MiddleCenter);

        CreateLabel(content.transform, "RelicsHeader", 28, TextAnchor.MiddleLeft).text = "Relics";
        relicRoot = CreateRow(content.transform, "RelicRow");

        CreateLabel(content.transform, "KillsHeader", 28, TextAnchor.MiddleLeft).text = "Kills";
        killRoot = CreateRow(content.transform, "KillRow");
        HorizontalLayoutGroup killRowLayout = killRoot.GetComponent<HorizontalLayoutGroup>();
        if (killRowLayout != null)
        {
            killRowLayout.spacing = 28f;
        }

        // Restart pinned to bottom of the black screen
        GameObject buttonObj = CreateUiObject("RestartButton", panelRoot.transform);
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 48f);
        buttonRect.sizeDelta = new Vector2(240f, 64f);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.sprite = whiteSprite;
        buttonImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        Button restartButton = buttonObj.AddComponent<Button>();
        restartButton.targetGraphic = buttonImage;
        restartButton.onClick.AddListener(OnRestartClicked);

        Text buttonLabel = CreateLabel(buttonObj.transform, "Label", 34, TextAnchor.MiddleCenter);
        buttonLabel.text = "Restart";
        buttonLabel.color = Color.black;
        StretchFull(buttonLabel.rectTransform);
        buttonLabel.raycastTarget = false;
        LayoutElement labelLayout = buttonLabel.GetComponent<LayoutElement>();
        if (labelLayout != null)
        {
            Destroy(labelLayout);
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        if (!visible)
        {
            CacheHudRoots();
            if (hidHudRoots == null)
            {
                return;
            }

            hidHudWasActive = new bool[hidHudRoots.Length];
            for (int i = 0; i < hidHudRoots.Length; i++)
            {
                if (hidHudRoots[i] == null)
                {
                    continue;
                }

                hidHudWasActive[i] = hidHudRoots[i].activeSelf;
                hidHudRoots[i].SetActive(false);
            }

            return;
        }

        RestoreGameplayHud();
    }

    void CacheHudRoots()
    {
        if (hidHudRoots != null)
        {
            return;
        }

        List<GameObject> roots = new List<GameObject>();
        if (targetCanvas == null)
        {
            targetCanvas = FindAnyObjectByType<Canvas>();
        }

        if (targetCanvas != null)
        {
            Transform t = targetCanvas.transform.Find("GoldHUD");
            if (t != null)
            {
                roots.Add(t.gameObject);
            }

            Transform health = targetCanvas.transform.Find("Health_Bar");
            if (health == null)
            {
                // health may be nested
                health = FindDeepChild(targetCanvas.transform, "Health_Bar");
            }

            if (health != null)
            {
                roots.Add(health.gameObject);
            }
        }

        hidHudRoots = roots.ToArray();
    }

    void RestoreGameplayHud()
    {
        if (hidHudRoots == null || hidHudWasActive == null)
        {
            return;
        }

        for (int i = 0; i < hidHudRoots.Length; i++)
        {
            if (hidHudRoots[i] != null)
            {
                hidHudRoots[i].SetActive(hidHudWasActive[i]);
            }
        }

        hidHudWasActive = null;
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    void PopulateRelics()
    {
        IReadOnlyList<Sprite> icons = RunStats.RelicIcons;
        if (icons.Count == 0)
        {
            Text empty = CreateLabel(relicRoot, "EmptyRelics", 24, TextAnchor.MiddleLeft);
            empty.text = "None";
            empty.color = new Color(1f, 1f, 1f, 0.55f);
            return;
        }

        for (int i = 0; i < icons.Count; i++)
        {
            CreateIcon(relicRoot, icons[i]);
        }
    }

    void PopulateKills()
    {
        IReadOnlyList<RunKillEntry> kills = RunStats.Kills;
        if (kills.Count == 0)
        {
            Text empty = CreateLabel(killRoot, "EmptyKills", 24, TextAnchor.MiddleLeft);
            empty.text = "None";
            empty.color = new Color(1f, 1f, 1f, 0.55f);
            return;
        }

        for (int i = 0; i < kills.Count; i++)
        {
            RunKillEntry entry = kills[i];
            GameObject row = CreateUiObject($"Kill_{entry.id}", killRoot);

            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            LayoutElement rowElement = row.AddComponent<LayoutElement>();
            rowElement.minHeight = iconSize;
            rowElement.preferredHeight = iconSize;

            ContentSizeFitter rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateIcon(row.transform, entry.icon);

            Text countLabel = CreateLabel(row.transform, "Count", 40, TextAnchor.MiddleLeft);
            countLabel.text = $"x {entry.count}";
            LayoutElement countLayout = countLabel.GetComponent<LayoutElement>();
            if (countLayout != null)
            {
                countLayout.minWidth = 56f;
                countLayout.preferredWidth = 72f;
            }
        }
    }

    GameObject CreateIcon(Transform parent, Sprite sprite)
    {
        if (relicIconPrefab != null)
        {
            GameObject iconObject = Instantiate(relicIconPrefab, parent);
            Image image = iconObject.GetComponent<Image>();
            if (image == null)
            {
                image = iconObject.GetComponentInChildren<Image>();
            }

            if (image != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
                image.enabled = sprite != null;
            }

            RectTransform rect = iconObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(iconSize, iconSize);
            }

            LayoutElement layout = iconObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = iconObject.AddComponent<LayoutElement>();
            }

            layout.minWidth = iconSize;
            layout.minHeight = iconSize;
            layout.preferredWidth = iconSize;
            layout.preferredHeight = iconSize;
            return iconObject;
        }

        GameObject fallback = CreateUiObject("Icon", parent);
        Image img = fallback.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : whiteSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);

        LayoutElement le = fallback.AddComponent<LayoutElement>();
        le.minWidth = iconSize;
        le.minHeight = iconSize;
        le.preferredWidth = iconSize;
        le.preferredHeight = iconSize;
        return fallback;
    }

    void ClearDynamic()
    {
        ClearChildren(relicRoot);
        ClearChildren(killRoot);
    }

    static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    void OnRestartClicked()
    {
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnRestartClicked();
        }
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    Text CreateLabel(Transform parent, string name, int size, TextAnchor align)
    {
        GameObject go = CreateUiObject(name, parent);
        Text text = go.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minHeight = size + 8f;
        layout.preferredHeight = size + 10f;
        return text;
    }

    Transform CreateRow(Transform parent, string name)
    {
        GameObject row = CreateUiObject(name, parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement element = row.AddComponent<LayoutElement>();
        element.minHeight = iconSize + 8f;
        element.preferredHeight = iconSize + 8f;
        return row.transform;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
