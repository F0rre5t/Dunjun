using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class HealthManager : MonoBehaviour
{
    public GameObject heartPrefab;
    public Transform heartRoot;
    public int maxHealth;
    public int currentHealth;

    public Animator playerAnimator;

    public bool IsDead { get; private set; }
    
    private List<GameObject> hearts = new List<GameObject>();
    private int baseMaxHealth;

    private PlayerControl playerControl;
    private RelicEffectApplier relicEffectApplier;
    private Rigidbody2D playerRb;
    private Coroutine deathRoutine;

    void Awake()
    {
        baseMaxHealth = maxHealth;
        currentHealth = maxHealth;
        EnsureHeartRoot();
    }

    void Start()
    {
        BuildHeartUI();
    }

    void OnEnable()
    {
        // If the bar was inactive during first Start skip, rebuild when shown for gameplay.
        if (hearts == null || hearts.Count == 0)
        {
            BuildHeartUI();
        }
        else
        {
            RefreshUI();
            RefreshHudBarLayout();
        }
    }

    public void BindPlayer(GameObject playerObj)
    {
        if (playerObj == null)
        {
            return;
        }

        playerAnimator = playerAnimator != null ? playerAnimator : playerObj.GetComponent<Animator>();
        playerControl = playerObj.GetComponent<PlayerControl>();
        relicEffectApplier = playerObj.GetComponent<RelicEffectApplier>();
        playerRb = playerObj.GetComponent<Rigidbody2D>();
        ResetForNewRun();
    }

    public void ResetForNewRun()
    {
        RelicInventory.Reset();
        GoldInventory.Reset();

        IsDead = false;
        maxHealth = baseMaxHealth;
        currentHealth = maxHealth;
        RebuildHeartUI();

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        if (playerControl != null)
        {
            playerControl.enabled = true;
        }

        RefreshUI();
    }

    void BuildHeartUI()
    {
        if (hearts.Count > 0)
        {
            return;
        }

        RebuildHeartUI();
    }

    void RebuildHeartUI()
    {
        Transform root = heartRoot != null ? heartRoot : transform;

        for (int i = hearts.Count - 1; i >= 0; i--)
        {
            if (hearts[i] != null)
            {
                Destroy(hearts[i]);
            }
        }

        hearts.Clear();

        for (int i = 0; i < maxHealth; i++)
        {
            GameObject h = Instantiate(heartPrefab, root);
            ConfigureHudIcon(h, 48f);
            hearts.Add(h);
        }

        RefreshUI();
        RefreshHudBarLayout();
    }

    public void IncreaseMaxHealth(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        maxHealth += amount;
        currentHealth += amount;

        Transform root = heartRoot != null ? heartRoot : transform;
        for (int i = 0; i < amount; i++)
        {
            GameObject h = Instantiate(heartPrefab, root);
            ConfigureHudIcon(h, 48f);
            hearts.Add(h);
        }

        RefreshUI();
        RefreshHudBarLayout();
    }

    void EnsureHeartRoot()
    {
        if (heartRoot != null)
        {
            return;
        }

        heartRoot = GetOrCreateHudContainer(transform, "HeartRoot", 0);
    }

    public static Transform GetOrCreateHudContainer(Transform parent, string containerName, int siblingIndex, int leftPadding = 0)
    {
        Transform existing = parent.Find(containerName);
        if (existing != null)
        {
            EnsureContainerLayout(existing.gameObject, leftPadding);
            return existing;
        }

        GameObject container = new GameObject(containerName, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        container.transform.SetSiblingIndex(siblingIndex);

        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 48f);

        HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(leftPadding, 0, 0, 0);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement layoutElement = container.AddComponent<LayoutElement>();
        layoutElement.minHeight = 48f;
        layoutElement.preferredHeight = 48f;
        layoutElement.flexibleWidth = 0f;

        return container.transform;
    }

    static void EnsureContainerLayout(GameObject container, int leftPadding)
    {
        RectTransform rect = container.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
        }

        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = container.AddComponent<HorizontalLayoutGroup>();
        }

        layout.padding = new RectOffset(leftPadding, 0, 0, 0);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = container.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement layoutElement = container.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = container.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 48f;
        layoutElement.preferredHeight = 48f;
        layoutElement.flexibleWidth = 0f;
    }

    public static void ConfigureHudIcon(GameObject iconObject, float size)
    {
        if (iconObject == null)
        {
            return;
        }

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(size, size);
        }

        LayoutElement layout = iconObject.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = iconObject.AddComponent<LayoutElement>();
        }

        layout.minWidth = size;
        layout.minHeight = size;
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    public void RefreshHudBarLayout()
    {
        RefreshHudBarLayout(transform);
    }

    public static void RefreshHudBarLayout(Transform hudBarRoot)
    {
        if (hudBarRoot == null)
        {
            return;
        }

        RectTransform rect = hudBarRoot as RectTransform;
        if (rect == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    public bool Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return false;
        }

        if (currentHealth >= maxHealth)
        {
            return false;
        }

        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        int gained = currentHealth - before;
        RefreshUI();
        if (gained > 0)
        {
            DifficultyDirector.Ensure().RegisterHeal(gained);
            return true;
        }

        return false;
    }

    public void ForceSetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        if (currentHealth > 0)
        {
            IsDead = false;
        }

        RefreshUI();
    }

    public void RefreshUI()
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            Transform fillLayer = hearts[i].transform.Find("Fill");

            if (i < currentHealth)
                fillLayer.gameObject.SetActive(true);
            else
                fillLayer.gameObject.SetActive(false);
        }
    }
        
    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        if (relicEffectApplier != null && relicEffectApplier.TryBlockIncomingDamage())
        {
            return;
        }

        int before = currentHealth;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        int lost = before - currentHealth;
        if (lost > 0)
        {
            RunStats.AddDamageTaken(lost);
        }

        if (currentHealth <= 0)
        {
            DifficultyDirector director = DifficultyDirector.Ensure();
            if (director.TryEmergencyStabilize(this))
            {
                if (lost > 0)
                {
                    director.RegisterDamage(lost, currentHealth);
                }

                RefreshUI();
                if (playerAnimator != null)
                {
                    playerAnimator.SetTrigger("damage");
                }

                return;
            }

            if (lost > 0)
            {
                director.RegisterDamage(lost, 0);
            }

            RefreshUI();
            IsDead = true;

            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("die");
            }

            if (playerControl != null)
            {
                playerControl.ForceStop();
                playerControl.enabled = false;
            }

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.bodyType = RigidbodyType2D.Static;
            }

            if (deathRoutine == null)
            {
                deathRoutine = StartCoroutine(WaitForDeathAnimation());
            }

            return;
        }

        if (lost > 0)
        {
            DifficultyDirector.Ensure().RegisterDamage(lost, currentHealth);
        }

        RefreshUI();

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("damage");
        }
    }

    IEnumerator WaitForDeathAnimation()
    {
        const float fallbackDeathDuration = 1.5f;
        const float maxWaitDuration = 5f;
        float targetDuration = fallbackDeathDuration;

        if (playerAnimator != null)
        {
            yield return null;

            AnimatorClipInfo[] clips = playerAnimator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0 && clips[0].clip != null)
            {
                targetDuration = clips[0].clip.length;
            }

            targetDuration += 0.3f;

            float elapsed = 0f;
            while (elapsed < maxWaitDuration)
            {
                AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Die") && state.normalizedTime >= 1f && !playerAnimator.IsInTransition(0))
                {
                    break;
                }

                if (elapsed >= targetDuration && state.IsName("Die"))
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fallbackDeathDuration);
        }

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.ShowGameOver();
        }
    }
}