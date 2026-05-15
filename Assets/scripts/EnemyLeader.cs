using UnityEngine;
using System.Collections;

/// <summary>Runs before <see cref="FollowerUnit"/> so rigidbody-led movement updates before follower targets use it.</summary>
[DefaultExecutionOrder(-40)]
public class EnemyLeader : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.65f;

    [Tooltip("Speed when chasing after the player is within hunt range. Tweaked slower than player's walk (~5 × wave mods).")]
    public float chaseSpeed = 4.6f;

    [Tooltip("Requires alwaysHuntPlayer. Roam/wander until the player enters this distance, then chase.")]
    public bool useProximityHunt = true;

    [Tooltip("Wave hunters roam until the player is within this horizontal distance.")]
    public float huntEngageDistance = 40f;

    [HideInInspector] public float waveMoveSpeedMultiplier = 1f;
    [HideInInspector] public float waveChaseSpeedMultiplier = 1f;
    [HideInInspector] public float waveShockwaveCooldownMultiplier = 1f;

    [Header("Wave abilities")]
    public EnemyWaveAbility activeWaveAbility = EnemyWaveAbility.None;

    [Tooltip("Harrier: chase leash multiplier.")]
    public float harrierChaseDistanceMul = 1.38f;

    [Tooltip("StubbornSnap: when outnumbered, creep toward player inside this fraction of chase distance.")]
    public float stubbornSnapDistanceFrac = 0.52f;

    [Tooltip("StubbornSnap: creep speed as a fraction of base move speed.")]
    public float stubbornSnapSpeedFrac = 0.48f;

    [Tooltip("EchoBlast: delay before the weaker follow-up.")]
    public float echoBlastDelay = 0.38f;

    [Tooltip("EchoBlast: second blast radius as fraction of primary.")]
    public float echoBlastRadiusMul = 0.48f;

    public float echoBlastForceMul = 0.55f;

    public float wanderRange = 40f;
    public float chaseDistance = 100f;

    [Tooltip("When on, always chases the player instead of wandering (wave hunters).")]
    public bool alwaysHuntPlayer;

    [Tooltip("Rotation smoothing toward move direction so follower formations do not jitter (degrees per second).")]
    public float turnTowardMoveDegreesPerSecond = 540f;

    [Header("Animation")]
    public Animator animator;

    [Header("Enemy Shockwave")]
    [Tooltip("Uses allowed each wave per enemy leader. Set by WaveManager.")]
    public int maxShockwavesPerWave = 2;

    [Tooltip("Player must stay within this range for windup before the enemy may shockwave.")]
    public float shockwaveEngageMaxDistance = 4.25f;

    [Tooltip("Seconds the player must remain in engage range before the first shockwave.")]
    public float shockwaveWindupSeconds = 1.35f;

    public float shockwaveRadius = 8f;
    public float shockwaveForce = 10f;
    public float shockwaveCooldown = 6f;
    public float shockwavePushDuration = 0.25f;
    public float shockwaveBattleLockDuration = 1.5f;

    [ColorUsage(true, true)]
    [Tooltip("Energy colour — matches the player's shockwave ripple but tinted crimson.")]
    public Color shockwaveVisualTint = new Color(1.1f, 0.31f, 0.09f, 1f); // HDR red/orange blast

    public GameObject shockwavePrefab;

    private Vector3 targetPos;
    private Transform player;
    private float shockwaveTimer = 0f;
    private int shockwavesRemaining;
    private Vector3 lastPosition;
    private float _animMovingBlend;
    private Coroutine echoRoutine;
    private Rigidbody _rb;
    private bool _defeated;
    private float _playerEngageTimer;
    private float _wanderStuckTimer;

    public bool IsDefeated => _defeated;

    Vector3 WorldPosition => _rb != null ? _rb.position : transform.position;

    /// <summary>Stops AI and collisions immediately when this leader loses a battle.</summary>
    public void MarkDefeated()
    {
        if (_defeated) return;

        _defeated = true;
        alwaysHuntPlayer = false;

        if (echoRoutine != null)
        {
            StopCoroutine(echoRoutine);
            echoRoutine = null;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        gameObject.SetActive(false);
        enabled = false;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (CrowdManager.Instance != null && CrowdManager.Instance.player != null)
            player = CrowdManager.Instance.player;

        lastPosition = WorldPosition;
        SetNewTarget();
    }

    void Update()
    {
        shockwaveTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (_defeated || CrowdManager.Instance == null) return;

        if (WaveManager.Instance != null && WaveManager.Instance.IsWaveTransitionPending)
        {
            UpdateAnimation();
            return;
        }

        int playerCount = CrowdManager.Instance.playerFollowers.Count + 1;
        int enemyCount = CrowdManager.Instance.GetEnemyPowerForLeader(transform);

        bool playerIsStronger = playerCount > enemyCount;
        bool enemyIsAtLeastEqual = enemyCount >= playerCount;

        float chaseLeash = ChaseDistanceEffective();

        if (player != null)
        {
            Vector3 playerPos = CrowdManager.Instance.GetActorWorldPosition(player);
            float distanceToPlayer = Vector3.Distance(WorldPosition, playerPos);
            UpdateShockwaveEngageTimer(distanceToPlayer);

            if (alwaysHuntPlayer)
            {
                if (useProximityHunt && distanceToPlayer > huntEngageDistance)
                {
                    Wander();
                    UpdateAnimation();
                    return;
                }

                if (activeWaveAbility == EnemyWaveAbility.StubbornSnap
                    && playerIsStronger
                    && distanceToPlayer <= chaseLeash * stubbornSnapDistanceFrac
                    && distanceToPlayer > 0.85f)
                {
                    CreepTowardPlayer(playerPos);
                }
                else
                    ChasePlayer(playerPos);

                TryUseShockwave(playerPos, distanceToPlayer);
                UpdateAnimation();
                return;
            }

            if (enemyIsAtLeastEqual && distanceToPlayer <= chaseLeash)
                TryUseShockwave(playerPos, distanceToPlayer);

            if (activeWaveAbility == EnemyWaveAbility.StubbornSnap
                && playerIsStronger
                && distanceToPlayer <= chaseLeash * stubbornSnapDistanceFrac
                && distanceToPlayer > 0.85f)
            {
                CreepTowardPlayer(playerPos);
                UpdateAnimation();
                return;
            }

            if (enemyIsAtLeastEqual && distanceToPlayer <= chaseLeash)
            {
                ChasePlayer(playerPos);
                UpdateAnimation();
                return;
            }

            if (playerIsStronger)
            {
                ChasePlayer(playerPos);
                UpdateAnimation();
                return;
            }
        }

        Wander();
        UpdateAnimation();
    }

    float ChaseDistanceEffective()
    {
        if (activeWaveAbility == EnemyWaveAbility.Harrier)
            return chaseDistance * harrierChaseDistanceMul;
        return chaseDistance;
    }

    void UpdateShockwaveEngageTimer(float distanceToPlayer)
    {
        if (distanceToPlayer <= shockwaveEngageMaxDistance)
            _playerEngageTimer += Time.fixedDeltaTime;
        else
            _playerEngageTimer = 0f;
    }

    float ShockwaveTriggerDistance()
    {
        return Mathf.Min(shockwaveEngageMaxDistance, shockwaveRadius);
    }

    void CreepTowardPlayer(Vector3 playerPos)
    {
        playerPos.y = WorldPosition.y;

        float speed = moveSpeed * Mathf.Max(0.05f, waveMoveSpeedMultiplier) * stubbornSnapSpeedFrac;
        MoveTo(playerPos, speed);
    }

    void UpdateAnimation()
    {
        if (animator == null || !HasIsMovingParameter()) return;

        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float movementAmount = Vector3.Distance(WorldPosition, lastPosition) / dt;

        _animMovingBlend = Mathf.Lerp(_animMovingBlend, movementAmount > 0.06f ? 1f : 0f, 0.32f);

        animator.SetBool("isMoving", _animMovingBlend > 0.45f);

        lastPosition = WorldPosition;
    }

    bool HasIsMovingParameter()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (var p in animator.parameters)
        {
            if (p.name == "isMoving") return true;
        }
        return false;
    }

    public void ResetWaveShockwaveState()
    {
        if (echoRoutine != null)
        {
            StopCoroutine(echoRoutine);
            echoRoutine = null;
        }

        shockwaveTimer = Mathf.Max(1.75f, shockwaveCooldown * 0.45f);
        shockwavesRemaining = Mathf.Max(0, maxShockwavesPerWave);
        _playerEngageTimer = 0f;
    }

    bool UsesWaveShockwaveLimit()
    {
        return CrowdManager.Instance != null
            && CrowdManager.Instance.useWaveManagerForSpawning
            && WaveManager.Instance != null;
    }

    bool CanUseShockwave(float distanceToPlayer)
    {
        if (shockwaveTimer > 0f)
            return false;

        if (distanceToPlayer > ShockwaveTriggerDistance())
            return false;

        if (_playerEngageTimer < shockwaveWindupSeconds)
            return false;

        if (UsesWaveShockwaveLimit())
            return shockwavesRemaining > 0;

        return activeWaveAbility == EnemyWaveAbility.EchoBlast;
    }

    bool TryUseShockwave(Vector3 playerPos, float distanceToPlayer)
    {
        if (!CanUseShockwave(distanceToPlayer))
            return false;

        if (UsesWaveShockwaveLimit())
            shockwavesRemaining = Mathf.Max(0, shockwavesRemaining - 1);

        ActivateEnemyShockwave();
        shockwaveTimer = shockwaveCooldown * Mathf.Max(0.05f, waveShockwaveCooldownMultiplier);
        return true;
    }

    void ActivateEnemyShockwave()
    {
        ShockwaveSfx.Play(transform.position);
        DoShockwaveBlast(1f, 1f, true);

        if (activeWaveAbility == EnemyWaveAbility.EchoBlast)
        {
            if (echoRoutine != null)
                StopCoroutine(echoRoutine);
            echoRoutine = StartCoroutine(CoEchoBlastFollowUp());
        }
    }

    IEnumerator CoEchoBlastFollowUp()
    {
        EnemyWaveAbility keep = EnemyWaveAbility.EchoBlast;
        yield return new WaitForSeconds(echoBlastDelay);

        if (this == null || activeWaveAbility != keep)
        {
            echoRoutine = null;
            yield break;
        }

        DoShockwaveBlast(echoBlastRadiusMul, echoBlastForceMul, false);
        echoRoutine = null;
    }

    void DoShockwaveBlast(float radiusMul, float forceMul, bool applyBattleLock)
    {
        if (applyBattleLock && CrowdManager.Instance != null)
            CrowdManager.Instance.LockBattleFor(shockwaveBattleLockDuration);

        float blastRadius = shockwaveRadius * radiusMul;

        if (shockwavePrefab != null)
        {
            Vector3 pos = transform.position;
            pos.y += 0.05f;

            GameObject rippleObj = Instantiate(shockwavePrefab, pos, Quaternion.identity);

            if (rippleObj.TryGetComponent<ShockwaveEffect>(out ShockwaveEffect ripple))
                ripple.Configure(blastRadius, shockwaveVisualTint);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);

        foreach (Collider hit in hits)
        {
            PlayerController playerController = hit.GetComponent<PlayerController>();

            if (playerController != null)
                StartCoroutine(SmoothPush(playerController.transform, forceMul));

            FollowerUnit unit = hit.GetComponent<FollowerUnit>();

            if (unit != null && unit.team == FollowerUnit.CrowdTeam.Player)
                StartCoroutine(SmoothPush(unit.transform, forceMul));
        }
    }

    IEnumerator SmoothPush(Transform target, float forceMultiplier = 1f)
    {
        if (target == null) yield break;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero) yield break;

        Vector3 start = target.position;
        Vector3 end = start + dir.normalized * (shockwaveForce * forceMultiplier);
        end.y = start.y;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shockwavePushDuration);

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            Vector3 from = target.position;
            Vector3 desired = Vector3.Lerp(start, end, eased);

            if (CrowdManager.Instance != null)
                CrowdManager.Instance.ApplyCrowdMovement(from, desired, target);
            else
                target.position = desired;

            yield return null;
        }

        if (target != null)
        {
            if (CrowdManager.Instance != null)
                CrowdManager.Instance.ApplyCrowdMovement(target.position, end, target);
            else
                target.position = end;
        }
    }

    void ChasePlayer(Vector3 playerPos)
    {
        playerPos.y = WorldPosition.y;
        MoveTo(playerPos, chaseSpeed * Mathf.Max(0.05f, waveChaseSpeedMultiplier));
    }

    void Wander()
    {
        float speed = moveSpeed * Mathf.Max(0.05f, waveMoveSpeedMultiplier);
        Vector3 before = WorldPosition;
        MoveTo(targetPos, speed);
        UpdateWanderStuckState(before, speed);

        if (HorizontalDistance(WorldPosition, targetPos) < 1f)
            SetNewTarget();
    }

    void UpdateWanderStuckState(Vector3 beforeMove, float speed)
    {
        float moved = HorizontalDistance(beforeMove, WorldPosition);
        float expected = speed * Time.fixedDeltaTime;

        if (moved < expected * 0.12f && HorizontalDistance(WorldPosition, targetPos) > 2f)
        {
            _wanderStuckTimer += Time.fixedDeltaTime;
            if (_wanderStuckTimer >= 0.55f)
            {
                _wanderStuckTimer = 0f;
                SetNewTarget();
            }
        }
        else
        {
            _wanderStuckTimer = 0f;
        }
    }

    void MoveTo(Vector3 destination, float speed)
    {
        Vector3 current = WorldPosition;
        Vector3 next = Vector3.MoveTowards(current, destination, speed * Time.fixedDeltaTime);

        if (CrowdManager.Instance != null)
            CrowdManager.Instance.ApplyCrowdMovement(current, next, transform);
        else if (_rb != null)
        {
            _rb.position = next;
            transform.position = next;
        }
        else
            transform.position = next;

        // Prefer actual displacement so obstacles/NavMesh do not fight instantaneous look-at-goal.
        Vector3 face = WorldPosition - current;
        face.y = 0f;
        float moved = face.magnitude;

        if (moved < 0.001f)
        {
            Vector3 dir = destination - WorldPosition;
            dir.y = 0f;
            face = dir;
        }

        if (face.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(face.normalized);
            float maxDeg = Mathf.Max(90f, turnTowardMoveDegreesPerSecond) * Time.fixedDeltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, maxDeg);
        }
    }

    void SetNewTarget()
    {
        _wanderStuckTimer = 0f;

        if (CrowdManager.Instance != null &&
            CrowdManager.Instance.TryPickReachableRoamPoint(WorldPosition, transform, out Vector3 reachable))
        {
            targetPos = reachable;
            return;
        }

        Vector3 center = Vector3.zero;
        float range = wanderRange;

        if (CrowdManager.Instance != null)
        {
            center = CrowdManager.Instance.GetPlayAreaOriginXZ();
            range = Mathf.Max(1f, CrowdManager.Instance.spawnRange);
        }

        float x = center.x + Random.Range(-range, range);
        float z = center.z + Random.Range(-range, range);
        Vector3 candidate = new Vector3(x, 0f, z);

        if (CrowdManager.Instance != null)
        {
            candidate = CrowdManager.Instance.SnapToWalkableHeight(candidate);
            if (CrowdManager.Instance.ShouldConstrainToNavMesh() &&
                CrowdNavMeshMovement.TrySampleOnNavMesh(
                    candidate,
                    CrowdManager.Instance.GetNavMeshSampleRadius() * 2.5f,
                    CrowdManager.Instance.GetNavMeshAreaMask(),
                    out Vector3 onMesh))
            {
                candidate = onMesh;
            }

            targetPos = candidate;
        }
        else
        {
            targetPos = new Vector3(x, transform.position.y, z);
        }
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_defeated || CrowdManager.Instance == null) return;

        PlayerController playerController = other.GetComponent<PlayerController>();
        FollowerUnit unit = other.GetComponent<FollowerUnit>();

        if (playerController != null)
        {
            CrowdManager.Instance.ResolveBattle(this);
            return;
        }

        if (unit != null)
        {
            if (unit.team == FollowerUnit.CrowdTeam.Neutral)
            {
                CrowdManager.Instance.ConvertToEnemy(unit.gameObject);
                return;
            }

            if (unit.team == FollowerUnit.CrowdTeam.Player)
            {
                CrowdManager.Instance.ResolveBattle(this);
                return;
            }
        }
    }
}