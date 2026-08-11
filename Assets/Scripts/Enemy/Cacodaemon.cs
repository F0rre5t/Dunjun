using UnityEngine;

public class Cacodaemon : Enemy
{
    [Header("Cacodaemon Specifics")]
    public float detectionRadius = 5f;
    public float windupDuration = 0.85f;
    public float chargeSpeed = 6f;
    public float chargeCooldown = 1.25f;

    [Header("Contact Damage")]
    public float attackRadius = 1f;
    public float attackCooldown = 1.2f;
    public int attackDamage = 1;
    public Transform attackPoint;
    public Vector2 attackBoxSize = new Vector2(0.8f, 0.6f);
    public Vector2 attackBoxOffset = new Vector2(0.4f, 0f);
    public LayerMask playerLayer;

    private Transform player;
    private float windupTimer;
    private float contactCooldownTimer;
    private float chargeReadyTimer;
    private float hurtTimer;

    private Vector2 chargeDirection;
    private bool isDying;

    private HealthManager healthManager;

    protected override void Awake()
    {
        base.Awake();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        healthManager = FindAnyObjectByType<HealthManager>();
    }

    protected override void Update()
    {
        if (isDying)
        {
            return;
        }

        if (hurtTimer > 0f)
        {
            hurtTimer -= Time.deltaTime;
            UpdateAnimation();
            return;
        }

        if (contactCooldownTimer > 0f)
        {
            contactCooldownTimer -= Time.deltaTime;
        }

        if (chargeReadyTimer > 0f)
        {
            chargeReadyTimer -= Time.deltaTime;
        }

        if (currentState == State.Charging)
        {
            TryContactDamage();
            UpdateAnimation();
            return;
        }

        if (currentState == State.Attacking)
        {
            UpdateWindup();
            TryContactDamage();
            UpdateAnimation();
            return;
        }

        CheckDetection();

        if (currentState == State.Moving || currentState == State.Idle)
        {
            base.Update();
        }
        else
        {
            UpdateAnimation();
        }
    }

    private void CheckDetection()
    {
        player = ResolvePlayer(player);
        if (player == null || chargeReadyTimer > 0f)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer > detectionRadius)
        {
            return;
        }

        BeginWindup();
    }

    private void BeginWindup()
    {
        currentState = State.Attacking;
        windupTimer = windupDuration;
        HaltMovement();

        player = ResolvePlayer(player);
        if (player != null)
        {
            TryFaceTarget(player.position);
        }

        if (anim != null)
        {
            anim.SetBool("isCharging", true);
        }
    }

    private void UpdateWindup()
    {
        player = ResolvePlayer(player);
        if (player != null)
        {
            TryFaceTarget(player.position);
        }

        windupTimer -= Time.deltaTime;
        if (windupTimer > 0f)
        {
            return;
        }

        BeginCharge();
    }

    private void BeginCharge()
    {
        player = ResolvePlayer(player);

        Vector2 toTarget = Vector2.right * FacingSign;
        if (player != null)
        {
            toTarget = (Vector2)(player.position - transform.position);
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = Vector2.right * FacingSign;
            }
        }

        chargeDirection = toTarget.normalized;
        currentState = State.Charging;
        currentSpeed = chargeSpeed;
        moveDirection = chargeDirection;
        TryFaceTarget(transform.position + (Vector3)chargeDirection);

        if (anim != null)
        {
            anim.SetBool("isCharging", true);
        }
    }

    private void EndCharge()
    {
        currentState = State.Idle;
        stateTimer = idleTime;
        HaltMovement();
        chargeReadyTimer = chargeCooldown;

        if (anim != null)
        {
            anim.SetBool("isCharging", false);
        }
    }

    private void TryContactDamage()
    {
        if (contactCooldownTimer > 0f || healthManager == null)
        {
            return;
        }

        player = ResolvePlayer(player);
        if (player == null)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer > attackRadius)
        {
            return;
        }

        healthManager.TakeDamage(attackDamage);
        contactCooldownTimer = attackCooldown;
    }

    public override void Move()
    {
        if (isDying || hurtTimer > 0f || currentState == State.Attacking)
        {
            HaltMovement();
            return;
        }

        if (currentState == State.Charging)
        {
            if (rb == null || chargeDirection.sqrMagnitude < 0.0001f)
            {
                EndCharge();
                return;
            }

            currentSpeed = chargeSpeed;
            moveDirection = chargeDirection;

            Vector2 delta = chargeDirection * GetModifiedSpeed(chargeSpeed) * Time.fixedDeltaTime;
            if (!TryMove(delta))
            {
                EndCharge();
            }

            return;
        }

        base.Move();
    }

    protected override void UpdateAnimation()
    {
        if (anim == null || isDying)
        {
            return;
        }

        bool charging = currentState == State.Attacking || currentState == State.Charging;
        anim.SetBool("isCharging", charging);

        bool isMoving = !charging
            && hurtTimer <= 0f
            && (currentState == State.Moving || currentState == State.Chasing);

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

        if (currentState == State.Attacking || currentState == State.Charging)
        {
            return;
        }

        if (anim != null)
        {
            anim.SetTrigger("damage");
        }

        hurtTimer = 0.3f;
        HaltMovement();
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
            anim.SetBool("isCharging", false);
            anim.SetBool("isMoving", false);
            anim.ResetTrigger("damage");
            anim.SetTrigger("die");
        }

        Destroy(gameObject, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

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
