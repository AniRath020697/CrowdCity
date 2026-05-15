using UnityEngine;
using System.Collections;

public class EnemyLeader : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float chaseSpeed = 4f;

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

    [Header("Animation")]
    public Animator animator;

    [Header("Enemy Shockwave")]
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
    private Vector3 lastPosition;
    private Coroutine echoRoutine;
    private Rigidbody _rb;

    Vector3 WorldPosition => _rb != null ? _rb.position : transform.position;

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
        if (CrowdManager.Instance == null) return;

        int playerCount = CrowdManager.Instance.playerFollowers.Count + 1;
        int enemyCount = CrowdManager.Instance.enemyFollowers.Count + 1;

        bool playerIsStronger = playerCount > enemyCount;
        bool enemyIsAtLeastEqual = enemyCount >= playerCount;

        float chaseLeash = ChaseDistanceEffective();
        float shockwaveReach = ShockwaveTriggerDistance();

        if (player != null)
        {
            Vector3 playerPos = CrowdManager.Instance.GetActorWorldPosition(player);
            float distanceToPlayer = Vector3.Distance(WorldPosition, playerPos);

            if (playerIsStronger
                && distanceToPlayer <= shockwaveReach
                && shockwaveTimer <= 0f)
            {
                ActivateEnemyShockwave();
                shockwaveTimer = shockwaveCooldown * Mathf.Max(0.05f, waveShockwaveCooldownMultiplier);
            }

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

    float ShockwaveTriggerDistance()
    {
        float r = shockwaveRadius;
        if (activeWaveAbility == EnemyWaveAbility.Harrier)
            return r * 1.15f;
        return r;
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

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float movementAmount = Vector3.Distance(WorldPosition, lastPosition) / dt;
        bool isMoving = movementAmount > 0.05f;

        animator.SetBool("isMoving", isMoving);

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

    void ActivateEnemyShockwave()
    {
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
            target.position = Vector3.Lerp(start, end, eased);
            yield return null;
        }

        if (target != null)
        {
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
        MoveTo(targetPos, moveSpeed * Mathf.Max(0.05f, waveMoveSpeedMultiplier));

        if (Vector3.Distance(transform.position, targetPos) < 1f)
            SetNewTarget();
    }

    void MoveTo(Vector3 destination, float speed)
    {
        Vector3 current = WorldPosition;
        Vector3 next = Vector3.MoveTowards(current, destination, speed * Time.fixedDeltaTime);

        if (CrowdManager.Instance != null && CrowdManager.Instance.ShouldConstrainToNavMesh()
            && CrowdManager.Instance.TryCrowdNavMove(current, next, out Vector3 clamped))
            next = clamped;

        if (CrowdManager.Instance != null)
            CrowdManager.Instance.ApplyPositionWithStreetGround(next, transform);
        else if (_rb != null)
        {
            _rb.position = next;
            transform.position = next;
        }
        else
            transform.position = next;

        Vector3 dir = destination - WorldPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void SetNewTarget()
    {
        Vector3 center = Vector3.zero;
        float range = wanderRange;

        if (CrowdManager.Instance != null)
        {
            center = CrowdManager.Instance.GetPlayAreaOriginXZ();
            range = Mathf.Max(1f, CrowdManager.Instance.spawnRange);
        }

        float x = center.x + Random.Range(-range, range);
        float z = center.z + Random.Range(-range, range);

        if (CrowdManager.Instance != null)
            targetPos = CrowdManager.Instance.SnapToWalkableHeight(new Vector3(x, 0f, z));
        else
            targetPos = new Vector3(x, transform.position.y, z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (CrowdManager.Instance == null) return;

        PlayerController playerController = other.GetComponent<PlayerController>();
        FollowerUnit unit = other.GetComponent<FollowerUnit>();

        if (playerController != null)
        {
            CrowdManager.Instance.ResolveBattle();
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
                CrowdManager.Instance.ResolveBattle();
                return;
            }
        }
    }
}