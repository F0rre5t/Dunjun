using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns multiple SpikeTrap prefabs in Isaac-style layouts.
/// Hang on a room child to preview; use Clear And Respawn while tuning.
/// </summary>
public class SpikeTrapPattern : MonoBehaviour
{
    public enum Shape
    {
        Ring,
        Line,
        Cross,
        CornerClusters,
        DoorGuards,
        Diagonals,
        Box,
        TwinRails
    }

    [Header("Prefab")]
    public GameObject spikePrefab;

    [Header("Shape")]
    public Shape shape = Shape.Cross;

    [Tooltip("Meaning depends on shape. Prefer ApplyShapeDefaults() / Room countOverride; Ring needs ~8, Cross arm ~2.")]
    [Min(1)] public int count = 2;

    [Tooltip("Ring radius")]
    [Min(0f)] public float radius = 2.8f;

    [Tooltip("Distance between neighboring spikes. Must be >= spike visual size or they pile into a blob.")]
    [Min(0.1f)] public float spacing = 1.4f;

    [Tooltip("Line / TwinRails: true = vertical")]
    public bool vertical;

    [Header("Room Fit (relative to this object's center)")]
    [Tooltip("Approx half-width of walkable floor")]
    [Min(0.5f)] public float roomHalfWidth = 7f;
    [Tooltip("Approx half-height of walkable floor")]
    [Min(0.5f)] public float roomHalfHeight = 3.2f;

    [Header("Corner Clusters")]
    [Min(1)] public int clusterSize = 2;
    [Tooltip("Pull corners inward from room edge")]
    [Min(0f)] public float cornerInset = 1.1f;

    [Header("Door Guards")]
    [Min(1)] public int spikesPerDoor = 3;
    [Tooltip("How far inside the room from each door")]
    [Min(0f)] public float doorInset = 1.1f;

    [Header("Box")]
    [Tooltip("Inset the box from roomHalfWidth / Height")]
    [Min(0f)] public float boxInset = 1.4f;

    [Header("Twin Rails")]
    [Tooltip("Gap between the two parallel rails")]
    [Min(0.1f)] public float railSeparation = 2.2f;

    [Header("Timing Override")]
    public bool overrideTiming = true;
    [Min(0f)] public float retractedHold = 1.2f;
    [Min(0f)] public float raisedHold = 0.8f;
    [Min(0f)] public float transitionTime = 0.12f;

    [Header("Stagger")]
    public bool syncTiming = true;
    [Min(0f)] public float sharedStartDelay;
    [Min(0f)] public float staggerStep = 0.08f;

    [Header("Damage Override")]
    public bool overrideDamage = true;
    [Min(0)] public int damage = 1;
    [Min(0f)] public float rehitCooldown = 0.6f;

    [Header("Spawn / Debug")]
    [Tooltip("Room-driven spawns leave this off; only enable for manual preview objects")]
    public bool spawnOnAwake;
    [Tooltip("In Play Mode, press this key to clear + respawn after changing Shape")]
    public KeyCode respawnKey = KeyCode.None;

    bool spawned;

    void Awake()
    {
        if (spawnOnAwake)
        {
            SpawnPattern();
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (respawnKey != KeyCode.None && Input.GetKeyDown(respawnKey))
        {
            ClearAndRespawn();
        }
    }

    [ContextMenu("Clear And Respawn")]
    public void ClearAndRespawn()
    {
        ClearSpawned();
        SpawnPattern();
    }

    [ContextMenu("Clear Spawned")]
    public void ClearSpawned()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        spawned = false;
    }

    /// <summary>
    /// Retract every spike and keep them sunk (room cleared).
    /// </summary>
    public void DisarmAll()
    {
        SpikeTrap[] spikes = GetComponentsInChildren<SpikeTrap>(true);
        for (int i = 0; i < spikes.Length; i++)
        {
            if (spikes[i] != null)
            {
                spikes[i].RetractAndDisarm();
            }
        }
    }

