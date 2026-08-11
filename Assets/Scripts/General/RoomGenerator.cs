using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds a connected room graph, then marks depth, boss room, shops, and door walls.
[DefaultExecutionOrder(-100)]
public class RoomGenerator : MonoBehaviour
{
    public enum Direction { Up, Right, Down, Left };
    public Direction direction;

    [Header("Room Info")]
    public GameObject roomPrefab;
    public int roomNumber;
    public Color startColor, endColor;
    private GameObject endRoom;

    [Header("Position Control")]
    public Transform generatorPoint;
    public float xoffset;
    public float yoffset;

    public List<Room> rooms = new List<Room>();
    private HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
    private List<Vector2Int> roomPositions = new List<Vector2Int>();
    private Vector2Int currentGridPosition;

    public WallTypes wallType;

    [Header("Player Spawn")]
    public GameBootstrap gameBootstrap;

    [Header("Fixed Room Spawns")]
    public GameObject minotaurPrefab;
    public GameObject goblinKingPrefab;

    [Header("Shop Rooms")]
    public bool forceSecondRoomAsShop;
    public float[] shopDepthPercents = { 0.3f, 0.6f, 0.9f };
    public GameObject[] shopRelicPrefabs;
    public float shopOfferSpacing = 2f;

    [Header("Play Mode")]
    public bool useMenuFlow;

    [Header("Boss Door Marker")]
    public GameObject bossDoorMarkerPrefab;
    public Vector2 bossMarkerOffsetUp = new Vector2(0f, 5f);
    public Vector2 bossMarkerOffsetDown = new Vector2(0f, -5f);
    public Vector2 bossMarkerOffsetLeft = new Vector2(-9f, 0.14f);
    public Vector2 bossMarkerOffsetRight = new Vector2(9f, 0.14f);

    public Room StartRoom => rooms.Count > 0 ? rooms[0] : null;
    public Room EndRoom => endRoom != null ? endRoom.GetComponent<Room>() : null;

    void Start()
    {
        if (!useMenuFlow)
        {
            Generate(Mathf.Max(1, roomNumber));
        }
    }

    public void Generate(int count)
    {
        if (rooms.Count > 0)
        {
            Debug.LogWarning("RoomGenerator: Generate called more than once this session.");
            return;
        }

        RunBossTarget.Clear();
        roomNumber = Mathf.Max(1, count);
        currentGridPosition = Vector2Int.zero;
        occupiedPositions.Clear();
        roomPositions.Clear();
        occupiedPositions.Add(currentGridPosition);

        Room firstRoom = Instantiate(roomPrefab, generatorPoint.position, generatorPoint.rotation).GetComponent<Room>();
        rooms.Add(firstRoom);
        roomPositions.Add(currentGridPosition);

        // Random walk: keep stepping into empty neighbour cells until we hit the room count.
        for (int i = 1; i < roomNumber; i++)
        {
            while (!ChangePointPosition())
            {
                int randomIndex = Random.Range(0, roomPositions.Count);
                currentGridPosition = roomPositions[randomIndex];
                generatorPoint.position = rooms[randomIndex].transform.position;
            }

            Room newRoom = Instantiate(roomPrefab, generatorPoint.position, generatorPoint.rotation).GetComponent<Room>();
            rooms.Add(newRoom);
            roomPositions.Add(currentGridPosition);
        }

        SetupAllRooms();

        rooms[0].GetComponent<SpriteRenderer>().color = startColor;

        // Farthest room by BFS becomes the boss room.
        int farthestIndex = FindFarthestRoomBFS();
        endRoom = rooms[farthestIndex].gameObject;
        endRoom.GetComponent<SpriteRenderer>().color = endColor;
        SetupFixedRoomSpawns(rooms[farthestIndex]);
        AssignShopRooms();
        PlaceBossDoorMarkers(farthestIndex);

        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (rooms.Count == 0)
        {
            return;
        }

        if (gameBootstrap == null)
        {
            gameBootstrap = FindAnyObjectByType<GameBootstrap>();
        }

        if (gameBootstrap != null)
        {
            gameBootstrap.SpawnPlayerAtRoom(rooms[0]);
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = rooms[0].GetSpawnPosition();
            if (CameraController.instance != null)
            {
                CameraController.instance.Changetarget(rooms[0].transform);
                CameraController.instance.SnapToTarget();
            }
        }
    }

