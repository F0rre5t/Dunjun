using System.Collections.Generic;
using UnityEngine;

public static class LootUtility
{
    public static RelicData GetRelicDataFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        RelicPickup pickup = prefab.GetComponent<RelicPickup>();
        if (pickup == null)
        {
            pickup = prefab.GetComponentInChildren<RelicPickup>(true);
        }

        return pickup != null ? pickup.RelicData : null;
    }

    public static bool IsRelicAvailable(GameObject relicPrefab)
    {
        RelicData data = GetRelicDataFromPrefab(relicPrefab);
        if (data == null)
        {
            return false;
        }

        return data.allowDuplicates || !RelicInventory.HasRelic(data.relicId);
    }

    public static GameObject PickAvailableRelicPrefab(IList<GameObject> relicPrefabs)
    {
        if (relicPrefabs == null || relicPrefabs.Count == 0)
        {
            return null;
        }

        List<GameObject> available = new List<GameObject>();
        for (int i = 0; i < relicPrefabs.Count; i++)
        {
            GameObject prefab = relicPrefabs[i];
            if (prefab != null && IsRelicAvailable(prefab))
            {
                available.Add(prefab);
            }
        }

        if (available.Count == 0)
        {
            return null;
        }

        return available[Random.Range(0, available.Count)];
    }

    public static GameObject SpawnAvailableRelic(IList<GameObject> relicPrefabs, Vector3 position, Transform parent = null)
    {
        GameObject prefab = PickAvailableRelicPrefab(relicPrefabs);
        if (prefab == null)
        {
            return null;
        }

        return Object.Instantiate(prefab, position, Quaternion.identity, parent);
    }

    public static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        return Object.Instantiate(prefab, position, Quaternion.identity, parent);
    }
}
