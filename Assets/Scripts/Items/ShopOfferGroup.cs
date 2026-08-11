using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Groups shop relic offers. Player may buy any/all offers as long as they can afford them.
/// </summary>
public class ShopOfferGroup : MonoBehaviour
{
    readonly List<RelicPickup> offers = new List<RelicPickup>();

    public void Register(RelicPickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        offers.Add(pickup);
        pickup.BindShopOffer(this);
    }

    public void NotifyPurchased(RelicPickup purchased)
    {
        if (purchased == null)
        {
            return;
        }

        offers.Remove(purchased);
    }
}