    // Try one step into a free neighbour. Returns false if the walk is stuck.
    bool ChangePointPosition()
    {
        List<Direction> availableDirections = new List<Direction>();

        if (!occupiedPositions.Contains(currentGridPosition + Vector2Int.up))
            availableDirections.Add(Direction.Up);
        if (!occupiedPositions.Contains(currentGridPosition + Vector2Int.right))
            availableDirections.Add(Direction.Right);
        if (!occupiedPositions.Contains(currentGridPosition + Vector2Int.down))
            availableDirections.Add(Direction.Down);
        if (!occupiedPositions.Contains(currentGridPosition + Vector2Int.left))
            availableDirections.Add(Direction.Left);

        if (availableDirections.Count == 0)
        {
            return false;
        }

        direction = availableDirections[Random.Range(0, availableDirections.Count)];

        switch (direction)
        {
            case Direction.Up:
                currentGridPosition += Vector2Int.up;
                generatorPoint.position += new Vector3(0, yoffset, 0);
                break;
            case Direction.Right:
                currentGridPosition += Vector2Int.right;
                generatorPoint.position += new Vector3(xoffset, 0, 0);
                break;
            case Direction.Down:
                currentGridPosition += Vector2Int.down;
                generatorPoint.position += new Vector3(0, -yoffset, 0);
                break;
            case Direction.Left:
                currentGridPosition += Vector2Int.left;
                generatorPoint.position += new Vector3(-xoffset, 0, 0);
                break;
        }

        occupiedPositions.Add(currentGridPosition);
        return true;
    }

    void SetupAllRooms()
    {
        Dictionary<Vector2Int, int> distances = CalculateAllDistances();

        for (int i = 0; i < rooms.Count; i++)
        {
            Vector2Int pos = roomPositions[i];
            Room room = rooms[i];

            room.roomUp = occupiedPositions.Contains(pos + Vector2Int.up);
            room.roomDown = occupiedPositions.Contains(pos + Vector2Int.down);
            room.roomLeft = occupiedPositions.Contains(pos + Vector2Int.left);
            room.roomRight = occupiedPositions.Contains(pos + Vector2Int.right);

            room.UpdateDoors();
            room.UpdateRooms(distances[pos]);
            SetupWalls(room);
        }
    }

    // Door layout is packed as bits: Up=8, Down=4, Left=2, Right=1.
    void SetupWalls(Room room)
    {
        int doorConfig = (room.roomUp ? 8 : 0) |
                         (room.roomDown ? 4 : 0) |
                         (room.roomLeft ? 2 : 0) |
                         (room.roomRight ? 1 : 0);

        GameObject wallPrefab = null;

        switch (doorConfig)
        {
            case 0:
                break;

            case 1:
                wallPrefab = wallType.WallR;
                break;
            case 2:
                wallPrefab = wallType.WallL;
                break;
            case 4:
                wallPrefab = wallType.WallD;
                break;
            case 8:
                wallPrefab = wallType.WallU;
                break;

            case 3:
                wallPrefab = wallType.WallRL;
                break;
            case 12:
                wallPrefab = wallType.WallUD;
                break;
            case 9:
                wallPrefab = wallType.WallUR;
                break;
            case 10:
                wallPrefab = wallType.WallUL;
                break;
            case 5:
                wallPrefab = wallType.WallDR;
                break;
            case 6:
                wallPrefab = wallType.WallDL;
                break;

            case 7:
                wallPrefab = wallType.WallDRL;
                break;
            case 11:
                wallPrefab = wallType.WallURL;
                break;
            case 13:
                wallPrefab = wallType.WallUDR;
                break;
            case 14:
                wallPrefab = wallType.WallUDL;
                break;

            case 15:
                wallPrefab = wallType.WallFour;
                break;
        }

        if (wallPrefab != null)
        {
            Instantiate(wallPrefab, room.transform.position, Quaternion.identity, room.transform);
        }
    }

