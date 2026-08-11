using UnityEngine;

public class KeyPickup : PickupBase
{
    [SerializeField] string rotateStateName = "Rotate";
    [SerializeField] Animator animator;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (animator != null && !string.IsNullOrEmpty(rotateStateName))
        {
            animator.Play(rotateStateName, 0, 0f);
        }
    }

    protected override bool OnPickedUp(Collider2D player)
    {
        KeyInventory.CollectKey();
        return true;
    }
}