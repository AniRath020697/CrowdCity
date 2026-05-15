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

    [Header("Game Settings")]
    public float gameTime = 60f;
    public int neutralNPCCount = 40;
    public int enemyStartCount = 6;
    public float spawnRange = 40f;

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
    private float timeLeft;
    private bool gameOver;
    private bool resolvingBattle = false;
    private GameObject _runtimeGameplayFloor;

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
            SnapRigidbodyToWalkable(player);

        if (!useWaveManagerForSpawning)
        {
            SpawnNeutralNPCs();
            SpawnEnemyCrowd();
        }

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
            EndGameByScore();
        }

        UpdateUI();
    }

    public void SpawnNeutralNPCs()
    {
        for (int i = 0; i < neutralNPCCount; i++)
        {
            GameObject npc = Instantiate(followerPrefab, GetRandomPosition(), Quaternion.identity);

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
        resolvingBattle = false;

        if (gameOver) return;
        if (enemyLeaderPrefab == null) return;

        enemyFollowers.Clear();

        GameObject enemyObj = Instantiate(enemyLeaderPrefab, GetRandomPosition(), Quaternion.identity);

        enemyLeader = enemyObj.GetComponent<EnemyLeader>();
        if (enemyLeader == null)
            enemyLeader = enemyObj.AddComponent<EnemyLeader>();

        enemyLeader.wanderRange = spawnRange;
        SetupLeaderPhysics(enemyObj);
        SetColor(enemyObj, enemyColor);

        for (int i = 0; i < enemyStartCount; i++)
        {
            Vector3 pos = enemyObj.transform.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            pos = SnapToWalkableHeight(new Vector3(pos.x, 0f, pos.z));

            GameObject follower = Instantiate(followerPrefab, pos, Quaternion.identity);

            FollowerUnit unit = follower.GetComponent<FollowerUnit>();
            if (unit == null)
                unit = follower.AddComponent<FollowerUnit>();

            enemyFollowers.Add(follower);
            unit.SetFollower(FollowerUnit.CrowdTeam.Enemy, enemyLeader.transform, enemyFollowers.Count - 1);

            if (useTeamTintOnFollowers)
                SetColor(follower, enemyColor);
            SetupFollowerAnimator(follower);
            SetupFollowerPhysics(follower);
        }

        RefreshEnemyFormation();
        ApplyWaveModifiersToActiveEnemy();
        UpdateUI();
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
        if (obj == null || gameOver || enemyLeader == null) return;

        FollowerUnit unit = obj.GetComponent<FollowerUnit>();
        if (unit == null) return;

        if (unit.team == FollowerUnit.CrowdTeam.Enemy) return;

        playerFollowers.Remove(obj);

        if (!enemyFollowers.Contains(obj))
            enemyFollowers.Add(obj);

        unit.SetFollower(FollowerUnit.CrowdTeam.Enemy, enemyLeader.transform, enemyFollowers.Count - 1);

        if (useTeamTintOnFollowers)
            SetColor(obj, enemyColor);
        SetupFollowerAnimator(obj);
        SetupFollowerPhysics(obj);

        RefreshPlayerFormation();
        RefreshEnemyFormation();
        UpdateUI();
    }

    public void ResolveBattle()
    {
        if (battleLocked || gameOver || resolvingBattle) return;

        resolvingBattle = true;

        int playerPower = playerFollowers.Count + 1;
        int enemyPower = enemyFollowers.Count + 1;

        if (playerPower >= enemyPower)
        {
            List<GameObject> stolenEnemies = new List<GameObject>(enemyFollowers);

            foreach (GameObject enemy in stolenEnemies)
            {
                ConvertToPlayer(enemy);
            }

            if (enemyLeader != null)
            {
                Destroy(enemyLeader.gameObject);
                enemyLeader = null;
            }

            if (WaveManager.Instance != null)
            {
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

    void RefreshPlayerFormation()
    {
        for (int i = 0; i < playerFollowers.Count; i++)
        {
            if (playerFollowers[i] == null) continue;

            FollowerUnit unit = playerFollowers[i].GetComponent<FollowerUnit>();

            if (unit != null)
                unit.SetFollowerIndex(i);
        }
    }

    void RefreshEnemyFormation()
    {
        if (enemyLeader == null) return;

        for (int i = 0; i < enemyFollowers.Count; i++)
        {
            if (enemyFollowers[i] == null) continue;

            FollowerUnit unit = enemyFollowers[i].GetComponent<FollowerUnit>();

            if (unit != null)
                unit.SetFollowerIndex(i);
        }
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
            enemyCrowdText.text = "ENEMY CROWD: " + (enemyFollowers.Count + 1);

        if (timerText != null)
            timerText.text = "TIME LEFT: " + Mathf.CeilToInt(timeLeft);
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

    void SetupFollowerPhysics(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();

        if (col == null)
        {
            SphereCollider sphere = obj.AddComponent<SphereCollider>();
            sphere.radius = 0.8f;
            sphere.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    void SetupLeaderPhysics(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();

        if (col == null)
        {
            CapsuleCollider capsule = obj.AddComponent<CapsuleCollider>();
            capsule.radius = 0.8f;
            capsule.height = 2f;
            capsule.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
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

        if (enemyLeader != null)
        {
            Destroy(enemyLeader.gameObject);
            enemyLeader = null;
        }
    }

    public void ApplyWaveModifiersToActiveEnemy()
    {
        if (enemyLeader == null || WaveManager.Instance == null)
            return;

        WaveZone z = WaveManager.Instance.CurrentWaveDefinition;
        if (z == null) return;

        enemyLeader.waveMoveSpeedMultiplier = z.enemyMoveSpeedMultiplier;
        enemyLeader.waveChaseSpeedMultiplier = z.enemyChaseSpeedMultiplier;
        enemyLeader.waveShockwaveCooldownMultiplier = z.enemyShockwaveCooldownMultiplier;
        enemyLeader.activeWaveAbility = z.enemyAbility;
    }

    public void SetRunTimerForWave(float seconds)
    {
        gameTime = Mathf.Max(1f, seconds);
        timeLeft = gameTime;
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
