using System.Collections;
using UnityEngine;

/// <summary>
/// Single floor spike. Assign three sprites (retracted → mid → raised).
/// Timing / damage are all Inspector-tunable. No Animator needed.
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("0 = sunk, 1 = mid, 2 = fully raised")]
    public Sprite[] frames = new Sprite[3];

    [Header("Timing (seconds)")]
    [Tooltip("How long the spike stays sunk before rising")]
    [Min(0f)] public float retractedHold = 1.2f;
    [Tooltip("How long the spike stays fully raised (damage window)")]
    [Min(0f)] public float raisedHold = 0.8f;
    [Tooltip("Duration of the mid frame when rising / retracting")]
    [Min(0f)] public float transitionTime = 0.12f;
    [Tooltip("Delay before this spike starts its first cycle")]
    [Min(0f)] public float startDelay;

    [Header("Damage")]
    [Min(0)] public int damage = 1;
    public LayerMask playerLayer;
    [Tooltip("Shared across ALL spikes: after one hit, ignore further spike damage for this long")]
    [Min(0f)] public float rehitCooldown = 0.9f;

    // Walking a line of spikes should cost one hit, not one per tile (Isaac-style).
    static float sharedNextHitTime;

    SpriteRenderer spriteRenderer;
    Collider2D hitCollider;
    HealthManager cachedHealth;
    bool cycleRunning;
    bool disarmed;
    Coroutine cycleRoutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitCollider = GetComponent<Collider2D>();
        cachedHealth = FindAnyObjectByType<HealthManager>();

        if (playerLayer.value == 0)
        {
            playerLayer = 1 << 9;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        SetFrame(0);
        SetHazardActive(false);
    }

    void Start()
    {
        if (!disarmed)
        {
            BeginCycle();
        }
    }

    void OnDisable()
    {
        StopCycle();
        SetHazardActive(false);
    }

    /// <summary>
    /// Stop cycling, show sunk frame, disable damage. Used when the room is cleared.
    /// </summary>
    public void RetractAndDisarm()
    {
        disarmed = true;
        StopCycle();
        SetFrame(0);
        SetHazardActive(false);
    }

    /// <summary>
    /// Apply timing / damage from a spawner, then (re)start the cycle.
    /// Safe to call right after Instantiate, before or after Start.
    /// </summary>
    public void ApplySettings(
        float newRetractedHold,
        float newRaisedHold,
        float newTransitionTime,
        float newStartDelay,
        int newDamage = -1,
        float newRehitCooldown = -1f)
    {
        retractedHold = Mathf.Max(0f, newRetractedHold);
        raisedHold = Mathf.Max(0f, newRaisedHold);
        transitionTime = Mathf.Max(0f, newTransitionTime);
        startDelay = Mathf.Max(0f, newStartDelay);

        if (newDamage >= 0)
        {
            damage = newDamage;
        }

        if (newRehitCooldown >= 0f)
        {
            rehitCooldown = newRehitCooldown;
        }
    }

    public void BeginCycle()
    {
        if (disarmed || !isActiveAndEnabled)
        {
            return;
        }

        StopCycle();
        cycleRoutine = StartCoroutine(CycleRoutine());
    }

    void StopCycle()
    {
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        cycleRunning = false;
    }

    IEnumerator CycleRoutine()
    {
        cycleRunning = true;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (enabled)
        {
            SetFrame(0);
            SetHazardActive(false);
            if (retractedHold > 0f)
            {
                yield return new WaitForSeconds(retractedHold);
            }

            SetFrame(1);
            SetHazardActive(false);
            if (transitionTime > 0f)
            {
                yield return new WaitForSeconds(transitionTime);
            }

            SetFrame(2);
            SetHazardActive(true);
            TryDamageOverlapping();
            if (raisedHold > 0f)
            {
                yield return new WaitForSeconds(raisedHold);
            }

            SetFrame(1);
            SetHazardActive(false);
            if (transitionTime > 0f)
            {
                yield return new WaitForSeconds(transitionTime);
            }
        }

        cycleRunning = false;
        cycleRoutine = null;
    }

    void SetFrame(int index)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (frames != null && index >= 0 && index < frames.Length && frames[index] != null)
        {
            spriteRenderer.sprite = frames[index];
        }
    }

    void SetHazardActive(bool active)
    {
        if (hitCollider != null)
        {
            hitCollider.enabled = active;
        }
    }

    void TryDamageOverlapping()
    {
        if (hitCollider == null || damage <= 0 || Time.time < sharedNextHitTime)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(playerLayer);
        filter.useTriggers = true;

        Collider2D[] hits = new Collider2D[8];
        int count = hitCollider.Overlap(filter, hits);
        for (int i = 0; i < count; i++)
        {
            if (TryDamage(hits[i]))
            {
                break;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    bool TryDamage(Collider2D other)
    {
        if (other == null || damage <= 0 || Time.time < sharedNextHitTime)
        {
            return false;
        }

        if (((1 << other.gameObject.layer) & playerLayer) == 0)
        {
            return false;
        }

        HealthManager health = other.GetComponent<HealthManager>();
        if (health == null)
        {
            health = other.GetComponentInParent<HealthManager>();
        }

        if (health == null)
        {
            health = cachedHealth;
        }

        if (health == null || health.IsDead)
        {
            return false;
        }

        health.TakeDamage(damage);
        sharedNextHitTime = Time.time + rehitCooldown;
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (frames == null || frames.Length != 3)
        {
            System.Array.Resize(ref frames, 3);
        }

        retractedHold = Mathf.Max(0f, retractedHold);
        raisedHold = Mathf.Max(0f, raisedHold);
        transitionTime = Mathf.Max(0f, transitionTime);
        startDelay = Mathf.Max(0f, startDelay);
        rehitCooldown = Mathf.Max(0f, rehitCooldown);
        damage = Mathf.Max(0, damage);
    }
#endif
}
