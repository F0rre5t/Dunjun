using System.Collections.Generic;
using UnityEngine;

public class RelicEffectApplier : MonoBehaviour
{
    PlayerControl playerControl;
    HealthManager healthManager;

    int baseAttackDamage;
    float baseSpeed;
    float baseAttackRange;

    int flatAttackBonus;
    float flatSpeedBonus;
    float flatAttackRangeBonus;
    float attackPercentBonus;
    float speedPercentBonus;
    bool firstAttackCritEnabled;
    float firstAttackCritMultiplier = 2f;
    float blockChance;
    float attackHealChance;
    int attackHealAmount;
    int healOnRoomClear;

    readonly HashSet<Room> roomsHealedFromClear = new HashSet<Room>();

    void Awake()
    {
        playerControl = GetComponent<PlayerControl>();
        healthManager = FindAnyObjectByType<HealthManager>();
        CacheBaseStats();
    }

    void OnEnable()
    {
        RelicInventory.RelicCleared += ResetToBaseStats;
        Room.RoomCleared += HandleRoomCleared;
    }

    void OnDisable()
    {
        RelicInventory.RelicCleared -= ResetToBaseStats;
        Room.RoomCleared -= HandleRoomCleared;
    }

    public void ApplyRelic(RelicData relic)
    {
        if (relic == null)
        {
            return;
        }

        flatAttackBonus += relic.attackBonus;
        flatSpeedBonus += relic.speedBonus;
        flatAttackRangeBonus += relic.attackRangeBonus;
        attackPercentBonus += relic.attackPercentBonus;
        speedPercentBonus += relic.speedPercentBonus;

        if (relic.firstAttackCritOnEnemy)
        {
            firstAttackCritEnabled = true;
            if (relic.firstAttackCritMultiplier > 0f)
            {
                firstAttackCritMultiplier = relic.firstAttackCritMultiplier;
            }
        }

        blockChance += relic.blockChance;
        attackHealChance += relic.attackHealChance;
        attackHealAmount += relic.attackHealAmount;
        healOnRoomClear += relic.healOnRoomClear;

        RecalculateStats();

        if (healthManager != null)
        {
            if (relic.maxHealthBonus > 0)
            {
                healthManager.IncreaseMaxHealth(relic.maxHealthBonus);
            }

            if (relic.healOnPickup > 0)
            {
                healthManager.Heal(relic.healOnPickup);
            }
        }
    }

    public int ResolveDamageAgainst(Enemy enemy, int baseDamage)
    {
        if (!firstAttackCritEnabled || enemy == null || enemy.HasBeenHitByPlayer)
        {
            return baseDamage;
        }

        enemy.MarkHitByPlayer();
        return Mathf.RoundToInt(baseDamage * firstAttackCritMultiplier);
    }

    public void ApplyOnHitEffects(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        for (int i = 0; i < relics.Count; i++)
        {
            RelicData relic = relics[i];
            if (relic == null)
            {
                continue;
            }

            if (!Mathf.Approximately(relic.onHitSlowPercent, 0f) && relic.onHitSlowDuration > 0f)
            {
                enemy.ApplySlowFromHit(relic.onHitSlowPercent, relic.onHitSlowDuration);
            }
        }

        PoisonRelicEffects.OnHitSettings poison = PoisonRelicEffects.GetOnHitSettings();
        if (poison.enabled)
        {
            enemy.ApplyPoisonFromHit(
                poison.damagePerTick,
                poison.duration,
                poison.tickInterval,
                poison.isPermanent);
        }
    }

    public bool TryBlockIncomingDamage()
    {
        if (blockChance <= 0f)
        {
            return false;
        }

        return Random.value < blockChance;
    }

    public void TryHealOnAttack()
    {
        if (healthManager == null || attackHealAmount <= 0 || attackHealChance <= 0f)
        {
            return;
        }

        if (Random.value < attackHealChance)
        {
            healthManager.Heal(attackHealAmount);
        }
    }

    void HandleRoomCleared(Room room)
    {
        if (room == null || healOnRoomClear <= 0 || healthManager == null)
        {
            return;
        }

        if (!roomsHealedFromClear.Add(room))
        {
            return;
        }

        healthManager.Heal(healOnRoomClear);
    }

    void CacheBaseStats()
    {
        if (playerControl == null)
        {
            return;
        }

        baseAttackDamage = playerControl.attackDamage;
        baseSpeed = playerControl.speed;
        baseAttackRange = playerControl.attackRange;
    }

    void RecalculateStats()
    {
        if (playerControl == null)
        {
            return;
        }

        playerControl.attackDamage = Mathf.RoundToInt((baseAttackDamage + flatAttackBonus) * (1f + attackPercentBonus));
        playerControl.speed = (baseSpeed + flatSpeedBonus) * (1f + speedPercentBonus);
        playerControl.attackRange = baseAttackRange + flatAttackRangeBonus;
    }

    void ResetToBaseStats()
    {
        flatAttackBonus = 0;
        flatSpeedBonus = 0;
        flatAttackRangeBonus = 0;
        attackPercentBonus = 0f;
        speedPercentBonus = 0f;
        firstAttackCritEnabled = false;
        firstAttackCritMultiplier = 2f;
        blockChance = 0f;
        attackHealChance = 0f;
        attackHealAmount = 0;
        healOnRoomClear = 0;
        roomsHealedFromClear.Clear();
        RecalculateStats();
    }
}
