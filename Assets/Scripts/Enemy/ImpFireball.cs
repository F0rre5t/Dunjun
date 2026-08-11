using UnityEngine;

public class ImpFireball : MonoBehaviour
{
    [Header("Defaults (overridden by Imp on spawn)")]
    public float speed = 4.5f;
    public int damage = 1;
    public float maxDistance = 8f;
    public LayerMask playerLayer;

    [Header("Sprite Animation")]
    
    public Sprite[] frames;
    
    public int holdFrame = 4;
    public float frameDuration = 0.1f;

    Vector2 direction = Vector2.right;
    Vector2 startPosition;
    bool charging;
    bool flying;
    bool consumed;
    bool ending;
    int frameIndex;
    float frameTimer;
    int holdIndex;
    Transform followPoint;
    Transform owner;
    Transform player;
    HealthManager healthManager;
    SpriteRenderer spriteRenderer;
    Animator animator;
    Collider2D bodyCollider;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        healthManager = FindAnyObjectByType<HealthManager>();

        if (animator != null)
        {
            animator.enabled = false;
        }

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
        Collider2D ownerCollider)
    {
        followPoint = hand;
        owner = ownerRoot;
        player = playerTarget;
        speed = Mathf.Max(0.1f, launchSpeed);
        damage = Mathf.Max(0, launchDamage);
        maxDistance = Mathf.Max(0.1f, launchMaxDistance);
        playerLayer = launchPlayerLayer;

        charging = true;
        flying = false;
        consumed = false;
        ending = false;
        frameIndex = 0;
        frameTimer = 0f;
        holdIndex = Mathf.Clamp(holdFrame, 1, GetFrameCount()) - 1;

        // Follow the hand in world space instead of parenting under its scale.
        transform.SetParent(null, true);
        SnapToFollowPoint();
        ApplyFrame(0);
        UpdateFacingPreview();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
            if (ownerCollider != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, ownerCollider, true);
            }
        }
    }

    void LateUpdate()
    {
        if (charging)
        {
            SnapToFollowPoint();
        }
    }

    void SnapToFollowPoint()
    {
        if (followPoint != null)
        {
            transform.position = followPoint.position;
        }
    }

    void Update()
    {
        if (ending)
        {
            TickEndFrames();
            return;
        }

        if (charging)
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            TickChargeFrames();
            return;
        }

        if (flying)
        {
            // Keep the held frame while flying.
        }
    }

    void FixedUpdate()
    {
        if (!flying || consumed || ending)
        {
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.fixedDeltaTime);

        if (Vector2.Distance(startPosition, transform.position) >= maxDistance)
        {
            BeginEndAndDestroy();
        }
    }

    void TickChargeFrames()
    {
        if (frames == null || frames.Length == 0)
        {
            BeginFlight();
            return;
        }

        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration)
        {
            return;
        }

        frameTimer = 0f;

        if (frameIndex < holdIndex)
        {
            frameIndex++;
            ApplyFrame(frameIndex);
            UpdateFacingPreview();

            if (frameIndex >= holdIndex)
            {
                BeginFlight();
            }
        }
        else
        {
            BeginFlight();
        }
    }

    void TickEndFrames()
    {
        if (frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration)
        {
            return;
        }

        frameTimer = 0f;
        frameIndex++;
        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        ApplyFrame(frameIndex);
    }

    void BeginFlight()
    {
        if (flying || ending)
        {
            return;
        }

        charging = false;
        flying = true;
        frameIndex = holdIndex;
        ApplyFrame(holdIndex);

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
            direction = ((Vector2)player.position - (Vector2)transform.position);
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
        if (!flying || consumed || ending || other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & playerLayer) == 0)
        {
            return;
        }

        consumed = true;

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

        BeginEndAndDestroy();
    }

    void BeginEndAndDestroy()
    {
        if (ending)
        {
            return;
        }

        ending = true;
        charging = false;
        flying = false;
        frameTimer = 0f;

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (frames != null && holdIndex + 1 < frames.Length)
        {
            frameIndex = holdIndex + 1;
            ApplyFrame(frameIndex);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    int GetFrameCount()
    {
        return frames != null && frames.Length > 0 ? frames.Length : 1;
    }

    void ApplyFrame(int index)
    {
        if (spriteRenderer == null || frames == null || index < 0 || index >= frames.Length)
        {
            return;
        }

        if (frames[index] != null)
        {
            spriteRenderer.sprite = frames[index];
        }
    }
}
