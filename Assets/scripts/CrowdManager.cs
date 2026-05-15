using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class CrowdManager : MonoBehaviour
{
    public static CrowdManager Instance;

    [Header("Main Objects")]
    public Transform player;
    public GameObject followerPrefab;
    public GameObject enemyLeaderPrefab;

    [Header("UI")]
    public TextMeshProUGUI playerCrowdText;
    public TextMeshProUGUI enemyCrowdText;
    public TextMeshProUGUI timerText;

    [Header("Wave end warning")]
    [Tooltip("Optional. Auto-created under the timer if empty. Shown only in the last few seconds of each wave.")]
    public TextMeshProUGUI waveEndTimerText;

    [Tooltip("Show wave end warning when this many seconds or less remain in the wave.")]
    public float waveEndWarningSeconds = 10f;

    [Tooltip("Pixels below the top edge for the wave end warning label.")]
    public float waveEndTimerTopOffset = 56f;

    [Header("Game Settings")]
    public float gameTime = 60f;
    public int neutralNPCCount = 40;
    [Tooltip("Total enemy followers spawned when a wave starts (WaveManager clamps to 1–2), split across active leaders.")]
    public int enemyStartCount = 1;
    public float spawnRange = 40f;

    [Header("Enemy wave spawn")]
    [Tooltip("Enemy leaders spawn at least this far from the player (horizontal metres).")]
    public float enemyLeaderSpawnMinPlayerDistance = 38f;

    [Tooltip("Avoid stacking leaders on one spot when several spawn in the same frame.")]
    public float enemyLeaderMinSeparation = 11f;

    [Header("City population")]
    [Tooltip("Optional center for city-wide pedestrian spawn/roam. If unset, bounds are derived from wave zones or play area.")]
    public Transform cityPopulationCenter;

    [Tooltip("Half-size of the city rectangle on X and Z (metres) when not auto-sized from wave zones.")]
    public Vector2 cityPopulationHalfExtents = new Vector2(85f, 75f);

    [Tooltip("Share of recruitable neutrals spawned closer to the current wave district (rest spread city-wide).")]
    [Range(0f, 1f)]
    public float neutralSpawnNearPlayAreaFraction = 0.38f;

    [Tooltip("Radius around the wave play area for denser recruitable spawns.")]
    public float neutralNearPlayAreaRadius = 36f;

    [Header("Walled city boundary")]
    [Tooltip("Keep player, enemies, followers, and spawns inside City Boundary (Wall 1–4).")]
    public bool clampCrowdsToCityBoundary = true;

    [Tooltip("Auto-resolved from City Boundary in the scene.")]
    public CityPlayableBounds playableBounds;

    [Header("City / walkable play space")]
    [Tooltip("Optional anchor in the city. If unset, the player's XZ at play start defines the spawn/roam center.")]
    public Transform playAreaCenter;

    [Tooltip("Creates a large invisible BoxCollider under the play area so characters have ground when the city art has no colliders.")]
    public bool createInvisibleGameplayFloor = true;

    [Tooltip("Extra margin added to both sides of the spawn square (world units).")]
    public float gameplayFloorExtraSize = 60f;

    [Tooltip("Thickness of the invisible floor collider.")]
    public float gameplayFloorThickness = 6f;

    [Tooltip("Walkable height when no collider is hit by the downward probe (e.g. street level in your city).")]
    public float gameplayFloorTopY;

    [Tooltip("Ray starts this far above the reference height (player / play area / fallback Y), not from world Y=600, so we hit streets instead of roofs.")]
    public float probeStartOffsetAboveReference = 6f;

    [Tooltip("Max ray length downward from the probe start.")]
    public float probeMaxDistance = 320f;

    [Tooltip("Ignore ground hits more than this many metres above the reference height (filters roofs / awnings).")]
    public float maxValidHitAboveReference = 18f;

    public LayerMask groundLayers = ~0;

    [Tooltip("Vertical offset above the surface for spawned actors.")]
    public float characterFeetOffset = 0.12f;

    [Tooltip("Disables the default plane Floor mesh/colliders so probes use the city or the invisible floor.")]
    public bool hideBuiltInFloorPlane;

    [Tooltip("Optional; if empty and hide is on, finds \"Floor\".")]
    public GameObject builtInFloor;

    [Tooltip("If set, all Rigidbodies under this transform become kinematic with no gravity (fixes imported cities that fall).")]
    public Transform environmentPhysicsRoot;

    [Header("NavMesh")]
    [Tooltip("When a NavMesh is baked, player / enemy / followers slide along walkable surfaces instead of walking through static scenery.")]
    public bool constrainCrowdsToNavMesh = true;

    [Tooltip("Search radius used to snap feet onto the NavMesh when clamping each movement step.")]
    public float navMeshSampleRadius = 1.25f;

    [Tooltip("NavMesh area mask (Walkable only = 1). Does not fix a bad bake, but ignores Not Walkable areas.")]
    public int navMeshAreaMask = 1;

    [Header("Building / obstacle blocking")]
    [Tooltip("Capsule-cast against building colliders only (trees, lamps, and street props stay passable).")]
    public bool blockMovementThroughObstacles = true;

    [Tooltip("Horizontal block radius (metres) for player, enemies, and followers.")]
    public float movementBlockRadius = 0.42f;

    [Tooltip("Capsule height used for obstacle casts (metres).")]
    public float movementBlockHeight = 1.5f;

    [Tooltip("Layers tested for movement blocking. WalkableGround and crowd colliders are always ignored.")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Adds MeshColliders to city building meshes that have no collider (fixes pass-through on some POLYGON props).")]
    public bool addMissingBuildingColliders = true;

    [Header("Crowd snag recovery")]
    [Tooltip("When NavMesh + building clamps cancel almost all of a step, probe sideways arcs so crowds do not stall in tight corners.")]
    public bool crowdCornerRecovery = true;

    [Tooltip("Only run recovery if the intended planar step was at least this long (metres).")]
    public float crowdCornerRecoveryIntentMin = 0.055f;

    [Tooltip("Stall ratio: recovered when planar progress is under intent times this.")]
    public float crowdCornerRecoveryProgressRatioMin = 0.22f;

    [Tooltip("Also stall if planar progress stays under this ceiling for small-intent moves.")]
    public float crowdCornerRecoveryMaxStallDist = 0.042f;

    [Tooltip("Recovery probe length multiplier applied to attempted step length.")]
    public float crowdCornerRecoveryStepScale = 0.92f;

    public float crowdCornerRecoveryMinProbe = 0.32f;
    public float crowdCornerRecoveryMaxProbe = 1.12f;

    [Tooltip("New slide must beat the baseline by at least this (metres) to swap in.")]
    public float crowdCornerImproveEpsilon = 0.02f;

    [Header("Crowd snag recovery — NavMesh fallback")]
    [Tooltip("Multiply NavMesh.SamplePosition radius when the normal radius fails so leaders slightly off-mesh can still slide.")]
    public float crowdNavMeshFromSampleRadiusMul = 4.5f;

    [Tooltip("Minimum wide sample radius after scaling (metres).")]
    public float crowdNavMeshWideSampleFloor = 4f;

    [Tooltip("If still nearly frozen after angled recovery, take one capsule-only slide (skip NavMesh) so enemies do not deadlock in tight wedges.")]
    public bool crowdObstacleOnlyBypassWhenNearlyStuck = true;

    [Tooltip("Bypass only when intended step length is above this.")]
    public float crowdObstacleBypassIntentMin = 0.06f;

    [Tooltip("Planar progress shorter than this after recovery counts as frozen.")]
    public float crowdNearlyStuckDistance = 0.028f;

    [Tooltip("Obstacle-only escape probe length clamps.")]
    public float crowdObstacleBypassMinStep = 0.38f;
    public float crowdObstacleBypassMaxStep = 1.45f;

    [Header("Crowd perf")]
    [Tooltip("Player/enemy follower units skip NavMesh slide and multi-pass snag rescue (cheap obstacle-only step). Huge win when recruited crowds get large.")]
    public bool lightweightRecruitedFollowerCrowd = true;

    [Header("Outer wall visibility")]
    [Tooltip("Clamp camera, spawn backdrop panels, and enable soft fog so void past the city walls is hidden.")]
    public bool limitViewBeyondCityWalls = true;

    [Header("Audio")]
    [Tooltip("Played when the player or an enemy leader uses shockwave (E / AI).")]
    public AudioClip shockwaveExplosionClip;

    [Range(0f, 1f)]
    public float shockwaveExplosionVolume = 0.9f;

    [Tooltip("Played when the player uses Turbo Dash (Left Shift).")]
    public AudioClip dashWhooshClip;

    [Range(0f, 1f)]
    public float dashWhooshVolume = 0.85f;

    [Tooltip("Played when the player uses Rally Cry (F).")]
    public AudioClip rallyCryPullClip;

    [Range(0f, 1f)]
    public float rallyCryPullVolume = 0.9f;

    [Tooltip("Use play area / street height for ground rays and NavMesh checks so crowds stay on sidewalks, not roofs or lamp tops.")]
    public bool usePlayAreaStreetHeight = true;

    [Tooltip("Reject NavMesh samples higher than street reference + this value (metres).")]
    public float navMeshMaxHeightAboveStreet = 1.35f;

    [Tooltip("Max metres below street reference a ground hit is still accepted (parking ramps / sunken roads).")]
    public float maxValidHitBelowStreet = 4f;

    [Header("Colors")]
    public Color neutralColor = Color.gray;
    public Color playerColor = Color.blue;
    public Color enemyColor = Color.red;

    [Header("Crowd visuals (RPG Tiny Hero etc.)")]
    [Tooltip("When true, tints follower meshes like the old capsules. Turn off for textured PBR heroes.")]
    public bool useTeamTintOnFollowers = false;

    [Tooltip("If set, assigned to each spawned follower's Animator (Idle/Run via bool isMoving). Use CrowdTinyHeroLocomotion for RPG Tiny Hero Duo.")]
    public RuntimeAnimatorController followerLocomotionController;

    [Tooltip("When followerLocomotionController is set, force this on before play.")]
    public bool applyFollowerLocomotionController = true;

    public List<GameObject> playerFollowers = new List<GameObject>();
    public List<GameObject> enemyFollowers = new List<GameObject>();

    [HideInInspector] public bool battleLocked = false;

    [Header("Wave mode")]
    [Tooltip("When true, WaveManager owns the first spawn and wave transitions. Leave off for classic single-wave / timer play.")]
    public bool useWaveManagerForSpawning;

    private EnemyLeader enemyLeader;
    readonly List<EnemyLeader> enemyLeaders = new List<EnemyLeader>();
    private float timeLeft;
    private bool gameOver;
    private bool resolvingBattle = false;
    private GameObject _runtimeGameplayFloor;
    private Vector3 _cityPopulationCenterXZ;
    private bool _cityBoundsConfigured;
    private int _lastTimerLayoutValue = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (GetComponent<GameplayHudLayoutBootstrap>() == null)
            gameObject.AddComponent<GameplayHudLayoutBootstrap>();

        RegisterShockwaveAudio();
    }

    void RegisterShockwaveAudio()
    {
        if (shockwaveExplosionClip != null)
            ShockwaveSfx.SetClip(shockwaveExplosionClip);

        ShockwaveSfx.Volume = shockwaveExplosionVolume;

        if (dashWhooshClip != null)
            DashSfx.SetClip(dashWhooshClip);

        DashSfx.Volume = dashWhooshVolume;

        if (rallyCryPullClip != null)
            RallyCrySfx.SetClip(rallyCryPullClip);

        RallyCrySfx.Volume = rallyCryPullVolume;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (shockwaveExplosionClip == null)
        {
            shockwaveExplosionClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio Effects/Explosion.wav");
        }

        if (dashWhooshClip == null)
        {
            dashWhooshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio Effects/whoosh.mp3");
        }

        if (rallyCryPullClip == null)
        {
            rallyCryPullClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio Effects/Pulling.mp3");
        }

        RegisterShockwaveAudio();
    }
#endif

    public bool ShouldConstrainToNavMesh()
    {
        return constrainCrowdsToNavMesh && CrowdNavMeshMovement.HasNavMeshData();
    }

    public float GetNavMeshSampleRadius()
    {
        return Mathf.Max(0.25f, navMeshSampleRadius);
    }

    public int GetNavMeshAreaMask()
    {
        return navMeshAreaMask;
    }

    /// <summary>Street / sidewalk height for the active wave district.</summary>
    public float GetStreetReferenceY()
    {
        if (usePlayAreaStreetHeight && playAreaCenter != null)
            return playAreaCenter.position.y;
        return gameplayFloorTopY;
    }

    public bool IsNavMeshPointOnStreet(Vector3 worldPos)
    {
        float streetY = GetStreetReferenceY();
        return worldPos.y <= streetY + navMeshMaxHeightAboveStreet
            && worldPos.y >= streetY - maxValidHitBelowStreet;
    }

    public bool TryCrowdNavMove(Vector3 fromWorld, Vector3 toWorld, out Vector3 result, bool preserveHeight = true)
    {
        float streetY = usePlayAreaStreetHeight ? GetStreetReferenceY() : float.NaN;
        float radius = GetNavMeshSampleRadius();

        if (CrowdNavMeshMovement.TryMove(
                fromWorld,
                toWorld,
                radius,
                out result,
                preserveHeight,
                streetY,
                navMeshMaxHeightAboveStreet,
                GetNavMeshAreaMask()))
            return true;

        float wide = Mathf.Max(
            radius * Mathf.Max(1.01f, crowdNavMeshFromSampleRadiusMul),
            Mathf.Max(radius + 0.25f, crowdNavMeshWideSampleFloor));

        if (wide <= radius + 0.004f)
            return false;

        return CrowdNavMeshMovement.TryMove(
            fromWorld,
            toWorld,
            wide,
            out result,
            preserveHeight,
            streetY,
            navMeshMaxHeightAboveStreet,
            GetNavMeshAreaMask());
    }

    /// <summary>NavMesh slide (optional) then building capsule block, then street snap + city clamp.</summary>
    public void ApplyCrowdMovement(Vector3 fromWorld, Vector3 toWorld, Transform actor)
    {
        if (actor == null)
            return;

        ResolveCrowdMove(fromWorld, toWorld, actor, out Vector3 resolved);
        ApplyPositionWithStreetGround(resolved, actor);
    }

    public void ResolveCrowdMove(Vector3 fromWorld, Vector3 toWorld, Transform actor, out Vector3 resolved)
    {
        resolved = ProjectileCrowdSlide(fromWorld, toWorld, actor);

        if (lightweightRecruitedFollowerCrowd && IsRecruitedFollowerMovement(actor))
            return;

        float intent = HorizontalDistance(fromWorld, toWorld);

        float improveEps = Mathf.Max(0.005f, crowdCornerImproveEpsilon);
        float ratioMin = Mathf.Clamp(crowdCornerRecoveryProgressRatioMin, 0.05f, 0.95f);
        float stallCeil = Mathf.Max(0.015f, crowdCornerRecoveryMaxStallDist);
        float intentMinRecover = Mathf.Max(0.02f, crowdCornerRecoveryIntentMin);

        if (crowdCornerRecovery && actor != null
            && intent >= intentMinRecover)
        {
            float achievedCorner = HorizontalDistance(fromWorld, resolved);
            float accept = Mathf.Min(stallCeil, intent * ratioMin);
            if (achievedCorner <= accept
                && TryPickBetterCrowdSlide(fromWorld, toWorld, actor, intent, resolved, improveEps, out Vector3 better))
            {
                resolved = better;
            }
        }

        float bypassIntent = Mathf.Max(0.04f, crowdObstacleBypassIntentMin);
        float nearly = Mathf.Max(0.018f, crowdNearlyStuckDistance);
        float achievedFinal = HorizontalDistance(fromWorld, resolved);
        float relativeProgress = intent > 0.001f ? achievedFinal / Mathf.Max(0.001f, intent) : 1f;
        bool crawlStall = intent >= bypassIntent && relativeProgress < 0.14f && achievedFinal < 0.068f;

        if (crowdObstacleOnlyBypassWhenNearlyStuck
            && actor != null
            && blockMovementThroughObstacles
            && intent >= bypassIntent
            && (achievedFinal <= nearly || crawlStall)
            && TryObstacleOnlyEscape(fromWorld, toWorld, actor, intent, resolved, out Vector3 brute))
        {
            resolved = brute;
        }
    }

    /// <summary>
    /// Player/enemy followers: skip NavMesh + heavy snag recovery (packed formations look like crawl-stalls and burn CPU).
    /// </summary>
    static bool IsRecruitedFollowerMovement(Transform actor)
    {
        if (actor == null)
            return false;

        FollowerUnit fu = actor.GetComponent<FollowerUnit>();
        return fu != null && fu.team != FollowerUnit.CrowdTeam.Neutral;
    }

    /// <summary>
    /// NavMesh slide (optional) plus building capsule block. No snag recovery (used for probe candidates).
    /// </summary>
    Vector3 ProjectileCrowdSlide(Vector3 fromWorld, Vector3 toWorld, Transform actor)
    {
        Vector3 resolved = toWorld;

        bool recruitFollower = lightweightRecruitedFollowerCrowd && IsRecruitedFollowerMovement(actor);

        if (!recruitFollower
            && ShouldConstrainToNavMesh()
            && TryCrowdNavMove(fromWorld, resolved, out Vector3 navPos))
        {
            resolved = navPos;
        }

        if (blockMovementThroughObstacles)
        {
            CrowdObstacleMovement.TryBlockHorizontalMove(
                fromWorld,
                resolved,
                movementBlockRadius,
                movementBlockHeight,
                obstacleLayers,
                actor,
                out Vector3 blocked);
            resolved = blocked;
        }

        return resolved;
    }

    Vector3 ProjectileObstacleOnly(Vector3 fromWorld, Vector3 toWorld, Transform actor)
    {
        Vector3 resolved = toWorld;

        if (blockMovementThroughObstacles)
        {
            CrowdObstacleMovement.TryBlockHorizontalMove(
                fromWorld,
                resolved,
                movementBlockRadius,
                movementBlockHeight,
                obstacleLayers,
                actor,
                out Vector3 blocked);
            resolved = blocked;
        }

        return resolved;
    }

    bool TryObstacleOnlyEscape(
        Vector3 fromWorld,
        Vector3 desiredWorld,
        Transform actor,
        float intent,
        Vector3 baselineResolved,
        out Vector3 chosen)
    {
        chosen = baselineResolved;

        Vector3 dv = desiredWorld - fromWorld;
        dv.y = 0f;
        float dLen = dv.magnitude;
        if (dLen < 0.0001f)
            return false;

        Vector3 fwd = dv / dLen;
        float baseProg = HorizontalDistance(fromWorld, baselineResolved);

        float step = Mathf.Clamp(
            intent * 0.62f,
            Mathf.Max(0.12f, crowdObstacleBypassMinStep),
            crowdObstacleBypassMaxStep);

        float bestProg = baseProg;
        Vector3 bestResolved = baselineResolved;

        const float yawSamplesDeg = 15f;

        for (int i = -8; i <= 8; i++)
        {
            if (i == 0)
                continue;

            Quaternion q = Quaternion.Euler(0f, i * yawSamplesDeg, 0f);
            Vector3 dir = q * fwd;
            Vector3 probeTo = fromWorld + dir * step;
            Vector3 slid = ProjectileObstacleOnly(fromWorld, probeTo, actor);
            float p = HorizontalDistance(fromWorld, slid);
            if (p > bestProg)
            {
                bestProg = p;
                bestResolved = slid;
            }
        }

        Vector3 perp = new Vector3(-fwd.z, 0f, fwd.x);
        for (float bias = -1f; bias <= 1f; bias += 2f)
        {
            Vector3 side = (perp * 0.94f + fwd * 0.12f * bias).normalized;
            Vector3 probeTo = fromWorld + side * (step * 0.92f);
            Vector3 slid = ProjectileObstacleOnly(fromWorld, probeTo, actor);
            float p = HorizontalDistance(fromWorld, slid);
            if (p > bestProg)
            {
                bestProg = p;
                bestResolved = slid;
            }
        }

        const float obstacleEscapeGain = 0.028f;

        if (bestProg <= baseProg + Mathf.Max(obstacleEscapeGain, intent * 0.07f))
            return false;

        chosen = bestResolved;
        return true;
    }

    bool TryPickBetterCrowdSlide(
        Vector3 fromWorld,
        Vector3 toWorld,
        Transform actor,
        float intent,
        Vector3 baselineResolved,
        float improveEpsilon,
        out Vector3 chosen)
    {
        chosen = baselineResolved;

        Vector3 dv = toWorld - fromWorld;
        dv.y = 0f;
        float dLen = dv.magnitude;
        if (dLen < 0.0001f)
            return false;

        Vector3 fwd = dv / dLen;
        float baseProg = HorizontalDistance(fromWorld, baselineResolved);

        float scale = Mathf.Clamp(crowdCornerRecoveryStepScale, 0.2f, 1.5f);
        float probe = Mathf.Clamp(intent * scale, crowdCornerRecoveryMinProbe, crowdCornerRecoveryMaxProbe);

        const float yawSamplesDeg = 12f;
        float bestProg = baseProg;
        Vector3 bestResolved = baselineResolved;

        for (int i = -6; i <= 6; i++)
        {
            if (i == 0)
                continue;

            Quaternion q = Quaternion.Euler(0f, i * yawSamplesDeg, 0f);
            Vector3 dir = q * fwd;
            Vector3 probeTo = fromWorld + dir * probe;
            Vector3 slid = ProjectileCrowdSlide(fromWorld, probeTo, actor);
            float p = HorizontalDistance(fromWorld, slid);
            if (p > bestProg)
            {
                bestProg = p;
                bestResolved = slid;
            }
        }

        // Extra wide sides (scrape along facades)
        Vector3 perp = new Vector3(-fwd.z, 0f, fwd.x);
        for (float s = -1f; s <= 1f; s += 2f)
        {
            Vector3 side = (fwd * 0.18f + perp * s).normalized;
            Vector3 probeTo = fromWorld + side * (probe * 0.88f);
            Vector3 slid = ProjectileCrowdSlide(fromWorld, probeTo, actor);
            float p = HorizontalDistance(fromWorld, slid);
            if (p > bestProg)
            {
                bestProg = p;
                bestResolved = slid;
            }
        }

        if (bestProg <= baseProg + improveEpsilon)
            return false;

        chosen = bestResolved;
        return true;
    }

    public static bool ShouldIgnoreForMovementBlock(Collider col, Transform ignoreRoot)
    {
        if (col == null)
            return true;
        if (IsCrowdCharacterCollider(col))
            return true;
        if (IsRuntimeGameplayFloorCollider(col))
            return true;
        if (ignoreRoot != null && (col.transform == ignoreRoot || col.transform.IsChildOf(ignoreRoot)))
            return true;

        int walkable = LayerMask.NameToLayer("WalkableGround");
        if (walkable >= 0 && col.gameObject.layer == walkable)
            return true;

        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0 && col.gameObject.layer == ignoreRaycast)
            return true;

        // Only buildings and the outer city walls block — trees, lamps, and props pass through.
        return !CrowdEnvironmentClassification.BlocksCrowdMovement(col);
    }

    public Vector3 GetActorWorldPosition(Transform t)
    {
        if (t == null) return Vector3.zero;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        return rb != null ? rb.position : t.position;
    }

    /// <summary>Moves the whole player crowd by the same delta as the leader (e.g. Turbo Dash).</summary>
    public void MovePlayerFollowersBy(Vector3 worldDelta)
    {
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude < 0.0001f) return;

        for (int i = 0; i < playerFollowers.Count; i++)
        {
            GameObject go = playerFollowers[i];
            if (go == null) continue;

            Transform t = go.transform;
            Rigidbody followerRb = t.GetComponent<Rigidbody>();
            Vector3 from = followerRb != null ? followerRb.position : t.position;
            Vector3 to = from + worldDelta;

            ApplyCrowdMovement(from, to, t);
        }
    }

    /// <summary>Sets world XZ from <paramref name="worldPos"/> and snaps feet to street ground.</summary>
    public void ApplyPositionWithStreetGround(Vector3 worldPos, Transform t)
    {
        if (t == null) return;

        worldPos = ClampToCityBoundary(worldPos, t);
        Vector3 s = SnapToWalkableHeight(new Vector3(worldPos.x, 0f, worldPos.z), t);
        worldPos.y = s.y;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = worldPos;
            t.position = worldPos;
        }
        else
        {
            t.position = worldPos;
        }
    }

    public void StickTransformToStreetGround(Transform t)
    {
        if (t == null) return;
        ApplyPositionWithStreetGround(t.position, t);
    }

    void OnDestroy()
    {
        if (_runtimeGameplayFloor != null)
            Destroy(_runtimeGameplayFloor);
    }

    void Start()
    {
        RegisterShockwaveAudio();
        timeLeft = gameTime;

        FreezeEnvironmentRigidbodies();

        if (hideBuiltInFloorPlane)
            HideBuiltInFloorPlane();

        if (!useWaveManagerForSpawning)
            EnsureGameplayFloor();

        if (player != null)
            SetColor(player.gameObject, playerColor);

        if (player != null)
        {
            ConfigureCrowdCharacterPhysics(player.gameObject);
            SnapRigidbodyToWalkable(player);
        }

        if (!useWaveManagerForSpawning)
        {
            SpawnNeutralNPCs();
            SpawnEnemyCrowd();
        }

        EnsureCityPlayableBounds();
        if (addMissingBuildingColliders)
            EnsureMissingBuildingColliders();
        if (limitViewBeyondCityWalls)
            CityBoundaryViewLimiter.Ensure(player);
        EnsureWaveEndTimerText();
        ApplyHudLayoutFor1024x768();
        UpdateUI();

        CrowdNavMeshMovement.RefreshAvailability();
    }

    void EnsureMissingBuildingColliders()
    {
        Transform root = environmentPhysicsRoot != null
            ? environmentPhysicsRoot
            : GameObject.Find("City")?.transform;

        if (root == null)
            return;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;

            Transform t = mf.transform;
            if (!CrowdEnvironmentClassification.ShouldReceiveBuildingCollider(t))
                continue;

            if (t.GetComponent<Collider>() != null)
                continue;

            MeshCollider meshCollider = t.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mf.sharedMesh;
            meshCollider.convex = false;
        }
    }

    public void EnsureCityPlayableBounds()
    {
        if (playableBounds == null)
            playableBounds = CityPlayableBounds.Instance;

        if (playableBounds != null)
        {
            playableBounds.RebuildBounds();
            return;
        }

        GameObject boundary = GameObject.Find("City Boundary");
        if (boundary == null)
            return;

        playableBounds = boundary.GetComponent<CityPlayableBounds>();
        if (playableBounds == null)
            playableBounds = boundary.AddComponent<CityPlayableBounds>();

        playableBounds.RebuildBounds();
    }

    Vector3 ClampToCityBoundary(Vector3 worldPos, Transform actor = null)
    {
        if (!clampCrowdsToCityBoundary)
            return worldPos;

        EnsureCityPlayableBounds();
        if (playableBounds == null || !playableBounds.IsValid)
            return worldPos;

        float inset = 0f;
        if (actor != null
            && (actor.GetComponent<PlayerController>() != null || actor.GetComponent<EnemyLeader>() != null))
        {
            inset = playableBounds.leaderEdgePadding;
        }

        return playableBounds.ClampXZ(worldPos, inset);
    }

    public Vector3 GetPlayAreaOriginXZ()
    {
        if (playAreaCenter != null)
            return new Vector3(playAreaCenter.position.x, 0f, playAreaCenter.position.z);
        if (player != null)
            return new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.zero;
    }

    public Vector3 GetCityPopulationCenterXZ()
    {
        if (cityPopulationCenter != null)
            return new Vector3(cityPopulationCenter.position.x, 0f, cityPopulationCenter.position.z);
        if (_cityBoundsConfigured)
            return _cityPopulationCenterXZ;
        return GetPlayAreaOriginXZ();
    }

    public Vector2 GetCityPopulationHalfExtents()
    {
        return new Vector2(
            Mathf.Max(20f, cityPopulationHalfExtents.x),
            Mathf.Max(20f, cityPopulationHalfExtents.y));
    }

    /// <summary>Fit city pedestrian bounds around all wave districts so neutrals fill the map.</summary>
    public void ConfigureCityPopulationFromWaveZones(WaveZone[] zones, float padding = 40f)
    {
        EnsureCityPlayableBounds();
        if (playableBounds != null && playableBounds.IsValid)
        {
            Bounds interior = playableBounds.InteriorBounds;
            _cityPopulationCenterXZ = new Vector3(interior.center.x, 0f, interior.center.z);
            cityPopulationHalfExtents = new Vector2(interior.extents.x, interior.extents.z);
            _cityBoundsConfigured = true;
            return;
        }

        if (zones == null || zones.Length == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        int count = 0;

        foreach (WaveZone zone in zones)
        {
            if (zone == null || zone.playAreaCenter == null)
                continue;

            Vector3 p = zone.playAreaCenter.position;
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
            count++;
        }

        if (count == 0)
            return;

        _cityPopulationCenterXZ = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        _cityBoundsConfigured = true;

        float halfX = (maxX - minX) * 0.5f + padding;
        float halfZ = (maxZ - minZ) * 0.5f + padding;
        cityPopulationHalfExtents = new Vector2(Mathf.Max(55f, halfX), Mathf.Max(55f, halfZ));
    }

    public bool TryGetRandomCityRoamPoint(out Vector3 worldPos)
    {
        worldPos = default;

        EnsureCityPlayableBounds();
        const int maxAttempts = 14;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = playableBounds != null && playableBounds.IsValid
                ? playableBounds.GetRandomPointXZ()
                : PickRandomCityXZRaw();

            candidate = SnapToWalkableHeight(candidate);

            bool useNav = ShouldConstrainToNavMesh();
            if (!useNav)
            {
                worldPos = candidate;
                return true;
            }

            if (CrowdNavMeshMovement.TrySampleOnNavMesh(candidate, navMeshSampleRadius * 2.5f, navMeshAreaMask, out Vector3 onMesh))
            {
                worldPos = onMesh;
                return true;
            }
        }

        worldPos = SnapToWalkableHeight(ClampToCityBoundary(GetCityPopulationCenterXZ()));
        return true;
    }

    /// <summary>Picks a roam point neutrals can reach without cutting through buildings.</summary>
    public bool TryPickReachableRoamPoint(Vector3 fromWorld, Transform actor, out Vector3 roamPoint)
    {
        roamPoint = default;
        if (actor == null)
            return false;

        const int maxAttempts = 16;
        float minProgress = 0.42f;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (!TryGetRandomCityRoamPoint(out Vector3 candidate))
                continue;

            candidate.y = fromWorld.y;

            float desired = HorizontalDistance(fromWorld, candidate);
            if (desired < 2f)
                continue;

            ResolveCrowdMove(fromWorld, candidate, actor, out Vector3 resolved);
            float traveled = HorizontalDistance(fromWorld, resolved);

            if (traveled >= desired * minProgress)
            {
                roamPoint = candidate;
                return true;
            }
        }

        if (TryGetRandomCityRoamPoint(out Vector3 fallback))
        {
            roamPoint = fallback;
            return true;
        }

        return false;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 PickRandomCityXZRaw()
    {
        Vector3 center = GetCityPopulationCenterXZ();
        Vector2 half = GetCityPopulationHalfExtents();
        float x = center.x + Random.Range(-half.x, half.x);
        float z = center.z + Random.Range(-half.y, half.y);
        return new Vector3(x, 0f, z);
    }

    Vector3 GetRandomNeutralSpawnPosition()
    {
        const int maxAttempts = 16;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 xz = Random.value < neutralSpawnNearPlayAreaFraction
                ? PickRandomNearPlayAreaXZ()
                : PickRandomCityXZ();

            Vector3 pos = SnapToWalkableHeight(xz);

            if (!ShouldConstrainToNavMesh())
                return pos;

            if (CrowdNavMeshMovement.TrySampleOnNavMesh(pos, navMeshSampleRadius * 2.5f, navMeshAreaMask, out Vector3 onMesh))
                return onMesh;
        }

        return GetRandomPosition();
    }

    Vector3 PickRandomCityXZ()
    {
        EnsureCityPlayableBounds();
        if (playableBounds != null && playableBounds.IsValid)
            return playableBounds.GetRandomPointXZ();

        return PickRandomCityXZRaw();
    }

    Vector3 PickRandomNearPlayAreaXZ()
    {
        Vector3 origin = GetPlayAreaOriginXZ();
        float radius = neutralNearPlayAreaRadius > 1f ? neutralNearPlayAreaRadius : spawnRange;
        float x = origin.x + Random.Range(-radius, radius);
        float z = origin.z + Random.Range(-radius, radius);
        return ClampToCityBoundary(new Vector3(x, 0f, z));
    }

    float GetReferenceGroundY()
    {
        if (playAreaCenter != null) return playAreaCenter.position.y;
        if (player != null) return player.position.y;
        return gameplayFloorTopY;
    }

    /// <summary>Ray origin height for ground probes at worldXZ — prefers the actor under that spot so movement away from playAreaCenter still hits street level.</summary>
    float GetSnapReferenceY(Vector3 worldXZ, Transform heightReference)
    {
        if (heightReference != null)
            return heightReference.position.y - characterFeetOffset;

        if (player != null)
        {
            Vector3 p = player.position;
            if ((p.x - worldXZ.x) * (p.x - worldXZ.x) + (p.z - worldXZ.z) * (p.z - worldXZ.z) < 4f)
                return p.y - characterFeetOffset;
        }

        return GetReferenceGroundY();
    }

    public static bool IsRuntimeGameplayFloorCollider(Collider c)
    {
        return c != null && c.gameObject.name == "CrowdGameplayFloor";
    }

    public static bool IsCrowdCharacterCollider(Collider c)
    {
        if (c == null) return true;
        if (c.GetComponentInParent<PlayerController>() != null) return true;
        if (c.GetComponentInParent<FollowerUnit>() != null) return true;
        if (c.GetComponentInParent<EnemyLeader>() != null) return true;
        return false;
    }

    bool TryEnvironmentRayHitDown(Vector3 xz, out RaycastHit chosen, Transform heightReference = null)
    {
        chosen = default;
        float refY = usePlayAreaStreetHeight
            ? GetStreetReferenceY()
            : GetSnapReferenceY(xz, heightReference);
        Vector3 origin = new Vector3(xz.x, refY + probeStartOffsetAboveReference, xz.z);

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeMaxDistance, groundLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        RaycastHit? bestReal = null;
        RaycastHit? bestFloor = null;
        float bestRealY = float.MaxValue;
        float bestFloorY = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            if (IsCrowdCharacterCollider(h.collider)) continue;
            if (h.point.y > refY + maxValidHitAboveReference) continue;
            if (usePlayAreaStreetHeight && h.point.y < refY - maxValidHitBelowStreet) continue;

            if (IsRuntimeGameplayFloorCollider(h.collider))
            {
                if (h.point.y < bestFloorY)
                {
                    bestFloorY = h.point.y;
                    bestFloor = h;
                }
                continue;
            }

            if (h.point.y < bestRealY)
            {
                bestRealY = h.point.y;
                bestReal = h;
            }
        }

        if (bestReal.HasValue)
        {
            chosen = bestReal.Value;
            return true;
        }

        if (bestFloor.HasValue)
        {
            chosen = bestFloor.Value;
            return true;
        }

        return false;
    }

    void FreezeEnvironmentRigidbodies()
    {
        if (environmentPhysicsRoot == null) return;

        foreach (Rigidbody rb in environmentPhysicsRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            if (player != null && (rb.transform == player || rb.transform.IsChildOf(player)))
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    float ProbeTopSurfaceY(Vector3 centerXZ)
    {
        if (TryEnvironmentRayHitDown(centerXZ, out RaycastHit hit))
            return hit.point.y;

        if (playAreaCenter != null) return playAreaCenter.position.y;
        if (player != null) return player.position.y;

        return gameplayFloorTopY;
    }

    void EnsureGameplayFloor()
    {
        if (!createInvisibleGameplayFloor)
            return;

        if (_runtimeGameplayFloor != null)
            Destroy(_runtimeGameplayFloor);

        Vector3 center = GetPlayAreaOriginXZ();
        float topY = ProbeTopSurfaceY(center);
        float span = Mathf.Max(40f, spawnRange * 2f + gameplayFloorExtraSize);
        float h = Mathf.Max(0.5f, gameplayFloorThickness);

        _runtimeGameplayFloor = new GameObject("CrowdGameplayFloor");
        _runtimeGameplayFloor.transform.SetParent(null);
        _runtimeGameplayFloor.transform.position = new Vector3(center.x, topY - h * 0.5f, center.z);

        BoxCollider box = _runtimeGameplayFloor.AddComponent<BoxCollider>();
        box.size = new Vector3(span, h, span);
        box.isTrigger = false;

        Rigidbody floorRb = _runtimeGameplayFloor.AddComponent<Rigidbody>();
        floorRb.isKinematic = true;
        floorRb.useGravity = false;
    }

    void HideBuiltInFloorPlane()
    {
        GameObject floor = builtInFloor != null ? builtInFloor : GameObject.Find("Floor");
        if (floor == null) return;

        foreach (Collider c in floor.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        foreach (Renderer r in floor.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
    }

    public Vector3 SnapToWalkableHeight(Vector3 worldXZ, Transform heightReference = null)
    {
        if (TryEnvironmentRayHitDown(worldXZ, out RaycastHit hit, heightReference))
            return new Vector3(worldXZ.x, hit.point.y + characterFeetOffset, worldXZ.z);

        if (ShouldConstrainToNavMesh()
            && CrowdNavMeshMovement.TrySampleOnNavMesh(
                worldXZ,
                GetNavMeshSampleRadius() * 2.5f,
                GetNavMeshAreaMask(),
                out Vector3 onMesh))
        {
            return new Vector3(worldXZ.x, onMesh.y + characterFeetOffset, worldXZ.z);
        }

        if (playAreaCenter != null)
            return new Vector3(worldXZ.x, playAreaCenter.position.y + characterFeetOffset, worldXZ.z);

        if (heightReference != null)
            return new Vector3(worldXZ.x, heightReference.position.y, worldXZ.z);

        return new Vector3(worldXZ.x, gameplayFloorTopY + characterFeetOffset, worldXZ.z);
    }

    public void SnapRigidbodyToWalkable(Transform t)
    {
        if (t == null) return;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        Vector3 p = rb != null ? rb.position : t.position;
        Vector3 s = SnapToWalkableHeight(new Vector3(p.x, 0f, p.z), t);
        p.y = s.y;

        if (rb != null)
            rb.position = p;
        else
            t.position = p;
    }

    /// <summary>Places the player (or any crowd actor) at a wave spawn with city clamp + street snap.</summary>
    public void PlaceActorAtWaveSpawn(Transform actor, Transform spawnPoint)
    {
        if (actor == null || spawnPoint == null)
            return;

        actor.gameObject.SetActive(true);

        Vector3 worldPos = spawnPoint.position;
        worldPos = ClampToCityBoundary(worldPos, actor);

        Rigidbody rb = actor.GetComponent<Rigidbody>();
        if (rb != null)
            rb.rotation = spawnPoint.rotation;
        else
            actor.rotation = spawnPoint.rotation;

        ApplyPositionWithStreetGround(worldPos, actor);
    }

    public void EnsurePlayerVisibleAndGrounded()
    {
        if (player == null)
            return;

        player.gameObject.SetActive(true);

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        ApplyPositionWithStreetGround(player.position, player);
    }

    public void ResnapPlayerCrowdToGround()
    {
        EnsurePlayerVisibleAndGrounded();

        for (int i = 0; i < playerFollowers.Count; i++)
        {
            GameObject go = playerFollowers[i];
            if (go == null)
                continue;

            go.SetActive(true);
            ApplyPositionWithStreetGround(go.transform.position, go.transform);
        }
    }

    void Update()
    {
        if (gameOver) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0)
        {
            timeLeft = 0;
            OnRunTimerExpired();
        }

        UpdateUI();
    }

    public void SpawnNeutralNPCs()
    {
        for (int i = 0; i < neutralNPCCount; i++)
        {
            GameObject npc = Instantiate(followerPrefab, GetRandomNeutralSpawnPosition(), Quaternion.identity);

            FollowerUnit unit = npc.GetComponent<FollowerUnit>();
            if (unit == null)
                unit = npc.AddComponent<FollowerUnit>();

            unit.SetNeutral();
            if (useTeamTintOnFollowers)
                SetColor(npc, neutralColor);
            SetupFollowerAnimator(npc);
            SetupFollowerPhysics(npc);
        }
    }

    public void SpawnEnemyCrowd()
    {
        if (useWaveManagerForSpawning)
        {
            int leaders = waveEnemyLeaderCount > 0 ? waveEnemyLeaderCount : 1;
            SpawnWaveEnemyLeaders(leaders);
            return;
        }

        SpawnClassicEnemyCrowd();
    }

    void SpawnStartingEnemyFollowersAcrossLeaders()
    {
        int total = Mathf.Clamp(enemyStartCount, 0, 2);
        if (total <= 0 || enemyLeaders.Count == 0)
            return;

        for (int f = 0; f < total; f++)
        {
            EnemyLeader leader = enemyLeaders[f % enemyLeaders.Count];
            if (leader == null) continue;

            SpawnEnemyFollowerForLeader(leader.transform, leader.transform.position);
        }
    }

    void SpawnClassicEnemyCrowd()
    {
        resolvingBattle = false;

        if (gameOver) return;
        if (enemyLeaderPrefab == null) return;

        DestroyActiveEnemyCrowd();

        GameObject enemyObj = Instantiate(enemyLeaderPrefab, GetRandomPosition(), Quaternion.identity);
        EnemyLeader leader = RegisterEnemyLeader(enemyObj);
        if (leader == null) return;

        leader.wanderRange = spawnRange;
        SetupLeaderPhysics(enemyObj);
        SetColor(enemyObj, enemyColor);

        SpawnStartingEnemyFollowersAcrossLeaders();

        RefreshEnemyFormation();
        ApplyWaveModifiersToActiveEnemies();
        UpdateUI();
    }

    [HideInInspector] public int waveEnemyLeaderCount = 3;

    public void SpawnWaveEnemyLeaders(int leaderCount)
    {
        resolvingBattle = false;

        if (gameOver || enemyLeaderPrefab == null || leaderCount <= 0)
            return;

        DestroyActiveEnemyCrowd();

        List<Vector3> placedXZ = new List<Vector3>(leaderCount);

        for (int i = 0; i < leaderCount; i++)
        {
            Vector3 pos = GetEnemySpawnAwayFromPlayer(placedXZ);

            GameObject enemyObj = Instantiate(enemyLeaderPrefab, pos, Quaternion.identity);
            EnemyLeader leader = RegisterEnemyLeader(enemyObj);
            if (leader == null) continue;

            leader.wanderRange = Mathf.Max(spawnRange, 120f);
            leader.alwaysHuntPlayer = true;
            leader.useProximityHunt = true;
            SetupLeaderPhysics(enemyObj);
            SetColor(enemyObj, enemyColor);

            placedXZ.Add(new Vector3(pos.x, 0f, pos.z));
        }

        SpawnStartingEnemyFollowersAcrossLeaders();

        SyncPrimaryEnemyLeader();
        RefreshEnemyFormation();
        ApplyWaveModifiersToActiveEnemies();
        UpdateUI();
    }

    EnemyLeader RegisterEnemyLeader(GameObject enemyObj)
    {
        EnemyLeader leader = enemyObj.GetComponent<EnemyLeader>();
        if (leader == null)
            leader = enemyObj.AddComponent<EnemyLeader>();

        if (!enemyLeaders.Contains(leader))
            enemyLeaders.Add(leader);

        return leader;
    }

    void SyncPrimaryEnemyLeader()
    {
        for (int i = enemyLeaders.Count - 1; i >= 0; i--)
        {
            if (enemyLeaders[i] == null)
                enemyLeaders.RemoveAt(i);
        }

        enemyLeader = enemyLeaders.Count > 0 ? enemyLeaders[0] : null;
    }

    void SpawnEnemyFollowerForLeader(Transform leaderTransform, Vector3 nearPosition)
    {
        Vector3 pos = nearPosition + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
        pos = SnapToWalkableHeight(new Vector3(pos.x, 0f, pos.z));

        GameObject follower = Instantiate(followerPrefab, pos, Quaternion.identity);

        FollowerUnit unit = follower.GetComponent<FollowerUnit>();
        if (unit == null)
            unit = follower.AddComponent<FollowerUnit>();

        int slot = CountFollowersForLeader(leaderTransform);
        enemyFollowers.Add(follower);
        unit.SetFollower(FollowerUnit.CrowdTeam.Enemy, leaderTransform, slot);

        if (useTeamTintOnFollowers)
            SetColor(follower, enemyColor);
        SetupFollowerAnimator(follower);
        SetupFollowerPhysics(follower);
    }

    int CountFollowersForLeader(Transform leaderTransform)
    {
        int count = 0;
        for (int i = 0; i < enemyFollowers.Count; i++)
        {
            if (enemyFollowers[i] == null) continue;

            FollowerUnit unit = enemyFollowers[i].GetComponent<FollowerUnit>();
            if (unit != null && unit.GetLeaderTransform() == leaderTransform)
                count++;
        }

        return count;
    }

    int CountEnemyPowerForLeader(Transform leaderTransform)
    {
        return CountFollowersForLeader(leaderTransform) + 1;
    }

    public int GetEnemyPowerForLeader(Transform leaderTransform)
    {
        if (leaderTransform == null)
            return 1;

        return CountEnemyPowerForLeader(leaderTransform);
    }

    EnemyLeader GetNearestEnemyLeader(Vector3 fromWorld)
    {
        EnemyLeader best = null;
        float bestSq = float.MaxValue;

        for (int i = 0; i < enemyLeaders.Count; i++)
        {
            EnemyLeader el = enemyLeaders[i];
            if (el == null) continue;

            float sq = (el.transform.position - fromWorld).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = el;
            }
        }

        return best;
    }

    /// <summary>
    /// Picks a walkable spawn for an enemy leader, far from the player (and optionally far from other leader XZ used this frame).
    /// </summary>
    public Vector3 GetEnemySpawnAwayFromPlayer(List<Vector3> otherLeaderXZ = null)
    {
        float minFromPlayer = Mathf.Max(12f, enemyLeaderSpawnMinPlayerDistance);
        float minFromPlayerSq = minFromPlayer * minFromPlayer;

        float minAlly = Mathf.Max(2.5f, enemyLeaderMinSeparation);
        float minAllySq = minAlly * minAlly;

        Vector3 playerXZ = Vector3.zero;
        bool hasPlayer = player != null;
        if (hasPlayer)
        {
            Vector3 ap = GetActorWorldPosition(player);
            playerXZ = new Vector3(ap.x, 0f, ap.z);
        }

        const int maxAttempts = 40;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = GetRandomNeutralSpawnPosition();
            Vector3 q = new Vector3(candidate.x, 0f, candidate.z);

            if (hasPlayer && (q - playerXZ).sqrMagnitude < minFromPlayerSq)
                continue;

            if (otherLeaderXZ != null)
            {
                bool tooClose = false;
                for (int j = 0; j < otherLeaderXZ.Count; j++)
                {
                    if ((q - otherLeaderXZ[j]).sqrMagnitude < minAllySq)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;
            }

            return candidate;
        }

        return GetEnemySpawnRadialFallback(playerXZ, hasPlayer, minFromPlayer, otherLeaderXZ, minAllySq);
    }

    Vector3 GetEnemySpawnRadialFallback(
        Vector3 playerXZ,
        bool hasPlayer,
        float minRadius,
        List<Vector3> otherLeaderXZ,
        float minAllySq)
    {
        Vector3 origin = hasPlayer ? playerXZ : GetPlayAreaOriginXZ();

        for (int i = 0; i < 28; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = minRadius + Random.Range(0f, Mathf.Max(spawnRange, 45f));
            Vector3 xz = origin + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            xz = ClampToCityBoundary(xz);
            Vector3 placed = SnapToWalkableHeight(new Vector3(xz.x, 0f, xz.z));
            Vector3 q = new Vector3(placed.x, 0f, placed.z);

            if (hasPlayer && (q - playerXZ).sqrMagnitude < minRadius * minRadius * 0.81f)
                continue;

            if (otherLeaderXZ != null)
            {
                bool bad = false;
                for (int j = 0; j < otherLeaderXZ.Count; j++)
                {
                    if ((q - otherLeaderXZ[j]).sqrMagnitude < minAllySq)
                    {
                        bad = true;
                        break;
                    }
                }

                if (bad)
                    continue;
            }

            return placed;
        }

        Vector3 last = SnapToWalkableHeight(ClampToCityBoundary(origin + Vector3.forward * minRadius));
        return last;
    }

    public Vector3 GetEnemySpawnNearPlayer(float minRadius = 14f, float maxRadius = 24f)
    {
        Vector3 center = GetPlayAreaOriginXZ();
        if (player != null)
            center = new Vector3(player.position.x, 0f, player.position.z);

        for (int i = 0; i < 16; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(minRadius, maxRadius);
            Vector3 xz = center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            xz = ClampToCityBoundary(xz);
            if (playableBounds != null && playableBounds.IsValid && !playableBounds.ContainsXZ(xz))
                continue;

            return SnapToWalkableHeight(xz);
        }

        return SnapToWalkableHeight(ClampToCityBoundary(center + Vector3.forward * minRadius));
    }

    public void PrunePlayerFollowerList()
    {
        for (int i = playerFollowers.Count - 1; i >= 0; i--)
        {
            if (playerFollowers[i] == null)
                playerFollowers.RemoveAt(i);
        }
    }

    Vector3 GetRandomPosition()
    {
        EnsureCityPlayableBounds();
        Vector3 xz = playableBounds != null && playableBounds.IsValid
            ? playableBounds.GetRandomPointXZ()
            : GetPlayAreaOriginXZ() + new Vector3(Random.Range(-spawnRange, spawnRange), 0f, Random.Range(-spawnRange, spawnRange));

        return SnapToWalkableHeight(ClampToCityBoundary(xz));
    }

    public void ConvertToPlayer(GameObject obj)
    {
        if (obj == null || gameOver) return;

        FollowerUnit unit = obj.GetComponent<FollowerUnit>();
        if (unit == null) return;

        if (unit.team == FollowerUnit.CrowdTeam.Player) return;

        bool wasEnemy = unit.team == FollowerUnit.CrowdTeam.Enemy;
        enemyFollowers.Remove(obj);

        if (!playerFollowers.Contains(obj))
            playerFollowers.Add(obj);

        unit.SetFollower(FollowerUnit.CrowdTeam.Player, player, playerFollowers.Count - 1);

        if (wasEnemy || useTeamTintOnFollowers)
            SetColor(obj, playerColor);
        SetupFollowerAnimator(obj);
        SetupFollowerPhysics(obj);

        RefreshPlayerFormation();
        RefreshEnemyFormation();
        UpdateUI();
    }

    public void ConvertToEnemy(GameObject obj)
    {
        if (obj == null || gameOver) return;

        EnemyLeader targetLeader = GetNearestEnemyLeader(obj.transform.position);
        if (targetLeader == null) return;

        FollowerUnit unit = obj.GetComponent<FollowerUnit>();
        if (unit == null) return;

        if (unit.team == FollowerUnit.CrowdTeam.Enemy) return;

        playerFollowers.Remove(obj);

        if (!enemyFollowers.Contains(obj))
            enemyFollowers.Add(obj);

        unit.SetFollower(
            FollowerUnit.CrowdTeam.Enemy,
            targetLeader.transform,
            CountFollowersForLeader(targetLeader.transform));

        if (useTeamTintOnFollowers)
            SetColor(obj, enemyColor);
        SetupFollowerAnimator(obj);
        SetupFollowerPhysics(obj);

        RefreshPlayerFormation();
        RefreshEnemyFormation();
        UpdateUI();
    }

    public void ResolveBattle(EnemyLeader opponent = null)
    {
        if (battleLocked || gameOver || resolvingBattle) return;

        resolvingBattle = true;

        int playerPower = playerFollowers.Count + 1;
        Transform opponentRoot = opponent != null ? opponent.transform : enemyLeader != null ? enemyLeader.transform : null;
        int enemyPower = opponentRoot != null
            ? CountEnemyPowerForLeader(opponentRoot)
            : enemyFollowers.Count + Mathf.Max(enemyLeaders.Count, enemyLeader != null ? 1 : 0);

        if (playerPower >= enemyPower)
        {
            if (opponentRoot != null)
            {
                List<GameObject> stolen = new List<GameObject>();
                for (int i = 0; i < enemyFollowers.Count; i++)
                {
                    GameObject go = enemyFollowers[i];
                    if (go == null) continue;

                    FollowerUnit fu = go.GetComponent<FollowerUnit>();
                    if (fu != null && fu.GetLeaderTransform() == opponentRoot)
                        stolen.Add(go);
                }

                if (opponent != null && opponent.alwaysHuntPlayer)
                {
                    for (int i = 0; i < stolen.Count; i++)
                    {
                        GameObject go = stolen[i];
                        if (go == null) continue;
                        enemyFollowers.Remove(go);
                        Destroy(go);
                    }
                }
                else
                {
                    foreach (GameObject enemy in stolen)
                        ConvertToPlayer(enemy);
                }

                if (opponent != null)
                    RemoveDefeatedEnemyLeader(opponent);
            }
            else
            {
                List<GameObject> stolenEnemies = new List<GameObject>(enemyFollowers);
                foreach (GameObject enemy in stolenEnemies)
                    ConvertToPlayer(enemy);

                DestroyActiveEnemyCrowd();
            }

            SyncPrimaryEnemyLeader();
            resolvingBattle = false;

            if (useWaveManagerForSpawning && WaveManager.Instance != null)
            {
                if (enemyLeaders.Count == 0)
                    WaveManager.Instance.OnEnemyWaveDefeated();
            }
            else
            {
                CancelInvoke(nameof(SpawnEnemyCrowd));
                Invoke(nameof(SpawnEnemyCrowd), 3f);
            }
        }
        else
        {
            gameOver = true;
            SceneManager.LoadScene("LoseScene");
        }

        UpdateUI();
    }

    void RemoveDefeatedEnemyLeader(EnemyLeader opponent)
    {
        if (opponent == null) return;

        opponent.MarkDefeated();
        enemyLeaders.Remove(opponent);

        if (opponent.gameObject != null)
            Destroy(opponent.gameObject);
    }

    /// <summary>Clears recruited followers so each wave starts with the player alone.</summary>
    public void ClearPlayerCrowdForNewWave()
    {
        for (int i = playerFollowers.Count - 1; i >= 0; i--)
        {
            GameObject go = playerFollowers[i];
            if (go != null)
                Destroy(go);
        }

        playerFollowers.Clear();
        RefreshPlayerFormation();
        UpdateUI();
    }

    public void LockBattleFor(float seconds)
    {
        battleLocked = true;
        CancelInvoke(nameof(UnlockBattle));
        Invoke(nameof(UnlockBattle), seconds);
    }

    void UnlockBattle()
    {
        battleLocked = false;
    }

    public void RefreshPlayerFormation()
    {
        PrunePlayerFollowerList();

        for (int i = 0; i < playerFollowers.Count; i++)
        {
            if (playerFollowers[i] == null) continue;

            FollowerUnit unit = playerFollowers[i].GetComponent<FollowerUnit>();

            if (unit != null)
            {
                unit.SetFollower(FollowerUnit.CrowdTeam.Player, player, i);
                unit.SetFollowerIndex(i);
            }
        }
    }

    void RefreshEnemyFormation()
    {
        for (int l = 0; l < enemyLeaders.Count; l++)
        {
            EnemyLeader el = enemyLeaders[l];
            if (el == null) continue;

            int index = 0;
            for (int i = 0; i < enemyFollowers.Count; i++)
            {
                if (enemyFollowers[i] == null) continue;

                FollowerUnit unit = enemyFollowers[i].GetComponent<FollowerUnit>();
                if (unit == null || unit.GetLeaderTransform() != el.transform) continue;

                unit.SetFollowerIndex(index);
                index++;
            }
        }
    }

    int GetTotalEnemyHeadcount()
    {
        int count = enemyLeaders.Count;
        for (int i = 0; i < enemyFollowers.Count; i++)
        {
            if (enemyFollowers[i] != null)
                count++;
        }

        return count;
    }

    void OnRunTimerExpired()
    {
        if (useWaveManagerForSpawning && WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveSurvived();
            return;
        }

        EndGameByScore();
    }

    void EndGameByScore()
    {
        gameOver = true;

        if (playerFollowers.Count + 1 >= enemyFollowers.Count + 1)
            SceneManager.LoadScene("WinScene");
        else
            SceneManager.LoadScene("LoseScene");
    }

    public void UpdateUI()
    {
        if (playerCrowdText != null)
            playerCrowdText.text = "PLAYER CROWD: " + (playerFollowers.Count + 1);

        if (enemyCrowdText != null)
            enemyCrowdText.text = "ENEMY CROWD: " + GetTotalEnemyHeadcount();

        if (timerText != null)
        {
            int displayed = Mathf.CeilToInt(timeLeft);
            timerText.text = "TIME LEFT: " + displayed;
            if (displayed != _lastTimerLayoutValue)
            {
                _lastTimerLayoutValue = displayed;
                UiLayout1024x768.ApplyTopRight(timerText, SafeTopRow(0));
            }
        }

        UpdateWaveEndTimerUI();
    }

    void EnsureWaveEndTimerText()
    {
        if (waveEndTimerText == null)
        {
            GameObject existing = FindUiObjectByName("WaveEndTimer");
            if (existing != null)
                waveEndTimerText = existing.GetComponent<TextMeshProUGUI>();

            if (waveEndTimerText == null && timerText != null)
            {
                var go = new GameObject("WaveEndTimer", typeof(RectTransform));
                Transform parent = timerText.transform.parent;
                if (parent == null)
                {
                    Canvas canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                        parent = canvas.transform;
                }

                if (parent != null)
                    go.transform.SetParent(parent, false);

                waveEndTimerText = go.AddComponent<TextMeshProUGUI>();
                waveEndTimerText.font = timerText.font;
                waveEndTimerText.fontStyle = FontStyles.Bold;
                waveEndTimerText.color = new Color(1f, 0.55f, 0.1f);
                waveEndTimerText.raycastTarget = false;
                go.SetActive(false);
            }
        }

        if (waveEndTimerText == null)
            return;

        if (waveEndTimerText.font == null && timerText != null)
            waveEndTimerText.font = timerText.font;

        ApplyWaveEndTimerLayout();
    }

    void ApplyWaveEndTimerLayout()
    {
        if (waveEndTimerText == null)
            return;

        UiLayout1024x768.ApplyTopCenter(waveEndTimerText, waveEndTimerTopOffset, 560f, 28);
    }

    static float SafeTopRow(int rowIndex)
    {
        return 28f + rowIndex * 40f;
    }

    public void ApplyHudLayoutFor1024x768()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        UiLayout1024x768.ConfigureCanvas(canvas);

        UiLayout1024x768.ApplyTopLeft(playerCrowdText, SafeTopRow(0));
        UiLayout1024x768.ApplyTopLeft(enemyCrowdText, SafeTopRow(1));
        UiLayout1024x768.ApplyTopRight(timerText, SafeTopRow(0));
        ApplyWaveEndTimerLayout();
    }

    static GameObject FindUiObjectByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null)
            return found;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
                return transforms[i].gameObject;
        }

        return null;
    }

    void UpdateWaveEndTimerUI()
    {
        EnsureWaveEndTimerText();

        if (waveEndTimerText == null)
            return;

        bool waveMode = useWaveManagerForSpawning && WaveManager.Instance != null;
        bool show = waveMode && timeLeft > 0f && timeLeft <= waveEndWarningSeconds;

        if (waveEndTimerText.gameObject.activeSelf != show)
            waveEndTimerText.gameObject.SetActive(show);

        if (show)
        {
            waveEndTimerText.text = "WAVE ENDS IN: " + Mathf.CeilToInt(timeLeft);
            ApplyWaveEndTimerLayout();
        }
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / Lit
    static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in / legacy

    void SetColor(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
                if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
            }
            r.materials = mats;
        }
    }

    void SetupFollowerAnimator(GameObject root)
    {
        if (root == null || !applyFollowerLocomotionController || followerLocomotionController == null)
            return;

        Animator anim = root.GetComponentInChildren<Animator>(true);
        if (anim == null)
            return;

        anim.runtimeAnimatorController = followerLocomotionController;
        anim.applyRootMotion = false;

        FollowerUnit fu = root.GetComponent<FollowerUnit>();
        if (fu != null)
            fu.InvalidateAnimatorParameterCache();
    }

    public void ConfigureCrowdCharacterPhysics(GameObject obj)
    {
        if (obj == null) return;

        foreach (CharacterController cc in obj.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = false;

        bool hasCollider = false;
        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            col.isTrigger = true;
            hasCollider = true;
        }

        if (!hasCollider)
        {
            SphereCollider sphere = obj.AddComponent<SphereCollider>();
            sphere.radius = 0.8f;
            sphere.isTrigger = true;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void SetupFollowerPhysics(GameObject obj)
    {
        ConfigureCrowdCharacterPhysics(obj);
    }

    void SetupLeaderPhysics(GameObject obj)
    {
        ConfigureCrowdCharacterPhysics(obj);
    }

    public void RebuildGameplayFloor()
    {
        EnsureGameplayFloor();
    }

    public void DestroyNeutralNPCsInScene()
    {
        FollowerUnit[] units = FindObjectsByType<FollowerUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FollowerUnit u in units)
        {
            if (u != null && u.team == FollowerUnit.CrowdTeam.Neutral)
                Destroy(u.gameObject);
        }
    }

    public void DestroyActiveEnemyCrowd()
    {
        for (int i = 0; i < enemyFollowers.Count; i++)
        {
            if (enemyFollowers[i] != null)
                Destroy(enemyFollowers[i]);
        }

        enemyFollowers.Clear();

        for (int i = 0; i < enemyLeaders.Count; i++)
        {
            if (enemyLeaders[i] != null)
                Destroy(enemyLeaders[i].gameObject);
        }

        enemyLeaders.Clear();
        enemyLeader = null;
    }

    public void ApplyWaveModifiersToActiveEnemies()
    {
        if (WaveManager.Instance == null) return;

        WaveZone z = WaveManager.Instance.CurrentWaveDefinition;
        if (z == null) return;

        for (int i = 0; i < enemyLeaders.Count; i++)
        {
            EnemyLeader el = enemyLeaders[i];
            if (el == null) continue;

            el.waveMoveSpeedMultiplier = z.enemyMoveSpeedMultiplier;
            el.waveChaseSpeedMultiplier = z.enemyChaseSpeedMultiplier;
            el.waveShockwaveCooldownMultiplier = z.enemyShockwaveCooldownMultiplier;
            el.maxShockwavesPerWave = z.shockwavesPerWave;
            el.activeWaveAbility = z.enemyAbility;
            el.ResetWaveShockwaveState();
        }
    }

    public void ApplyWaveModifiersToActiveEnemy()
    {
        ApplyWaveModifiersToActiveEnemies();
    }

    public void SetRunTimerForWave(float seconds)
    {
        gameTime = Mathf.Max(1f, seconds);
        timeLeft = gameTime;

        if (waveEndTimerText != null)
            waveEndTimerText.gameObject.SetActive(false);
    }

    public void SetResolvingBattle(bool value)
    {
        resolvingBattle = value;
    }

    public void ResetRunStateForNewWave()
    {
        gameOver = false;
        resolvingBattle = false;
    }

    public void MarkGameOver()
    {
        gameOver = true;
    }
}
