using UnityEngine;

[System.Serializable]
public class SpikeShapeWeight
{
    public SpikeTrapPattern.Shape shape = SpikeTrapPattern.Shape.Cross;
    [Min(0f)] public float weight = 1f;
}

[System.Serializable]
public class RoomSpikeEncounter
{
    
    public int minStep = 1;
    
    public int maxStep = 1;

    [Range(0f, 1f)] public float spawnChance = 1f;

    public SpikeShapeWeight[] shapes;

    [Header("Optional Overrides")]
    
    [Min(0)] public int countOverride;
    
    [Min(0f)] public float radiusOverride;
    
    public bool randomizeLineOrientation = true;
}
