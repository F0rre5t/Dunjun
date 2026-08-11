using UnityEngine;
using System.Collections;

[System.Serializable]
public class MinotaurAttack
{
    
    public float attackRadius = 1.5f;

    public float attackDuration = 0.6f;

    public int attackDamage = 1;

    public float weight = 1f;

    public float damageHitDelay = 0f;

    public Vector2 attackBoxSize = new Vector2(0.8f, 0.6f);
    public Vector2 attackBoxOffset = new Vector2(0.4f, 0f);
}

public class Minotaur : Enemy
{
    [Header("Minotaur Specifics")]
    public float detectionRadius = 5f;
    public float loseAggroRadius = 10f;

    [Header("Attack Timers")]
    public float attackCooldown = 1.2f;

    [Header("Attacks")]
    public MinotaurAttack[] attacks = new MinotaurAttack[3]
    {
        new MinotaurAttack { attackRadius = 1.2f, attackDuration = 0.5f, attackDamage = 1, weight = 3f },
        new MinotaurAttack { attackRadius = 2.0f, attackDuration = 1f, attackDamage = 2, weight = 2f },
        new MinotaurAttack { attackRadius = 3.0f, attackDuration = 0.9f, attackDamage = 2, weight = 1f },
    };

    [Header("Attack Hitbox")]
    public Transform attackPoint;
    public LayerMask playerLayer;

    private Transform player;
    private float cooldownTimer;
    private float animTimer;
    private float hurtTimer;

    private SpriteRenderer sr;
    private HealthManager healthManager;

    private bool canDealDamage;
    private int currentAttackIndex = -1;

    private static readonly string[] AttackTriggers = { "attack1", "attack2", "attack3" };

    private bool isDead;
    private float attackPointBaseX;

    protected override void Awake()
    {
        base.Awake();
        sr = GetComponentInChildren<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        healthManager = FindAnyObjectByType<HealthManager>();

        if (attackPoint != null)
        {
            attackPointBaseX = Mathf.Abs(attackPoint.localPosition.x);
        }
    }

    protected override void Start()
    {
        base.Start();
        MirrorAttackPoint();
    }

    protected override void OnFacingDirectionChanged(float previousSign, float newSign)
    {
        MirrorAttackPoint();
        ResolveSpawnPenetration();
    }

    private void MirrorAttackPoint()
    {
        if (attackPoint == null || attackPointBaseX <= 0f) return;

        Vector3 localPos = attackPoint.localPosition;
        localPos.x = attackPointBaseX * FacingSign;
        attackPoint.localPosition = localPos;
    }

    protected override void Update()
    {
        if (isDead) return;

        if (hurtTimer > 0)
        {
            hurtTimer -= Time.deltaTime;
        }
        else
        {
            if (cooldownTimer > 0)
            {
                cooldownTimer -= Time.deltaTime;
            }

            CheckDetection();
        }

        base.Update();
    }

    private void CheckDetection()
    {
        player = ResolvePlayer(player);
        if (player == null)
        {
            return;
        }

        if (hurtTimer > 0)
        {
            return;
        }

        if (currentState == State.Attacking)
        {
            animTimer -= Time.deltaTime;
            if (animTimer <= 0)
            {
                currentState = State.Chasing;
                currentAttackIndex = -1;
            }
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float currentSight = (currentState == State.Chasing || currentState == State.Attacking)
            ? loseAggroRadius
            : detectionRadius;

        int chosenAttack = PickAttackIndex(distanceToPlayer);

        if (chosenAttack >= 0)
        {
            if (cooldownTimer <= 0)
            {
                StartAttack(chosenAttack, distanceToPlayer);
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

    private int PickAttackIndex(float distanceToPlayer)
    {
        if (attacks == null || attacks.Length == 0) return -1;

        float totalWeight = 0f;
        for (int i = 0; i < attacks.Length; i++)
        {
            MinotaurAttack attack = attacks[i];
            if (attack == null || attack.weight <= 0f) continue;
            if (distanceToPlayer <= attack.attackRadius)
            {
                totalWeight += attack.weight;
            }
        }

        if (totalWeight <= 0f) return -1;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < attacks.Length; i++)
        {
            MinotaurAttack attack = attacks[i];
            if (attack == null || attack.weight <= 0f) continue;
            if (distanceToPlayer > attack.attackRadius) continue;

            cumulative += attack.weight;
            if (roll <= cumulative)
            {
                return i;
            }
        }

        return -1;
    }

    private void StartAttack(int attackIndex, float distanceToPlayer)
    {
        MinotaurAttack attack = attacks[attackIndex];
        currentAttackIndex = attackIndex;

        currentState = State.Attacking;
        currentSpeed = 0;
        moveDirection = Vector2.zero;

        float clipLength = GetAttackClipLength(attackIndex);
        animTimer = attack.attackDuration > 0f ? attack.attackDuration : clipLength;
        cooldownTimer = attackCooldown;
        canDealDamage = true;

        TryFaceTarget(player.position);

        if (anim != null && attackIndex < AttackTriggers.Length)
        {
            anim.SetTrigger(AttackTriggers[attackIndex]);
        }

        ScheduleDamageHit(attack, clipLength);
    }

    private float GetAttackClipLength(int attackIndex)
    {
        if (attackIndex < 0 || attackIndex >= AttackTriggers.Length) return 0.75f;

        string clipName = AttackTriggers[attackIndex];
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == clipName)
                {
                    return clips[i].length;
                }
            }
        }

        if (attacks != null && attackIndex < attacks.Length && attacks[attackIndex] != null)
        {
            return attacks[attackIndex].attackDuration;
        }

        return 0.75f;
    }

