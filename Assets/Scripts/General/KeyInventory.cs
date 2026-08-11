using UnityEngine;
using System;

public static class KeyInventory
{
    public static bool HasKey { get; private set; }

    public static event Action KeyCollected;
    public static event Action RoomChanged;

    public static void CollectKey()
    {
        HasKey = true;
        KeyCollected?.Invoke();
    }

    public static void Reset()
    {
        HasKey = false;
        RoomChanged?.Invoke();
    }

    public static void ResetWithoutClosingDoors()
    {
        HasKey = false;
        // Do not invoke RoomChanged; doors should stay as they are.
    }
}