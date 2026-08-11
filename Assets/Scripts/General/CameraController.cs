using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController instance;

    public float speed;

    public Transform target;

    [Header("Fit Room In View")]
    [Tooltip("Half-width of a room that must stay visible (doors are ~9)")]
    public float fitHalfWidth = 9.5f;
    [Tooltip("Half-height of a room that must stay visible (doors are ~5)")]
    public float fitHalfHeight = 5.5f;
    [Tooltip("If on, orthographic size always covers fitHalfWidth/Height for current aspect")]
    public bool autoFitRoom = true;

    Camera cam;

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        ApplyRoomFit();
    }

    void Start()
    {
        ApplyRoomFit();
    }

    void Update()
    {
        if (autoFitRoom)
        {
            ApplyRoomFit();
        }

        if (target != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                new Vector3(target.position.x, target.position.y, transform.position.z),
                speed * Time.deltaTime);
        }
    }

    public void Changetarget(Transform newtarget)
    {
        target = newtarget;
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    public void ApplyRoomFit()
    {
        if (!autoFitRoom || cam == null || !cam.orthographic)
        {
            return;
        }

        float aspect = cam.aspect;
        if (aspect < 0.01f)
        {
            return;
        }

        // Orthographic size is vertical half-extent. Grow it until both width and height fit.
        float sizeForHeight = fitHalfHeight;
        float sizeForWidth = fitHalfWidth / aspect;
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }
}
