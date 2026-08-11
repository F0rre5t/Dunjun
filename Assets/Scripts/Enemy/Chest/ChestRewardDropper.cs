using UnityEngine;

public class ChestRewardDropper : MonoBehaviour
{
    [Header("Guaranteed Relic")]
    
    public GameObject[] relicPrefabs;

    public GameObject fallbackPrefab;

    public void DropReward(Vector3 position)
    {
        if (LootUtility.SpawnAvailableRelic(relicPrefabs, position) != null)
        {
            return;
        }

        LootUtility.SpawnPrefab(fallbackPrefab, position);
    }
}
