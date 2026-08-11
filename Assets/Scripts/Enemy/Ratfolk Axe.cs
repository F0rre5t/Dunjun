using UnityEngine;
using System.Collections;

public class RatfolkAxe : Enemy
{
    [Header("Ratfolk Specifics")]
    public float detectionRadius = 5f;
    public float loseAggroRadius = 10f; 
    public float attackRadius = 1.5f;   
    
    [Header("Attack Timers")]
    public float attackCooldown = 1.2f; 
    public float attackDuration = 0.5f; 

    [Header("Attack Damage")]
    public int attackDamage = 1;
    public Transform attackPoint;
    public Vector2 attackBoxSize = new Vector2(0.8f, 0.6f);
    public Vector2 attackBoxOffset = new Vector2(0.4f, 0f);
    public LayerMask playerLayer;

    private Transform player;
    private float cooldownTimer; 
    private float animTimer;     
    private float hurtTimer; 
    
    private SpriteRenderer sr;
    private HealthManager healthManager;

    private bool canDealDamage;

    protected override void Awake()
    {
        base.Awake();
        chaseStandoffDistance = Mathf.Max(chaseStandoffDistance, attackRadius * 0.85f);
        sr = GetComponent<SpriteRenderer>(); 
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) 
        {
            player = playerObj.transform;
        }

        healthManager = FindAnyObjectByType<HealthManager>();
    }

    protected override void Update()
    {
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
            }
            return; 
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float currentSight = (currentState == State.Chasing || currentState == State.Attacking) ? loseAggroRadius : detectionRadius;

        if (distanceToPlayer <= attackRadius)
        {
            if (cooldownTimer <= 0)
            {
                currentState = State.Attacking;
                currentSpeed = 0;              
                moveDirection = Vector2.zero;
                
                animTimer = attackDuration;    
                cooldownTimer = attackCooldown;

                canDealDamage = true;

                TryFaceTarget(player.position);

                if (anim != null) 
                {
                    anim.SetTrigger("attack"); 
                }
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
        else
        {
            if (currentState == State.Chasing || currentState == State.Attacking)
            {
                currentState = State.Idle;
                currentSpeed = normalSpeed;
                stateTimer = idleTime;
                moveDirection = Vector2.zero;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseAggroRadius);

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
        NotifyDeath();
        TryDropLoot();

        hurtTimer = 999f; 
        HaltMovement();

        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null) coll.enabled = false;

        if (anim != null)
        {
            anim.SetTrigger("die"); 
        }

        Destroy(gameObject, 1f);
    }

    public void DealDamage()
    {
        if (!canDealDamage) return;
        if (attackPoint == null) return;

        Vector2 center = (Vector2)attackPoint.position + new Vector2(
            attackBoxOffset.x * Mathf.Sign(transform.localScale.x),
            attackBoxOffset.y);

        Collider2D hit = Physics2D.OverlapBox(center, attackBoxSize, 0f, playerLayer);
        if (hit == null) return;

        canDealDamage = false;

        if (healthManager != null)
        {
            healthManager.TakeDamage(attackDamage);
        }
    }
}