    // BFS distance from the start room for every cell.
    Dictionary<Vector2Int, int> CalculateAllDistances()
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int startPos = roomPositions[0];
        queue.Enqueue(startPos);
        distances[startPos] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distances[current];

            Vector2Int[] neighbors = new Vector2Int[]
            {
                current + Vector2Int.up,
                current + Vector2Int.right,
                current + Vector2Int.down,
                current + Vector2Int.left
            };

            foreach (Vector2Int neighbor in neighbors)
            {
                if (occupiedPositions.Contains(neighbor) && !distances.ContainsKey(neighbor))
                {
                    distances[neighbor] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return distances;
    }

    void SetupFixedRoomSpawns(Room farthestRoom)
    {
        if (farthestRoom == null)
        {
            return;
        }

        farthestRoom.SetAsEndRoom(true);

        GameObject bossPrefab = PickRandomBossPrefab();
        RunBossTarget.Set(bossPrefab);

        if (bossPrefab != null)
        {
            farthestRoom.SetFixedSpawn(bossPrefab);
        }
        else
        {
            Debug.LogWarning("RoomGenerator: no boss prefab assigned (Minotaur / Goblin King).");
            RunBossTarget.Clear();
        }
    }

    // Markers sit in the neighbour room, in front of the door that leads to the boss.
    void PlaceBossDoorMarkers(int endRoomIndex)
    {
        if (bossDoorMarkerPrefab == null || endRoomIndex < 0 || endRoomIndex >= rooms.Count)
        {
            return;
        }

        Vector2Int endPos = roomPositions[endRoomIndex];
        TryPlaceBossDoorMarker(endPos + Vector2Int.down, bossMarkerOffsetUp);
        TryPlaceBossDoorMarker(endPos + Vector2Int.up, bossMarkerOffsetDown);
        TryPlaceBossDoorMarker(endPos + Vector2Int.right, bossMarkerOffsetLeft);
        TryPlaceBossDoorMarker(endPos + Vector2Int.left, bossMarkerOffsetRight);
    }

    void TryPlaceBossDoorMarker(Vector2Int neighborPos, Vector2 roomLocalOffset)
    {
        int neighborIndex = roomPositions.IndexOf(neighborPos);
        if (neighborIndex < 0)
        {
            return;
        }

        PlaceBossDoorMarker(rooms[neighborIndex], roomLocalOffset);
    }

    void PlaceBossDoorMarker(Room room, Vector2 roomLocalOffset)
    {
        if (room == null)
        {
            return;
        }

        GameObject marker = Instantiate(bossDoorMarkerPrefab, room.transform);
        marker.name = Room.BossDoorMarkerName;
        marker.transform.localPosition = new Vector3(roomLocalOffset.x, roomLocalOffset.y, 0f);
        marker.transform.localRotation = Quaternion.identity;
    }

    GameObject PickRandomBossPrefab()
    {
        bool hasMinotaur = minotaurPrefab != null;
        bool hasGoblinKing = goblinKingPrefab != null;

        if (hasMinotaur && hasGoblinKing)
        {
            return Random.value < 0.5f ? minotaurPrefab : goblinKingPrefab;
        }

        if (hasMinotaur)
        {
            return minotaurPrefab;
        }

        if (hasGoblinKing)
        {
            return goblinKingPrefab;
        }

        return null;
    }

    // Place shops near configured depth percents of the longest path.
    void AssignShopRooms()
    {
        if (shopRelicPrefabs == null || shopRelicPrefabs.Length == 0)
        {
            Debug.LogWarning("RoomGenerator: shopRelicPrefabs is empty; shop rooms will have nothing to offer.");
        }

        HashSet<Room> claimed = new HashSet<Room>();

        if (forceSecondRoomAsShop && rooms.Count > 1)
        {
            Room second = rooms[1];
            if (!second.IsEndRoom)
            {
                claimed.Add(second);
                second.ConfigureAsShop(shopRelicPrefabs, shopOfferSpacing);
            }

            return;
        }

        if (shopDepthPercents == null || shopDepthPercents.Length == 0)
        {
            return;
        }

        int maxStep = GetMaxStepDistance();
        if (maxStep <= 1)
        {
            Debug.LogWarning("RoomGenerator: map too small to place shops by depth percent.");
            return;
        }

        for (int i = 0; i < shopDepthPercents.Length; i++)
        {
            float percent = Mathf.Clamp01(shopDepthPercents[i]);
            int targetStep = Mathf.Clamp(Mathf.RoundToInt(percent * maxStep), 1, Mathf.Max(1, maxStep - 1));
            Room shopRoom = FindShopCandidate(targetStep, claimed);
            if (shopRoom == null)
            {
                Debug.LogWarning($"RoomGenerator: no shop candidate near {percent:P0} depth (step {targetStep}/{maxStep}).");
                continue;
            }

            claimed.Add(shopRoom);
            shopRoom.ConfigureAsShop(shopRelicPrefabs, shopOfferSpacing);
        }
    }

    int GetMaxStepDistance()
    {
        int maxStep = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null)
            {
                maxStep = Mathf.Max(maxStep, rooms[i].stepToStart);
            }
        }

