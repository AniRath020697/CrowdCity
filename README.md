# CrowD City

CS617 final project — grow your crowd across three districts of one city map.

## Team

- **Team CrowD City**
- Aniruddha Rath | ar35067n@pace.edu
- Om Jadhav | oj82545n@pace.edu

## How to play

1. Open the project in **Unity 6** (same version used for development).
2. Open **MainMenu** and press **Play**, or build with **File → Build Settings** (scenes are already listed).
3. **Main menu:** Space — start game.
4. **Gameplay:** WASD move. Recruit gray neutrals by touching them. Win a wave by defeating **all enemy leaders** or surviving until the timer ends. Lose if an enemy wins a crowd battle.
5. **Wave super powers** (one per wave): Wave 1 **Shift** Turbo Dash · Wave 2 **F** Rally Cry · Wave 3 shockwave echo bonus. **E** shockwave on every wave (**2 uses per wave**). Enemies can shockwave too (**2 uses per leader per wave**).
6. **Pause:** Escape.
7. **Win / Lose:** Space — retry · **M** — main menu.

## Scenes (build order)

1. `Assets/Scenes/MainMenu.unity`
2. `Assets/Scenes/SampleScene.unity` (three wave districts on one map)
3. `Assets/Scenes/LoseScene.unity`
4. `Assets/Scenes/WinScene.unity`

## Notes for graders

- Wave progression and enemy counts are driven by `WaveManager` (3 / 5 / 7 hunters; followers per leader 0 / 1 / 2).
- City boundary clamping uses `CityPlayableBounds` on the **City Boundary** walls (no map props removed).
- NavMesh should cover **streets only** so crowds stay on sidewalks (see `NavMeshSceneBootstrap` comments).
