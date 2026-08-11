using UnityEngine;

public class DoorState : MonoBehaviour
{
    [SerializeField] GameObject closedVisual;
    [SerializeField] GameObject openVisual;
    [SerializeField] bool startOpen = false;

    private void Awake()
    {
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        if (closedVisual != null) closedVisual.SetActive(!open);
        if (openVisual != null) openVisual.SetActive(open);
    }
}