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

    [Header("Game Settings")]
    public float gameTime = 60f;
    public int neutralNPCCount = 40;
    public int enemyStartCount = 6;
    public float spawnRange = 40f;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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
        return CrowdNavMeshMovement.TryMove(
            fromWorld,
            toWorld,
            GetNavMeshSampleRadius(),
            out result,
            preserveHeight,
            streetY,
            navMeshMaxHeightAboveStreet,
            GetNavMeshAreaMask());
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

            ApplyPositionWithStreetGround(to, t);
        }
    }

    /// <summary>Sets world XZ from <paramref name="worldPos"/> and snaps feet to street ground.</summary>
    public void ApplyPositionWithStreetGround(Vector3 worldPos, Transform t)
    {
        if (t == null) return;

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

        EnsureWaveEndTimerText();
        UpdateUI();

        CrowdNavMeshMovement.RefreshAvailability();
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
        Vector3 center = GetCityPopulationCenterXZ();
        Vector2 half = GetCityPopulationHalfExtents();

        const int maxAttempts = 14;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = center.x + Random.Range(-half.x, half.x);
            float z = center.z + Random.Range(-half.y, half.y);
            Vector3 candidate = SnapToWalkableHeight(new Vector3(x, 0f, z));

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

        worldPos = SnapToWalkableHeight(center);
        return true;
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
        Vector3 center = GetCityPopulationCenterXZ();
        Vector2 half = GetCityPopulationHalfExtents();
        float x = center.x + Random.Range(-half.x, half.x);
        float z = center.z + Random.Range(-half.y, half.y);
        return new Vector3(x, 0f, z);
    }

    Vector3 PickRandomNearPlayAreaXZ()
    {
        Vector3 origin = GetPlayAreaOriginXZ();
        float radius = neutralNearPlayAreaRadius > 1f ? neutralNearPlayAreaRadius : spawnRange;
        float x = origin.x + Random.Range(-radius, radius);
        float z = origin.z + Random.Range(-radius, radius);
        return new Vector3(x, 0f, z);
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

    static bool IsRuntimeGameplayFloorCollider(Collider c)
    {
        return c != null && c.gameObject.name == "CrowdGameplayFloor";
    }

    static bool IsCrowdCharacterCollider(Collider c)
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
            SpawnWaveEnemyLeaders(leaders, spawnOneEnemyNearPlayer: true);
            return;
        }

        SpawnClassicEnemyCrowd();
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

        for (int i = 0; i < enemyStartCount; i++)
            SpawnEnemyFollowerForLeader(leader.transform, enemyObj.transform.position);

        RefreshEnemyFormation();
        ApplyWaveModifiersToActiveEnemies();
        UpdateUI();
    }

    [HideInInspector] public int waveEnemyLeaderCount = 3;

    public void SpawnWaveEnemyLeaders(int leaderCount, bool spawnOneEnemyNearPlayer)
    {
        resolvingBattle = false;

        if (gameOver || enemyLeaderPrefab == null || leaderCount <= 0)
            return;

        DestroyActiveEnemyCrowd();

        for (int i = 0; i < leaderCount; i++)
        {
            Vector3 pos = i == 0 && spawnOneEnemyNearPlayer
                ? GetEnemySpawnNearPlayer()
                : GetRandomNeutralSpawnPosition();

            GameObject enemyObj = Instantiate(enemyLeaderPrefab, pos, Quaternion.identity);
            EnemyLeader leader = RegisterEnemyLeader(enemyObj);
            if (leader == null) continue;

            leader.wanderRange = Mathf.Max(spawnRange, 120f);
            leader.alwaysHuntPlayer = true;
            SetupLeaderPhysics(enemyObj);
            SetColor(enemyObj, enemyColor);

            for (int f = 0; f < enemyStartCount; f++)
                SpawnEnemyFollowerForLeader(leader.transform, pos);
        }

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
            return SnapToWalkableHeight(xz);
        }

        return SnapToWalkableHeight(center + Vector3.forward * minRadius);
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
        Vector3 o = GetPlayAreaOriginXZ();
        float x = o.x + Random.Range(-spawnRange, spawnRange);
        float z = o.z + Random.Range(-spawnRange, spawnRange);
        return SnapToWalkableHeight(new Vector3(x, 0f, z));
    }

    public void ConvertToPlayer(GameObject obj)
    {
        if (obj == null || gameOver) return;

        FollowerUnit unit = obj.GetComponent<FollowerUnit>();
        if (unit == null) return;

        if (unit.team == FollowerUnit.CrowdTeam.Player) return;

        enemyFollowers.Remove(obj);

        if (!playerFollowers.Contains(obj))
            playerFollowers.Add(obj);

        unit.SetFollower(FollowerUnit.CrowdTeam.Player, player, playerFollowers.Count - 1);

        if (useTeamTintOnFollowers)
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

                foreach (GameObject enemy in stolen)
                    ConvertToPlayer(enemy);

                if (opponent != null)
                {
                    enemyLeaders.Remove(opponent);
                    Destroy(opponent.gameObject);
                }
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
            timerText.text = "TIME LEFT: " + Mathf.CeilToInt(timeLeft);

        UpdateWaveEndTimerUI();
    }

    void EnsureWaveEndTimerText()
    {
        if (waveEndTimerText != null || timerText == null)
            return;

        var go = new GameObject("WaveEndTimer", typeof(RectTransform));
        go.transform.SetParent(timerText.transform.parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        RectTransform timerRt = timerText.rectTransform;
        rt.anchorMin = timerRt.anchorMin;
        rt.anchorMax = timerRt.anchorMax;
        rt.pivot = timerRt.pivot;
        rt.anchoredPosition = timerRt.anchoredPosition + new Vector2(0f, -36f);
        rt.sizeDelta = timerRt.sizeDelta;

        waveEndTimerText = go.AddComponent<TextMeshProUGUI>();
        waveEndTimerText.font = timerText.font;
        waveEndTimerText.fontSize = timerText.fontSize;
        waveEndTimerText.fontStyle = FontStyles.Bold;
        waveEndTimerText.color = new Color(1f, 0.55f, 0.1f);
        waveEndTimerText.alignment = TextAlignmentOptions.TopRight;
        waveEndTimerText.raycastTarget = false;
        go.SetActive(false);
    }

    void UpdateWaveEndTimerUI()
    {
        if (waveEndTimerText == null)
            return;

        bool waveMode = useWaveManagerForSpawning && WaveManager.Instance != null;
        bool show = waveMode && timeLeft > 0f && timeLeft <= waveEndWarningSeconds;

        if (waveEndTimerText.gameObject.activeSelf != show)
            waveEndTimerText.gameObject.SetActive(show);

        if (show)
            waveEndTimerText.text = "WAVE ENDS IN: " + Mathf.CeilToInt(timeLeft);
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
            el.activeWaveAbility = z.enemyAbility;
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
