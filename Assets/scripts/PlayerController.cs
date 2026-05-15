using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public Animator animator; // 👈 DRAG YOUR HUMAN MODEL HERE

    [HideInInspector] public float waveMoveSpeedMultiplier = 1f;
    [HideInInspector] public float waveShockwaveCooldownMultiplier = 1f;

    [Header("Wave abilities")]
    [Tooltip("Assigned each wave by WaveManager.")]
    public PlayerWaveAbility activeWaveAbility = PlayerWaveAbility.None;

    [Tooltip("TurboDash (Left Shift): travel distance.")]
    public float waveDashDistance = 5.5f;

    [Tooltip("How long the dash lasts (seconds).")]
    public float waveDashDuration = 0.2f;

    public float waveDashCooldown = 2.65f;

    [Tooltip("RallyCry (F): max distance to recruit a neutral.")]
    public float rallyCryRadius = 15f;

    public float rallyCryCooldown = 10f;

    [Tooltip("SurgePulse: delay before the follow-up ring.")]
    public float surgePulseDelay = 0.34f;

    [Tooltip("SurgePulse: second ring radius as a fraction of the main shockwave.")]
    public float surgePulseRadiusMul = 0.58f;

    public float surgePulseForceMul = 0.72f;

    [Header("Shockwave")]
    public float shockwaveRadius = 8f;
    public float shockwaveForce = 12f;
    public float shockwaveCooldown = 5f;
    public float shockwavePushDuration = 0.25f;
    public float shockwaveBattleLockDuration = 1f;

    [ColorUsage(true, true)]
    [Tooltip("Energy colour for both the mesh ripple and pulse light.")]
    public Color shockwaveVisualTint = new Color(0.25f, 0.92f, 1.15f, 1f); // HDR blue

    public GameObject shockwavePrefab;
    public TextMeshProUGUI shockwaveCooldownText;

    private Rigidbody rb;
    private float shockwaveTimer = 0f;
    private Vector3 moveDir;
    private float dashCooldownRemaining;
    private float rallyCooldownRemaining;
    private bool dashInProgress;
    private Coroutine surgeRoutine;
    private Coroutine dashRoutine;

    [Header("NavMesh")]
    [Tooltip("Slide along the baked NavMesh when CrowdManager allows it and data exists.")]
    public bool useNavMeshConstraint = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        moveDir = new Vector3(h, 0f, v).normalized;

        if (animator != null && HasIsMovingParameter())
        {
            bool isMoving = moveDir.magnitude > 0.1f;
            animator.SetBool("isMoving", isMoving);
        }

        UpdateCooldownUI();

        dashCooldownRemaining -= Time.deltaTime;
        rallyCooldownRemaining -= Time.deltaTime;
        if (dashCooldownRemaining < 0f) dashCooldownRemaining = 0f;
        if (rallyCooldownRemaining < 0f) rallyCooldownRemaining = 0f;

        if (activeWaveAbility == PlayerWaveAbility.TurboDash
            && Input.GetKeyDown(KeyCode.LeftShift)
            && !dashInProgress
            && dashCooldownRemaining <= 0f)
        {
            if (dashRoutine != null)
                StopCoroutine(dashRoutine);
            dashRoutine = StartCoroutine(CoTurboDash());
        }

        if (activeWaveAbility == PlayerWaveAbility.RallyCry
            && Input.GetKeyDown(KeyCode.F)
            && rallyCooldownRemaining <= 0f)
        {
            TryRallyCry();
        }

        if (Input.GetKeyDown(KeyCode.E) && shockwaveTimer <= 0f)
        {
            ActivateShockwave();
            shockwaveTimer = shockwaveCooldown * Mathf.Max(0.05f, waveShockwaveCooldownMultiplier);
        }
    }

    void FixedUpdate()
    {
        if (dashInProgress)
            return;

        float speed = moveSpeed * Mathf.Max(0.05f, waveMoveSpeedMultiplier);
        Vector3 next = rb.position + moveDir * speed * Time.fixedDeltaTime;

        bool nav = useNavMeshConstraint
            && CrowdManager.Instance != null
            && CrowdManager.Instance.ShouldConstrainToNavMesh();

        if (nav && CrowdManager.Instance.TryCrowdNavMove(rb.position, next, out Vector3 clamped))
            next = clamped;

        if (CrowdManager.Instance != null)
            CrowdManager.Instance.ApplyPositionWithStreetGround(next, transform);
        else
            rb.MovePosition(next);

        if (moveDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDir);
    }

    void UpdateCooldownUI()
    {
        if (shockwaveTimer > 0f)
        {
            shockwaveTimer -= Time.deltaTime;

            if (shockwaveCooldownText != null)
                shockwaveCooldownText.text = "SHOCKWAVE: " + Mathf.CeilToInt(shockwaveTimer);
        }
        else
        {
            shockwaveTimer = 0f;

            if (shockwaveCooldownText != null)
                shockwaveCooldownText.text = "SHOCKWAVE: READY";
        }
    }

    public void ResetWaveAbilityState()
    {
        if (surgeRoutine != null)
        {
            StopCoroutine(surgeRoutine);
            surgeRoutine = null;
        }

        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
        }

        dashInProgress = false;
        dashCooldownRemaining = 0f;
        rallyCooldownRemaining = 0f;
    }

    void ActivateShockwave()
    {
        SpawnShockwaveVisual(shockwaveRadius);

        if (CrowdManager.Instance != null)
            CrowdManager.Instance.LockBattleFor(shockwaveBattleLockDuration);

        PushShockwaveTargets(shockwaveRadius, 1f);

        if (activeWaveAbility == PlayerWaveAbility.SurgePulse)
        {
            if (surgeRoutine != null)
                StopCoroutine(surgeRoutine);
            surgeRoutine = StartCoroutine(CoSurgePulseFollowUp());
        }
    }

    void SpawnShockwaveVisual(float radius)
    {
        if (shockwavePrefab == null) return;

        Vector3 pos = transform.position;
        pos.y += 0.05f;

        GameObject rippleObj = Instantiate(shockwavePrefab, pos, Quaternion.identity);

        if (rippleObj.TryGetComponent<ShockwaveEffect>(out ShockwaveEffect ripple))
            ripple.Configure(radius, shockwaveVisualTint);
    }

    void PushShockwaveTargets(float radius, float forceMultiplier)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            EnemyLeader enemy = hit.GetComponent<EnemyLeader>();
            FollowerUnit follower = hit.GetComponent<FollowerUnit>();

            if (enemy != null)
                StartCoroutine(SmoothPush(enemy.transform, forceMultiplier));

            if (follower != null && follower.team == FollowerUnit.CrowdTeam.Enemy)
                StartCoroutine(SmoothPush(follower.transform, forceMultiplier));
        }
    }

    IEnumerator CoSurgePulseFollowUp()
    {
        PlayerWaveAbility keep = PlayerWaveAbility.SurgePulse;
        yield return new WaitForSeconds(surgePulseDelay);

        if (this == null || activeWaveAbility != keep)
        {
            surgeRoutine = null;
            yield break;
        }

        float r = shockwaveRadius * surgePulseRadiusMul;
        SpawnShockwaveVisual(r);
        PushShockwaveTargets(r, surgePulseForceMul);
        surgeRoutine = null;
    }

    IEnumerator CoTurboDash()
    {
        dashInProgress = true;

        Vector3 dir = moveDir.sqrMagnitude > 0.01f
            ? moveDir
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        float duration = Mathf.Max(0.05f, waveDashDuration);
        float speed = waveDashDistance / duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float step = speed * Time.fixedDeltaTime;
            Vector3 next = rb.position + dir * step;

            bool nav = useNavMeshConstraint
                && CrowdManager.Instance != null
                && CrowdManager.Instance.ShouldConstrainToNavMesh();

            if (nav && CrowdManager.Instance.TryCrowdNavMove(rb.position, next, out Vector3 clamped))
                next = clamped;

            if (CrowdManager.Instance != null)
                CrowdManager.Instance.ApplyPositionWithStreetGround(next, transform);
            else
                rb.MovePosition(next);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        dashInProgress = false;
        dashCooldownRemaining = waveDashCooldown;
        dashRoutine = null;
    }

    void TryRallyCry()
    {
        if (CrowdManager.Instance == null) return;

        FollowerUnit best = null;
        float bestSq = rallyCryRadius * rallyCryRadius;

        Collider[] hits = Physics.OverlapSphere(transform.position, rallyCryRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider c in hits)
        {
            FollowerUnit fu = c.GetComponent<FollowerUnit>();
            if (fu == null || fu.team != FollowerUnit.CrowdTeam.Neutral) continue;

            float sq = (fu.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = fu;
            }
        }

        if (best == null) return;

        CrowdManager.Instance.ConvertToPlayer(best.gameObject);
        rallyCooldownRemaining = rallyCryCooldown;
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

    bool HasIsMovingParameter()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (var p in animator.parameters)
        {
            if (p.name == "isMoving") return true;
        }
        return false;
    }
}