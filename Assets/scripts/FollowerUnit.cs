using UnityEngine;

public class FollowerUnit : MonoBehaviour
{
    public enum CrowdTeam
    {
        Neutral,
        Player,
        Enemy
    }

    public CrowdTeam team = CrowdTeam.Neutral;

    [Header("Follow Movement")]
    public float followSpeed = 7f;

    [Header("Back Blob Formation")]
    public float spacing = 1.4f;

    [Tooltip("Followers never target a spot closer than this to the leader (stops large crowds from piling on the player).")]
    public float minDistanceFromLeader = 1.45f;

    public float blobRandomness = 0.45f;
    public float blobWaveAmount = 0.25f;
    public float blobWaveSpeed = 2.5f;

    [Header("Neutral Roaming")]
    public float roamSpeed = 2.5f;
    public float roamRange = 40f;
    public float roamChangeTime = 2f;

    [Header("Animation")]
    [Tooltip("Uses Animator bool \"isMoving\" when the controller exposes it (e.g. CrowdTinyHeroLocomotion).")]
    public bool driveLocomotionAnimator = true;

    public float idleMoveThreshold = 0.05f;

    [Header("NavMesh")]
    [Tooltip("Slide along the baked NavMesh when CrowdManager allows it and data exists.")]
    public bool useNavMeshConstraint = true;

    private Transform leader;
    private int indexInCrowd;

    private Vector3 roamTarget;
    private float roamTimer;

    private float randomOffsetX;
    private float randomOffsetZ;
    private float randomPhase;

    private Animator _animator;
    private bool _checkedParam;
    private bool _hasIsMoving;
    private Vector3 _lastXZ;
    private Rigidbody _rb;

