using UnityEngine;

public class AnimatorEventForwarder : MonoBehaviour
{
    public void DealDamage()
    {
        Minotaur minotaur = GetComponentInParent<Minotaur>();
        if (minotaur != null)
        {
            minotaur.DealDamage();
            return;
        }

        RatfolkAxe ratfolk = GetComponentInParent<RatfolkAxe>();
        if (ratfolk != null)
        {
            ratfolk.DealDamage();
        }
    }

    public void SpawnFireball()
    {
        Imp imp = GetComponentInParent<Imp>();
        if (imp != null)
        {
            imp.SpawnFireball();
        }
    }

    public void SpawnCoinBag()
    {
        GoblinKing goblinKing = GetComponentInParent<GoblinKing>();
        if (goblinKing != null)
        {
            goblinKing.SpawnCoinBag();
        }
    }

    public void DealSlamDamage()
    {
        GoblinKing goblinKing = GetComponentInParent<GoblinKing>();
        if (goblinKing != null)
        {
            goblinKing.DealSlamDamage();
        }
    }
}
