using UnityEngine;

public class GoblinKing : Enemy
{
    enum SkillPhase
    {
        None,
        Grabbing,
        Throwing,
        Jumping,    // Play jump on the ground before leaving
        Ascending,  // Rise while holding the last jump frame
        Airborne,   // Optional pause at the apex
        Slamming,   // Fall and play slam
        Eating,
        Panicking
    }

    [Header("Goblin King Specifics")]
    public float detectionRadius = 7f;
    public float loseAggroRadius = 12f;
    public float throwAttackRadius = 6f;
    public float slamAttackRadius = 3.5f;

    [Header("Attack Timers")]
    public float attackCooldown = 1.8f;
    public float grabDuration = 1f;
    public float throwDuration = 0.9f;
    
    public float coinBagSpawnDelay = 0f;
    
    public float jumpDuration = 1f;
    
    public float airHoverDuration = 0f;
    
    public float slamDuration = 0.8f;
    public float eatDuration = 1.35f;
    public float panicDuration = 1.35f;

    [Header("Skill Weights")]
    public float throwWeight = 3f;
    public float slamWeight = 2f;
    
    public float eatWeight = 2f;
    [Range(0.05f, 1f)]
    public float eatHealthThreshold = 0.5f;

    [Header("Coin Bag Throw")]
    public CoinBag coinBagPrefab;
    public Transform handPoint;
    public float coinBagSpeed = 5f;
    public int coinBagDamage = 1;
    public float coinBagMaxDistance = 9f;

    [Header("Jump Slam")]
    public float airChaseSpeed = 4.5f;
    
    public float jumpUpSpeed = 9f;
    
    public float jumpGravity = 14f;
    
    public float slamGravity = 36f;
    
    public float maxFlightHeight = 3f;
    public int slamDamage = 2;
    public float slamHitRadius = 1.35f;
    
    public float slamHitDelay = 0.05f;
    
    public Transform attackPoint;
    public LayerMask playerLayer;

    [Header("Eat Heal")]
    public int eatHealAmount = 8;
    
    public int eatInterruptBonusDamage = 50;

    Transform player;
    HealthManager healthManager;
    Collider2D bodyCollider;
    Collider2D playerCollider;
    float cooldownTimer;
    float phaseTimer;
    float hurtTimer;
    float handPointBaseX;
    bool isDead;
    bool canSpawnCoinBag;
    bool canDealSlamDamage;
    bool eatHealPending;
    SkillPhase skillPhase = SkillPhase.None;
    Vector3 visualBaseLocalPos;
    Transform visualTransform;
    float flightHeight;
    float verticalVelocity;
    bool slamDamagePending;
    bool slamLanding;

