using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRelic", menuName = "Rouge/Relic Data")]
public class RelicData : ScriptableObject
{
    [Header("Identity")]
    public string relicId;
    public string displayName;

    [Header("UI")]
    [TextArea(2, 4)]
    public string description;
    public Sprite hudIcon;

    [Header("Pickup Rules")]
    public bool allowDuplicates;

    [Header("Shop Price")]
    [Min(0)] public int priceMin = 20;
    [Min(0)] public int priceMax = 20;

    [Header("Stat Bonuses")]
    public int attackBonus;
    [Range(-1f, 5f)] public float attackPercentBonus;
    public int maxHealthBonus;
    public float speedBonus;
    [Range(-1f, 5f)] public float speedPercentBonus;
    public float attackRangeBonus;
    public int healOnPickup;
    public int healOnRoomClear;

    [Header("Combat Effects")]
    public bool firstAttackCritOnEnemy;
    [Min(1f)] public float firstAttackCritMultiplier = 2f;
    [Range(0f, 1f)] public float blockChance;
    [Range(0f, 1f)] public float attackHealChance;
    public int attackHealAmount;

    [Header("On-Hit Debuff")]
    [Range(0f, 1f)] public float onHitSlowPercent;
    public float onHitSlowDuration = 3f;
    public int onHitPoisonDamagePerTick;
    public float onHitPoisonDuration = 4f;
    public float onHitPoisonTickInterval = 1f;

    [Header("Poison Trail")]
    public bool leavesPoisonTrail;
    public float poisonTrailSpawnDistance = 0.35f;
    public float poisonCloudLifetime = 1.2f;
    public float poisonCloudRadius = 0.45f;
    public int poisonTrailPoisonDamagePerTick = 1;
    public float poisonTrailPoisonDuration = 4f;
    public float poisonTrailPoisonTickInterval = 1f;
    public float poisonTrailReapplyCooldown = 2f;
    public float poisonMaxExposurePerEnemy = 6f;

    [Header("Poison Attribute")]
    
    public bool isPoisonAttribute;
    
    public int poisonDamageBonus;
    
    public float poisonDurationBonus;

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }

    public int RollShopPrice()
    {
        int min = Mathf.Max(0, priceMin);
        int max = Mathf.Max(min, priceMax);
        return UnityEngine.Random.Range(min, max + 1);
    }

    // Shop / inventory effect text. Prefer the written description.
    public string GetBriefEffect()
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        string stats = BuildStatLine();
        return string.IsNullOrEmpty(stats) ? "No effect" : stats;
    }

    public string GetEffectSummary()
    {
        // Prefer the written description so inventory doesn't show the same effect twice.
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        string stats = BuildStatLine();
        return string.IsNullOrEmpty(stats) ? "No effect" : stats;
    }

    string BuildStatLine()
    {
        List<string> parts = new List<string>();

        if (attackBonus != 0)
        {
            parts.Add($"Attack {FormatSigned(attackBonus)}");
        }

        if (!Mathf.Approximately(attackPercentBonus, 0f))
        {
            parts.Add($"Attack {FormatSignedPercent(attackPercentBonus)}");
        }

        if (maxHealthBonus != 0)
        {
            parts.Add($"Max HP {FormatSigned(maxHealthBonus)}");
        }

        if (speedBonus != 0f)
        {
            parts.Add($"Speed {FormatSigned(speedBonus)}");
        }

        if (!Mathf.Approximately(speedPercentBonus, 0f))
        {
            parts.Add($"Speed {FormatSignedPercent(speedPercentBonus)}");
        }

        if (attackRangeBonus != 0f)
        {
            parts.Add($"Attack Range {FormatSigned(attackRangeBonus)}");
        }

        if (healOnPickup != 0)
        {
            parts.Add($"Heal {healOnPickup} HP on pickup");
        }

        if (healOnRoomClear > 0)
        {
            parts.Add($"Heal {healOnRoomClear} HP on first room clear");
        }

        if (firstAttackCritOnEnemy)
        {
            int critPercent = Mathf.RoundToInt(firstAttackCritMultiplier * 100f);
            parts.Add($"First hit on each enemy is a critical hit ({critPercent}% damage)");
        }

        if (!Mathf.Approximately(blockChance, 0f))
        {
            int blockPercent = Mathf.RoundToInt(blockChance * 100f);
            parts.Add($"{blockPercent}% chance to block attacks");
        }

        if (!Mathf.Approximately(attackHealChance, 0f) && attackHealAmount > 0)
        {
            int healPercent = Mathf.RoundToInt(attackHealChance * 100f);
            parts.Add($"{healPercent}% chance to heal {attackHealAmount} HP when dealing damage");
        }

        if (!Mathf.Approximately(onHitSlowPercent, 0f) && onHitSlowDuration > 0f)
        {
            int slowPercent = Mathf.RoundToInt(onHitSlowPercent * 100f);
            parts.Add($"Slow enemy move speed by {slowPercent}% for {onHitSlowDuration:0.#}s on hit");
        }

        if (onHitPoisonDamagePerTick > 0 && onHitPoisonDuration > 0f)
        {
            parts.Add($"Poison enemy for {onHitPoisonDamagePerTick} damage every {onHitPoisonTickInterval:0.#}s ({onHitPoisonDuration:0.#}s) on hit");
        }

        if (leavesPoisonTrail && poisonTrailPoisonDamagePerTick > 0 && poisonTrailPoisonDuration > 0f)
        {
            parts.Add($"Leave poison mist while moving ({poisonTrailPoisonDamagePerTick} damage every {poisonTrailPoisonTickInterval:0.#}s for {poisonTrailPoisonDuration:0.#}s; enemies adapt after {poisonMaxExposurePerEnemy:0.#}s)");
        }

        if (isPoisonAttribute)
        {
            parts.Add("Poison attribute: x1.5 poison vs Minotaur; 2 relics boost poison, 3 make it permanent");
        }

        if (poisonDamageBonus > 0)
        {
            parts.Add($"Poison damage +{poisonDamageBonus}");
        }

        if (poisonDurationBonus > 0f)
        {
            parts.Add($"Poison duration +{poisonDurationBonus:0.#}s");
        }

        return string.Join(", ", parts);
    }

    static string FormatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    static string FormatSigned(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "0";
        }

        if (value > 0f)
        {
            return Mathf.Approximately(value % 1f, 0f) ? $"+{(int)value}" : $"+{value:0.#}";
        }

        return Mathf.Approximately(value % 1f, 0f) ? ((int)value).ToString() : $"{value:0.#}";
    }

    static string FormatSignedPercent(float value)
    {
        int percent = Mathf.RoundToInt(value * 100f);
        return percent > 0 ? $"+{percent}%" : $"{percent}%";
    }
}
