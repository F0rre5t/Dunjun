using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PoisonCloud : MonoBehaviour
{
    static Sprite sharedSprite;

    SpriteRenderer spriteRenderer;
    CircleCollider2D trigger;
    float lifetime;
    float spawnTime;
    float startScale;
    float endScale;
    int damagePerTick;
    float poisonDuration;
    float tickInterval;
    float reapplyCooldown;
    float maxTotalExposure;
    bool poisonIsPermanent;
    bool isActive;

    public static Sprite GetSharedSprite()
    {
        if (sharedSprite != null)
        {
            return sharedSprite;
        }

        const int size = 24;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.44f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - distance / radius);
                alpha = alpha * alpha * alpha;
                pixels[y * size + x] = new Color(0.35f, 0.98f, 0.22f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
        return sharedSprite;
    }

    void Awake()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSharedSprite();
        spriteRenderer.sortingLayerName = "Ground";
        spriteRenderer.sortingOrder = 6;

        trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
    }

    public void Activate(
        Vector3 position,
        float radius,
        float cloudLifetime,
        int poisonDamagePerTick,
        float poisonDebuffDuration,
        float poisonTickInterval,
        float poisonReapplyCooldown,
        float poisonMaxTotalExposure,
        bool permanent,
        SpriteRenderer sortReference)
    {
        transform.position = position;
        lifetime = Mathf.Max(0.2f, cloudLifetime);
        spawnTime = Time.time;
        float visualScale = Mathf.Max(0.8f, radius * 2.4f);
        startScale = visualScale;
        endScale = visualScale * 1.25f;
        damagePerTick = poisonDamagePerTick;
        poisonDuration = poisonDebuffDuration;
        tickInterval = poisonTickInterval;
        reapplyCooldown = poisonReapplyCooldown;
        maxTotalExposure = poisonMaxTotalExposure;
        poisonIsPermanent = permanent;
        isActive = true;

        trigger.radius = 0.5f;
        transform.localScale = Vector3.one * startScale;

        if (sortReference != null)
        {
            spriteRenderer.sortingLayerID = sortReference.sortingLayerID;
            spriteRenderer.sortingOrder = sortReference.sortingOrder - 1;
        }
        else
        {
            spriteRenderer.sortingLayerName = "Ground";
            spriteRenderer.sortingOrder = 6;
        }

        spriteRenderer.color = new Color(1f, 1f, 1f, 0.95f);
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        float elapsed = Time.time - spawnTime;
        float progress = Mathf.Clamp01(elapsed / lifetime);
        float scale = Mathf.Lerp(startScale, endScale, progress);
        transform.localScale = new Vector3(scale, scale, 1f);

        Color color = spriteRenderer.color;
        color.a = Mathf.Lerp(0.9f, 0f, progress);
        spriteRenderer.color = color;

        if (elapsed >= lifetime)
        {
            PoisonTrailSpawner.Recycle(this);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryPoisonEnemy(other);
    }

    void TryPoisonEnemy(Collider2D other)
    {
        if (!isActive || damagePerTick <= 0)
        {
            return;
        }

        if (!poisonIsPermanent && poisonDuration <= 0f)
        {
            return;
        }

        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = other.GetComponentInParent<Enemy>();
        }

        if (enemy != null)
        {
            enemy.ApplyPoisonFromTrail(
                damagePerTick,
                poisonDuration,
                tickInterval,
                reapplyCooldown,
                maxTotalExposure,
                poisonIsPermanent);
        }
    }
}