        return maxStep;
    }

    Room FindShopCandidate(int targetStep, HashSet<Room> claimed)
    {
        Room best = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (!IsValidShopCandidate(room, claimed))
            {
                continue;
            }

            // Prefer exact depth; push away from other shops when possible.
            int stepDelta = Mathf.Abs(room.stepToStart - targetStep);
            int adjacencyPenalty = IsAdjacentToClaimedShop(room, claimed) ? 1000 : 0;
            int score = stepDelta + adjacencyPenalty;
            if (score < bestScore)
            {
                bestScore = score;
                best = room;
            }
        }

        return best;
    }

    bool IsAdjacentToClaimedShop(Room room, HashSet<Room> claimed)
    {
        if (room == null || claimed == null || claimed.Count == 0)
        {
            return false;
        }

        int roomIndex = rooms.IndexOf(room);
        if (roomIndex < 0 || roomIndex >= roomPositions.Count)
        {
            return false;
        }

        Vector2Int roomPos = roomPositions[roomIndex];
        foreach (Room claimedRoom in claimed)
        {
            int claimedIndex = rooms.IndexOf(claimedRoom);
            if (claimedIndex < 0 || claimedIndex >= roomPositions.Count)
            {
                continue;
            }

            Vector2Int claimedPos = roomPositions[claimedIndex];
            int manhattan = Mathf.Abs(roomPos.x - claimedPos.x) + Mathf.Abs(roomPos.y - claimedPos.y);
            if (manhattan <= 1)
            {
                return true;
            }
        }

        return false;
    }

    bool IsValidShopCandidate(Room room, HashSet<Room> claimed)
    {
        if (room == null || claimed.Contains(room))
        {
            return false;
        }

        if (room.stepToStart <= 0 || room.IsEndRoom || room.IsShopRoom)
        {
            return false;
        }

        return true;
    }

    int FindFarthestRoomBFS()
    {
        Dictionary<Vector2Int, int> positionToIndex = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < roomPositions.Count; i++)
        {
            positionToIndex[roomPositions[i]] = i;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();

        Vector2Int startPos = roomPositions[0];
        queue.Enqueue(startPos);
        distances[startPos] = 0;

        int maxDistance = 0;
        Vector2Int farthestPos = startPos;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distances[current];

            Vector2Int[] neighbors = new Vector2Int[]
            {
                current + Vector2Int.up,
                current + Vector2Int.right,
                current + Vector2Int.down,
                current + Vector2Int.left
            };

            foreach (Vector2Int neighbor in neighbors)
            {
                if (occupiedPositions.Contains(neighbor) && !distances.ContainsKey(neighbor))
                {
                    int newDistance = currentDistance + 1;
                    distances[neighbor] = newDistance;
                    queue.Enqueue(neighbor);

                    if (newDistance > maxDistance)
                    {
                        maxDistance = newDistance;
                        farthestPos = neighbor;
                    }
                }
            }
        }

        return positionToIndex[farthestPos];
    }

    [System.Serializable]
    public class WallTypes
    {
        public GameObject WallL, WallR, WallU, WallD,
                          WallUR, WallUL, WallDR, WallDL, WallRL, WallUD,
                          WallUDR, WallUDL, WallURL, WallDRL,
                          WallFour;
    }
}