    private void ScheduleDamageHit(MinotaurAttack attack, float clipLength)
    {
        CancelInvoke(nameof(DealDamage));

        float delay = attack.damageHitDelay > 0f
            ? attack.damageHitDelay
            : clipLength * 0.5f;
        delay = Mathf.Clamp(delay, 0.05f, clipLength);

        Invoke(nameof(DealDamage), delay);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(DealDamage));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseAggroRadius);

        if (attacks == null) return;

        Color[] attackColors = { Color.red, new Color(1f, 0.5f, 0f), Color.magenta };
        float facing = FacingSign;

        for (int i = 0; i < attacks.Length; i++)
        {
            MinotaurAttack attack = attacks[i];
            if (attack == null) continue;

            Gizmos.color = i < attackColors.Length ? attackColors[i] : Color.white;
            Gizmos.DrawWireSphere(transform.position, attack.attackRadius);

            if (attackPoint != null)
            {
                // Mirror X only so Y offsets stay upright when facing left.
                Vector3 center = attackPoint.position + new Vector3(
                    attack.attackBoxOffset.x * facing,
                    attack.attackBoxOffset.y,
                    0f);
                Gizmos.DrawWireCube(center, attack.attackBoxSize);
            }
        }
    }

    public override void Move()
    {
        if (currentState == State.Attacking || hurtTimer > 0)
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
        if (anim != null)
        {
            bool isMovingState = (currentState == State.Moving || currentState == State.Chasing);
            if (hurtTimer > 0) isMovingState = false;
            anim.SetBool("isMoving", isMovingState);
        }
    }

    public override void TakeDamage(int damage, bool playHitFlash = true)
    {
        base.TakeDamage(damage, playHitFlash);

        if (currentHealth <= 0) return;

        if (currentState != State.Attacking)
        {
            if (anim != null)
            {
                anim.SetTrigger("damage");
                hurtTimer = 0.3f;
                HaltMovement();
            }
        }
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        NotifyDeath();
        TryDropLoot();

        currentState = State.Idle;
        hurtTimer = 0f;
        HaltMovement();
        CancelInvoke(nameof(DealDamage));

        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null) coll.enabled = false;

        if (anim != null)
        {
            anim.SetBool("isMoving", false);
            anim.ResetTrigger("attack1");
            anim.ResetTrigger("attack2");
            anim.ResetTrigger("attack3");
            anim.ResetTrigger("damage");
            anim.SetTrigger("die");
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        const float fallbackDeathDuration = 0.5f;
        float waitDuration = fallbackDeathDuration;

        if (anim != null)
        {
            yield return null;

            AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0 && clips[0].clip != null)
            {
                waitDuration = clips[0].clip.length;
            }

            float elapsed = 0f;
            while (elapsed < waitDuration)
            {
                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("death") && state.normalizedTime >= 1f && !anim.IsInTransition(0))
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(waitDuration);
        }

        Destroy(gameObject);
    }

    public void DealDamage()
    {
        if (!canDealDamage) return;
        if (attackPoint == null) return;
        if (currentAttackIndex < 0 || currentAttackIndex >= attacks.Length) return;

        MinotaurAttack attack = attacks[currentAttackIndex];
        // Mirror X only; AttackPoint already flips with facing.
        Vector2 center = (Vector2)attackPoint.position + new Vector2(
            attack.attackBoxOffset.x * FacingSign,
            attack.attackBoxOffset.y);

        Collider2D hit = Physics2D.OverlapBox(center, attack.attackBoxSize, 0f, playerLayer);
        if (hit == null) return;

        canDealDamage = false;

        if (healthManager == null)
        {
            healthManager = hit.GetComponent<HealthManager>();
            if (healthManager == null)
            {
                healthManager = hit.GetComponentInParent<HealthManager>();
            }
        }

        if (healthManager != null)
        {
            healthManager.TakeDamage(attack.attackDamage);
        }
    }
}
