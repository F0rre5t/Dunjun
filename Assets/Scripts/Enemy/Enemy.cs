using UnityEngine;

public class Enemy : MonoBehaviour
{
    protected Rigidbody2D rb;
    protected Animator anim;

    [Header("Identity (Run Summary)")]
    [Tooltip("Stable id for kill grouping. Empty = cleaned prefab/object name.")]
    public string enemyId;
    [Tooltip("Icon shown on the end-of-run kill list. Empty = facing sprite.")]
    public Sprite summaryIcon;
    [Tooltip("If false, this enemy is omitted from run kill stats.")]
    public bool countInRunSummary = true;

    [Header("Health Stats")]
    public int maxHealth = 30;
    public int currentHealth;

    [Header("Base Stats")]
    public float normalSpeed;
    public float chaseSpeed; 
    public float currentSpeed;

    [Header("Random Move")]
    public int directionCount = 8;
    public float moveTime = 2.0f;
    public float idleTime = 1.0f;

    public Vector3 faceDir;

    protected enum State { Moving, Idle, Chasing, Attacking, Charging }
    protected State currentState;
    protected float stateTimer;
    protected Vector2 moveDirection;

    protected Transform facingTransform;
    protected SpriteRenderer facingSpriteRenderer;
    protected float rootScaleMagnitudeX = 1f;

    [Header("Facing")]
    [SerializeField] protected float facingDeadZone = 0.2f;

    [Header("Movement")]
    [SerializeField] protected float chaseStandoffDistance = 1.1f;
    [SerializeField] protected float separationRadius = 0.5f;
    [SerializeField] protected float separationStrength = 1.5f;

    protected Room roomOwner;
    bool hitByPlayer;
    private static int enemyLayerMask = -1;

    public bool HasBeenHitByPlayer => hitByPlayer;

    public void MarkHitByPlayer()
    {
        hitByPlayer = true;
    }

    float slowMultiplier = 1f;
    float slowEndTime;
    int poisonDamagePerTick;
    float poisonTickInterval = 1f;
    float poisonEndTime;
    float nextPoisonTickTime;
    float poisonExposureBudget;
    bool poisonExposureBudgetSet;
    bool poisonIsPermanent;

    SpriteRenderer statusSpriteRenderer;
    float hitFlashEndTime;
    static readonly Color SlowTintColor = new Color(0.45f, 0.78f, 1f);
    static readonly Color PoisonTintColor = new Color(0.45f, 0.95f, 0.3f);
    const float HitFlashDuration = 0.15f;
    const float SlowPulseSpeed = 12f;

    public void ApplySlowFromHit(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f)
        {
            return;
        }

