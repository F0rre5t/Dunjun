using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ConsumablePickup : PickupBase
{
    [SerializeField] ConsumableData consumableData;

    public ConsumableData ConsumableData => consumableData;

    void Awake()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
            return;
        }

        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    public void SetConsumableData(ConsumableData data)
    {
        consumableData = data;
    }

    protected override bool OnPickedUp(Collider2D player)
    {
        if (consumableData == null)
        {
            Debug.LogWarning($"ConsumablePickup on {name} has no ConsumableData assigned.");
            return false;
        }

        bool applied = false;

        if (consumableData.healAmount > 0)
        {
            HealthManager health = FindHealthManager(player);
            if (health == null)
            {
                Debug.LogWarning("ConsumablePickup: HealthManager not found.");
            }
            else if (consumableData.ignoreWhenFullHealth && health.currentHealth >= health.maxHealth)
            {
                // Heal-only pickups stay on the ground at full HP.
                if (consumableData.goldAmount <= 0)
                {
                    return false;
                }
            }
            else if (health.Heal(consumableData.healAmount))
            {
                applied = true;
            }
        }

        if (consumableData.goldAmount > 0)
        {
            GoldInventory.Add(consumableData.goldAmount);
            applied = true;
        }

        return applied;
    }

    static HealthManager FindHealthManager(Collider2D player)
    {
        HealthManager health = player.GetComponent<HealthManager>();
        if (health != null)
        {
            return health;
        }

        health = player.GetComponentInParent<HealthManager>();
        if (health != null)
        {
            return health;
        }

        return Object.FindAnyObjectByType<HealthManager>();
    }
}
