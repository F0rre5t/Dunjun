using UnityEngine;

public class Imp : Enemy
{
    [Header("Imp Specifics")]
    public float detectionRadius = 6f;
    public float loseAggroRadius = 11f;
    public float attackRadius = 5f;

    [Header("Attack Timers")]
    public float attackCooldown = 1.6f;
    public float attackDuration = 1f;
    
    public float fireballSpawnDelay = 0.2f;

    [Header("Fireball")]
    public ImpFireball fireballPrefab;
    public Transform handPoint;
    public float fireballSpeed = 4.5f;
    public int fireballDamage = 1;
    public float fireballMaxDistance = 8f;
    public LayerMask playerLayer;

    Transform player;
    HealthManager healthManager;
    float cooldownTimer;
    float animTimer;
    float hurtTimer;
    bool canSpawnFireball;
    bool isDead;
    float handPointBaseX;
    Collider2D bodyCollider;

    protected override void Awake()
    {
        base.Awake();
        chaseStandoffDistance = Mathf.Max(chaseStandoffDistance, attackRadius * 0.55f);
        bodyCollider = GetComponent<Collider2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        healthManager = FindAnyObjectByType<HealthManager>();

        if (handPoint != null)
        {
            handPointBaseX = Mathf.Abs(handPoint.localPosition.x);
        }
    }

    protected override void Start()
    {
        base.Start();
        ResolveHandPoint();
        MirrorHandPoint();
    }

    void ResolveHandPoint()
    {
        if (handPoint == null)
        {
            Transform found = transform.Find("HandPoint");
            if (found == null)
            {
                found = transform.Find("Visual/HandPoint");
            }

            handPoint = found;
        }

        if (handPoint != null)
        {
            handPointBaseX = Mathf.Abs(handPoint.localPosition.x);
        }
    }

    protected override void OnFacingDirectionChanged(float previousSign, float newSign)
    {
        MirrorHandPoint();
        ResolveSpawnPenetration();
    }

    void MirrorHandPoint()
    {
        if (handPoint == null || handPointBaseX <= 0f)
        {
            return;
        }

        Vector3 localPos = handPoint.localPosition;
        localPos.x = handPointBaseX * FacingSign;
        handPoint.localPosition = localPos;
    }

    protected override void Update()
    {
        if (isDead)
        {
            return;
        }

        if (hurtTimer > 0f)
        {
            hurtTimer -= Time.deltaTime;
        }
        else
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            CheckDetection();
        }

        base.Update();
    }

    void CheckDetection()
    {
        player = ResolvePlayer(player);
        if (player == null || hurtTimer > 0f)
        {
            return;
        }

        if (currentState == State.Attacking)
        {
            animTimer -= Time.deltaTime;
            if (animTimer <= 0f)
            {
                currentState = State.Chasing;
            }

            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float currentSight = (currentState == State.Chasing || currentState == State.Attacking)
            ? loseAggroRadius
            : detectionRadius;

        if (distanceToPlayer <= attackRadius)
        {
            if (cooldownTimer <= 0f)
            {
                BeginAttack();
            }
            else
            {
                currentState = State.Chasing;
                currentSpeed = 0f;
                moveDirection = Vector2.zero;
                TryFaceTarget(player.position);
            }
        }
        else if (distanceToPlayer <= currentSight)
        {
            currentState = State.Chasing;
            TryFaceTarget(player.position);

            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            SetChaseMovement(toPlayer, distanceToPlayer, chaseSpeed);
        }
        else if (currentState == State.Chasing || currentState == State.Attacking)
        {
            currentState = State.Idle;
            currentSpeed = normalSpeed;
            stateTimer = idleTime;
            moveDirection = Vector2.zero;
        }
    }

    void BeginAttack()
    {
        currentState = State.Attacking;
        currentSpeed = 0f;
        moveDirection = Vector2.zero;
        animTimer = attackDuration;
        cooldownTimer = attackCooldown;
        canSpawnFireball = true;

        TryFaceTarget(player.position);
        MirrorHandPoint();

        if (anim != null)
        {
            anim.SetTrigger("attack");
        }

        CancelInvoke(nameof(SpawnFireball));
        float delay = Mathf.Clamp(fireballSpawnDelay, 0f, Mathf.Max(0.01f, attackDuration));
        Invoke(nameof(SpawnFireball), delay);
    }

    public void SpawnFireball()
    {
        if (!canSpawnFireball || isDead || fireballPrefab == null)
        {
            return;
        }

        canSpawnFireball = false;
        CancelInvoke(nameof(SpawnFireball));
        ResolveHandPoint();
        MirrorHandPoint();

        if (handPoint == null)
        {
            Debug.LogWarning($"{name}: HandPoint is missing; fireball will spawn at the enemy center.", this);
        }

        Vector3 spawnPos = handPoint != null ? handPoint.position : transform.position;
        player = ResolvePlayer(player);

        ImpFireball fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
        fireball.transform.SetParent(null, true);
        fireball.transform.position = spawnPos;
        fireball.Arm(
            handPoint,
            transform,
            player,
            fireballSpeed,
            fireballDamage,
            fireballMaxDistance,
            playerLayer,
            bodyCollider);
    }

    public override void Move()
    {
        if (currentState == State.Attacking || hurtTimer > 0f || isDead)
        {
            HaltMovement();
        }
        else
        {
            base.Move();
        }
    }

    protected override void UpdateAnimation()
    {
        if (anim == null || isDead)
        {
            return;
        }

        bool isMovingState = currentState == State.Moving || currentState == State.Chasing;
        if (hurtTimer > 0f)
        {
            isMovingState = false;
        }

        anim.SetBool("isMoving", isMovingState);
    }

    public override void TakeDamage(int damage, bool playHitFlash = true)
    {
        base.TakeDamage(damage, playHitFlash);
        if (currentHealth <= 0 || isDead)
        {
            return;
        }

        if (currentState != State.Attacking && anim != null)
        {
            anim.SetTrigger("damage");
            hurtTimer = 0.3f;
            HaltMovement();
        }
    }

    protected override void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        canSpawnFireball = false;
        CancelInvoke(nameof(SpawnFireball));

        NotifyDeath();
        TryDropLoot();
        HaltMovement();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (anim != null)
        {
            anim.SetTrigger("die");
        }

        Destroy(gameObject, 0.7f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseAggroRadius);

        if (handPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(handPoint.position, 0.08f);
        }
    }
}
