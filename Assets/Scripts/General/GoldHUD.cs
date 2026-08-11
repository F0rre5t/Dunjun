using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(115)]
public class GoldHUD : MonoBehaviour
{
    [SerializeField] Canvas targetCanvas;
    [SerializeField] Sprite coinIcon;
    [SerializeField] Vector2 anchoredPosition = new Vector2(-24f, -24f);
    [SerializeField] int fontSize = 40;
    [SerializeField] int timerFontSize = 34;
    [SerializeField] int depthLabelFontSize = 34;
    [SerializeField] int depthNumberFontSize = 44;
    [SerializeField] float iconTextSpacing = 4f;
    [SerializeField] float bossIconSize = 52f;
    [SerializeField] float bossCoinSpacing = 12f;
    [SerializeField] float timerBossSpacing = 12f;
    [SerializeField] float depthTimerSpacing = 12f;
    [SerializeField] Color timerColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] Color depthLabelColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] Color depthNumberColor = new Color(1f, 0.22f, 0.22f, 1f);

    Text amountText;
    Text timerText;
    Text depthLabelText;
    Text depthNumberText;
    Image iconImage;
    Image bossIconImage;
    LayoutElement bossIconLayout;
    LayoutElement bossSpacerLayout;
    Font uiFont;
    GameObject hudRoot;
    Transform cachedDepthTarget;
    Room cachedDepthRoom;
    int cachedDepth = int.MinValue;

    public GameObject HudRoot => hudRoot;

    void OnEnable()
    {
        GoldInventory.GoldChanged += OnGoldChanged;
        RunBossTarget.Changed += RefreshBossIcon;
    }

    void OnDisable()
    {
        GoldInventory.GoldChanged -= OnGoldChanged;
        RunBossTarget.Changed -= RefreshBossIcon;
    }

    void Start()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindAnyObjectByType<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning("GoldHUD: Canvas not found.");
            return;
        }

        uiFont = UiFonts.Get();

        BuildHud();
        Refresh(GoldInventory.Amount);
        RefreshBossIcon();
        RefreshTimer();
        RefreshDepth();

        // Keep HUD hidden on menu / difficulty select; GameFlowController shows it in gameplay.
        bool gameplayActive = GameFlowController.Instance != null && GameFlowController.Instance.IsGameplayActive;
        SetVisible(gameplayActive);
    }

    public void SetVisible(bool visible)
    {
        if (hudRoot != null)
        {
            hudRoot.SetActive(visible);
        }
    }

    void Update()
    {
        RefreshTimer();
        RefreshDepth();
    }

    void OnGoldChanged(int amount)
    {
        Refresh(amount);
    }

    void Refresh(int amount)
    {
        if (amountText != null)
        {
            amountText.text = amount.ToString();
        }
    }

    void RefreshTimer()
    {
        if (timerText == null)
        {
            return;
        }

        timerText.text = RunStats.FormatElapsed();
    }

    void RefreshDepth()
    {
        if (depthNumberText == null)
        {
            return;
        }

        Transform target = CameraController.instance != null ? CameraController.instance.target : null;
        if (target != cachedDepthTarget)
        {
            cachedDepthTarget = target;
            cachedDepthRoom = cachedDepthTarget != null ? cachedDepthTarget.GetComponent<Room>() : null;
            cachedDepth = int.MinValue;
        }

        int depth = cachedDepthRoom != null ? cachedDepthRoom.stepToStart : 0;
        if (depth == cachedDepth)
        {
            return;
        }

        cachedDepth = depth;
        depthNumberText.text = depth.ToString();
    }

    void RefreshBossIcon()
    {
        if (bossIconImage == null)
        {
            return;
        }

        Sprite icon = RunBossTarget.Icon;
        bool hasBoss = icon != null;
        bossIconImage.sprite = icon;
        bossIconImage.enabled = hasBoss;
        bossIconImage.color = Color.white;

        if (bossIconLayout != null)
        {
            float size = hasBoss ? bossIconSize : 0f;
            bossIconLayout.minWidth = size;
            bossIconLayout.minHeight = size;
            bossIconLayout.preferredWidth = size;
            bossIconLayout.preferredHeight = size;
            bossIconLayout.ignoreLayout = !hasBoss;
        }

        if (bossSpacerLayout != null)
        {
            float space = hasBoss ? bossCoinSpacing : 0f;
            bossSpacerLayout.minWidth = space;
            bossSpacerLayout.preferredWidth = space;
            bossSpacerLayout.ignoreLayout = !hasBoss;
        }
    }

    void BuildHud()
    {
        if (hudRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("GoldHUD", typeof(RectTransform));
        root.transform.SetParent(targetCanvas.transform, false);
        hudRoot = root;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(560f, 48f);

        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = iconTextSpacing;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Left to right: depth label, depth value, timer, boss, gold.
        GameObject depthLabelObject = new GameObject("DepthLabel", typeof(RectTransform), typeof(Text));
        depthLabelObject.transform.SetParent(root.transform, false);
        depthLabelText = depthLabelObject.GetComponent<Text>();
        depthLabelText.font = uiFont;
        depthLabelText.fontSize = depthLabelFontSize;
        depthLabelText.fontStyle = FontStyle.Bold;
        depthLabelText.color = depthLabelColor;
        depthLabelText.alignment = TextAnchor.MiddleRight;
        depthLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        depthLabelText.verticalOverflow = VerticalWrapMode.Overflow;
        depthLabelText.raycastTarget = false;
        depthLabelText.text = "Depth:";

        Outline depthLabelOutline = depthLabelObject.AddComponent<Outline>();
        depthLabelOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        depthLabelOutline.effectDistance = new Vector2(1.2f, -1.2f);

        LayoutElement depthLabelLayout = depthLabelObject.AddComponent<LayoutElement>();
        depthLabelLayout.minWidth = 12f;
        depthLabelLayout.preferredHeight = 48f;

        ContentSizeFitter depthLabelFitter = depthLabelObject.AddComponent<ContentSizeFitter>();
        depthLabelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        depthLabelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject depthNumberObject = new GameObject("DepthNumber", typeof(RectTransform), typeof(Text));
        depthNumberObject.transform.SetParent(root.transform, false);
        depthNumberText = depthNumberObject.GetComponent<Text>();
        depthNumberText.font = uiFont;
        depthNumberText.fontSize = depthNumberFontSize;
        depthNumberText.fontStyle = FontStyle.Bold;
        depthNumberText.color = depthNumberColor;
        depthNumberText.alignment = TextAnchor.MiddleRight;
        depthNumberText.horizontalOverflow = HorizontalWrapMode.Overflow;
        depthNumberText.verticalOverflow = VerticalWrapMode.Overflow;
        depthNumberText.raycastTarget = false;
        depthNumberText.text = "0";

        Outline depthNumberOutline = depthNumberObject.AddComponent<Outline>();
        depthNumberOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        depthNumberOutline.effectDistance = new Vector2(1.2f, -1.2f);

        LayoutElement depthNumberLayout = depthNumberObject.AddComponent<LayoutElement>();
        depthNumberLayout.minWidth = 12f;
        depthNumberLayout.preferredHeight = 48f;

        ContentSizeFitter depthNumberFitter = depthNumberObject.AddComponent<ContentSizeFitter>();
        depthNumberFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        depthNumberFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject depthSpacerObject = new GameObject("DepthSpacer", typeof(RectTransform));
        depthSpacerObject.transform.SetParent(root.transform, false);
        LayoutElement depthSpacerLayout = depthSpacerObject.AddComponent<LayoutElement>();
        depthSpacerLayout.minWidth = depthTimerSpacing;
        depthSpacerLayout.preferredWidth = depthTimerSpacing;
        depthSpacerLayout.minHeight = 1f;

        GameObject timerObject = new GameObject("RunTimer", typeof(RectTransform), typeof(Text));
        timerObject.transform.SetParent(root.transform, false);
        timerText = timerObject.GetComponent<Text>();
        timerText.font = uiFont;
        timerText.fontSize = timerFontSize;
        timerText.fontStyle = FontStyle.Bold;
        timerText.color = timerColor;
        timerText.alignment = TextAnchor.MiddleRight;
        timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        timerText.verticalOverflow = VerticalWrapMode.Overflow;
        timerText.raycastTarget = false;
        timerText.text = "00:00:00";

        Outline timerOutline = timerObject.AddComponent<Outline>();
        timerOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        timerOutline.effectDistance = new Vector2(1.2f, -1.2f);

        LayoutElement timerLayout = timerObject.AddComponent<LayoutElement>();
        timerLayout.minWidth = 12f;
        timerLayout.preferredHeight = 48f;

        ContentSizeFitter timerFitter = timerObject.AddComponent<ContentSizeFitter>();
        timerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        timerFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject timerSpacerObject = new GameObject("TimerSpacer", typeof(RectTransform));
        timerSpacerObject.transform.SetParent(root.transform, false);
        LayoutElement timerSpacerLayout = timerSpacerObject.AddComponent<LayoutElement>();
        timerSpacerLayout.minWidth = timerBossSpacing;
        timerSpacerLayout.preferredWidth = timerBossSpacing;
        timerSpacerLayout.minHeight = 1f;

        GameObject bossObject = new GameObject("BossIcon", typeof(RectTransform), typeof(Image));
        bossObject.transform.SetParent(root.transform, false);
        bossIconImage = bossObject.GetComponent<Image>();
        bossIconImage.raycastTarget = false;
        bossIconImage.preserveAspect = true;
        bossIconImage.enabled = false;

        bossIconLayout = bossObject.AddComponent<LayoutElement>();
        bossIconLayout.minWidth = 0f;
        bossIconLayout.minHeight = 0f;
        bossIconLayout.preferredWidth = 0f;
        bossIconLayout.preferredHeight = 0f;
        bossIconLayout.ignoreLayout = true;

        GameObject spacerObject = new GameObject("BossSpacer", typeof(RectTransform));
        spacerObject.transform.SetParent(root.transform, false);
        bossSpacerLayout = spacerObject.AddComponent<LayoutElement>();
        bossSpacerLayout.minWidth = 0f;
        bossSpacerLayout.preferredWidth = 0f;
        bossSpacerLayout.minHeight = 1f;
        bossSpacerLayout.ignoreLayout = true;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        iconImage = iconObject.GetComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
        if (coinIcon != null)
        {
            iconImage.sprite = coinIcon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.color = new Color(1f, 0.85f, 0.2f, 1f);
        }

        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.minWidth = 48f;
        iconLayout.minHeight = 48f;
        iconLayout.preferredWidth = 48f;
        iconLayout.preferredHeight = 48f;

        GameObject textObject = new GameObject("Amount", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(root.transform, false);
        amountText = textObject.GetComponent<Text>();
        amountText.font = uiFont;
        amountText.fontSize = fontSize;
        amountText.fontStyle = FontStyle.Bold;
        amountText.color = new Color(1f, 0.92f, 0.55f, 1f);
        amountText.alignment = TextAnchor.MiddleLeft;
        amountText.horizontalOverflow = HorizontalWrapMode.Overflow;
        amountText.verticalOverflow = VerticalWrapMode.Overflow;
        amountText.raycastTarget = false;

        LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
        textLayout.minWidth = 12f;
        textLayout.preferredHeight = 48f;

        ContentSizeFitter textFitter = textObject.AddComponent<ContentSizeFitter>();
        textFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        textFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }
}
