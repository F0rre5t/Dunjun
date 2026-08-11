using UnityEngine;

public class Chest : Enemy
{
    bool opened;

    protected override void Awake()
    {
        base.Awake();
        maxHealth = 1;
        currentHealth = 1;
        normalSpeed = 0f;
        chaseSpeed = 0f;
        currentSpeed = 0f;
        moveDirection = Vector2.zero;
        currentState = State.Idle;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.mass = 1f;
        }
    }

    protected override void Start()
    {
        NormalizeChildFacingScale();
    }

    protected override void Update()
    {
    }

    protected override void FixedUpdate()
    {
    }

    public override void Move()
    {
    }

    public override void TakeDamage(int damage, bool playHitFlash = true)
    {
        if (opened)
        {
            return;
        }

        Die();
    }

    protected override void Die()
    {
        if (opened)
        {
            return;
        }

        opened = true;

        ChestRewardDropper dropper = GetComponent<ChestRewardDropper>();
        if (dropper != null)
        {
            dropper.DropReward(transform.position);
        }

        // Real chests do not count toward room clear.
        Destroy(gameObject);
    }
}