    Vector3 WorldPosition => _rb != null ? _rb.position : transform.position;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        MakeRandomOffset();
        SetNewRoamTarget();
        CacheAnimator();
        _lastXZ = FlattenY(transform.position);
    }

    public void SetNeutral()
    {
        team = CrowdTeam.Neutral;
        leader = null;
        indexInCrowd = 0;
        SetNewRoamTarget();
        _lastXZ = FlattenY(transform.position);
    }

    public void SetFollower(CrowdTeam newTeam, Transform newLeader, int index)
    {
        team = newTeam;
        leader = newLeader;
        indexInCrowd = index;
        MakeRandomOffset();
        _lastXZ = FlattenY(transform.position);
    }

    public void SetFollowerIndex(int index)
    {
        indexInCrowd = index;
    }

    public Transform GetLeaderTransform() => leader;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (team == CrowdTeam.Neutral)
        {
            Roam(dt);
            return;
        }

        if (leader == null) return;

        Vector3 current = WorldPosition;
        Vector3 targetPos = GetBackBlobPosition();
        targetPos.y = current.y;

        float moveSpeed = followSpeed;
        if (team == CrowdTeam.Player
            && leader != null
            && CrowdManager.Instance != null
            && leader == CrowdManager.Instance.player)
        {
            PlayerController pc = leader.GetComponent<PlayerController>();
            if (pc != null && pc.IsTurboDashing)
                moveSpeed = Mathf.Max(moveSpeed, pc.TurboDashMoveSpeed * 1.08f);
        }

        float maxStep = moveSpeed * dt;
        Vector3 flatDelta = targetPos - current;
        flatDelta.y = 0f;
        float dist = flatDelta.magnitude;
        if (dist > 0.01f && dist < minDistanceFromLeader)
            maxStep *= Mathf.Clamp01(dist / minDistanceFromLeader);

        Vector3 next = Vector3.MoveTowards(current, targetPos, maxStep);

        TryNavMeshClamp(current, ref next);
        ApplyWorldPosition(next);

        Vector3 dir = targetPos - WorldPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Lerp(transform.rotation, look, 8f * dt);
        }
    }

    void LateUpdate()
    {
        UpdateLocomotionAnimator();
    }

    void CacheAnimator()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_animator == null) return;

        if (!_checkedParam)
            RefreshAnimatorParameterCache();
    }

    /// <summary>Call after <see cref="CrowdManager"/> assigns a new <see cref="Animator.runtimeAnimatorController"/>.</summary>
    public void InvalidateAnimatorParameterCache()
    {
        _checkedParam = false;
        _hasIsMoving = false;
    }

    void RefreshAnimatorParameterCache()
    {
        if (_animator == null) return;

        _checkedParam = true;
        _hasIsMoving = HasBoolParameter(_animator, "isMoving");
    }

    void UpdateLocomotionAnimator()
    {
        if (!driveLocomotionAnimator) return;

        CacheAnimator();
        if (_animator == null || !_hasIsMoving) return;

        Vector3 cur = FlattenY(transform.position);
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = Vector3.Distance(cur, _lastXZ) / dt;
        _lastXZ = cur;

        _animator.SetBool("isMoving", speed > idleMoveThreshold);
    }

    static Vector3 FlattenY(Vector3 p)
    {
        p.y = 0f;
        return p;
    }

    bool TryNavMeshClamp(Vector3 from, ref Vector3 to)
    {
        if (!useNavMeshConstraint || CrowdManager.Instance == null || !CrowdManager.Instance.ShouldConstrainToNavMesh())
            return false;

        if (CrowdManager.Instance.TryCrowdNavMove(from, to, out Vector3 clamped))
        {
            to = clamped;
            return true;
        }

        return false;
    }

    static bool HasBoolParameter(Animator anim, string name)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;

        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
                return true;
        }

        return false;
    }

    Vector3 GetBackBlobPosition()
    {
        int row = indexInCrowd / 5;
        int column = indexInCrowd % 5;

        float xOffset = (column - 2f) * spacing;
        float zOffset = -(row + 1) * spacing;

        float rowCurve = Mathf.Abs(column - 2f) * 0.45f;
        zOffset -= rowCurve;

        float waveX = Mathf.Sin(Time.time * blobWaveSpeed + randomPhase) * blobWaveAmount;
        float waveZ = Mathf.Cos(Time.time * blobWaveSpeed + randomPhase) * blobWaveAmount;

        xOffset += randomOffsetX + waveX;
        zOffset += randomOffsetZ + waveZ;

        Vector3 leaderPos = leader.position;
        if (CrowdManager.Instance != null && leader == CrowdManager.Instance.player)
            leaderPos = CrowdManager.Instance.GetActorWorldPosition(leader);

        Vector3 back = GetLeaderBackDirection();
        Vector3 right = Vector3.Cross(Vector3.up, back);
        if (right.sqrMagnitude < 0.0001f)
            right = leader.right;
        else
            right.Normalize();

        Vector3 pos = leaderPos + right * xOffset + back * Mathf.Abs(zOffset);

        Vector3 offset = pos - leaderPos;
        offset.y = 0f;
        float minDist = Mathf.Max(0.5f, minDistanceFromLeader);
        if (offset.sqrMagnitude < minDist * minDist)
        {
            offset = offset.sqrMagnitude > 0.0001f ? offset.normalized * minDist : back * minDist;
            pos = leaderPos + offset;
        }

        pos.y = leaderPos.y;
        return pos;
    }

    Vector3 GetLeaderBackDirection()
    {
        Vector3 back = -leader.forward;
        back.y = 0f;

        if (back.sqrMagnitude < 0.01f)
            back = Vector3.back;

        if (CrowdManager.Instance != null && leader == CrowdManager.Instance.player)
        {
            Rigidbody leaderRb = leader.GetComponent<Rigidbody>();
            if (leaderRb != null)
            {
                Vector3 vel = leaderRb.linearVelocity;
                vel.y = 0f;
                if (vel.sqrMagnitude > 0.35f)
                    back = -vel.normalized;
            }
        }

        return back.normalized;
    }

    void MakeRandomOffset()
    {
        randomOffsetX = Random.Range(-blobRandomness, blobRandomness);
        randomOffsetZ = Random.Range(-blobRandomness, blobRandomness);
        randomPhase = Random.Range(0f, 10f);
    }

    void Roam(float dt)
    {
        roamTimer += dt;

        Vector3 current = WorldPosition;

        if (roamTimer >= roamChangeTime || Vector3.Distance(current, roamTarget) < 1f)
            SetNewRoamTarget();

        Vector3 next = Vector3.MoveTowards(current, roamTarget, roamSpeed * dt);

        TryNavMeshClamp(current, ref next);
        ApplyWorldPosition(next);

        Vector3 dir = roamTarget - WorldPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Lerp(transform.rotation, look, 5f * dt);
        }
    }

    void ApplyWorldPosition(Vector3 worldPos)
    {
        if (CrowdManager.Instance != null)
            CrowdManager.Instance.ApplyPositionWithStreetGround(worldPos, transform);
        else if (_rb != null)
        {
            _rb.position = worldPos;
            transform.position = worldPos;
        }
        else
            transform.position = worldPos;
    }

    void SetNewRoamTarget()
    {
        roamTimer = 0f;

        if (team == CrowdTeam.Neutral && CrowdManager.Instance != null &&
            CrowdManager.Instance.TryGetRandomCityRoamPoint(out Vector3 cityPoint))
        {
            roamTarget = cityPoint;
            return;
        }

        Vector3 center = Vector3.zero;
        float range = roamRange;

        if (CrowdManager.Instance != null)
        {
            center = CrowdManager.Instance.GetPlayAreaOriginXZ();
            range = Mathf.Max(1f, CrowdManager.Instance.spawnRange);
        }

        float x = center.x + Random.Range(-range, range);
        float z = center.z + Random.Range(-range, range);

        if (CrowdManager.Instance != null)
            roamTarget = CrowdManager.Instance.SnapToWalkableHeight(new Vector3(x, 0f, z));
        else
            roamTarget = new Vector3(x, transform.position.y, z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (CrowdManager.Instance == null) return;

        PlayerController player = other.GetComponent<PlayerController>();
        EnemyLeader enemyLeader = other.GetComponent<EnemyLeader>();
        FollowerUnit otherUnit = other.GetComponent<FollowerUnit>();

        if (team == CrowdTeam.Neutral)
        {
            if (player != null)
            {
                CrowdManager.Instance.ConvertToPlayer(gameObject);
                return;
            }

            if (enemyLeader != null)
            {
                CrowdManager.Instance.ConvertToEnemy(gameObject);
                return;
            }

            if (otherUnit != null)
            {
                if (otherUnit.team == CrowdTeam.Player)
                {
                    CrowdManager.Instance.ConvertToPlayer(gameObject);
                    return;
                }

                if (otherUnit.team == CrowdTeam.Enemy)
                {
                    CrowdManager.Instance.ConvertToEnemy(gameObject);
                    return;
                }
            }
        }

        if (team == CrowdTeam.Player)
        {
            if (enemyLeader != null)
            {
                CrowdManager.Instance.ResolveBattle(enemyLeader);
                return;
            }

            if (otherUnit != null && otherUnit.team == CrowdTeam.Enemy)
            {
                CrowdManager.Instance.ResolveBattle(ResolveEnemyLeaderFromUnit(otherUnit));
                return;
            }
        }

        if (team == CrowdTeam.Enemy)
        {
            EnemyLeader myLeader = leader != null ? leader.GetComponent<EnemyLeader>() : null;

            if (player != null)
            {
                CrowdManager.Instance.ResolveBattle(myLeader);
                return;
            }

            if (otherUnit != null && otherUnit.team == CrowdTeam.Player)
            {
                CrowdManager.Instance.ResolveBattle(myLeader);
                return;
            }
        }
    }

    static EnemyLeader ResolveEnemyLeaderFromUnit(FollowerUnit enemyUnit)
    {
        if (enemyUnit == null) return null;

        Transform leaderT = enemyUnit.GetLeaderTransform();
        if (leaderT != null)
        {
            EnemyLeader el = leaderT.GetComponent<EnemyLeader>();
            if (el != null) return el;
        }

        return enemyUnit.GetComponentInParent<EnemyLeader>();
    }
}
