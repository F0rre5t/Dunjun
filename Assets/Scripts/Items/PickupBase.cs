using UnityEngine;

public class PickupBase : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!OnPickedUp(other)) return;
        Destroy(gameObject);
    }

    /// <summary>
    /// Return true if the pickup was consumed and should be destroyed.
    /// </summary>
    protected virtual bool OnPickedUp(Collider2D player)
    {
        return true;
    }
}
