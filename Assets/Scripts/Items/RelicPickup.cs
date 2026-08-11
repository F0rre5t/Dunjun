using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RelicPickup : PickupBase
{
    [SerializeField] RelicData relicData;

    ShopOfferGroup shopOfferGroup;
    int shopPrice = -1;

    public RelicData RelicData => relicData;
    public bool IsShopOffer => shopOfferGroup != null;
    public int ShopPrice => Mathf.Max(0, shopPrice);

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

    public void SetRelicData(RelicData data)
    {
        relicData = data;
        ApplyVisualFromData();
    }

    public void BindShopOffer(ShopOfferGroup group)
    {
        shopOfferGroup = group;
        shopPrice = relicData != null ? relicData.RollShopPrice() : 0;
    }

    void ApplyVisualFromData()
    {
        if (relicData == null || relicData.hudIcon == null)
        {
            return;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = relicData.hudIcon;
        }
    }

    protected override bool OnPickedUp(Collider2D player)
    {
        if (relicData == null)
        {
            Debug.LogWarning($"RelicPickup on {name} has no RelicData assigned.");
            return false;
        }

        int price = shopOfferGroup != null ? ShopPrice : 0;
        if (price > 0 && !GoldInventory.TrySpend(price))
        {
            return false;
        }

        if (!RelicInventory.TryAdd(relicData))
        {
            if (price > 0)
            {
                GoldInventory.Add(price);
            }

            return false;
        }

        RelicEffectApplier applier = player.GetComponent<RelicEffectApplier>();
        if (applier == null)
        {
            applier = player.GetComponentInParent<RelicEffectApplier>();
        }

        if (applier != null)
        {
            applier.ApplyRelic(relicData);
        }

        shopOfferGroup?.NotifyPurchased(this);
        return true;
    }
}
