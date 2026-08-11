using UnityEngine;
using System.Collections;

public class Mimic : Enemy
{
    [Header("Mimic Specifics")]
    public float surpriseDuration = 0.45f;
    public float attackRadius = 1f;
    public float attackCooldown = 1.2f;

    [Header("Attack Damage")]
    public int attackDamage = 1;
    public Transform attackPoint;
    public Vector2 attackBoxSize = new Vector2(0.8f, 0.6f);
    public Vector2 attackBoxOffset = new Vector2(0.4f, 0f);
    public LayerMask playerLayer;

    private Transform player;
    private float surpriseTimer;
    private float cooldownTimer;

    private bool isAwakened;
    private bool isDying;

    private SpriteRenderer sr;
    private HealthManager healthManager;

    protected override void Awake()
    {
        base.Awake();
        chaseStandoffDistance = attackRadius * 0.85f;
        sr = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        healthManager = FindAnyObjectByType<HealthManager>();

        currentState = State.Idle;
        currentSpeed = 0f;
        moveDirection = Vector2.zero;
        stateTimer = 0f;

        if (anim != null)
        {
            anim.enabled = false;
        }
    }

    protected override void Update()
    {
        if (isDying)
        {
            return;
        }

        if (!isAwakened)
        {
            return;
        }

        if (surpriseTimer > 0f)
        {
            surpriseTimer -= Time.deltaTime;
            if (surpriseTimer <= 0f)
            {
                currentState = State.Chasing;
            }

            UpdateAnimation();
            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        ChasePlayer();
        UpdateAnimation();
    }

    private void ChasePlayer()
    {
        player = ResolvePlayer(player);
        if (player == null)
        {
            return;
        }

        currentState = State.Chasing;
        TryFaceTarget(player.position);

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRadius)
        {
            if (cooldownTimer <= 0f)
            {
                TryDealDamage(distanceToPlayer);
                cooldownTimer = attackCooldown;
            }

            currentSpeed = chaseSpeed;
            moveDirection = ((Vector2)(player.position - transform.position)).normalized;
            return;
        }

        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        SetChaseMovement(toPlayer, distanceToPlayer, chaseSpeed);
    }

    private void TryDealDamage(float distanceToPlayer)
    {
        if (healthManager == null)
        {
            return;
        }

        if (distanceToPlayer <= attackRadius)
        {
            healthManager.TakeDamage(attackDamage);
            return;
        }

        if (attackPoint == null)
        {
            return;
        }

        Vector2 center = (Vector2)attackPoint.position + new Vector2(
            attackBoxOffset.x * Mathf.Sign(transform.localScale.x),
            attackBoxOffset.y);
        Collider2D hit = Physics2D.OverlapBox(center, attackBoxSize, 0f, playerLayer);
        if (hit == null)
        {
            return;
        }

        healthManager.TakeDamage(attackDamage);
    }

    public override void Move()
    {
        if (!isAwakened || surpriseTimer > 0f || isDying)
        {
            HaltMovement();
            return;
        }

        base.Move();
    }

    protected override void UpdateAnimation()
    {
        if (anim == null || !anim.enabled || isDying)
        {
            return;
        }

        bool isMoving = isAwakened
            && surpriseTimer <= 0f
            && currentState == State.Chasing;

        anim.SetBool("isMoving", isMoving);
    }

    public override void TakeDamage(int damage, bool playHitFlash = true)
    {
        if (isDying)
        {
            return;
        }

        base.TakeDamage(damage, playHitFlash);

        if (currentHealth <= 0)
        {
            return;
        }

        if (!isAwakened)
        {
            Awaken();
        }
    }

    private void Awaken()
    {
        isAwakened = true;
        surpriseTimer = surpriseDuration;
        currentState = State.Idle;
        HaltMovement();

        player = ResolvePlayer(player);
        if (player != null)
        {
            TryFaceTarget(player.position);
        }

        if (anim != null)
        {
            anim.enabled = true;
            anim.SetTrigger("surprise");
        }
    }

    protected override void Die()
    {
        if (isDying)
        {
            return;
        }

        isDying = true;

        NotifyDeath();
        TryDropLoot();
        HaltMovement();

        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null)
        {
            coll.enabled = false;
        }

        if (anim != null)
        {
            anim.enabled = true;
            anim.SetBool("isMoving", false);
            anim.ResetTrigger("surprise");
            anim.ResetTrigger("die");
            anim.Play("death", 0, 0f);
        }

        Destroy(gameObject, 0.55f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        if (attackPoint != null)
        {
            Vector3 center = attackPoint.position + new Vector3(
                attackBoxOffset.x * Mathf.Sign(transform.localScale.x),
                attackBoxOffset.y,
                0f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, attackBoxSize);
        }
    }
}
