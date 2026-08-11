using UnityEngine;
using UnityEngine.UI;

// World-space shop labels: price above, name and effect below.
public class ShopRelicLabel : MonoBehaviour
{
    const float WorldScale = 0.01f;
    const float GapBelowSprite = 0.2f;
    const float GapAboveSprite = 0.2f;
    const float LabelWidth = 180f;
    const float EffectLineSpacing = 1.15f;

    [SerializeField] Color nameColor = new Color(1f, 0.92f, 0.72f, 1f);
    [SerializeField] Color effectColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] Color priceColor = new Color(1f, 0.86f, 0.35f, 1f);

    RectTransform priceRoot;
    Text priceText;
    Text nameText;
    Text effectText;

    public static ShopRelicLabel Attach(RelicPickup pickup)
    {
        if (pickup == null)
        {
            return null;
        }

        ShopRelicLabel existing = pickup.GetComponentInChildren<ShopRelicLabel>();
        if (existing != null)
        {
            // Rebuild so layout changes apply to already spawned shops.
            Transform price = pickup.transform.Find("ShopPrice");
            if (price != null)
            {
                Object.Destroy(price.gameObject);
            }

            Object.Destroy(existing.gameObject);
        }

        GameObject root = new GameObject("ShopLabel");
        root.transform.SetParent(pickup.transform, false);

        ShopRelicLabel label = root.AddComponent<ShopRelicLabel>();
        label.Build(pickup);
        return label;
    }

    void Build(RelicPickup pickup)
    {
        BuildBelowLabel(pickup);
        BuildPriceLabel(pickup);
        Refresh(pickup);
        Reposition(pickup);
    }

    void BuildBelowLabel(RelicPickup pickup)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Door";
        canvas.sortingOrder = 20;

        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(LabelWidth, 140f);

        float parentScale = Mathf.Max(0.001f, pickup.transform.lossyScale.x);
        root.localScale = Vector3.one * (WorldScale / parentScale);

        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        nameText = CreateText(root, "Name", 22, FontStyle.Bold, nameColor, EffectLineSpacing);
        effectText = CreateText(root, "Effect", 16, FontStyle.Normal, effectColor, EffectLineSpacing);
    }

    void BuildPriceLabel(RelicPickup pickup)
    {
        GameObject priceObject = new GameObject("ShopPrice", typeof(RectTransform));
        priceObject.transform.SetParent(pickup.transform, false);
        priceRoot = priceObject.GetComponent<RectTransform>();

        Canvas canvas = priceObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Door";
        canvas.sortingOrder = 21;

        float parentScale = Mathf.Max(0.001f, pickup.transform.lossyScale.x);
        priceRoot.sizeDelta = new Vector2(LabelWidth, 32f);
        priceRoot.localScale = Vector3.one * (WorldScale / parentScale);

        priceText = CreateText(priceRoot, "Price", 20, FontStyle.Bold, priceColor, 1f);
        priceText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        priceText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        priceText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        priceText.rectTransform.anchoredPosition = Vector2.zero;
        priceText.alignment = TextAnchor.MiddleCenter;
        priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        priceText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void Refresh(RelicPickup pickup)
    {
        RelicData data = pickup != null ? pickup.RelicData : null;

        if (nameText != null)
        {
            nameText.text = data != null ? data.GetDisplayName() : (pickup != null ? pickup.name : string.Empty);
        }

        if (effectText != null)
        {
            effectText.text = data != null ? data.GetBriefEffect() : string.Empty;
        }

        if (priceText != null)
        {
            if (pickup != null && pickup.IsShopOffer)
            {
                priceText.text = $"{pickup.ShopPrice} Gold";
                priceText.gameObject.SetActive(true);
                if (priceRoot != null)
                {
                    priceRoot.gameObject.SetActive(true);
                }
            }
            else
            {
                priceText.text = string.Empty;
                if (priceRoot != null)
                {
                    priceRoot.gameObject.SetActive(false);
                }
            }
        }

        // Force layout after text changes so name/effect don't overlap.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }

    void Reposition(RelicPickup pickup)
    {
        SpriteRenderer renderer = pickup.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = pickup.GetComponentInChildren<SpriteRenderer>();
        }

        Bounds bounds = renderer != null
            ? renderer.bounds
            : new Bounds(pickup.transform.position, Vector3.one);

        transform.position = new Vector3(bounds.center.x, bounds.min.y - GapBelowSprite, pickup.transform.position.z);
        transform.rotation = Quaternion.identity;

        if (priceRoot != null)
        {
            priceRoot.position = new Vector3(bounds.center.x, bounds.max.y + GapAboveSprite, pickup.transform.position.z);
            priceRoot.rotation = Quaternion.identity;
        }
    }

    static Text CreateText(RectTransform parent, string objectName, int fontSize, FontStyle style, Color color, float lineSpacing)
    {
        GameObject go = new GameObject(objectName);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(LabelWidth, 0f);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minWidth = LabelWidth;
        layout.preferredWidth = LabelWidth;
        layout.flexibleWidth = 0f;

        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text text = go.AddComponent<Text>();
        text.font = UiFonts.Get();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = lineSpacing;
        text.raycastTarget = false;
        return text;
    }
}
