using System.Collections.Generic;
using UnityEngine;

public class PoisonTrailSpawner : MonoBehaviour
{
    static Transform cloudRoot;
    static readonly Stack<PoisonCloud> pool = new Stack<PoisonCloud>();
    static readonly HashSet<PoisonCloud> activeClouds = new HashSet<PoisonCloud>();

    [SerializeField] float spawnDistance = 0.35f;

    PoisonRelicEffects.TrailSettings activeTrail;
    SpriteRenderer playerSpriteRenderer;
    Vector3 lastPosition;
    Vector2 lastMoveDirection;
    float distanceSinceLastSpawn;

    void Awake()
    {
        EnsureCloudRoot();
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        lastPosition = transform.position;
    }

    void OnEnable()
    {
        RelicInventory.RelicAdded += OnRelicAdded;
        RelicInventory.RelicCleared += OnRelicCleared;
        RefreshActiveTrail();
    }

    void OnDisable()
    {
        RelicInventory.RelicAdded -= OnRelicAdded;
        RelicInventory.RelicCleared -= OnRelicCleared;
    }

    void OnRelicAdded(RelicData relic)
    {
        RefreshActiveTrail();
    }

    void OnRelicCleared()
    {
        activeTrail = default;
        RecycleAllActiveClouds();
    }

    void RefreshActiveTrail()
    {
        activeTrail = PoisonRelicEffects.GetTrailSettings();
    }

    void FixedUpdate()
    {
        if (!activeTrail.enabled)
        {
            lastPosition = transform.position;
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - lastPosition;
        float moveDistance = new Vector2(delta.x, delta.y).magnitude;

        if (moveDistance > 0.001f)
        {
            lastMoveDirection = new Vector2(delta.x, delta.y).normalized;
        }

        if (moveDistance <= 0.001f)
        {
            lastPosition = currentPosition;
            return;
        }

        float spacing = activeTrail.spawnDistance > 0f
            ? activeTrail.spawnDistance
            : spawnDistance;

        distanceSinceLastSpawn += moveDistance;
        while (distanceSinceLastSpawn >= spacing)
        {
            float overshoot = distanceSinceLastSpawn - spacing;
            Vector3 spawnPosition = currentPosition - (Vector3)lastMoveDirection * overshoot;
            if (lastMoveDirection.sqrMagnitude > 0.01f)
            {
                spawnPosition -= (Vector3)lastMoveDirection * 0.12f;
            }

            SpawnCloud(spawnPosition);
            distanceSinceLastSpawn = overshoot;
        }

        lastPosition = currentPosition;
    }

    void SpawnCloud(Vector3 position)
    {
        PoisonCloud cloud = GetCloud();
        cloud.Activate(
            position,
            activeTrail.cloudRadius,
            activeTrail.cloudLifetime,
            activeTrail.damagePerTick,
            activeTrail.duration,
            activeTrail.tickInterval,
            activeTrail.reapplyCooldown,
            activeTrail.maxExposurePerEnemy,
            activeTrail.isPermanent,
            playerSpriteRenderer);
        activeClouds.Add(cloud);
    }

    static PoisonCloud GetCloud()
    {
        EnsureCloudRoot();

        while (pool.Count > 0)
        {
            PoisonCloud pooled = pool.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        GameObject cloudObject = new GameObject("PoisonCloud", typeof(PoisonCloud));
        cloudObject.transform.SetParent(cloudRoot, false);
        return cloudObject.GetComponent<PoisonCloud>();
    }

    public static void Recycle(PoisonCloud cloud)
    {
        if (cloud == null)
        {
            return;
        }

        cloud.Deactivate();
        activeClouds.Remove(cloud);
        pool.Push(cloud);
    }

    static void RecycleAllActiveClouds()
    {
        PoisonCloud[] clouds = new PoisonCloud[activeClouds.Count];
        activeClouds.CopyTo(clouds);
        for (int i = 0; i < clouds.Length; i++)
        {
            Recycle(clouds[i]);
        }
    }

    static void EnsureCloudRoot()
    {
        if (cloudRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("PoisonTrailRoot");
        cloudRoot = rootObject.transform;
    }
}
