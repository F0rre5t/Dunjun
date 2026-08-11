using UnityEngine;

public class LootDropper : MonoBehaviour
{
    [System.Serializable]
    public class LootEntry
    {
        public GameObject prefab;
        public float weight = 1f;
    }

    [Header("Consumable / Gold Table")]
    [Range(0f, 1f)]
    public float dropChance = 0.3f;
    public LootEntry[] lootTable;

    [Header("Optional Relic Chance")]
    [Range(0f, 1f)]
    
    public float relicDropChance;
    public GameObject[] relicPrefabs;

    public void TryDrop(Vector3 position)
    {
        TryDropFromTable(position);
        TryDropRelic(position);
    }

    void TryDropFromTable(Vector3 position)
    {
        if (lootTable == null || lootTable.Length == 0)
        {
            return;
        }

        DifficultyDirector director = DifficultyDirector.Ensure();
        float chance = dropChance * director.GetDropChanceMultiplier();
        if (Random.value > chance)
        {
            return;
        }

        GameObject prefab = PickWeightedPrefab(lootTable, director.GetPotionWeightMultiplier());
        LootUtility.SpawnPrefab(prefab, position);
    }

    void TryDropRelic(Vector3 position)
    {
        if (relicDropChance <= 0f || relicPrefabs == null || relicPrefabs.Length == 0)
        {
            return;
        }

        if (Random.value > relicDropChance)
        {
            return;
        }

        LootUtility.SpawnAvailableRelic(relicPrefabs, position);
    }

    static GameObject PickWeightedPrefab(LootEntry[] table, float potionWeightMultiplier)
    {
        float totalWeight = 0f;
        float[] effective = new float[table.Length];
        for (int i = 0; i < table.Length; i++)
        {
            float weight = Mathf.Max(0f, table[i].weight);
            if (IsPotionPrefab(table[i].prefab))
            {
                weight *= Mathf.Max(0f, potionWeightMultiplier);
            }

            effective[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < table.Length; i++)
        {
            float weight = effective[i];
            if (roll <= weight)
            {
                return table[i].prefab;
            }

            roll -= weight;
        }

        return null;
    }

    static bool IsPotionPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        if (prefab.name.IndexOf("HealthPotion", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        ConsumablePickup pickup = prefab.GetComponent<ConsumablePickup>();
        if (pickup == null)
        {
            pickup = prefab.GetComponentInChildren<ConsumablePickup>(true);
        }

        if (pickup == null || pickup.ConsumableData == null)
        {
            return false;
        }

        return pickup.ConsumableData.healAmount > 0;
    }
}
