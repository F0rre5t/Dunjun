using System;
using System.Collections.Generic;

public static class RelicInventory
{
    static readonly List<RelicData> collected = new List<RelicData>();

    public static IReadOnlyList<RelicData> Collected => collected;

    public static event Action<RelicData> RelicAdded;
    public static event Action RelicCleared;

    public static bool TryAdd(RelicData relic)
    {
        if (relic == null)
        {
            return false;
        }

        if (!relic.allowDuplicates && HasRelic(relic.relicId))
        {
            return false;
        }

        collected.Add(relic);
        RelicAdded?.Invoke(relic);
        return true;
    }

    public static bool HasRelic(string relicId)
    {
        if (string.IsNullOrEmpty(relicId))
        {
            return false;
        }

        for (int i = 0; i < collected.Count; i++)
        {
            if (collected[i] != null && collected[i].relicId == relicId)
            {
                return true;
            }
        }

        return false;
    }

    public static void Reset()
    {
        collected.Clear();
        RelicCleared?.Invoke();
    }
}