    [ContextMenu("Spawn Pattern Now")]
    public void SpawnPattern()
    {
        if (spawned)
        {
            return;
        }

        if (spikePrefab == null)
        {
            Debug.LogWarning($"{name}: SpikeTrapPattern needs a spikePrefab.", this);
            return;
        }

        Vector3[] positions = BuildPositions();
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject instance = Instantiate(
                spikePrefab,
                transform.position + positions[i],
                Quaternion.identity,
                transform);

            SpikeTrap spike = instance.GetComponent<SpikeTrap>();
            if (spike == null)
            {
                continue;
            }

            float delay = syncTiming ? sharedStartDelay : sharedStartDelay + staggerStep * i;

            if (overrideTiming || overrideDamage || !syncTiming || sharedStartDelay > 0f)
            {
                float rh = overrideTiming ? retractedHold : spike.retractedHold;
                float raised = overrideTiming ? raisedHold : spike.raisedHold;
                float transition = overrideTiming ? transitionTime : spike.transitionTime;
                int dmg = overrideDamage ? damage : -1;
                float rehit = overrideDamage ? rehitCooldown : -1f;
                spike.ApplySettings(rh, raised, transition, delay, dmg, rehit);
            }
            else if (delay > 0f)
            {
                spike.startDelay = delay;
            }
        }

