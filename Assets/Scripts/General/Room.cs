using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

[System.Serializable]
public class EncounterEnemySlot
{
    public GameObject prefab;
    [Min(0)] public int minCount;
    [Min(0)] public int maxCount = 1;
}

[System.Serializable]
public class RoomEncounter
{
    
    public int minStep = 1;
    
    public int maxStep = 1;

    public EncounterEnemySlot[] composition;

    [Header("Legacy Random Pool")]
    public GameObject[] enemyPool;
    public int minEnemies = 1;
    public int maxEnemies = 3;
}

public class Room : MonoBehaviour
{
    public const string BossDoorMarkerName = "BossDoorMarker";

    public static event System.Action<Room> RoomCleared;

    public GameObject doorLeft, doorRight, doorUp, doorDown;
    public bool roomLeft, roomRight, roomUp, roomDown;

    public int stepToStart;
    public Text step;

    [Header("Transition Settings")]
    public float doorCloseDelay = 0.5f;

    [Header("Enemy Encounters")]
    public List<RoomEncounter> encounters;
    public GameObject[] fallbackEnemyPool;
    public int fallbackMin = 1;
    public int fallbackMax = 2;

    [Header("Fixed Spawn Override")]
    public GameObject fixedEnemyPrefab;
    public int fixedEnemyCount = 1;

    [Header("Key Drop")]
    public GameObject keyDropPrefab;

    [Header("Loot Control")]
    public bool suppressLootOnLastEnemy = true;

    [Header("Post-Clear Chest")]
    
    [Range(0f, 1f)] public float chestSpawnChance = 0.2f;
    
    [Range(0f, 1f)] public float mimicChance = 0.2f;
    public GameObject chestPrefab;
    public GameObject mimicPrefab;
    
    public float chestSpawnOffsetRadius = 0.75f;

    [Header("Player Spawn")]
    
    public Transform playerSpawnPoint;

    [Header("Shop")]
    [SerializeField] Color shopRoomTint = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] float shopOfferSpacing = 2f;
    [SerializeField] float shopOfferYOffset;

    [Header("Spike Traps (by step, like enemy encounters)")]
    
    public GameObject spikeTrapPrefab;
    
    public List<RoomSpikeEncounter> spikeEncounters;
    public bool allowSpikesInBossRoom;
    public bool allowSpikesInShopRoom;

    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();
    private bool isEndRoom;
    private bool isShopRoom;
    private bool hasClearedBefore;
    private bool isReplayCombat;
    private GameObject[] shopRelicPrefabs;
    private bool shopOffersSpawned;
    private SpikeTrapPattern spikePattern;

    public bool IsEndRoom => isEndRoom;
    public bool IsShopRoom => isShopRoom;

    private void Awake()
    {
        ResolveSpawnPoint();

        // Depth is shown on the top HUD now; hide the old center-room step number.
        if (step != null)
        {
            step.gameObject.SetActive(false);
        }
    }

    void ResolveSpawnPoint()
    {
        if (playerSpawnPoint != null)
        {
            return;
        }

        Transform namedSpawn = transform.Find("PlayerSpawnPoint");
        if (namedSpawn != null)
        {
            playerSpawnPoint = namedSpawn;
            return;
        }

        Transform roomArea = transform.Find("RoomArea");
        if (roomArea != null)
        {
            playerSpawnPoint = roomArea;
        }
    }

    public Vector3 GetSpawnPosition()
    {
        ResolveSpawnPoint();
        return playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        ResolveSpawnPoint();
        Vector3 pos = GetSpawnPosition();
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, 0.35f);
        Gizmos.DrawLine(pos, pos + Vector3.up * 0.6f);
    }
