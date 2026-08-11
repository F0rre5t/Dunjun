using System.Collections.Generic;
using UnityEngine;

public struct RunKillEntry
{
    public string id;
    public Sprite icon;
    public int count;
}

// Per-run stats for the end-of-run summary.
public static class RunStats
{
    static float startTime;
    static float endTime;
    static bool running;
    static bool hasEnded;
    static int damageTaken;

    static readonly List<RunKillEntry> kills = new List<RunKillEntry>();
    static readonly List<Sprite> relicIcons = new List<Sprite>();

    public static bool IsRunning => running;
    public static int DamageTaken => damageTaken;
    public static IReadOnlyList<RunKillEntry> Kills => kills;
    public static IReadOnlyList<Sprite> RelicIcons => relicIcons;

    public static float ElapsedSeconds
    {
        get
        {
            if (!running && !hasEnded)
            {
                return 0f;
            }

            float end = running ? Time.unscaledTime : endTime;
            return Mathf.Max(0f, end - startTime);
        }
    }

    public static void Begin()
    {
        Reset();
        startTime = Time.unscaledTime;
        endTime = startTime;
        running = true;
        hasEnded = false;
    }

    public static void Stop()
    {
        if (!running)
        {
            return;
        }

        endTime = Time.unscaledTime;
        running = false;
        hasEnded = true;
        CaptureRelics();
    }

    public static void Reset()
    {
        startTime = 0f;
        endTime = 0f;
        running = false;
        hasEnded = false;
        damageTaken = 0;
        kills.Clear();
        relicIcons.Clear();
    }

    public static void AddDamageTaken(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        damageTaken += amount;
    }

    public static void RegisterKill(string id, Sprite icon)
    {
        if (string.IsNullOrEmpty(id))
        {
            id = "enemy";
        }

        for (int i = 0; i < kills.Count; i++)
        {
            RunKillEntry entry = kills[i];
            if (entry.id == id)
            {
                entry.count += 1;
                if (entry.icon == null && icon != null)
                {
                    entry.icon = icon;
                }

                kills[i] = entry;
                return;
            }
        }

        kills.Add(new RunKillEntry
        {
            id = id,
            icon = icon,
            count = 1
        });
    }

    public static void CaptureRelics()
    {
        relicIcons.Clear();
        IReadOnlyList<RelicData> collected = RelicInventory.Collected;
        for (int i = 0; i < collected.Count; i++)
        {
            RelicData relic = collected[i];
            if (relic != null && relic.hudIcon != null)
            {
                relicIcons.Add(relic.hudIcon);
            }
        }
    }

    public static string FormatElapsed()
    {
        float elapsed = ElapsedSeconds;
        int totalCentiseconds = Mathf.FloorToInt(elapsed * 100f);
        int minutes = totalCentiseconds / 6000;
        int seconds = (totalCentiseconds / 100) % 60;
        int centiseconds = totalCentiseconds % 100;
        return $"{minutes:00}:{seconds:00}:{centiseconds:00}";
    }
}