    protected override void Awake()
    {
        base.Awake();
        chaseStandoffDistance = Mathf.Max(chaseStandoffDistance, slamAttackRadius * 0.35f);
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

        if (anim != null && anim.transform != transform)
        {
            visualTransform = anim.transform;
            visualBaseLocalPos = visualTransform.localPosition;
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
        else if (skillPhase != SkillPhase.None)
        {
            TickSkillPhase();
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

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float currentSight = (currentState == State.Chasing || currentState == State.Attacking || currentState == State.Charging)
            ? loseAggroRadius
            : detectionRadius;

        int skill = PickSkill(distanceToPlayer);
        if (skill >= 0)
        {
            if (cooldownTimer <= 0f)
            {
                BeginSkill(skill);
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
        else if (currentState == State.Chasing || currentState == State.Attacking || currentState == State.Charging)
        {
            currentState = State.Idle;
            currentSpeed = normalSpeed;
            stateTimer = idleTime;
            moveDirection = Vector2.zero;
        }
    }

    int PickSkill(float distanceToPlayer)
    {
        float totalWeight = 0f;
        bool canThrow = distanceToPlayer <= throwAttackRadius && throwWeight > 0f;
        bool canSlam = distanceToPlayer <= slamAttackRadius && slamWeight > 0f;
        bool canEat = IsLowHealth() && eatWeight > 0f && distanceToPlayer <= throwAttackRadius;

        if (canThrow) totalWeight += throwWeight;
        if (canSlam) totalWeight += slamWeight;
        if (canEat) totalWeight += eatWeight;

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        if (canThrow)
        {
            cumulative += throwWeight;
            if (roll <= cumulative) return 0;
        }

        if (canSlam)
        {
            cumulative += slamWeight;
            if (roll <= cumulative) return 1;
        }

        if (canEat)
        {
            return 2;
        }

        return -1;
    }

    bool IsLowHealth()
    {
        return maxHealth > 0 && (float)currentHealth / maxHealth <= eatHealthThreshold;
    }

    void BeginSkill(int skill)
    {
        cooldownTimer = attackCooldown;
        TryFaceTarget(player.position);
        MirrorHandPoint();

        switch (skill)
        {
            case 0:
                BeginThrowSequence();
                break;
            case 1:
                BeginJumpSequence();
                break;
            case 2:
                BeginEat();
                break;
        }
    }

    void BeginThrowSequence()
    {
        skillPhase = SkillPhase.Grabbing;
        currentState = State.Attacking;
        HaltMovement();
        phaseTimer = Mathf.Max(0.05f, grabDuration);
        canSpawnCoinBag = false;

        if (anim != null)
        {
            anim.SetTrigger("grab");
        }
    }

    void BeginJumpSequence()
    {
        // Finish the jump clip on the ground first.
        skillPhase = SkillPhase.Jumping;
        currentState = State.Attacking;
        HaltMovement();
        phaseTimer = Mathf.Max(0.05f, jumpDuration);
        canDealSlamDamage = false;
        slamDamagePending = false;
        slamLanding = false;
        flightHeight = 0f;
        verticalVelocity = 0f;
        ApplyFlightVisual();

        if (anim != null)
        {
            anim.ResetTrigger("slam");
            anim.SetTrigger("jump");
        }
    }

    void BeginEat()
    {
        skillPhase = SkillPhase.Eating;
        currentState = State.Attacking;
        HaltMovement();
        phaseTimer = Mathf.Max(0.05f, eatDuration);
        eatHealPending = true;

        if (anim != null)
        {
            anim.SetTrigger("eat");
        }
    }

    void BeginPanic()
    {
        CancelInvoke(nameof(SpawnCoinBag));
        CancelInvoke(nameof(DealSlamDamage));
        canSpawnCoinBag = false;
        canDealSlamDamage = false;
        eatHealPending = false;

        skillPhase = SkillPhase.Panicking;
        currentState = State.Attacking;
        HaltMovement();
        phaseTimer = Mathf.Max(0.05f, panicDuration);
        ResetFlight();
        RestoreAnimatorSpeed();
        SetIgnorePlayerCollision(false);

        if (anim != null)
        {
            anim.ResetTrigger("eat");
            anim.SetTrigger("panic");
        }

        ApplyEatInterruptBonusDamage();
    }

    void ApplyEatInterruptBonusDamage()
    {
        if (isDead || eatInterruptBonusDamage <= 0)
        {
            return;
        }

        // Call base so we do not re-enter skill interrupt logic.
        base.TakeDamage(eatInterruptBonusDamage, playHitFlash: true);
    }

    void TickSkillPhase()
    {
        phaseTimer -= Time.deltaTime;

        switch (skillPhase)
        {
            case SkillPhase.Grabbing:
                HaltMovement();
                if (phaseTimer <= 0f)
                {
                    EnterThrow();
                }
                break;

            case SkillPhase.Throwing:
                HaltMovement();
                // Spawn the coin bag when throw finishes, or on timeout.
                if (IsAnimatorStateFinished("throw") || phaseTimer <= 0f)
                {
                    if (canSpawnCoinBag)
                    {
                        SpawnCoinBag();
                    }

                    EndSkill();
                }
                break;

            case SkillPhase.Jumping:
                // Start rising after jump finishes or times out.
                HaltMovement();
                if (IsAnimatorStateFinished("jump") || phaseTimer <= 0f)
                {
                    LaunchAscending();
                }
                break;

            case SkillPhase.Ascending:
                // Rise without chasing sideways into the player.
                HaltMovement();
                if (verticalVelocity <= 0f)
                {
                    verticalVelocity = 0f;
                    if (airHoverDuration > 0f)
                    {
                        EnterAirborne();
                    }
                    else
                    {
                        EnterSlam();
                    }
                }
                break;

            case SkillPhase.Airborne:
                UpdateAirborneChase();
                if (phaseTimer <= 0f)
                {
                    EnterSlam();
                }
                break;

            case SkillPhase.Slamming:
                if (slamLanding)
                {
                    // Finish slam on the ground before resolving damage.
                    HaltMovement();
                    if (IsAnimatorStateFinished("slam") || phaseTimer <= 0f)
                    {
                        if (slamDamagePending)
                        {
                            canDealSlamDamage = true;
                            DealSlamDamage();
                        }

                        EndSkill();
                    }

                    break;
                }

                // Hold slam frame one while falling toward the landing point.
                HoldSlamFirstFrame();
                UpdateAirborneChase();
                if (flightHeight <= 0f && verticalVelocity <= 0f)
                {
                    LandFromSlam();
                }
                else if (phaseTimer <= 0f)
                {
                    flightHeight = 0f;
                    verticalVelocity = 0f;
                    LandFromSlam();
                }
                break;

            case SkillPhase.Eating:
                HaltMovement();
                if (phaseTimer <= 0f)
                {
                    FinishEatHeal();
                    EndSkill();
                }
                break;

            case SkillPhase.Panicking:
                HaltMovement();
                if (phaseTimer <= 0f)
                {
                    EndSkill();
                }
                break;
        }
    }

    void EnterThrow()
    {
        skillPhase = SkillPhase.Throwing;
        currentState = State.Attacking;
        HaltMovement();
        phaseTimer = Mathf.Max(0.05f, throwDuration);
        canSpawnCoinBag = true;

        TryFaceTarget(player != null ? player.position : transform.position + faceDir);
        MirrorHandPoint();

        if (anim != null)
        {
            anim.SetTrigger("throw");
        }

        CancelInvoke(nameof(SpawnCoinBag));
        // Prefer throw completion; a positive delay is only a fallback.
        if (coinBagSpawnDelay > 0f)
        {
            float delay = Mathf.Clamp(coinBagSpawnDelay, 0.01f, Mathf.Max(0.01f, throwDuration));
            Invoke(nameof(SpawnCoinBag), delay);
        }
    }

    void LaunchAscending()
    {
        skillPhase = SkillPhase.Ascending;
        currentState = State.Charging;
        HaltMovement();
        flightHeight = 0f;
        verticalVelocity = Mathf.Max(0.1f, jumpUpSpeed);
        // Ignore player collision while airborne.
        SetIgnorePlayerCollision(true);
        ApplyFlightVisual();
        // Jump has no exit transition so it freezes on the last frame.
    }

    void EnterAirborne()
    {
        skillPhase = SkillPhase.Airborne;
        currentState = State.Charging;
        phaseTimer = Mathf.Max(0.05f, airHoverDuration);
        verticalVelocity = 0f;
        ApplyFlightVisual();
    }

    void EnterSlam()
    {
        skillPhase = SkillPhase.Slamming;
        currentState = State.Charging;
        // Timeout guard for the fall; play slam after landing.
        phaseTimer = 3f;
        canDealSlamDamage = false;
        slamDamagePending = true;
        slamLanding = false;
        verticalVelocity = Mathf.Min(verticalVelocity, 0f);

        player = ResolvePlayer(player);
        if (player != null)
        {
            TryFaceTarget(player.position);
        }

        // Enter fall on slam frame one.
        if (anim != null)
        {
            anim.ResetTrigger("jump");
            anim.ResetTrigger("slam");
            anim.Play("slam", 0, 0f);
            anim.speed = 0f;
        }
    }

    void HoldSlamFirstFrame()
    {
        if (anim == null || slamLanding)
        {
            return;
        }

        anim.speed = 0f;
        if (anim.IsInTransition(0))
        {
            return;
        }

        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName("slam") || info.normalizedTime > 0.001f)
        {
            anim.Play("slam", 0, 0f);
        }
    }

    bool IsAnimatorStateFinished(string stateName)
    {
        if (anim == null)
        {
            return true;
        }

        if (anim.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName) && info.normalizedTime >= 1f;
    }

    void LandFromSlam()
    {
        if (slamLanding || skillPhase != SkillPhase.Slamming)
        {
            return;
        }

        slamLanding = true;
        flightHeight = 0f;
        verticalVelocity = 0f;
        ApplyFlightVisual();
        HaltMovement();

        // Only after landing do we play slam through.
        phaseTimer = Mathf.Max(0.05f, slamDuration);
        if (anim != null)
        {
            anim.speed = 1f;
            anim.Play("slam", 0, 0f);
        }

        // Optional delayed smash damage during slam.
        if (slamDamagePending && slamHitDelay > 0f)
        {
            canDealSlamDamage = true;
            CancelInvoke(nameof(DealSlamDamage));
            Invoke(nameof(DealSlamDamage), slamHitDelay);
        }
    }

    void UpdateAirborneChase()
    {
        player = ResolvePlayer(player);
        if (player == null || rb == null)
        {
            HaltMovement();
            return;
        }

        TryFaceTarget(player.position);
        // Aim with the foot AttackPoint, not body center.
        Vector2 landingNode = attackPoint != null
            ? (Vector2)attackPoint.position
            : (Vector2)transform.position;
        Vector2 toPlayer = (Vector2)player.position - landingNode;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            HaltMovement();
            return;
        }

        currentSpeed = airChaseSpeed;
        moveDirection = toPlayer.normalized;
        currentState = State.Charging;
    }

    void IntegrateFlight(float gravity)
    {
        verticalVelocity -= gravity * Time.fixedDeltaTime;
        flightHeight += verticalVelocity * Time.fixedDeltaTime;

        if (flightHeight > maxFlightHeight)
        {
            flightHeight = maxFlightHeight;
            if (verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }
        }

        if (flightHeight < 0f)
        {
            flightHeight = 0f;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
            }
        }

        ApplyFlightVisual();
    }

    void ApplyFlightVisual()
    {
        if (visualTransform == null)
        {
            return;
        }

        Vector3 localPos = visualTransform.localPosition;
        localPos.y = visualBaseLocalPos.y + flightHeight;
        visualTransform.localPosition = localPos;
    }

    void ResetFlight()
    {
        flightHeight = 0f;
        verticalVelocity = 0f;
        slamDamagePending = false;
        ApplyFlightVisual();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (flightHeight > 0f
            || skillPhase == SkillPhase.Ascending
            || skillPhase == SkillPhase.Airborne
            || skillPhase == SkillPhase.Slamming)
        {
            ApplyFlightVisual();
        }
    }

    void FinishEatHeal()
    {
        if (!eatHealPending || isDead)
        {
            return;
        }

        eatHealPending = false;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, eatHealAmount));
    }

    public void SpawnCoinBag()
    {
        if (!canSpawnCoinBag || isDead || coinBagPrefab == null)
        {
            return;
        }

        canSpawnCoinBag = false;
        CancelInvoke(nameof(SpawnCoinBag));
        ResolveHandPoint();
        MirrorHandPoint();

        Vector3 spawnPos = handPoint != null ? handPoint.position : transform.position;
        player = ResolvePlayer(player);

        CoinBag bag = Instantiate(coinBagPrefab, spawnPos, Quaternion.identity);
        bag.transform.SetParent(null, true);
        bag.transform.position = spawnPos;
        bag.Arm(
            handPoint,
            transform,
            player,
            coinBagSpeed,
            coinBagDamage,
            coinBagMaxDistance,
            playerLayer,
            bodyCollider,
            launchImmediately: true);
    }

    public void DealSlamDamage()
    {
        if (!canDealSlamDamage || isDead)
        {
            return;
        }

        canDealSlamDamage = false;
        slamDamagePending = false;
        CancelInvoke(nameof(DealSlamDamage));

        if (healthManager == null)
        {
            return;
        }

        Vector2 center = attackPoint != null
            ? (Vector2)attackPoint.position
            : (Vector2)transform.position;

        Collider2D hit = Physics2D.OverlapCircle(center, slamHitRadius, playerLayer);
        if (hit == null)
        {
            return;
        }

        healthManager.TakeDamage(slamDamage);
    }

    void EndSkill()
    {
        CancelInvoke(nameof(SpawnCoinBag));
        CancelInvoke(nameof(DealSlamDamage));
        canSpawnCoinBag = false;
        canDealSlamDamage = false;
        eatHealPending = false;
        slamDamagePending = false;
        slamLanding = false;
        skillPhase = SkillPhase.None;
        ResetFlight();
        RestoreAnimatorSpeed();
        SetIgnorePlayerCollision(false);
        ResolveSpawnPenetration();

        currentState = State.Chasing;
        HaltMovement();
        ReturnToLocomotionAnimation();
    }

    void RestoreAnimatorSpeed()
    {
        if (anim != null)
        {
            anim.speed = 1f;
        }
    }

    void ReturnToLocomotionAnimation()
    {
        if (anim == null || isDead)
        {
            return;
        }

        RestoreAnimatorSpeed();
        anim.ResetTrigger("jump");
        anim.ResetTrigger("slam");
        // Jump/slam freeze on purpose; force idle when the skill ends.
        anim.Play("idle", 0, 0);
    }

    public override void Move()
    {
        if (isDead || hurtTimer > 0f)
        {
            HaltMovement();
            return;
        }

        if (skillPhase == SkillPhase.Jumping)
        {
            // Ground wind-up with no movement.
            return;
        }

        if (skillPhase == SkillPhase.Ascending)
        {
            // Vertical rise only.
            IntegrateFlight(jumpGravity);
            return;
        }

        if (skillPhase == SkillPhase.Airborne)
        {
            verticalVelocity = 0f;
            ApplyFlightVisual();
            TryAirChaseMove();
            return;
        }

        if (skillPhase == SkillPhase.Slamming)
        {
            if (!slamLanding)
            {
                IntegrateFlight(slamGravity);
                TryAirChaseMove();
            }
            return;
        }

        if (skillPhase != SkillPhase.None || currentState == State.Attacking)
        {
            HaltMovement();
            return;
        }

        base.Move();
    }

    void TryAirChaseMove()
    {
        if (rb == null || moveDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 delta = moveDirection * GetModifiedSpeed(airChaseSpeed) * Time.fixedDeltaTime;
        if (TryAirMove(delta))
        {
            return;
        }

        // Slide along walls instead of getting stuck.
        Vector2 slideX = new Vector2(delta.x, 0f);
        Vector2 slideY = new Vector2(0f, delta.y);
        if (slideX.sqrMagnitude > 0f)
        {
            TryAirMove(slideX);
        }

        if (slideY.sqrMagnitude > 0f)
        {
            TryAirMove(slideY);
        }
    }

    bool TryAirMove(Vector2 delta)
    {
        if (rb == null || delta.sqrMagnitude <= 0f)
        {
            return true;
        }

        float distance = delta.magnitude;
        Vector2 direction = delta / distance;

        // Air movement ignores players and enemies.
        int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        int playerLayerIndex = LayerMask.NameToLayer("Player");
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (playerLayerIndex >= 0) mask &= ~(1 << playerLayerIndex);
        if (enemyLayerIndex >= 0) mask &= ~(1 << enemyLayerIndex);

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

    void SetIgnorePlayerCollision(bool ignore)
    {
        if (bodyCollider == null)
        {
            return;
        }

        player = ResolvePlayer(player);
        if (player == null)
        {
            return;
        }

        if (playerCollider == null || playerCollider.transform != player)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (playerCollider != null)
        {
            Physics2D.IgnoreCollision(bodyCollider, playerCollider, ignore);
        }
    }

    protected override void UpdateAnimation()
    {
        if (anim == null || isDead)
        {
            return;
        }

        // In attack range on cooldown, play idle instead of move.
        bool busy = skillPhase != SkillPhase.None || hurtTimer > 0f;
        anim.SetBool("isMoving", !busy && IsCurrentlyMoving());
    }

    public override void TakeDamage(int damage, bool playHitFlash = true)
    {
        base.TakeDamage(damage, playHitFlash);
        if (currentHealth <= 0 || isDead)
        {
            return;
        }

        if (skillPhase == SkillPhase.Eating)
        {
            BeginPanic();
            return;
        }

        if (skillPhase != SkillPhase.None)
        {
            return;
        }

        if (anim != null)
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
        CancelInvoke(nameof(SpawnCoinBag));
        CancelInvoke(nameof(DealSlamDamage));
        canSpawnCoinBag = false;
        canDealSlamDamage = false;
        eatHealPending = false;
        slamDamagePending = false;
        slamLanding = false;
        skillPhase = SkillPhase.None;
        ResetFlight();
        RestoreAnimatorSpeed();
        SetIgnorePlayerCollision(false);

        NotifyDeath();
        TryDropLoot();
        HaltMovement();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (anim != null)
        {
            anim.SetBool("isMoving", false);
            anim.ResetTrigger("damage");
            anim.SetTrigger("die");
        }

        Destroy(gameObject, 1f);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(SpawnCoinBag));
        CancelInvoke(nameof(DealSlamDamage));
        RestoreAnimatorSpeed();
        SetIgnorePlayerCollision(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, throwAttackRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamAttackRadius);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseAggroRadius);

        Vector3 slamCenter = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawWireSphere(slamCenter, slamHitRadius);

        if (handPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(handPoint.position, 0.08f);
        }
    }
}
