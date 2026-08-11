using UnityEngine;

[CreateAssetMenu(fileName = "NewConsumable", menuName = "Rouge/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;

    [Header("UI")]
    [TextArea(2, 4)]
    public string description;

    [Header("Heal")]
    
    [Min(0)] public int healAmount = 1;

    public bool ignoreWhenFullHealth = true;

    [Header("Gold")]
    
    [Min(0)] public int goldAmount;

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
