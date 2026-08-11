using UnityEngine;

public class CoinBag : MonoBehaviour
{
    [Header("Defaults (overridden by GoblinKing on spawn)")]
    public float speed = 5f;
    public int damage = 1;
    public float maxDistance = 9f;
    public LayerMask playerLayer;

    Vector2 direction = Vector2.right;
    Vector2 startPosition;
    bool flying;
    bool consumed;
    Transform followPoint;
    Transform owner;
    Transform player;
    HealthManager healthManager;
    SpriteRenderer spriteRenderer;
    Collider2D bodyCollider;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        healthManager = FindAnyObjectByType<HealthManager>();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }
    }

    public void Arm(
        Transform hand,
        Transform ownerRoot,
        Transform playerTarget,
        float launchSpeed,
        int launchDamage,
        float launchMaxDistance,
        LayerMask launchPlayerLayer,
        Collider2D ownerCollider,
        bool launchImmediately = true)
    {
        followPoint = hand;
        owner = ownerRoot;
        player = playerTarget;
        speed = Mathf.Max(0.1f, launchSpeed);
        damage = Mathf.Max(0, launchDamage);
        maxDistance = Mathf.Max(0.1f, launchMaxDistance);
        playerLayer = launchPlayerLayer;

        flying = false;
        consumed = false;

        transform.SetParent(null, true);
        SnapToFollowPoint();
        UpdateFacingPreview();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
            if (ownerCollider != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, ownerCollider, true);
            }
        }

        if (launchImmediately)
        {
            BeginFlight();
        }
    }

    void LateUpdate()
    {
        if (!flying && !consumed)
        {
            SnapToFollowPoint();
        }
    }

    void FixedUpdate()
    {
        if (!flying || consumed)
        {
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.fixedDeltaTime);

        if (Vector2.Distance(startPosition, transform.position) >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void SnapToFollowPoint()
    {
        if (followPoint != null)
        {
            transform.position = followPoint.position;
        }
    }

    public void BeginFlight()
    {
        if (flying || consumed)
        {
            return;
        }

        SnapToFollowPoint();
        transform.SetParent(null, true);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        if (player != null)
        {
            direction = (Vector2)player.position - (Vector2)transform.position;
        }
        else if (followPoint != null && owner != null)
        {
            float dx = followPoint.position.x - owner.position.x;
            float face = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : 1f;
            direction = Vector2.right * face;
        }
        else
        {
            direction = Vector2.right;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        startPosition = transform.position;
        flying = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direction.x < 0f;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }
    }

    void UpdateFacingPreview()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (player != null)
        {
            spriteRenderer.flipX = player.position.x < transform.position.x;
            return;
        }

        if (followPoint != null && owner != null)
        {
            spriteRenderer.flipX = followPoint.position.x < owner.position.x;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!flying || consumed || other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & playerLayer) == 0)
        {
            return;
        }

        consumed = true;
        flying = false;

        HealthManager targetHealth = other.GetComponent<HealthManager>();
        if (targetHealth == null)
        {
            targetHealth = other.GetComponentInParent<HealthManager>();
        }

        if (targetHealth == null)
        {
            targetHealth = healthManager;
        }

        if (targetHealth != null && damage > 0)
        {
            targetHealth.TakeDamage(damage);
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        Destroy(gameObject);
    }
}
