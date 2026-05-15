using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Optionally builds a <see cref="NavMeshSurface"/> when the scene loads.
/// For a city pack, bake only streets — not buildings, plants, or lamps — or crowds will walk on roofs and props.
/// </summary>
[DefaultExecutionOrder(-200)]
public class NavMeshSceneBootstrap : MonoBehaviour
{
    [Tooltip("Drag the NavMesh Surface that should cover walkable ground.")]
    public NavMeshSurface surface;

    [Tooltip("When true, calls BuildNavMesh() in Awake. Turn off after you bake to disk to avoid long load times.")]
    public bool buildOnAwake;

    /*
     * RECOMMENDED BAKE SETUP (Unity AI Navigation package):
     * 1. Edit → Project Settings → Tags and Layers → add layer "WalkableGround".
     * 2. Put ONLY sidewalks / roads / plaza floors on WalkableGround (MeshCollider or box on ground).
     * 3. Move buildings, trees, lamps, benches to layer "Default" or "Props" — do NOT include them in the bake.
     * 4. Select your Navmesh object → NavMesh Surface:
     *    - Collect Objects: All (or a parent that only contains streets)
     *    - Include Layers: WalkableGround ONLY (not Everything)
     *    - Agent Radius ~0.5, Height ~2, Max Slope ~45
     * 5. Bake. Disable buildOnAwake here after saving the baked data.
     * 6. On CrowdManager: set Ground Layers to WalkableGround + your street colliders.
     *    Place each wave Play Area Center at street height in that district.
     */

    void Awake()
    {
        if (buildOnAwake && surface != null)
        {
            surface.BuildNavMesh();
            CrowdNavMeshMovement.RefreshAvailability();
        }
    }
}