#endif

    private Coroutine pendingSpawnRoutine;

    private void OnEnable()
    {
        KeyInventory.KeyCollected += HandleKeyCollected;
        KeyInventory.RoomChanged += HandleRoomEntered;
    }

    private void OnDisable()
    {
        KeyInventory.KeyCollected -= HandleKeyCollected;
        KeyInventory.RoomChanged -= HandleRoomEntered;
    }

    public void UpdateDoors()
    {
        if (doorLeft != null) doorLeft.SetActive(roomLeft);
        if (doorRight != null) doorRight.SetActive(roomRight);
        if (doorUp != null) doorUp.SetActive(roomUp);
        if (doorDown != null) doorDown.SetActive(roomDown);
    }

    public void UpdateRooms(int steps)
    {
        stepToStart = steps;
        if (step != null) step.text = stepToStart.ToString();
    }

    public void SetFixedSpawn(GameObject prefab, int count = 1)
    {
        fixedEnemyPrefab = prefab;
        fixedEnemyCount = Mathf.Max(1, count);
    }

    public void SetAsEndRoom(bool value)
    {
        isEndRoom = value;
    }

    public void ConfigureAsShop(GameObject[] relicPrefabs, float offerSpacing)
    {
        isShopRoom = true;
        shopRelicPrefabs = relicPrefabs;
        shopOfferSpacing = Mathf.Max(0.5f, offerSpacing);
        fixedEnemyPrefab = null;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = shopRoomTint;
        }

        if (step != null)
        {
            step.text = $"{stepToStart}\nShop";
        }

        // Shop exits stay open; picking / buying is optional.
        ApplyDoorState(false);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (CameraController.instance.target != transform)
            {
                NotifyPreviousRoomLostFocus();
                CameraController.instance.Changetarget(transform);

                // Clear neighbour boss markers once the camera enters the boss room.
                if (isEndRoom)
                {
                    ClearAllBossDoorMarkers();
                }

                if (isShopRoom)
                {
                    EnterShopRoom();
                    return;
                }

                ScheduleSpawn();
            }
        }
    }

    void NotifyPreviousRoomLostFocus()
    {
        if (CameraController.instance == null || CameraController.instance.target == null)
        {
            return;
        }

        Room previous = CameraController.instance.target.GetComponent<Room>();
        if (previous != null && previous != this)
        {
            previous.OnLostFocus();
        }
    }

    void OnLostFocus()
    {
        // Clear unsold shop relics when leaving so players cannot farm and come back.
        if (isShopRoom)
        {
            DespawnShopOffers(allowRespawn: false);
        }
    }

    void DespawnShopOffers(bool allowRespawn)
    {
        ShopOfferGroup[] groups = GetComponentsInChildren<ShopOfferGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null)
            {
                Destroy(groups[i].gameObject);
            }
        }

        Transform offers = transform.Find("ShopOffers");
        if (offers != null)
        {
            Destroy(offers.gameObject);
        }

        shopOffersSpawned = !allowRespawn;
    }

    void EnterShopRoom()
    {
        if (pendingSpawnRoutine != null)
        {
            StopCoroutine(pendingSpawnRoutine);
            pendingSpawnRoutine = null;
        }

        KeyInventory.ResetWithoutClosingDoors();
        ApplyDoorState(false);
        SpawnShopOffersIfNeeded();
    }

    private void ScheduleSpawn()
    {
        if (pendingSpawnRoutine != null)
        {
            StopCoroutine(pendingSpawnRoutine);
        }

        pendingSpawnRoutine = StartCoroutine(DelayedRoomTransition());
    }

    private IEnumerator DelayedRoomTransition()
    {
        yield return new WaitForSeconds(doorCloseDelay);

        if (isShopRoom)
        {
            EnterShopRoom();
            yield break;
        }

        KeyInventory.Reset();
        SpawnEnemies();
        SpawnSpikeTraps();
        pendingSpawnRoutine = null;
    }

    private void ClearExistingEnemies()
    {
        Enemy[] existing = GetComponentsInChildren<Enemy>(true);
        foreach (Enemy enemy in existing)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        activeEnemies.Clear();
    }

    void ClearSpikeTraps()
    {
        if (spikePattern != null)
        {
            spikePattern.ClearSpawned();
        }
    }

    void DisarmSpikeTraps()
    {
        if (spikePattern != null)
        {
            spikePattern.DisarmAll();
        }
    }

    void SpawnSpikeTraps()
    {
        ClearSpikeTraps();

        if (spikeTrapPrefab == null || spikeEncounters == null || spikeEncounters.Count == 0)
        {
            return;
        }

        if (isShopRoom && !allowSpikesInShopRoom)
        {
            return;
        }

        if (isEndRoom && !allowSpikesInBossRoom)
        {
            return;
        }

        RoomSpikeEncounter band = FindSpikeEncounterForStep(stepToStart);
        if (band == null || band.spawnChance <= 0f)
        {
            return;
        }

        float spawnChance = band.spawnChance * DifficultyDirector.Ensure().GetSpikeSpawnMultiplier();
        if (UnityEngine.Random.value > spawnChance)
        {
            return;
        }

        if (!TryPickSpikeShape(band, out SpikeTrapPattern.Shape shape))
        {
            return;
        }

        SpikeTrapPattern pattern = EnsureSpikePattern();
        pattern.spikePrefab = spikeTrapPrefab;
        pattern.spawnOnAwake = false;
        pattern.ApplyShapeDefaults(shape, band.countOverride);

        if (band.radiusOverride > 0f)
        {
            pattern.radius = band.radiusOverride;
        }

        if (band.randomizeLineOrientation
            && (shape == SpikeTrapPattern.Shape.Line || shape == SpikeTrapPattern.Shape.TwinRails))
        {
            pattern.vertical = UnityEngine.Random.value < 0.5f;
        }

        pattern.ClearAndRespawn();
    }

    SpikeTrapPattern EnsureSpikePattern()
    {
        if (spikePattern != null)
        {
            return spikePattern;
        }

        Transform existing = transform.Find("SpikePattern");
        if (existing != null)
        {
            spikePattern = existing.GetComponent<SpikeTrapPattern>();
            if (spikePattern != null)
            {
                return spikePattern;
            }
        }

        GameObject host = new GameObject("SpikePattern");
        host.transform.SetParent(transform, false);
        host.transform.localPosition = Vector3.zero;
        spikePattern = host.AddComponent<SpikeTrapPattern>();
        spikePattern.spawnOnAwake = false;
        return spikePattern;
    }

    RoomSpikeEncounter FindSpikeEncounterForStep(int step)
    {
        if (spikeEncounters == null || spikeEncounters.Count == 0)
        {
            return null;
        }

        RoomSpikeEncounter best = null;
        int bestSpan = int.MaxValue;

        for (int i = 0; i < spikeEncounters.Count; i++)
        {
            RoomSpikeEncounter encounter = spikeEncounters[i];
            if (encounter == null)
            {
                continue;
            }

            int min = Mathf.Min(encounter.minStep, encounter.maxStep);
            int max = Mathf.Max(encounter.minStep, encounter.maxStep);
            if (step < min || step > max)
            {
                continue;
            }

            int span = max - min;
            if (best == null || span < bestSpan)
            {
                best = encounter;
                bestSpan = span;
            }
        }

        return best;
    }

    static bool TryPickSpikeShape(RoomSpikeEncounter band, out SpikeTrapPattern.Shape shape)
    {
        shape = SpikeTrapPattern.Shape.Cross;
        if (band == null || band.shapes == null || band.shapes.Length == 0)
        {
            return false;
        }

        DifficultyDirector director = DifficultyDirector.Ensure();
        float total = 0f;
        float[] effective = new float[band.shapes.Length];
        for (int i = 0; i < band.shapes.Length; i++)
        {
            SpikeShapeWeight entry = band.shapes[i];
            if (entry == null || entry.weight <= 0f)
            {
                effective[i] = 0f;
                continue;
            }

            float weight = entry.weight * director.GetSpikeShapeWeightMultiplier(entry.shape);
            effective[i] = Mathf.Max(0f, weight);
            total += effective[i];
        }

        if (total <= 0f)
        {
            return false;
        }

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < band.shapes.Length; i++)
        {
            float weight = effective[i];
            if (weight <= 0f)
            {
                continue;
            }

            cumulative += weight;
            if (roll <= cumulative)
            {
                shape = band.shapes[i].shape;
                return true;
            }
        }

        for (int i = band.shapes.Length - 1; i >= 0; i--)
        {
            if (effective[i] > 0f)
            {
                shape = band.shapes[i].shape;
                return true;
            }
        }

        return false;
    }

    private void SpawnEnemies()
    {
        ClearExistingEnemies();

        // Revisited rooms still fight, but only drop a key on clear.
        isReplayCombat = hasClearedBefore;

        if (isShopRoom)
        {
            ApplyDoorState(false);
            SpawnShopOffersIfNeeded();
            return;
        }

        DifficultyDirector.Ensure().NotifyCombatRoomStarted(stepToStart);

        if (fixedEnemyPrefab != null)
        {
            SpawnPrefabList(CreateRepeatedList(fixedEnemyPrefab, fixedEnemyCount));
            if (activeEnemies.Count == 0)
            {
                DifficultyDirector.Ensure().NotifyCombatRoomEnded();
            }

            return;
        }

        RoomEncounter match = FindEncounterForStep(stepToStart);
        if (match != null && HasComposition(match))
        {
            SpawnPrefabList(BuildCompositionSpawnList(match.composition));
            if (activeEnemies.Count == 0)
            {
                DifficultyDirector.Ensure().NotifyCombatRoomEnded();
            }

            return;
        }

        GameObject[] poolToUse = (match != null && match.enemyPool != null && match.enemyPool.Length > 0)
            ? match.enemyPool
            : fallbackEnemyPool;
        int numToSpawn = (match != null && match.enemyPool != null && match.enemyPool.Length > 0)
            ? UnityEngine.Random.Range(match.minEnemies, match.maxEnemies + 1)
            : UnityEngine.Random.Range(fallbackMin, fallbackMax + 1);

        if (poolToUse == null || poolToUse.Length == 0 || numToSpawn <= 0)
        {
            ApplyDoorState(false);
            DifficultyDirector.Ensure().NotifyCombatRoomEnded();
            return;
        }

        List<GameObject> randomList = new List<GameObject>(numToSpawn);
        for (int i = 0; i < numToSpawn; i++)
        {
            randomList.Add(poolToUse[UnityEngine.Random.Range(0, poolToUse.Length)]);
        }

        SpawnPrefabList(randomList);
        if (activeEnemies.Count == 0)
        {
            DifficultyDirector.Ensure().NotifyCombatRoomEnded();
        }
    }

    static bool HasComposition(RoomEncounter encounter)
    {
        if (encounter == null || encounter.composition == null || encounter.composition.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < encounter.composition.Length; i++)
        {
            EncounterEnemySlot slot = encounter.composition[i];
            if (slot != null && slot.prefab != null && slot.maxCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    static List<GameObject> BuildCompositionSpawnList(EncounterEnemySlot[] slots)
    {
        List<GameObject> list = new List<GameObject>();
        if (slots == null)
        {
            return list;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            EncounterEnemySlot slot = slots[i];
            if (slot == null || slot.prefab == null)
            {
                continue;
            }

            int min = Mathf.Max(0, Mathf.Min(slot.minCount, slot.maxCount));
            int max = Mathf.Max(0, Mathf.Max(slot.minCount, slot.maxCount));
            int count = UnityEngine.Random.Range(min, max + 1);
            for (int n = 0; n < count; n++)
            {
                list.Add(slot.prefab);
            }
        }

        return list;
    }

    static List<GameObject> CreateRepeatedList(GameObject prefab, int count)
    {
        List<GameObject> list = new List<GameObject>();
        if (prefab == null)
        {
            return list;
        }

        int safeCount = Mathf.Max(0, count);
        for (int i = 0; i < safeCount; i++)
        {
            list.Add(prefab);
        }

        return list;
    }

    RoomEncounter FindEncounterForStep(int step)
    {
        if (encounters == null || encounters.Count == 0)
        {
            return null;
        }

        RoomEncounter best = null;
        int bestSpan = int.MaxValue;

        for (int i = 0; i < encounters.Count; i++)
        {
            RoomEncounter encounter = encounters[i];
            if (encounter == null)
            {
                continue;
            }

            int min = Mathf.Min(encounter.minStep, encounter.maxStep);
            int max = Mathf.Max(encounter.minStep, encounter.maxStep);
            if (step < min || step > max)
            {
                continue;
            }

            int span = max - min;
            if (best == null || span < bestSpan)
            {
                best = encounter;
                bestSpan = span;
            }
        }

        return best;
    }

    void SpawnShopOffersIfNeeded()
    {
        if (shopOffersSpawned)
        {
            return;
        }

        shopOffersSpawned = true;

        // Filter against currently owned relics at the moment the player enters.
        List<GameObject> choices = BuildShopChoices(3);
        if (choices.Count == 0)
        {
            Debug.LogWarning($"Shop room at step {stepToStart} has no available relics to offer.");
            return;
        }

        Transform offerRoot = new GameObject("ShopOffers").transform;
        offerRoot.SetParent(transform, false);
        offerRoot.localPosition = Vector3.zero;

        ShopOfferGroup group = offerRoot.gameObject.AddComponent<ShopOfferGroup>();
        Vector3 center = transform.position + new Vector3(0f, shopOfferYOffset, 0f);
        int count = choices.Count;

        for (int i = 0; i < count; i++)
        {
            float xOffset = (i - (count - 1) * 0.5f) * shopOfferSpacing;
            Vector3 spawnPos = center + new Vector3(xOffset, 0f, 0f);
            RelicPickup pickup = CreateShopPickup(choices[i], spawnPos, offerRoot);
            if (pickup != null)
            {
                group.Register(pickup);
                ShopRelicLabel.Attach(pickup);
            }
        }
    }

    List<GameObject> BuildShopChoices(int count)
    {
        List<GameObject> available = new List<GameObject>();
        if (shopRelicPrefabs != null)
        {
            for (int i = 0; i < shopRelicPrefabs.Length; i++)
            {
                GameObject prefab = shopRelicPrefabs[i];
                if (LootUtility.IsRelicAvailable(prefab))
                {
                    available.Add(prefab);
                }
            }
        }

        for (int i = available.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            GameObject temp = available[i];
            available[i] = available[swapIndex];
            available[swapIndex] = temp;
        }

        if (available.Count > count)
        {
            available.RemoveRange(count, available.Count - count);
        }

        return available;
    }

    RelicPickup CreateShopPickup(GameObject relicPrefab, Vector3 position, Transform parent)
    {
        if (relicPrefab == null)
        {
            return null;
        }

        // Instantiate the authored pickup prefab so each relic keeps its designed scale.
        GameObject instance = Instantiate(relicPrefab, position, Quaternion.identity, parent);
        RelicPickup pickup = instance.GetComponent<RelicPickup>();
        if (pickup == null)
        {
            pickup = instance.GetComponentInChildren<RelicPickup>(true);
        }

        if (pickup == null)
        {
            Debug.LogWarning($"Shop relic prefab '{relicPrefab.name}' is missing RelicPickup.");
            Destroy(instance);
            return null;
        }

        return pickup;
    }

    void SpawnPrefabList(List<GameObject> prefabs)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            ApplyDoorState(false);
            return;
        }

        List<GameObject> spawnedObjects = new List<GameObject>();
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject enemyPrefab = prefabs[i];
            if (enemyPrefab == null)
            {
                continue;
            }

            Vector3 spawnPos = FindValidEnemySpawnPosition(enemyPrefab);
            GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);
            spawnedObjects.Add(enemyObj);
        }

        foreach (GameObject obj in spawnedObjects)
        {
            Enemy[] enemies = obj.GetComponentsInChildren<Enemy>(true);
            foreach (Enemy enemy in enemies)
            {
                enemy.SetRoom(this);
                activeEnemies.Add(enemy);
            }
        }

        if (activeEnemies.Count == 0)
        {
            ApplyDoorState(false);
        }
    }

    public void OnEnemyDied(Enemy enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            return;
        }

        activeEnemies.Remove(enemy);

        if (activeEnemies.Count == 0)
        {
            // Keep isReplayCombat until loot resolves.
            // Die() drops loot after NotifyDeath; clearing the flag early would leak loot.
            bool replayClear = isReplayCombat;
            hasClearedBefore = true;

            // Retract spikes after clear so the player can leave safely.
            DisarmSpikeTraps();
            DifficultyDirector.Ensure().NotifyCombatRoomEnded();

            RoomCleared?.Invoke(this);

            if (isEndRoom)
            {
                if (GameFlowController.Instance != null)
                {
                    GameFlowController.Instance.ShowVictory();
                }

                return;
            }

            Vector3 dropPos = enemy != null ? enemy.transform.position : transform.position;
            if (keyDropPrefab != null)
            {
                Instantiate(keyDropPrefab, dropPos, Quaternion.identity);
            }
            else
            {
                ApplyDoorState(false);
            }

            // Replay clears only drop a key, never chests.
            if (!replayClear)
            {
                TrySpawnPostClearChest(dropPos);
            }
        }
    }

    void TrySpawnPostClearChest(Vector3 nearPosition)
    {
        if (isShopRoom || isEndRoom)
        {
            return;
        }

        if (chestSpawnChance <= 0f || Random.value > chestSpawnChance)
        {
            return;
        }

        bool spawnMimic = mimicChance > 0f
            && mimicPrefab != null
            && Random.value <= mimicChance;
        GameObject prefab = spawnMimic ? mimicPrefab : chestPrefab;
        if (prefab == null)
        {
            return;
        }

        Vector3 spawnPos = nearPosition;
        if (chestSpawnOffsetRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * chestSpawnOffsetRadius;
            spawnPos += new Vector3(offset.x, offset.y, 0f);
        }

        // Chests are optional props and do not gate room clear.
        Instantiate(prefab, spawnPos, Quaternion.identity, transform);
    }

    private void HandleRoomEntered()
    {
        if (isShopRoom)
        {
            ApplyDoorState(false);
            return;
        }

        ApplyDoorState(true);
    }

    private void HandleKeyCollected()
    {
        ApplyDoorState(false);
    }

    private void ApplyDoorState(bool showDoors)
    {
        if (roomLeft && doorLeft != null) doorLeft.SetActive(showDoors);
        if (roomRight && doorRight != null) doorRight.SetActive(showDoors);
        if (roomUp && doorUp != null) doorUp.SetActive(showDoors);
        if (doorDown != null && roomDown) doorDown.SetActive(showDoors);
    }

    static void ClearAllBossDoorMarkers()
    {
        Room[] allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        for (int i = 0; i < allRooms.Length; i++)
        {
            if (allRooms[i] != null)
            {
                allRooms[i].ClearBossDoorMarkers();
            }
        }
    }

    void ClearBossDoorMarkers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == BossDoorMarkerName)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void Start()
    {
        if (stepToStart == 0)
        {
            Invoke(nameof(ForceInitialDoors), 0.1f);
        }
    }

    private void ForceInitialDoors()
    {
        ApplyDoorState(true);
        ScheduleSpawn();
    }

    public bool ShouldDropLootForEnemy()
    {
        // Keep replay flag for the whole fight so the last hit still skips resource drops.
        if (isReplayCombat)
        {
            return false;
        }

        if (!suppressLootOnLastEnemy) return true;
        return activeEnemies.Count > 0;
    }

    private Vector3 FindValidEnemySpawnPosition(GameObject enemyPrefab)
    {
        const int maxAttempts = 16;
        const float spawnRadius = 1.5f;
        Vector2 footprint = GetEnemySpawnFootprint(enemyPrefab);

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = transform.position + new Vector3(
                UnityEngine.Random.Range(-spawnRadius, spawnRadius),
                UnityEngine.Random.Range(-spawnRadius, spawnRadius),
                0f);

            if (IsEnemySpawnClear(candidate, footprint))
            {
                return candidate;
            }
        }

        return transform.position;
    }

    private static Vector2 GetEnemySpawnFootprint(GameObject enemyPrefab)
    {
        if (enemyPrefab == null) return Vector2.one;

        Collider2D col = enemyPrefab.GetComponent<Collider2D>();
        if (col == null) return Vector2.one;

        Vector3 scale = enemyPrefab.transform.localScale;
        if (col is BoxCollider2D box)
        {
            return Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        }

        return Vector2.one * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * 0.5f;
    }

    private bool IsEnemySpawnClear(Vector3 position, Vector2 footprint)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(position, footprint, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger) continue;
            if (hit is TilemapCollider2D) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            return false;
        }

        return true;
    }
}
