using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    [Header("Difficulty Room Counts")]
    [Min(1)] public int easyRooms = 12;
    [Min(1)] public int mediumRooms = 15;
    [Min(1)] public int hardRooms = 20;

    [Header("References")]
    public RoomGenerator roomGenerator;
    public GameObject healthBar;
    public GoldHUD goldHud;
    public RunSummaryUI runSummaryUI;

    [Header("Overlays")]
    public GameObject menuOverlay;
    public GameObject difficultyOverlay;
    public GameObject gameOverOverlay;
    public GameObject victoryOverlay;

    [Header("Buttons")]
    public Button playButton;
    public Button easyButton;
    public Button mediumButton;
    public Button hardButton;
    public Button gameOverRestartButton;
    public Button victoryRestartButton;
    [Tooltip("Difficulty screen toggle for dynamic aid A/B tests")]
    public Toggle aidSystemToggle;
    public Text aidSystemToggleLabel;

    bool endingTriggered;
    DifficultyDirector difficultyDirector;

    public bool IsGameplayActive
    {
        get
        {
            if (endingTriggered)
            {
                return false;
            }

            if (runSummaryUI != null && runSummaryUI.IsVisible)
            {
                return false;
            }

            if (menuOverlay != null && menuOverlay.activeInHierarchy)
            {
                return false;
            }

            if (difficultyOverlay != null && difficultyOverlay.activeInHierarchy)
            {
                return false;
            }

            if (gameOverOverlay != null && gameOverOverlay.activeInHierarchy)
            {
                return false;
            }

            if (victoryOverlay != null && victoryOverlay.activeInHierarchy)
            {
                return false;
            }

            return true;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (Instance != this)
        {
            return;
        }

        ResolveReferences();
        difficultyDirector = DifficultyDirector.Ensure();
        EnsureAidSystemToggle();
        DisableTextRaycastsOnButtons();
        BindButtons();
        SyncAidToggleUi();

        if (UsesMenuFlow())
        {
            ValidateSetup();
            ShowMenu();
        }
        else
        {
            // No menu: dungeon already generates in RoomGenerator.Start
            difficultyDirector.BeginRun(DifficultyDirector.RunDifficulty.Normal);
            RunStats.Begin();
            ShowGameplay();
        }
    }

    bool UsesMenuFlow()
    {
        if (roomGenerator == null)
        {
            roomGenerator = FindAnyObjectByType<RoomGenerator>();
        }

        return roomGenerator == null || roomGenerator.useMenuFlow;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnPlayClicked()
    {
        if (endingTriggered)
        {
            return;
        }

        ShowDifficultySelect();
    }

    public void OnEasyClicked()
    {
        BeginGame(easyRooms, DifficultyDirector.RunDifficulty.Easy);
    }

    public void OnMediumClicked()
    {
        BeginGame(mediumRooms, DifficultyDirector.RunDifficulty.Normal);
    }

    public void OnHardClicked()
    {
        BeginGame(hardRooms, DifficultyDirector.RunDifficulty.Hard);
    }

    public void OnAidSystemToggleChanged(bool enabled)
    {
        DifficultyDirector.Ensure().SetSystemEnabled(enabled);
        SyncAidToggleUi();
    }

    public void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ShowGameOver()
    {
        ShowRunEnd(victory: false);
    }

    public void ShowVictory()
    {
        ShowRunEnd(victory: true);
    }

    void ShowRunEnd(bool victory)
    {
        if (endingTriggered)
        {
            return;
        }

        endingTriggered = true;
        RunStats.Stop();
        SetGameplayHudVisible(false);

        // Prefer the shared summary panel; keep legacy overlays as fallback.
        if (runSummaryUI == null)
        {
            runSummaryUI = FindAnyObjectByType<RunSummaryUI>();
        }

        if (runSummaryUI != null)
        {
            SetOverlay(null);
            runSummaryUI.Show(victory);
            return;
        }

        SetOverlay(victory ? victoryOverlay : gameOverOverlay);
    }

    void BeginGame(int roomCount, DifficultyDirector.RunDifficulty difficulty)
    {
        if (roomGenerator == null)
        {
            Debug.LogError("GameFlowController: RoomGenerator reference is missing.");
            return;
        }

        DifficultyDirector director = DifficultyDirector.Ensure();
        if (aidSystemToggle != null)
        {
            director.SetSystemEnabled(aidSystemToggle.isOn);
        }

        director.BeginRun(difficulty);
        // Show HUD before Generate so HealthManager/RelicBar can bind while active.
        SetGameplayHudVisible(true);
        RunStats.Begin();
        roomGenerator.Generate(roomCount);
        ShowGameplay();
    }

    void ShowMenu()
    {
        endingTriggered = false;
        RunStats.Reset();
        if (runSummaryUI != null)
        {
            runSummaryUI.Hide();
        }

        SetOverlay(menuOverlay);
        SetGameplayHudVisible(false);
    }

    void ShowDifficultySelect()
    {
        if (difficultyOverlay == null)
        {
            Debug.LogError("GameFlowController: DifficultyOverlay is missing.");
            return;
        }

        SetOverlay(difficultyOverlay);
    }

    void ShowGameplay()
    {
        if (runSummaryUI != null)
        {
            runSummaryUI.Hide();
        }

        SetOverlay(null);
        SetGameplayHudVisible(true);
    }

    void SetGameplayHudVisible(bool visible)
    {
        if (healthBar != null)
        {
            healthBar.SetActive(visible);
        }

        if (goldHud == null)
        {
            goldHud = FindAnyObjectByType<GoldHUD>();
        }

        if (goldHud != null)
        {
            goldHud.SetVisible(visible);
        }
        else if (!visible)
        {
            // GoldHUD may not have built yet; hide a pre-existing root if present.
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existing = canvas.transform.Find("GoldHUD");
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }
            }
        }
    }

    void SetOverlay(GameObject activeOverlay)
    {
        if (menuOverlay != null)
        {
            menuOverlay.SetActive(activeOverlay == menuOverlay);
        }

        if (difficultyOverlay != null)
        {
            difficultyOverlay.SetActive(activeOverlay == difficultyOverlay);
        }

        if (gameOverOverlay != null)
        {
            gameOverOverlay.SetActive(activeOverlay == gameOverOverlay);
        }

        if (victoryOverlay != null)
        {
            victoryOverlay.SetActive(activeOverlay == victoryOverlay);
        }
    }

    void BindButtons()
    {
        Bind(playButton, OnPlayClicked);
        Bind(easyButton, OnEasyClicked);
        Bind(mediumButton, OnMediumClicked);
        Bind(hardButton, OnHardClicked);
        Bind(gameOverRestartButton, OnRestartClicked);
        Bind(victoryRestartButton, OnRestartClicked);

        if (aidSystemToggle != null)
        {
            aidSystemToggle.onValueChanged.RemoveListener(OnAidSystemToggleChanged);
            aidSystemToggle.onValueChanged.AddListener(OnAidSystemToggleChanged);
        }
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    void ResolveReferences()
    {
        if (roomGenerator == null)
        {
            roomGenerator = FindAnyObjectByType<RoomGenerator>();
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("GameFlowController: Canvas not found.");
            return;
        }

        Transform canvasRoot = canvas.transform;

        menuOverlay ??= FindObject(canvasRoot, "MenuOverlay");
        difficultyOverlay ??= FindObject(canvasRoot, "DifficultyOverlay");
        gameOverOverlay ??= FindObject(canvasRoot, "GameOverOverlay");
        victoryOverlay ??= FindObject(canvasRoot, "VictoryOverlay");
        healthBar ??= FindObject(canvasRoot, "Health_Bar");
        goldHud ??= FindAnyObjectByType<GoldHUD>();

        if (runSummaryUI == null)
        {
            runSummaryUI = GetComponent<RunSummaryUI>();
        }

        if (runSummaryUI == null)
        {
            runSummaryUI = FindAnyObjectByType<RunSummaryUI>();
        }

        if (runSummaryUI == null)
        {
            runSummaryUI = gameObject.AddComponent<RunSummaryUI>();
        }

        playButton ??= FindButton(menuOverlay, "PlayButton", "Play", "Button");
        easyButton ??= FindButton(difficultyOverlay, "EasyButton", "Easy", "Button");
        mediumButton ??= FindButton(difficultyOverlay, "MediumButton", "Medium");
        hardButton ??= FindButton(difficultyOverlay, "HardButton", "Hard");

        if (easyButton == null || mediumButton == null || hardButton == null)
        {
            ResolveDifficultyButtons(difficultyOverlay);
        }

        gameOverRestartButton ??= FindButton(gameOverOverlay, "RestartButton", "Restart", "Button");
        victoryRestartButton ??= FindButton(victoryOverlay, "RestartButton", "VictoryRestartButton", "Restart", "Button");

        if (aidSystemToggle == null && difficultyOverlay != null)
        {
            Transform toggleTf = FindDeepChild(difficultyOverlay.transform, "AidSystemToggle");
            if (toggleTf != null)
            {
                aidSystemToggle = toggleTf.GetComponent<Toggle>();
            }
        }
    }

    void EnsureAidSystemToggle()
    {
        if (difficultyOverlay == null)
        {
            return;
        }

        if (aidSystemToggle == null)
        {
            Transform existing = FindDeepChild(difficultyOverlay.transform, "AidSystemToggle");
            if (existing != null)
            {
                aidSystemToggle = existing.GetComponent<Toggle>();
            }
        }

        if (aidSystemToggle == null)
        {
            aidSystemToggle = CreateAidSystemToggle(difficultyOverlay.transform);
        }

        if (aidSystemToggleLabel == null && aidSystemToggle != null)
        {
            Transform labelTf = aidSystemToggle.transform.Find("Label");
            if (labelTf != null)
            {
                aidSystemToggleLabel = labelTf.GetComponent<Text>();
            }
        }

        DifficultyDirector director = DifficultyDirector.Ensure();
        aidSystemToggle.SetIsOnWithoutNotify(director.SystemEnabled);
        SyncAidToggleUi();
    }

    Toggle CreateAidSystemToggle(Transform parent)
    {
        GameObject root = new GameObject("AidSystemToggle", typeof(RectTransform), typeof(Toggle));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, 48f);
        rootRt.sizeDelta = new Vector2(420f, 48f);

        Toggle toggle = root.GetComponent<Toggle>();

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.SetParent(rootRt, false);
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.anchoredPosition = new Vector2(24f, 0f);
        bgRt.sizeDelta = new Vector2(36f, 36f);
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform checkRt = check.GetComponent<RectTransform>();
        checkRt.SetParent(bgRt, false);
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(6f, 6f);
        checkRt.offsetMax = new Vector2(-6f, -6f);
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = new Color(0.35f, 0.85f, 0.45f, 1f);

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.SetParent(rootRt, false);
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(72f, 0f);
        labelRt.offsetMax = new Vector2(0f, 0f);
        Text labelText = label.GetComponent<Text>();
        labelText.font = UiFonts.Get();
        labelText.fontSize = 22;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        aidSystemToggleLabel = labelText;

        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;
        return toggle;
    }

    void SyncAidToggleUi()
    {
        DifficultyDirector director = DifficultyDirector.Ensure();
        bool on = director.SystemEnabled;
        if (aidSystemToggle != null && aidSystemToggle.isOn != on)
        {
            aidSystemToggle.SetIsOnWithoutNotify(on);
        }

        if (aidSystemToggleLabel != null)
        {
            aidSystemToggleLabel.text = on
                ? "Dynamic Difficulty: ON"
                : "Dynamic Difficulty: OFF";
        }
    }

    void DisableTextRaycastsOnButtons()
    {
        DisableTextRaycasts(playButton);
        DisableTextRaycasts(easyButton);
        DisableTextRaycasts(mediumButton);
        DisableTextRaycasts(hardButton);
        DisableTextRaycasts(gameOverRestartButton);
        DisableTextRaycasts(victoryRestartButton);
    }

    static void DisableTextRaycasts(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != button.targetGraphic)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    void ValidateSetup()
    {
        if (roomGenerator == null)
        {
            Debug.LogError("GameFlowController: RoomGenerator reference is missing.");
        }

        if (menuOverlay == null)
        {
            Debug.LogError("GameFlowController: MenuOverlay not found under Canvas.");
        }

        if (difficultyOverlay == null)
        {
            Debug.LogError("GameFlowController: DifficultyOverlay not found under Canvas.");
        }

        if (playButton == null)
        {
            Debug.LogError("GameFlowController: PlayButton not found. UI clicks will not work.");
        }
    }

    void ResolveDifficultyButtons(GameObject overlay)
    {
        if (overlay == null)
        {
            return;
        }

        Button[] buttons = overlay.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
        {
            return;
        }

        System.Array.Sort(buttons, (a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        if (easyButton == null && buttons.Length > 0)
        {
            easyButton = buttons[0];
        }

        if (mediumButton == null && buttons.Length > 1)
        {
            mediumButton = buttons[1];
        }

        if (hardButton == null && buttons.Length > 2)
        {
            hardButton = buttons[2];
        }
    }

    static GameObject FindObject(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == objectName)
            {
                return all[i].gameObject;
            }
        }

        return null;
    }

    static Button FindButton(GameObject overlay, params string[] names)
    {
        if (overlay == null)
        {
            return null;
        }

        Transform overlayRoot = overlay.transform;
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = overlayRoot.Find(names[i]);
            if (found == null)
            {
                found = FindDeepChild(overlayRoot, names[i]);
            }

            if (found != null)
            {
                Button button = found.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }
            }
        }

        return overlay.GetComponentInChildren<Button>(true);
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
}