        spawned = true;
    }

    /// <summary>
    /// Sensible count per shape so Cross arm length is not reused as Ring spike count.
    /// </summary>
    public static int DefaultCountForShape(Shape shape)
    {
        switch (shape)
        {
            case Shape.Ring:
                return 8;
            case Shape.Line:
                return 5;
            case Shape.Cross:
                return 2;
            case Shape.Diagonals:
                return 2;
            case Shape.TwinRails:
                return 5;
            case Shape.CornerClusters:
                return 2;
            case Shape.DoorGuards:
                return 3;
            case Shape.Box:
                return 1;
            default:
                return 2;
        }
    }

    public void ApplyShapeDefaults(Shape newShape, int countOverride = 0)
    {
        shape = newShape;
        count = countOverride > 0 ? countOverride : DefaultCountForShape(newShape);
    }

    Vector3[] BuildPositions()
    {
        switch (shape)
        {
            case Shape.Line:
                return BuildLine(vertical);
            case Shape.Cross:
                return BuildCross();
            case Shape.CornerClusters:
                return BuildCornerClusters();
            case Shape.DoorGuards:
                return BuildDoorGuards();
            case Shape.Diagonals:
                return BuildDiagonals();
            case Shape.Box:
                return BuildBox();
            case Shape.TwinRails:
                return BuildTwinRails();
            default:
                return BuildRing();
        }
    }

    Vector3[] BuildRing()
    {
        int n = Mathf.Max(1, count);
        Vector3[] pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float angle = (Mathf.PI * 2f * i) / n;
            pts[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }
        return pts;
    }

    Vector3[] BuildLine(bool isVertical)
    {
        int n = Mathf.Max(1, count);
        Vector3[] pts = new Vector3[n];
        float start = -0.5f * (n - 1) * spacing;
        for (int i = 0; i < n; i++)
        {
            float t = start + i * spacing;
            pts[i] = isVertical ? new Vector3(0f, t, 0f) : new Vector3(t, 0f, 0f);
        }
        return pts;
    }

    Vector3[] BuildCross()
    {
        int arm = Mathf.Max(1, count);
        List<Vector3> pts = new List<Vector3> { Vector3.zero };
        for (int i = 1; i <= arm; i++)
        {
            float d = i * spacing;
            pts.Add(new Vector3(d, 0f, 0f));
            pts.Add(new Vector3(-d, 0f, 0f));
            pts.Add(new Vector3(0f, d, 0f));
            pts.Add(new Vector3(0f, -d, 0f));
        }
        return pts.ToArray();
    }

    Vector3[] BuildCornerClusters()
    {
        int size = Mathf.Max(1, clusterSize);
        float cx = Mathf.Max(0.5f, roomHalfWidth - cornerInset);
        float cy = Mathf.Max(0.5f, roomHalfHeight - cornerInset);
        Vector2[] corners =
        {
            new Vector2(-cx, cy),
            new Vector2(cx, cy),
            new Vector2(-cx, -cy),
            new Vector2(cx, -cy)
        };

        List<Vector3> pts = new List<Vector3>();
        float origin = -0.5f * (size - 1) * spacing;
        for (int c = 0; c < corners.Length; c++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pts.Add(new Vector3(
                        corners[c].x + origin + x * spacing,
                        corners[c].y + origin + y * spacing,
                        0f));
                }
            }
        }
        return pts.ToArray();
    }

    Vector3[] BuildDoorGuards()
    {
        int n = Mathf.Max(1, spikesPerDoor);
        float start = -0.5f * (n - 1) * spacing;
        float x = Mathf.Max(0.5f, roomHalfWidth - doorInset);
        float y = Mathf.Max(0.5f, roomHalfHeight - doorInset);

        List<Vector3> pts = new List<Vector3>();

        // Up / Down: horizontal rows
        for (int i = 0; i < n; i++)
        {
            float t = start + i * spacing;
            pts.Add(new Vector3(t, y, 0f));
            pts.Add(new Vector3(t, -y, 0f));
        }

        // Left / Right: vertical rows
        for (int i = 0; i < n; i++)
        {
            float t = start + i * spacing;
            pts.Add(new Vector3(-x, t, 0f));
            pts.Add(new Vector3(x, t, 0f));
        }

        return pts.ToArray();
    }

    Vector3[] BuildDiagonals()
    {
        int arm = Mathf.Max(1, count);
        List<Vector3> pts = new List<Vector3> { Vector3.zero };
        for (int i = 1; i <= arm; i++)
        {
            float d = i * spacing;
            pts.Add(new Vector3(d, d, 0f));
            pts.Add(new Vector3(-d, -d, 0f));
            pts.Add(new Vector3(d, -d, 0f));
            pts.Add(new Vector3(-d, d, 0f));
        }
        return pts.ToArray();
    }

    Vector3[] BuildBox()
    {
        float halfW = Mathf.Max(spacing, roomHalfWidth - boxInset);
        float halfH = Mathf.Max(spacing, roomHalfHeight - boxInset);
        List<Vector3> pts = new List<Vector3>();

        int cols = Mathf.Max(2, Mathf.RoundToInt((halfW * 2f) / spacing) + 1);
        int rows = Mathf.Max(2, Mathf.RoundToInt((halfH * 2f) / spacing) + 1);

        for (int i = 0; i < cols; i++)
        {
            float x = Mathf.Lerp(-halfW, halfW, cols == 1 ? 0.5f : i / (float)(cols - 1));
            pts.Add(new Vector3(x, halfH, 0f));
            pts.Add(new Vector3(x, -halfH, 0f));
        }

        for (int i = 1; i < rows - 1; i++)
        {
            float y = Mathf.Lerp(-halfH, halfH, i / (float)(rows - 1));
            pts.Add(new Vector3(-halfW, y, 0f));
            pts.Add(new Vector3(halfW, y, 0f));
        }

        return pts.ToArray();
    }

    Vector3[] BuildTwinRails()
    {
        int n = Mathf.Max(1, count);
        float start = -0.5f * (n - 1) * spacing;
        float halfGap = railSeparation * 0.5f;
        List<Vector3> pts = new List<Vector3>(n * 2);

        for (int i = 0; i < n; i++)
        {
            float t = start + i * spacing;
            if (vertical)
            {
                pts.Add(new Vector3(-halfGap, t, 0f));
                pts.Add(new Vector3(halfGap, t, 0f));
            }
            else
            {
                pts.Add(new Vector3(t, -halfGap, 0f));
                pts.Add(new Vector3(t, halfGap, 0f));
            }
        }

        return pts.ToArray();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        Vector3[] positions = BuildPositions();
        for (int i = 0; i < positions.Length; i++)
        {
            Gizmos.DrawWireSphere(transform.position + positions[i], 0.18f);
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Gizmos.DrawWireCube(transform.position, new Vector3(roomHalfWidth * 2f, roomHalfHeight * 2f, 0f));
    }
#endif
}
