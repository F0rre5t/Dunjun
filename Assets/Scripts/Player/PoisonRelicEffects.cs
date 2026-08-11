using System.Collections.Generic;
using UnityEngine;

// Poison set bonuses and shared on-hit / trail stats.
public static class PoisonRelicEffects
{
    public const int SetBoostThreshold = 2;
    public const int PermanentThreshold = 3;
    public const int SetDamageBonus = 1;
    public const float SetDurationBonus = 1.5f;
    public const float MinotaurPoisonDamageMultiplier = 1.5f;

    public struct OnHitSettings
    {
        public bool enabled;
        public int damagePerTick;
        public float duration;
        public float tickInterval;
        public bool isPermanent;
    }

    public struct TrailSettings
    {
        public bool enabled;
        public float spawnDistance;
        public float cloudLifetime;
        public float cloudRadius;
        public int damagePerTick;
        public float duration;
        public float tickInterval;
        public float reapplyCooldown;
        public float maxExposurePerEnemy;
        public bool isPermanent;
    }

    public static int CountPoisonRelics()
    {
        int count = 0;
        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        for (int i = 0; i < relics.Count; i++)
        {
            RelicData relic = relics[i];
            if (relic != null && relic.isPoisonAttribute)
            {
                count++;
            }
        }

        return count;
    }

    public static OnHitSettings GetOnHitSettings()
    {
        int bestDamage = 0;
        float bestDuration = 0f;
        float bestTickInterval = 1f;
        int damageBonus = 0;
        float durationBonus = 0f;
        bool found = false;

        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        for (int i = 0; i < relics.Count; i++)
        {
            RelicData relic = relics[i];
            if (relic == null)
            {
                continue;
            }

            damageBonus += Mathf.Max(0, relic.poisonDamageBonus);
            durationBonus += Mathf.Max(0f, relic.poisonDurationBonus);

            if (relic.onHitPoisonDamagePerTick <= 0 || relic.onHitPoisonDuration <= 0f)
            {
                continue;
            }

            found = true;
            if (relic.onHitPoisonDamagePerTick > bestDamage)
            {
                bestDamage = relic.onHitPoisonDamagePerTick;
                bestTickInterval = relic.onHitPoisonTickInterval;
            }

            if (relic.onHitPoisonDuration > bestDuration)
            {
                bestDuration = relic.onHitPoisonDuration;
            }
        }

        if (!found)
        {
            return default;
        }

        ApplySetBonuses(ref bestDamage, ref bestDuration, out bool isPermanent);

        return new OnHitSettings
        {
            enabled = true,
            damagePerTick = bestDamage + damageBonus,
            duration = bestDuration + durationBonus,
            tickInterval = Mathf.Max(0.1f, bestTickInterval),
            isPermanent = isPermanent
        };
    }

    public static TrailSettings GetTrailSettings()
    {
        RelicData trailRelic = null;
        int damageBonus = 0;
        float durationBonus = 0f;

        IReadOnlyList<RelicData> relics = RelicInventory.Collected;
        for (int i = 0; i < relics.Count; i++)
        {
            RelicData relic = relics[i];
            if (relic == null)
            {
                continue;
            }

            damageBonus += Mathf.Max(0, relic.poisonDamageBonus);
            durationBonus += Mathf.Max(0f, relic.poisonDurationBonus);

            if (trailRelic == null && relic.leavesPoisonTrail)
            {
                trailRelic = relic;
            }
        }

        if (trailRelic == null
            || trailRelic.poisonTrailPoisonDamagePerTick <= 0
            || trailRelic.poisonTrailPoisonDuration <= 0f)
        {
            return default;
        }

        int damage = trailRelic.poisonTrailPoisonDamagePerTick;
        float duration = trailRelic.poisonTrailPoisonDuration;
        ApplySetBonuses(ref damage, ref duration, out bool isPermanent);

        return new TrailSettings
        {
            enabled = true,
            spawnDistance = trailRelic.poisonTrailSpawnDistance,
            cloudLifetime = trailRelic.poisonCloudLifetime,
            cloudRadius = trailRelic.poisonCloudRadius,
            damagePerTick = damage + damageBonus,
            duration = duration + durationBonus,
            tickInterval = trailRelic.poisonTrailPoisonTickInterval,
            reapplyCooldown = trailRelic.poisonTrailReapplyCooldown,
            maxExposurePerEnemy = isPermanent ? -1f : trailRelic.poisonMaxExposurePerEnemy,
            isPermanent = isPermanent
        };
    }

    public static int ScalePoisonDamageForTarget(Enemy enemy, int damage)
    {
        if (damage <= 0 || enemy == null)
        {
            return damage;
        }

        if (enemy is Minotaur)
        {
            return Mathf.Max(1, Mathf.RoundToInt(damage * MinotaurPoisonDamageMultiplier));
        }

        return damage;
    }

    static void ApplySetBonuses(ref int damage, ref float duration, out bool isPermanent)
    {
        int poisonCount = CountPoisonRelics();
        isPermanent = poisonCount >= PermanentThreshold;

        if (poisonCount >= SetBoostThreshold)
        {
            damage += SetDamageBonus;
            duration += SetDurationBonus;
        }
    }
}
