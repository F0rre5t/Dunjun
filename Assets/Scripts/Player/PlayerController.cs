using UnityEngine;

public class PlayerControl : MonoBehaviour 
{
    Rigidbody2D rb;
    Animator anim;
    
    private HealthManager healthManager;
    RelicEffectApplier relicEffectApplier;

    [Header("Movement")]
    public float speed;
    Vector2 movement;
    private bool isAttacking = false;

    [Header("Combat Settings")]
    public Transform attackPoint;      
    public float attackRange = 0.5f;   
    public int attackDamage = 10;      
    public LayerMask enemyLayers;

    Collider2D bodyCollider;
    const float CastSkin = 0.02f;
    static readonly float[] UnstickRadii = { 0.02f, 0.04f, 0.06f, 0.08f, 0.12f, 0.18f, 0.28f };
    readonly Collider2D[] overlapBuffer = new Collider2D[8];
    readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        ConfigureCombatPhysics();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        healthManager = GetComponent<HealthManager>();
        if (healthManager == null)
        {
            healthManager = FindAnyObjectByType<HealthManager>();
        }

        relicEffectApplier = GetComponent<RelicEffectApplier>();
    }

    void Update()
    {
        if (healthManager != null && healthManager.IsDead)
        {
            movement = Vector2.zero;
            return;
        }

        if (!isAttacking)
        {
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.y = Input.GetAxisRaw("Vertical");

            if (movement.x != 0)
            {
                transform.localScale = new Vector3(movement.x, 1, 1);
            }
        }
        else
        {
            movement = Vector2.zero; 
        }

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            isAttacking = true;
            anim.SetTrigger("doAttack");
        }

        SwitchAnim();
    }

    private void FixedUpdate()
    {
        if (healthManager != null && healthManager.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // If stuck in a wall, unstick this frame instead of fighting input.
        if (HasSolidOverlap())
        {
            TryUnstick();
            return;
        }

        Vector2 delta = movement * speed * Time.fixedDeltaTime;
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        if (TryCastMove(delta, out RaycastHit2D hit, allowPartialMove: false))
        {
            return;
        }

        Vector2 beforeAxes = rb.position;
        TryCastMove(new Vector2(delta.x, 0f), out _);
        TryCastMove(new Vector2(0f, delta.y), out _);
        if ((rb.position - beforeAxes).sqrMagnitude > 0f)
        {
            return;
        }

        if (hit.collider == null)
        {
            return;
        }

        Vector2 slide = delta - Vector2.Dot(delta, hit.normal) * hit.normal;
        if (slide.sqrMagnitude > 0f)
        {
            TryCastMove(slide, out _);
        }
    }

    ContactFilter2D SolidFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer),
            useTriggers = false
        };
    }

    bool HasSolidOverlap()
    {
        if (bodyCollider == null)
        {
            return false;
        }

        return bodyCollider.Overlap(SolidFilter(), overlapBuffer) > 0;
    }

    void SetBodyPosition(Vector2 position)
    {
        rb.position = position;
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    bool WouldOverlapAt(Vector2 position)
    {
        Vector2 original = rb.position;
        SetBodyPosition(position);
        bool overlaps = HasSolidOverlap();
        SetBodyPosition(original);
        return overlaps;
    }

    void TryUnstick()
    {
        if (bodyCollider == null || rb == null)
        {
            return;
        }

        Vector2 origin = rb.position;

        // Prefer free space along the input direction first.
        if (movement.sqrMagnitude > 0f)
        {
            Vector2 wish = movement.normalized;
            for (int i = 0; i < UnstickRadii.Length; i++)
            {
                Vector2 candidate = origin + wish * UnstickRadii[i];
                if (!WouldOverlapAt(candidate))
                {
                    SetBodyPosition(candidate);
                    return;
                }
            }
        }

        // Ring-search a free point; avoid concave Distance normals.
        for (int i = 0; i < UnstickRadii.Length; i++)
        {
            float radius = UnstickRadii[i];
            const int steps = 16;
            for (int step = 0; step < steps; step++)
            {
                float angle = step * Mathf.PI * 2f / steps;
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!WouldOverlapAt(candidate))
                {
                    SetBodyPosition(candidate);
                    return;
                }
            }
        }
    }

    bool TryCastMove(Vector2 delta, out RaycastHit2D hit, bool allowPartialMove = true)
    {
        hit = default;

        if (delta.sqrMagnitude <= 0f || rb == null)
        {
            return false;
        }

        float distance = delta.magnitude;
        Vector2 direction = delta / distance;
        ContactFilter2D filter = SolidFilter();

        int hitCount = rb.Cast(direction, filter, castBuffer, distance + CastSkin);

        float allowedDistance = distance;
        if (hitCount > 0)
        {
            int closestIndex = 0;
            float closestDistance = castBuffer[0].distance;
            for (int i = 1; i < hitCount; i++)
            {
                if (castBuffer[i].distance < closestDistance)
                {
                    closestDistance = castBuffer[i].distance;
                    closestIndex = i;
                }
            }

            hit = castBuffer[closestIndex];
            allowedDistance = Mathf.Min(distance, closestDistance - CastSkin);
        }

        bool movedFully = allowedDistance >= distance * 0.95f;
        if (!allowPartialMove && hitCount > 0 && !movedFully)
        {
            return false;
        }

        if (allowedDistance <= 0f)
        {
            return false;
        }

        SetBodyPosition(rb.position + direction * allowedDistance);
        return movedFully;
    }

    void SwitchAnim()
    {
        anim.SetFloat("Speed", movement.magnitude);
    }

    public void DamageEnemy()
    {
        if (attackPoint == null) return;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
        bool dealtDamage = false;

        foreach (Collider2D enemyCollider in hitEnemies)
        {
            Enemy enemyScript = enemyCollider.GetComponent<Enemy>();
            
            if (enemyScript != null)
            {
                int damage = attackDamage;
                if (relicEffectApplier != null)
                {
                    damage = relicEffectApplier.ResolveDamageAgainst(enemyScript, attackDamage);
                }

                enemyScript.TakeDamage(damage);
                if (relicEffectApplier != null)
                {
                    relicEffectApplier.ApplyOnHitEffects(enemyScript);
                }

                dealtDamage = true;
            }
        }

        if (dealtDamage && relicEffectApplier != null)
        {
            relicEffectApplier.TryHealOnAttack();
        }
    }

    public void AttackFinished()
    {
        isAttacking = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    public void ForceStop()
    {
        isAttacking = false;
        movement = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public static void ConfigureCombatPhysics()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
        }

        if (playerLayer >= 0 && enemyLayer >= 0)
        {
            // Movement ignores enemy hard colliders; attacks still use overlaps.
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }
    }
}
