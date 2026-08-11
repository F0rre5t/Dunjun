# Dunjun

Procedural dungeon roguelike with in-run dynamic difficulty (MSc project).

## Playable build

Playable build (itch.io): *add your itch link here*

## Controls

- **WASD** — move
- **Left mouse** — attack
- **Q** — relic inventory

## Source overview

- `Assets/Scripts/General/RoomGenerator.cs` — procedural room graph, depth, shops, boss room
- `Assets/Scripts/General/DifficultyDirector.cs` — distress model and aid policy (A/B toggle)
- `Assets/Scripts/General/GameFlowController.cs` — menu, difficulty, DDA switch
- `Assets/Scripts/General/Room.cs` — combat rooms, loot, spikes, shop offers
- `Assets/Data/Relics/` — relic ScriptableObjects

## Open in Unity

1. Install **Unity 6000.3.10f1** (or matching Unity 6)
2. Open this folder as a Unity project
3. Open `Assets/Scenes/SampleScene.unity`
4. Enter Play mode (menu flow: choose difficulty, optional DDA toggle)

For marking, prefer the itch build above. This repository is the source and supporting material.