        float multiplier = Mathf.Clamp01(1f - percent);
        slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
        slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
    }

    public bool HasAdaptedToPoison => poisonExposureBudgetSet && poisonExposureBudget <= 0f;

    public void ApplyPoisonDebuff(
        int damagePerTick,
        float duration,
        float tickInterval,
        float maxTotalExposure = -1f,
        bool permanent = false)
    {
        if (damagePerTick <= 0)
        {
            return;
        }

        if (!permanent && duration <= 0f)
        {
            return;
        }

        if (!permanent && maxTotalExposure >= 0f)
        {
            if (!poisonExposureBudgetSet)
            {
                poisonExposureBudget = maxTotalExposure;
                poisonExposureBudgetSet = true;
            }

            if (poisonExposureBudget <= 0f)
            {
                return;
            }
        }

        float effectiveDuration = duration;
        if (!permanent && poisonExposureBudgetSet)
        {
            effectiveDuration = Mathf.Min(duration, poisonExposureBudget);
            if (effectiveDuration <= 0f)
            {
                return;
            }

            poisonExposureBudget -= effectiveDuration;
        }

        poisonDamagePerTick = Mathf.Max(poisonDamagePerTick, damagePerTick);
        poisonTickInterval = Mathf.Max(0.1f, tickInterval);
        poisonIsPermanent = poisonIsPermanent || permanent;
        poisonEndTime = poisonIsPermanent
            ? float.PositiveInfinity
            : Mathf.Max(poisonEndTime, Time.time + effectiveDuration);

        if (nextPoisonTickTime <= Time.time)
        {
            nextPoisonTickTime = Time.time + poisonTickInterval;
        }
    }

    public void ApplyPoisonFromHit(int damagePerTick, float duration, float tickInterval, bool permanent = false)
    {
        ApplyPoisonDebuff(damagePerTick, duration, tickInterval, -1f, permanent);
    }

    float trailPoisonCooldownEndTime;

    public void ApplyPoisonFromTrail(
        int damagePerTick,
        float duration,
        float tickInterval,
        float reapplyCooldown,
        float maxTotalExposure,
        bool permanent = false)
    {
        if (!permanent && HasAdaptedToPoison)
        {
            return;
        }

        if (Time.time < trailPoisonCooldownEndTime)
        {
            return;
        }

        trailPoisonCooldownEndTime = Time.time + Mathf.Max(0.5f, reapplyCooldown);
        ApplyPoisonDebuff(damagePerTick, duration, tickInterval, maxTotalExposure, permanent);
    }

    protected virtual void TakePoisonDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        damage = PoisonRelicEffects.ScalePoisonDamageForTarget(this, damage);
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void ClearPoison()
    {
        poisonDamagePerTick = 0;
        poisonEndTime = 0f;
        nextPoisonTickTime = 0f;
        poisonIsPermanent = false;
    }

    protected float GetModifiedSpeed(float speed)
    {
        if (Time.time >= slowEndTime)
        {
            slowMultiplier = 1f;
        }

        return speed * slowMultiplier;
    }

    void TickPoison()
    {
        if (poisonDamagePerTick <= 0)
        {
            return;
        }

        if (!poisonIsPermanent && Time.time >= poisonEndTime)
        {
            ClearPoison();
            ApplyStatusTint();
            return;
        }

        if (Time.time < nextPoisonTickTime)
        {
            return;
        }

        nextPoisonTickTime = Time.time + poisonTickInterval;
        TakePoisonDamage(poisonDamagePerTick);
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        rootScaleMagnitudeX = Mathf.Abs(transform.localScale.x);
        facingTransform = anim != null && anim.transform != transform ? anim.transform : transform;
        if (UsesChildFacing)
        {
            facingSpriteRenderer = facingTransform.GetComponent<SpriteRenderer>();
        }

        currentHealth = maxHealth; 

        statusSpriteRenderer = GetComponent<SpriteRenderer>();
        if (statusSpriteRenderer == null)
        {
            statusSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        currentSpeed = normalSpeed;
        faceDir = Vector3.right;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        currentState = State.Moving;
        stateTimer = moveTime;
        DecideMoveDirection();
    }

    public void SetRoom(Room room)
    {
        roomOwner = room;
    }

    protected void NotifyDeath()
    {
        if (countInRunSummary)
        {
            RunStats.RegisterKill(GetSummaryId(), GetSummaryIcon());
        }

        if (roomOwner != null)
        {
            roomOwner.OnEnemyDied(this);
        }
    }

    public string GetSummaryId()
    {
        if (!string.IsNullOrEmpty(enemyId))
        {
            return enemyId;
        }

        string raw = gameObject.name;
        int cloneIndex = raw.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (cloneIndex >= 0)
        {
            raw = raw.Substring(0, cloneIndex);
        }

        return raw.Trim();
    }

    public Sprite GetSummaryIcon()
    {
        if (summaryIcon != null)
        {
            return summaryIcon;
        }

        if (facingSpriteRenderer != null && facingSpriteRenderer.sprite != null)
        {
            return facingSpriteRenderer.sprite;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    public virtual void TakeDamage(int damage, bool playHitFlash = true)
    {
        if (playHitFlash)
        {
            PlayHitFlash();
        }

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected void PlayHitFlash()
    {
        hitFlashEndTime = Time.time + HitFlashDuration;
        ApplyStatusTint();
    }

    protected bool IsSlowed()
    {
        if (Time.time >= slowEndTime)
        {
            return false;
        }

        return slowMultiplier < 0.999f;
    }

    protected bool IsPoisoned()
    {
        if (poisonDamagePerTick <= 0)
        {
            return false;
        }

        return poisonIsPermanent || Time.time < poisonEndTime;
    }

    protected void ApplyStatusTint()
    {
        if (statusSpriteRenderer == null)
        {
            return;
        }

        if (Time.time < hitFlashEndTime)
        {
            statusSpriteRenderer.color = Color.red;
            return;
        }

        if (IsSlowed())
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * SlowPulseSpeed);
            statusSpriteRenderer.color = Color.Lerp(Color.white, SlowTintColor, pulse * 0.85f);
            return;
        }

        if (IsPoisoned())
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * SlowPulseSpeed);
            statusSpriteRenderer.color = Color.Lerp(Color.white, PoisonTintColor, pulse * 0.75f);
            return;
        }

        statusSpriteRenderer.color = Color.white;
    }

    protected void TryDropLoot()
    {
        if (roomOwner != null && !roomOwner.ShouldDropLootForEnemy())
        {
            return;
        }

        LootDropper dropper = GetComponent<LootDropper>();
        if (dropper != null)
        {
            dropper.TryDrop(transform.position);
        }
    }

    protected virtual void Die()
    {
        NotifyDeath();
        TryDropLoot();
        Destroy(gameObject); 
    }

    protected virtual void Start()
    {
        NormalizeChildFacingScale();
        ResolveSpawnPenetration();
    }

    protected void NormalizeChildFacingScale()
    {
        if (!UsesChildFacing) return;

        Vector3 faceScale = facingTransform.localScale;
        float visualScaleX = Mathf.Abs(faceScale.x);
        if (visualScaleX < 0.001f) visualScaleX = 1f;
        faceScale.x = visualScaleX;
        faceScale.y = Mathf.Abs(faceScale.y);
        facingTransform.localScale = faceScale;

        if (facingSpriteRenderer != null)
        {
            facingSpriteRenderer.flipX = FacingSign < 0f;
        }
    }

    protected void ResolveSpawnPenetration()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null || rb == null) return;

        int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            mask &= ~(1 << enemyLayer);
        }

        for (int i = 0; i < 12; i++)
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = mask
            };

            Collider2D[] overlaps = new Collider2D[6];
            int count = col.Overlap(filter, overlaps);
            if (count == 0) break;

            bool moved = false;
            for (int j = 0; j < count; j++)
            {
                Collider2D other = overlaps[j];
                if (other == null || other.transform.IsChildOf(transform)) continue;

                ColliderDistance2D distance = Physics2D.Distance(col, other);
                if (distance.isOverlapped)
                {
                    rb.position += distance.normal * distance.distance;
                    moved = true;
                }
            }

            if (!moved) break;

            transform.position = rb.position;
            Physics2D.SyncTransforms();
        }
    }

    protected virtual void Update()
    {
        if (currentState == State.Moving || currentState == State.Idle)
        {
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                if (currentState == State.Moving)
                {
                    currentState = State.Idle;
                    stateTimer = idleTime;
                    moveDirection = Vector2.zero;
                }
                else
                {
                    currentState = State.Moving;
                    stateTimer = moveTime;
                    DecideMoveDirection();
                }
            }
        }

        UpdateAnimation();
    }

    protected virtual void FixedUpdate()
    {
        TickPoison();
        Move();
    }

    protected virtual void DecideMoveDirection()
    {
        currentSpeed = normalSpeed;

        float angleStep = 360f / directionCount;
        float randomAngle = Random.Range(0, directionCount) * angleStep;

        float radian = randomAngle * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));

        UpdateFacingDirection(moveDirection.x);
    }

    protected float FacingSign => Mathf.Sign(faceDir.x == 0f ? 1f : faceDir.x);

    protected bool UsesChildFacing => facingTransform != transform;

    protected Transform ResolvePlayer(Transform current)
    {
        if (current != null)
        {
            return current;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    protected void TryFaceTarget(Vector3 worldTarget)
    {
        float deltaX = worldTarget.x - transform.position.x;
        UpdateFacingDirection(deltaX);
    }

    protected virtual void UpdateFacingDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= facingDeadZone)
        {
            return;
        }

        float newSign = Mathf.Sign(dirX);
        if (Mathf.Approximately(newSign, FacingSign))
        {
            return;
        }

        float previousSign = FacingSign;
        faceDir.x = newSign;
        float sign = FacingSign;

        if (UsesChildFacing)
        {
            Vector3 rootScale = transform.localScale;
            rootScale.x = rootScaleMagnitudeX;
            transform.localScale = rootScale;

            Vector3 faceScale = facingTransform.localScale;
            float visualScaleX = Mathf.Abs(faceScale.x);
            if (visualScaleX < 0.001f) visualScaleX = 1f;
            faceScale.x = visualScaleX;
            faceScale.y = Mathf.Abs(faceScale.y);
            facingTransform.localScale = faceScale;

            if (facingSpriteRenderer != null)
            {
                facingSpriteRenderer.flipX = sign < 0f;
            }
        }
        else
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * sign;
            transform.localScale = s;
        }

        if (!Mathf.Approximately(previousSign, sign))
        {
            OnFacingDirectionChanged(previousSign, sign);
        }
    }

    protected virtual void OnFacingDirectionChanged(float previousSign, float newSign)
    {
    }

    public virtual void Move()
    {
        if (rb == null)
        {
            return;
        }

        if (currentState == State.Moving || currentState == State.Chasing)
        {
            Vector2 direction = moveDirection;
            ApplySeparation(ref direction);
            Vector2 delta = direction * GetModifiedSpeed(currentSpeed) * Time.fixedDeltaTime;
            bool movedFully = TryMove(delta);

            if (!movedFully && currentState == State.Chasing && delta.sqrMagnitude > 0f)
            {
                Vector2 slideX = new Vector2(delta.x, 0f);
                Vector2 slideY = new Vector2(0f, delta.y);

                if (slideX.sqrMagnitude > 0f)
                {
                    movedFully = TryMove(slideX);
                }

                if (!movedFully && slideY.sqrMagnitude > 0f)
                {
                    TryMove(slideY);
                }
            }
            else if (currentState == State.Moving && !movedFully && delta.sqrMagnitude > 0f)
            {
                DecideMoveDirection();
                stateTimer = moveTime;
            }
        }
    }

    protected void SetChaseMovement(Vector2 toPlayer, float distanceToPlayer, float speed)
    {
        if (distanceToPlayer <= chaseStandoffDistance)
        {
            currentSpeed = 0f;
            moveDirection = Vector2.zero;
            return;
        }

        currentSpeed = speed;
        moveDirection = toPlayer.normalized;
    }

    protected void ApplySeparation(ref Vector2 direction)
    {
        if (enemyLayerMask < 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            enemyLayerMask = enemyLayer >= 0 ? (1 << enemyLayer) : 0;
        }

        if (enemyLayerMask == 0)
        {
            return;
        }

        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, separationRadius, enemyLayerMask);
        Vector2 separation = Vector2.zero;

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D other = nearby[i];
            if (other == null)
            {
                continue;
            }

            Rigidbody2D otherRb = other.attachedRigidbody;
            if (otherRb == rb)
            {
                continue;
            }

            Vector2 away = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = away.magnitude;
            if (dist < 0.001f)
            {
                away = Random.insideUnitCircle;
                if (away.sqrMagnitude < 0.001f)
                {
                    away = Vector2.right;
                }
                separation += away.normalized;
                continue;
            }

            float weight = 1f - dist / separationRadius;
            separation += away.normalized * weight;
        }

        if (separation.sqrMagnitude <= 0f)
        {
            return;
        }

        if (direction.sqrMagnitude > 0f)
        {
            direction = (direction + separation * separationStrength).normalized;
        }
        else
        {
            direction = separation.normalized;
        }
    }

    protected bool TryMove(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0f)
        {
            return true;
        }

        float distance = delta.magnitude;
        Vector2 direction = delta / distance;

        // Soft separation only between enemies.
        // Hard casts between big colliders cause jitter.
        int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            mask &= ~(1 << enemyLayer);
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = false
        };

        RaycastHit2D[] hits = new RaycastHit2D[8];
        int hitCount = rb.Cast(direction, filter, hits, distance);

        float allowedDistance = distance;
        if (hitCount > 0)
        {
            allowedDistance = hits[0].distance;
            for (int i = 1; i < hitCount; i++)
            {
                if (hits[i].distance < allowedDistance)
                {
                    allowedDistance = hits[i].distance;
                }
            }

            allowedDistance = Mathf.Max(0f, allowedDistance - 0.02f);
        }

        if (allowedDistance <= 0f)
        {
            return false;
        }

        rb.MovePosition(rb.position + direction * allowedDistance);
        return allowedDistance >= distance * 0.9f;
    }

    protected bool IsCurrentlyMoving()
    {
        return (currentState == State.Moving || currentState == State.Chasing)
            && currentSpeed > 0.01f
            && moveDirection.sqrMagnitude > 0.01f;
    }

    protected void HaltMovement()
    {
        moveDirection = Vector2.zero;
        currentSpeed = 0f;
    }

    protected virtual void UpdateAnimation()
    {
        if (anim != null)
        {
            anim.SetBool("isMoving", IsCurrentlyMoving());
        }
    }

    protected virtual void LateUpdate()
    {
        MirrorAnimatedPositionForFlipX();
        ApplyStatusTint();
    }

    protected void MirrorAnimatedPositionForFlipX()
    {
        if (!UsesChildFacing || facingSpriteRenderer == null || !facingSpriteRenderer.flipX) return;

        Vector3 localPos = facingTransform.localPosition;
        localPos.x = -localPos.x;
        facingTransform.localPosition = localPos;
    }
}
