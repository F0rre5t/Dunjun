using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(120)]
public class RelicInspectUI : MonoBehaviour
{
    [SerializeField] KeyCode toggleKey = KeyCode.Q;
    [SerializeField] Canvas targetCanvas;
    [SerializeField] float toggleCooldown = 0.2f;

    GameObject panelRoot;
    GameObject emptyStateRoot;
    GameObject scrollRoot;
    Transform listContent;
    Text titleText;
    bool isOpen;
    float nextToggleTime;

    readonly List<GameObject> entryObjects = new List<GameObject>();
    Font uiFont;

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

    void Awake()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindAnyObjectByType<Canvas>();
        }
    }

    void Start()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("RelicInspectUI: Canvas not found.");
            return;
        }

        uiFont = UiFonts.Get();

        BuildPanel();
        SetPanelVisible(false);
    }

    void Update()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (Time.unscaledTime < nextToggleTime)
        {
            return;
        }

        if (!WasToggleKeyPressed())
        {
            return;
        }

        if (!isOpen && !CanOpenDuringGameplay())
        {
            return;
        }

        SetPanelVisible(!isOpen);
        nextToggleTime = Time.unscaledTime + toggleCooldown;
    }

    bool WasToggleKeyPressed()
    {
        // Project uses Active Input Handling = Both. Mixing new + old APIs (or
        // relying on wasPressedThisFrame alone) can open and close in consecutive
        // frames. Legacy GetKeyDown is stable for a single edge here.
        return Input.GetKeyDown(toggleKey);
    }

    void OnRelicAdded(RelicData relic)
    {
        if (!isOpen)
        {
            return;
        }

        RefreshList();
    }

    void OnRelicCleared()
    {
        if (!isOpen)
        {
            return;
        }

        RefreshList();
    }

    bool CanOpenDuringGameplay()
    {
        GameFlowController flow = GameFlowController.Instance;
        if (flow != null && flow.IsGameplayActive)
        {
            return true;
        }

        RoomGenerator generator = FindAnyObjectByType<RoomGenerator>();
        return generator != null && generator.rooms.Count > 0;
    }

    void SetPanelVisible(bool visible)
    {
        isOpen = visible;
        panelRoot.SetActive(visible);

        if (visible)
        {
            RefreshList();
        }
    }

    void RefreshList()
    {
        ClearEntries();

        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        bool hasRelics = relics.Count > 0;

        if (emptyStateRoot != null)
        {
            emptyStateRoot.SetActive(!hasRelics);
        }

        if (scrollRoot != null)
        {
            scrollRoot.SetActive(hasRelics);
        }

        if (!hasRelics)
        {
            return;
        }

        for (int i = 0; i < relics.Count; i++)
        {
            CreateEntry(relics[i], i + 1);
        }
    }

    void ClearEntries()
    {
        for (int i = 0; i < entryObjects.Count; i++)
        {
            if (entryObjects[i] != null)
            {
                Destroy(entryObjects[i]);
            }
        }

        entryObjects.Clear();
    }

    void CreateEntry(RelicData relic, int order)
    {
        if (relic == null || listContent == null)
        {
            return;
        }

        GameObject entry = new GameObject($"RelicEntry_{order}", typeof(RectTransform));
        entry.transform.SetParent(listContent, false);
        entryObjects.Add(entry);

        LayoutElement entryLayout = entry.AddComponent<LayoutElement>();
        entryLayout.flexibleWidth = 1f;
        entryLayout.minWidth = 0f;

        HorizontalLayoutGroup layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Image icon = CreateImage(entry.transform, "Icon", new Vector2(48f, 48f), new Color(1f, 1f, 1f, 0.15f));
        icon.sprite = relic.hudIcon;
        icon.preserveAspect = true;
        icon.color = Color.white;

        GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform));
        textColumn.transform.SetParent(entry.transform, false);
        LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
        textColumnLayout.flexibleWidth = 1f;
        textColumnLayout.minWidth = 0f;

        VerticalLayoutGroup textLayout = textColumn.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 4f;
        textLayout.childAlignment = TextAnchor.UpperLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;

        ContentSizeFitter textColumnFitter = textColumn.AddComponent<ContentSizeFitter>();
        textColumnFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        textColumnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ContentSizeFitter entryFitter = entry.AddComponent<ContentSizeFitter>();
        entryFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        entryFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText(textColumn.transform, "Name", $"{order}. {relic.GetDisplayName()}", 22, FontStyle.Bold, new Color(1f, 0.92f, 0.72f), wrapText: false);
        CreateText(textColumn.transform, "Effects", relic.GetEffectSummary(), 18, FontStyle.Normal, new Color(0.88f, 0.88f, 0.88f), wrapText: true);
    }

    void BuildPanel()
    {
        panelRoot = new GameObject("RelicInspectPanel", typeof(RectTransform));
        panelRoot.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image backdrop = panelRoot.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.45f);
        backdrop.raycastTarget = true;

        GameObject window = CreatePanel(windowName: "Window", parent: panelRoot.transform, size: new Vector2(540f, 580f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0f, 0.5f);
        windowRect.anchorMax = new Vector2(0f, 0.5f);
        windowRect.pivot = new Vector2(0f, 0.5f);
        windowRect.anchoredPosition = new Vector2(20f, 0f);

        Image windowBg = window.GetComponent<Image>();
        windowBg.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);

        VerticalLayoutGroup windowLayout = window.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(18, 18, 18, 18);
        windowLayout.spacing = 12f;
        windowLayout.childAlignment = TextAnchor.UpperLeft;
        windowLayout.childControlWidth = true;
        windowLayout.childControlHeight = true;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        titleText = CreateText(window.transform, "Title", "Relics", 28, FontStyle.Bold, new Color(1f, 0.92f, 0.72f));
        CreateText(window.transform, "Hint", "Press Q to close", 16, FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f));

        scrollRoot = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(window.transform, false);
        LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
        scrollLayout.minHeight = 360f;
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        Image scrollBg = scrollRoot.GetComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.25f);

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        Scrollbar verticalScrollbar = CreateVerticalScrollbar(scrollRoot.transform);
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = 4f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        StretchRect(viewportRect);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 8);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        listContent = content.transform;

        emptyStateRoot = CreateText(window.transform, "EmptyState", "No relics yet", 20, FontStyle.Italic, new Color(0.75f, 0.75f, 0.75f)).gameObject;
    }

    GameObject CreatePanel(string windowName, Transform parent, Vector2 size)
    {
        GameObject panel = new GameObject(windowName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return panel;
    }

    Text CreateText(Transform parent, string objectName, string value, int fontSize, FontStyle style, Color color, bool wrapText = true)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = wrapText ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.lineSpacing = 1.15f;
        text.alignByGeometry = false;
        text.raycastTarget = false;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.minHeight = fontSize + 12;
        layout.flexibleWidth = 1f;

        if (wrapText)
        {
            ContentSizeFitter fitter = textObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        return text;
    }

    Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        GameObject scrollbarRoot = new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarRoot.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarRoot.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(14f, 0f);
        scrollbarRect.anchoredPosition = Vector2.zero;

        Image trackImage = scrollbarRoot.GetComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.35f);

        Scrollbar scrollbar = scrollbarRoot.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarRoot.transform, false);
        RectTransform slidingAreaRect = slidingArea.GetComponent<RectTransform>();
        StretchRect(slidingAreaRect);
        slidingAreaRect.offsetMin = new Vector2(2f, 6f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -6f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slidingArea.transform, false);

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.82f, 0.74f, 0.52f, 0.95f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        return scrollbar;
    }

    Image CreateImage(Transform parent, string objectName, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform iconRect = imageObject.GetComponent<RectTransform>();
        iconRect.sizeDelta = size;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        LayoutElement layout = imageObject.AddComponent<LayoutElement>();
        layout.minWidth = size.x;
        layout.minHeight = size.y;
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        return image;
    }

    static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
