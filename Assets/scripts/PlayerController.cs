using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base walk speed. Enemies chase at ~5.5 so you cannot outrun them without Turbo Dash.")]
    public float moveSpeed = 5f;
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
    [Tooltip("Uses allowed each wave (player and enemies). Set by WaveManager.")]
    public int maxShockwavesPerWave = 2;

    public float shockwaveRadius = 8f;
    public float shockwaveForce = 12f;
    public float shockwaveCooldown = 5f;
    public float shockwavePushDuration = 0.25f;
    public float shockwaveBattleLockDuration = 1f;

    [ColorUsage(true, true)]
    [Tooltip("Energy colour for both the mesh ripple and pulse light.")]
    public Color shockwaveVisualTint = new Color(0.25f, 0.92f, 1.15f, 1f); // HDR blue

    public GameObject shockwavePrefab;

    [Header("Ability FX")]
    [Tooltip("Ring / streak / beam bursts. Falls back to Shockwave Prefab if empty.")]
    public GameObject abilityEffectPrefab;

    [ColorUsage(true, true)]
    public Color dashEffectTint = new Color(0.22f, 0.88f, 1.25f, 0.95f);

    [ColorUsage(true, true)]
    public Color rallyEffectTint = new Color(0.52f, 0.88f, 1.22f, 0.9f);

    [Tooltip("Distance between dash streak bursts along the path.")]
    public float dashEffectSpacing = 1.35f;

    [Header("UI")]
    [Tooltip("Canvas object named \"Shockwave\" — shows uses / cooldown for E.")]
    public TextMeshProUGUI shockwaveStatusText;

    [Tooltip("Optional. Turbo Dash / Rally Cry status (e.g. ShockwaveCooldownText).")]
    public TextMeshProUGUI shockwaveCooldownText;

    [Header("Super power key hint")]
    [Tooltip("Bottom-left key label (e.g. [SHIFT]). Auto-created at Play if empty.")]
    public TextMeshProUGUI superPowerKeyHintText;

    [Tooltip("Legacy flag — HUD layout is always applied for 1024×768.")]
    public bool applySuperPowerUiLayout = true;

    private Rigidbody rb;
    private float shockwaveTimer = 0f;
    private int shockwavesRemaining;
    private Vector3 moveDir;
    private float dashCooldownRemaining;
    private float rallyCooldownRemaining;
    private bool dashInProgress;

    public bool IsTurboDashing => dashInProgress;

    public float TurboDashMoveSpeed =>
        waveDashDistance / Mathf.Max(0.05f, waveDashDuration);
    private Coroutine surgeRoutine;
    private Coroutine dashRoutine;
    private string _lastShockwaveStatusLayoutText;
    private string _lastSuperPowerStatusLayoutText;
    private string _lastSuperPowerHintLayoutText;
    private GameObject _shockwaveUiRoot;

    [Header("NavMesh")]
    [Tooltip("Slide along the baked NavMesh when CrowdManager allows it and data exists.")]
    public bool useNavMeshConstraint = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        ResolveShockwaveUi();
        ApplyHudLayoutFor1024x768();
    }

    public void ApplyHudLayoutFor1024x768(bool force = false)
    {
        if (force)
        {
            _lastShockwaveStatusLayoutText = null;
            _lastSuperPowerStatusLayoutText = null;
            _lastSuperPowerHintLayoutText = null;
        }

        ConfigureShockwaveStatusUILayout(force);
        ConfigureSuperPowerStatusUILayout(force);
        ConfigureSuperPowerKeyHintUILayout(force);
    }

    void ResolveShockwaveUi()
    {
        if (shockwaveStatusText != null)
        {
            CacheShockwaveUiRoot(shockwaveStatusText.gameObject);
            return;
        }

        GameObject shockwaveObject = FindUiObjectByName("Shockwave");
        if (shockwaveObject == null)
            return;

        CacheShockwaveUiRoot(shockwaveObject);
        shockwaveStatusText = shockwaveObject.GetComponent<TextMeshProUGUI>();
        if (shockwaveStatusText == null)
            shockwaveStatusText = shockwaveObject.GetComponentInChildren<TextMeshProUGUI>(true);

        _lastShockwaveStatusLayoutText = null;
        ConfigureShockwaveStatusUILayout(force: true);
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

    void CacheShockwaveUiRoot(GameObject fromObject)
    {
        if (fromObject.name == "Shockwave")
            _shockwaveUiRoot = fromObject;
        else if (fromObject.transform.parent != null && fromObject.transform.parent.name == "Shockwave")
            _shockwaveUiRoot = fromObject.transform.parent.gameObject;
        else
            _shockwaveUiRoot = fromObject;
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
        UpdateSuperPowerKeyHintUI();

        dashCooldownRemaining -= Time.deltaTime;
        rallyCooldownRemaining -= Time.deltaTime;
        if (dashCooldownRemaining < 0f) dashCooldownRemaining = 0f;
        if (rallyCooldownRemaining < 0f) rallyCooldownRemaining = 0f;

        HandleWaveSuperPowerInput();
    }

    void HandleWaveSuperPowerInput()
    {
        switch (activeWaveAbility)
        {
            case PlayerWaveAbility.TurboDash:
                if (Input.GetKeyDown(KeyCode.LeftShift)
                    && !dashInProgress
                    && dashCooldownRemaining <= 0f)
                {
                    if (dashRoutine != null)
                        StopCoroutine(dashRoutine);
                    dashRoutine = StartCoroutine(CoTurboDash());
                }
                break;

            case PlayerWaveAbility.RallyCry:
                if (Input.GetKeyDown(KeyCode.F) && rallyCooldownRemaining <= 0f)
                    TryRallyCry();
                break;
        }

        if (Input.GetKeyDown(KeyCode.E) && CanUseShockwave())
            UseShockwave();
    }

    bool UsesWaveShockwaveLimit()
    {
        return CrowdManager.Instance != null
            && CrowdManager.Instance.useWaveManagerForSpawning
            && WaveManager.Instance != null;
    }

    bool CanUseShockwave()
    {
        if (shockwaveTimer > 0f)
            return false;

        if (UsesWaveShockwaveLimit())
            return shockwavesRemaining > 0;

        return true;
    }

    void UseShockwave()
    {
        if (UsesWaveShockwaveLimit())
            shockwavesRemaining = Mathf.Max(0, shockwavesRemaining - 1);

        ActivateShockwave();
        shockwaveTimer = shockwaveCooldown * Mathf.Max(0.05f, waveShockwaveCooldownMultiplier);
    }

    void FixedUpdate()
    {
        if (dashInProgress)
            return;

        float speed = moveSpeed * Mathf.Max(0.05f, waveMoveSpeedMultiplier);
        Vector3 from = rb.position;
        Vector3 next = from + moveDir * speed * Time.fixedDeltaTime;

        if (CrowdManager.Instance != null)
            CrowdManager.Instance.ApplyCrowdMovement(from, next, transform);
        else
            rb.MovePosition(next);

        if (moveDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDir);
    }

    void UpdateCooldownUI()
    {
        ResolveShockwaveUi();

        if (shockwaveTimer > 0f)
            shockwaveTimer -= Time.deltaTime;

        bool showWavePower = activeWaveAbility != PlayerWaveAbility.None
            && activeWaveAbility != PlayerWaveAbility.SurgePulse;
        bool showShockwave = UsesWaveShockwaveLimit() || activeWaveAbility == PlayerWaveAbility.None;

        if (shockwaveStatusText != null)
        {
            UpdateShockwaveStatusLabel(showShockwave);
            UpdateSuperPowerStatusLabel(showWavePower);
            return;
        }

        UpdateLegacyCombinedStatusLabel(showWavePower, showShockwave);
    }

    void UpdateShockwaveStatusLabel(bool show)
    {
        SetShockwaveUiActive(show);
        if (!show || shockwaveStatusText == null)
            return;

        shockwaveStatusText.text = GetShockwaveStatusLine();
        ConfigureShockwaveStatusUILayout();
    }

    void UpdateSuperPowerStatusLabel(bool show)
    {
        if (shockwaveCooldownText == null)
            return;

        shockwaveCooldownText.gameObject.SetActive(show);
        if (!show)
            return;

        string powerName = GetWaveSuperPowerDisplayName(activeWaveAbility, true);
        float cooldown = GetActiveSuperPowerCooldownRemaining();
        shockwaveCooldownText.text = cooldown > 0f
            ? powerName + " = " + Mathf.CeilToInt(cooldown)
            : powerName + " = READY";
        ConfigureSuperPowerStatusUILayout();
    }

    void UpdateLegacyCombinedStatusLabel(bool showWavePower, bool showShockwave)
    {
        if (shockwaveCooldownText == null)
            return;

        if (!showWavePower && !showShockwave)
        {
            shockwaveCooldownText.gameObject.SetActive(false);
            return;
        }

        shockwaveCooldownText.gameObject.SetActive(true);

        string status = "";
        if (showWavePower)
        {
            string powerName = GetWaveSuperPowerDisplayName(activeWaveAbility, true);
            float cooldown = GetActiveSuperPowerCooldownRemaining();
            status = cooldown > 0f
                ? powerName + " = " + Mathf.CeilToInt(cooldown)
                : powerName + " = READY";
        }

        if (showShockwave)
        {
            string shockLine = GetShockwaveStatusLine();
            status = string.IsNullOrEmpty(status) ? shockLine : status + "\n" + shockLine;
        }

        shockwaveCooldownText.text = status;
        ConfigureSuperPowerStatusUILayout();
    }

    void SetShockwaveUiActive(bool active)
    {
        if (_shockwaveUiRoot != null)
            _shockwaveUiRoot.SetActive(active);
        else if (shockwaveStatusText != null)
            shockwaveStatusText.gameObject.SetActive(active);
    }

    string GetShockwaveStatusLine()
    {
        if (UsesWaveShockwaveLimit())
        {
            if (shockwavesRemaining <= 0)
                return "SHOCKWAVE = 0";

            if (shockwaveTimer > 0f)
                return "SHOCKWAVE = " + Mathf.CeilToInt(shockwaveTimer);

            return "SHOCKWAVE = " + shockwavesRemaining;
        }

        if (shockwaveTimer > 0f)
            return "SHOCKWAVE = " + Mathf.CeilToInt(shockwaveTimer);

        return "SHOCKWAVE = READY";
    }

    void ConfigureShockwaveStatusUILayout(bool force = false)
    {
        if (shockwaveStatusText == null)
            return;

        string text = shockwaveStatusText.text;
        if (!force && text == _lastShockwaveStatusLayoutText)
            return;

        _lastShockwaveStatusLayoutText = text;
        UiLayout1024x768.ApplyBottomCenter(shockwaveStatusText, SafeMarginY, 520f, 22);
    }

    void ConfigureSuperPowerStatusUILayout(bool force = false)
    {
        if (shockwaveCooldownText == null)
            return;

        string text = shockwaveCooldownText.text;
        if (!force && text == _lastSuperPowerStatusLayoutText)
            return;

        _lastSuperPowerStatusLayoutText = text;
        UiLayout1024x768.ApplyBottomRight(shockwaveCooldownText, SafeMarginY, 400f, 72f);
    }

    void ConfigureSuperPowerKeyHintUILayout(bool force = false)
    {
        if (superPowerKeyHintText == null)
            return;

        string text = superPowerKeyHintText.text;
        if (!force && text == _lastSuperPowerHintLayoutText)
            return;

        _lastSuperPowerHintLayoutText = text;
        UiLayout1024x768.ApplyBottomLeft(superPowerKeyHintText, SafeMarginY, 300f);
    }

    static float SafeMarginY => 32f;

    float GetActiveSuperPowerCooldownRemaining()
    {
        switch (activeWaveAbility)
        {
            case PlayerWaveAbility.TurboDash:
                return dashCooldownRemaining;
            case PlayerWaveAbility.RallyCry:
                return rallyCooldownRemaining;
            default:
                return 0f;
        }
    }

    static string GetWaveSuperPowerDisplayName(PlayerWaveAbility ability, bool upperCase = false)
    {
        string name;
        switch (ability)
        {
            case PlayerWaveAbility.TurboDash: name = "Turbo Dash"; break;
            case PlayerWaveAbility.RallyCry: name = "Rally Cry"; break;
            case PlayerWaveAbility.SurgePulse: name = "Surge Pulse"; break;
            default: name = "Shockwave"; break;
        }

        return upperCase ? name.ToUpperInvariant() : name;
    }

    string GetWaveSuperPowerDisplayName()
    {
        return GetWaveSuperPowerDisplayName(activeWaveAbility);
    }

    static string GetWaveSuperPowerKeyLabel(PlayerWaveAbility ability)
    {
        switch (ability)
        {
            case PlayerWaveAbility.TurboDash: return "Shift";
            case PlayerWaveAbility.RallyCry: return "F";
            case PlayerWaveAbility.SurgePulse: return "E";
            default: return "-";
        }
    }

    void EnsureSuperPowerKeyHintUI()
    {
        if (superPowerKeyHintText != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        var go = new GameObject("SuperPowerKeyHint", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        superPowerKeyHintText = go.AddComponent<TextMeshProUGUI>();
        superPowerKeyHintText.fontStyle = FontStyles.Bold;
        superPowerKeyHintText.color = new Color(0.92f, 0.95f, 1f);

        if (shockwaveStatusText != null)
            superPowerKeyHintText.font = shockwaveStatusText.font;
        else if (shockwaveCooldownText != null)
            superPowerKeyHintText.font = shockwaveCooldownText.font;

        ConfigureSuperPowerKeyHintUILayout();
    }

    void UpdateSuperPowerKeyHintUI()
    {
        EnsureSuperPowerKeyHintUI();
        if (superPowerKeyHintText == null)
            return;

        superPowerKeyHintText.enableWordWrapping = false;
        superPowerKeyHintText.overflowMode = TextOverflowModes.Overflow;

        bool hasWaveKey = activeWaveAbility != PlayerWaveAbility.None
            && activeWaveAbility != PlayerWaveAbility.SurgePulse;
        bool hasShockwaveKey = UsesWaveShockwaveLimit() || !hasWaveKey;

        if (!hasWaveKey && !hasShockwaveKey)
        {
            superPowerKeyHintText.gameObject.SetActive(false);
            return;
        }

        superPowerKeyHintText.gameObject.SetActive(true);

        string hint = "";
        if (hasWaveKey)
            hint = "[" + GetWaveSuperPowerKeyLabel(activeWaveAbility).ToUpperInvariant() + "]";
        if (hasShockwaveKey)
            hint += (hint.Length > 0 ? "  " : "") + "[E]";

        superPowerKeyHintText.text = hint;
        ConfigureSuperPowerKeyHintUILayout();
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
        shockwaveTimer = 0f;
        shockwavesRemaining = Mathf.Max(0, maxShockwavesPerWave);
        UpdateSuperPowerKeyHintUI();
    }

    void ActivateShockwave()
    {
        ShockwaveSfx.Play(transform.position);
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
        DashSfx.Play(transform.position);

        Vector3 dir = moveDir.sqrMagnitude > 0.01f
            ? moveDir
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        float duration = Mathf.Max(0.05f, waveDashDuration);
        float speed = waveDashDistance / duration;
        float elapsed = 0f;
        float distSinceLastFx = dashEffectSpacing;

        SpawnDashBurst(rb.position, dir, waveDashDistance * 0.35f);

        while (elapsed < duration)
        {
            float step = speed * Time.fixedDeltaTime;
            Vector3 from = rb.position;
            Vector3 next = from + dir * step;

            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.ApplyCrowdMovement(from, next, transform);
                Vector3 delta = rb.position - from;
                if (delta.sqrMagnitude > 0.0001f)
                    CrowdManager.Instance.MovePlayerFollowersBy(delta);
            }
            else
                rb.MovePosition(next);

            distSinceLastFx += step;
            if (distSinceLastFx >= dashEffectSpacing)
            {
                SpawnDashStreak(rb.position, dir, dashEffectSpacing * 1.1f);
                distSinceLastFx = 0f;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        SpawnDashBurst(rb.position, dir, waveDashDistance * 0.25f);
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

        Vector3 recruitPos = best.transform.position;
        RallyCrySfx.Play(transform.position);
        SpawnRallyCryEffects(transform.position, recruitPos);
        CrowdManager.Instance.ConvertToPlayer(best.gameObject);
        rallyCooldownRemaining = rallyCryCooldown;
    }

    GameObject GetAbilityEffectPrefab()
    {
        if (abilityEffectPrefab != null)
            return abilityEffectPrefab;
        return shockwavePrefab;
    }

    void SpawnDashBurst(Vector3 position, Vector3 direction, float streakLength)
    {
        Vector3 p = position;
        p.y += 0.08f;
        SpawnAbilityStreak(p, direction, streakLength, dashEffectTint, 0.28f);
        SpawnAbilityRing(p, Mathf.Clamp(streakLength * 0.55f, 2f, 5f), dashEffectTint, 0.32f);
    }

    void SpawnDashStreak(Vector3 position, Vector3 direction, float streakLength)
    {
        Vector3 p = position;
        p.y += 0.06f;
        SpawnAbilityStreak(p, direction, streakLength, dashEffectTint, 0.2f);
    }

    void SpawnRallyCryEffects(Vector3 from, Vector3 to)
    {
        Vector3 playerPos = from;
        playerPos.y += 0.05f;
        SpawnAbilityAttractField(playerPos, rallyCryRadius * 0.95f, rallyEffectTint, 0.48f);

        Vector3 neutralPos = to;
        neutralPos.y += 0.05f;
        SpawnAbilityPullStream(neutralPos, playerPos, rallyEffectTint, 0.42f);
        SpawnAbilityAbsorbPulse(neutralPos, playerPos, rallyEffectTint, 0.36f);
    }

    void SpawnAbilityAttractField(Vector3 center, float radius, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(center, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureContractingRing(center, radius, theme, effectLifetime);
    }

    void SpawnAbilityPullStream(Vector3 fromNeutral, Vector3 toPlayer, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(fromNeutral, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigurePullStream(fromNeutral, toPlayer, theme, effectLifetime);
    }

    void SpawnAbilityAbsorbPulse(Vector3 atNeutral, Vector3 pullTarget, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(atNeutral, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureAbsorbIntoPoint(atNeutral, pullTarget, theme, effectLifetime);
    }

    void SpawnAbilityRing(Vector3 position, float radius, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(position, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureRing(radius, theme, effectLifetime);
    }

    void SpawnAbilityStreak(Vector3 position, Vector3 direction, float length, Color theme, float effectLifetime)
    {
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        GameObject fxObj = SpawnAbilityFxObject(position, rot);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureStreak(dir, length, theme, effectLifetime);
    }

    void SpawnAbilityBeam(Vector3 from, Vector3 to, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(from, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureBeam(from, to, theme, effectLifetime);
    }

    void SpawnAbilityRecruitPulse(Vector3 position, Color theme, float effectLifetime)
    {
        GameObject fxObj = SpawnAbilityFxObject(position, Quaternion.identity);
        if (fxObj == null) return;

        GetAbilityBurst(fxObj).ConfigureRecruitPulse(position, theme, effectLifetime);
    }

    GameObject SpawnAbilityFxObject(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetAbilityEffectPrefab();
        if (prefab == null) return null;

        return Instantiate(prefab, position, rotation);
    }

    static AbilityBurstEffect GetAbilityBurst(GameObject fxObj)
    {
        if (fxObj.TryGetComponent<AbilityBurstEffect>(out AbilityBurstEffect existing))
            return existing;

        if (fxObj.TryGetComponent<ShockwaveEffect>(out ShockwaveEffect legacy))
            Destroy(legacy);

        return fxObj.AddComponent<AbilityBurstEffect>();
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