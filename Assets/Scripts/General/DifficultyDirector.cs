using UnityEngine;

/// <summary>
/// Runtime difficulty aid based on how much the player is struggling.
/// If disabled, all multipliers stay at 1 so it can be used as an A/B control.
/// </summary>
[DefaultExecutionOrder(-150)]
public class DifficultyDirector : MonoBehaviour
{
    public enum RunDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public static DifficultyDirector Instance { get; private set; }

    [Header("Master Switch (A/B playtest)")]
    [SerializeField] bool systemEnabled = true;

    [Header("Distress Weights")]
    [SerializeField] float weightHp = 0.40f;
    [SerializeField] float weightRoomHurt = 0.30f;
    [SerializeField] float weightNoHeal = 0.20f;
    [SerializeField] float weightNearDeath = 0.10f;

    [Header("Aid Curve")]
    [SerializeField] float aidThreshold = 0.24f;
    [SerializeField] int cooldownRooms = 2;
    [SerializeField] float potionMultMax = 3.0f;
    [SerializeField] float dropMultMax = 1.2f;
    [SerializeField] float spikeMultMin = 0.60f;
    [SerializeField] float emergencySaveMax = 0.65f;
    [SerializeField] int nearDeathHp = 1;

    [Header("Aid Strength By Difficulty")]
    [SerializeField] float easyAidStrength = 1.0f;
    [SerializeField] float normalAidStrength = 0.40f;
    [SerializeField] float hardAidStrength = 0.0f;
    [SerializeField] float normalEmergencyScale = 0.25f;

    RunDifficulty difficulty = RunDifficulty.Normal;
    HealthManager health;

    int combatStep;
    int damageTakenThisRoom;
    int noHealStreak;
    int nearDeathCount;
    int cooldownRemaining;
    bool roomActive;
    bool healedThisRoom;
    float roomAidT;
    bool roomAidConsumed;

    public bool SystemEnabled => systemEnabled;
    public RunDifficulty CurrentDifficulty => difficulty;
    public float CurrentDistress { get; private set; }
    public int NearDeathCount => nearDeathCount;
    public int NoHealStreak => noHealStreak;
    public int CooldownRemaining => cooldownRemaining;

    public event System.Action<bool> SystemEnabledChanged;

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

    public static DifficultyDirector Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        DifficultyDirector existing = FindAnyObjectByType<DifficultyDirector>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameFlowController host = FindAnyObjectByType<GameFlowController>();
        GameObject go = host != null ? host.gameObject : new GameObject("DifficultyDirector");
        Instance = go.GetComponent<DifficultyDirector>();
        if (Instance == null)
        {
            Instance = go.AddComponent<DifficultyDirector>();
        }

        return Instance;
    }

    public void SetSystemEnabled(bool enabled)
    {
        if (systemEnabled == enabled)
        {
            return;
        }

        systemEnabled = enabled;
        SystemEnabledChanged?.Invoke(systemEnabled);
    }

    public void ToggleSystemEnabled()
    {
        SetSystemEnabled(!systemEnabled);
    }

    // Called once when a run starts.
    public void BeginRun(RunDifficulty runDifficulty)
    {
        difficulty = runDifficulty;
        health = FindAnyObjectByType<HealthManager>(FindObjectsInactive.Include);
        combatStep = 0;
        damageTakenThisRoom = 0;
        noHealStreak = 0;
        nearDeathCount = 0;
        cooldownRemaining = 0;
        roomActive = false;
        healedThisRoom = false;
        roomAidT = 0f;
        roomAidConsumed = false;
        CurrentDistress = 0f;
    }

    // Entering a combat room. We freeze an aid value for this room.
    public void NotifyCombatRoomStarted(int step)
    {
        combatStep = Mathf.Max(0, step);
        health = health != null ? health : FindAnyObjectByType<HealthManager>(FindObjectsInactive.Include);
        damageTakenThisRoom = 0;
        healedThisRoom = false;
        roomActive = true;
        roomAidConsumed = false;
        RefreshDistress();
        roomAidT = ComputeAidT();
    }

    // Leaving a combat room. Track healing gaps and start cooldown if aid was used.
    public void NotifyCombatRoomEnded()
    {
        if (!roomActive)
        {
            return;
        }

        roomActive = false;

        if (!healedThisRoom)
        {
            noHealStreak++;
        }

        RefreshDistress();

        bool usedAid = roomAidT > 0f || roomAidConsumed;
        roomAidT = 0f;
        roomAidConsumed = false;

        if (usedAid)
        {
            ArmCooldown();
        }
        else if (cooldownRemaining > 0)
        {
            cooldownRemaining--;
        }
    }

    // Player took a hit. Distress goes up and aid may get stronger mid-room.
    public void RegisterDamage(int amount, int healthAfter)
    {
        if (amount <= 0)
        {
            return;
        }

        damageTakenThisRoom += amount;
        if (healthAfter > 0 && healthAfter <= nearDeathHp)
        {
            nearDeathCount++;
        }

        RefreshDistress();
        if (roomActive && cooldownRemaining <= 0)
        {
            roomAidT = Mathf.Max(roomAidT, ComputeAidT());
        }
    }

    public void RegisterHeal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        healedThisRoom = true;
        noHealStreak = 0;
        RefreshDistress();
    }

    public float GetDropChanceMultiplier()
    {
        float t = GetActiveAidT();
        if (t <= 0f)
        {
            return 1f;
        }

        return 1f + (dropMultMax - 1f) * t;
    }

    public float GetPotionWeightMultiplier()
    {
        float t = GetActiveAidT();
        if (t <= 0f)
        {
            return 1f;
        }

        return 1f + (potionMultMax - 1f) * t;
    }

    public float GetSpikeSpawnMultiplier()
    {
        float t = GetActiveAidT();
        if (t <= 0f)
        {
            return 1f;
        }

        return 1f - (1f - spikeMultMin) * t;
    }

    // Prefer simpler spike layouts when the player is getting help.
    public float GetSpikeShapeWeightMultiplier(SpikeTrapPattern.Shape shape)
    {
        float t = GetActiveAidT();
        if (t <= 0f)
        {
            return 1f;
        }

        switch (shape)
        {
            case SpikeTrapPattern.Shape.Cross:
            case SpikeTrapPattern.Shape.Line:
            case SpikeTrapPattern.Shape.Ring:
                return 1f + 0.75f * t;
            case SpikeTrapPattern.Shape.CornerClusters:
            case SpikeTrapPattern.Shape.DoorGuards:
                return 1f;
            case SpikeTrapPattern.Shape.Diagonals:
                return Mathf.Max(0.2f, 1f - 0.25f * t);
            case SpikeTrapPattern.Shape.Box:
            case SpikeTrapPattern.Shape.TwinRails:
                return Mathf.Max(0.15f, 1f - 0.45f * t);
            default:
                return 1f;
        }
    }

    // Small chance to survive a killing blow at 1 HP.
    public bool TryEmergencyStabilize(HealthManager target)
    {
        if (!systemEnabled || target == null)
        {
            return false;
        }

        RefreshDistress();
        float t = ComputeAidT();
        if (t <= 0f)
        {
            return false;
        }

        float saveChance = emergencySaveMax * GetEmergencyScale() * t;
        if (Random.value >= saveChance)
        {
            return false;
        }

        target.ForceSetHealth(1);
        roomAidT = Mathf.Max(roomAidT, t);
        ArmCooldown();
        roomAidConsumed = true;
        RefreshDistress();
        return true;
    }

    float GetActiveAidT()
    {
        if (!systemEnabled || GetAidStrength() <= 0f || cooldownRemaining > 0)
        {
            return 0f;
        }

        return roomActive ? roomAidT : ComputeAidT();
    }

    float GetAidStrength()
    {
        switch (difficulty)
        {
            case RunDifficulty.Easy:
                return easyAidStrength;
            case RunDifficulty.Normal:
                return normalAidStrength;
            default:
                return hardAidStrength;
        }
    }

    float GetEmergencyScale()
    {
        switch (difficulty)
        {
            case RunDifficulty.Easy:
                return 1f;
            case RunDifficulty.Normal:
                return normalEmergencyScale;
            default:
                return 0f;
        }
    }

    // Only aid once distress clears the threshold, then scale by difficulty.
    float ComputeAidT()
    {
        if (!systemEnabled || GetAidStrength() <= 0f || cooldownRemaining > 0)
        {
            return 0f;
        }

        if (CurrentDistress < aidThreshold)
        {
            return 0f;
        }

        float denom = Mathf.Max(0.0001f, 1f - aidThreshold);
        float t = (CurrentDistress - aidThreshold) / denom;
        return Mathf.Clamp01(t) * GetAidStrength();
    }

    // Weighted mix of low HP, room damage, heal drought, and near-death hits.
    void RefreshDistress()
    {
        health = health != null ? health : FindAnyObjectByType<HealthManager>(FindObjectsInactive.Include);
        float hpFactor = 0f;
        if (health != null && health.maxHealth > 0)
        {
            hpFactor = 1f - (health.currentHealth / (float)health.maxHealth);
        }

        float expected = ExpectedDamageForStep(combatStep);
        float ratio = damageTakenThisRoom / Mathf.Max(0.35f, expected);
        float hurtFactor = Mathf.Clamp01((ratio - 0.6f) / 1.4f);
        float noHealFactor = Mathf.Clamp01(noHealStreak / 4f);
        float nearDeathFactor = Mathf.Clamp01(nearDeathCount / 3f);

        CurrentDistress = Mathf.Clamp01(
            weightHp * hpFactor
            + weightRoomHurt * hurtFactor
            + weightNoHeal * noHealFactor
            + weightNearDeath * nearDeathFactor);
    }

    // Rough expected damage by depth so early/late rooms are comparable.
    static readonly float[] ExpectedDamageByStep =
    {
        0.17f, 0.21f, 0.28f, 0.33f, 0.39f, 0.47f,
        0.55f, 0.58f, 0.66f, 0.70f, 0.75f, 0.80f
    };

    static float ExpectedDamageForStep(int step)
    {
        if (step < 0)
        {
            return ExpectedDamageByStep[0];
        }

        if (step < ExpectedDamageByStep.Length)
        {
            return ExpectedDamageByStep[step];
        }

        return 0.80f + (step - 11) * 0.05f;
    }

    void ArmCooldown()
    {
        cooldownRemaining = Mathf.Max(cooldownRemaining, Mathf.Max(1, cooldownRooms));
    }
}
