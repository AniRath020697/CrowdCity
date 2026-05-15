using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerWaveAbility
{
    None,
    [Tooltip("Left Shift: short burst dash (cooldown between dashes).")]
    TurboDash,
    [Tooltip("F: recruit the nearest neutral in range (cooldown).")]
    RallyCry,
    [Tooltip("Shockwave (E) releases a second smaller pulse shortly after.")]
    SurgePulse
}

public enum EnemyWaveAbility
{
    None,
    [Tooltip("Commits to chase from farther away.")]
    Harrier,
    [Tooltip("When outnumbered, still creeps toward the player if you get close.")]
    StubbornSnap,
    [Tooltip("After a shockwave, a weaker second blast fires moments later.")]
    EchoBlast
}

/// <summary>
/// Three escalating zones on one map: each wave sets play area, crowd sizes, teleports the player,
/// and applies numeric modifiers plus optional unique abilities. Clear a wave by defeating the enemy
/// or surviving until the timer ends; lose only when the enemy wins a crowd battle. After wave 3, win.
/// </summary>
[DefaultExecutionOrder(50)]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Refs")]
    [Tooltip("Uses this crowd + player. Auto-finds if empty.")]
    public CrowdManager crowd;

    [Header("Flow")]
    [Tooltip("When on, CrowdManager does not spawn neutrals/enemy at Start — this component starts wave 1.")]
    public bool controlCrowdSpawns = true;

    [Tooltip("Seconds after clearing a wave (defeat or survive) before the next zone loads.")]
    public float delayBeforeNextWave = 3f;

    [Header("Defaults")]
    [Tooltip("Each wave: 60s, 3/5/7 enemy leaders, 1–2 enemy followers total at spawn (then they recruit neutrals), neutrals 45/55/65, and abilities TurboDash/RallyCry/SurgePulse vs Harrier/StubbornSnap/EchoBlast.")]
    public bool applyStandardWaveProgression = true;

    [Tooltip("If every wave still has None for player or enemy ability, assign distinct ones for the first three waves (TurboDash → RallyCry → SurgePulse, Harrier → StubbornSnap → EchoBlast).")]
    public bool autoDistinctAbilitiesWhenUnset = true;

    [Header("Waves (exactly 3 recommended)")]
    public WaveZone[] waves = new WaveZone[3];

    int _waveIndex;
    bool _runComplete;
    bool _waveTransitionPending;
    bool _lastClearByDefeatingAllLeaders;

    public int CurrentWaveNumber => Mathf.Clamp(_waveIndex + 1, 1, Mathf.Max(1, waves != null ? waves.Length : 1));
    public WaveZone CurrentWaveDefinition => waves != null && _waveIndex >= 0 && _waveIndex < waves.Length ? waves[_waveIndex] : null;
    public bool IsWaveTransitionPending => _waveTransitionPending;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (crowd == null)
            crowd = FindFirstObjectByType<CrowdManager>();

        if (crowd != null && controlCrowdSpawns)
            crowd.useWaveManagerForSpawning = true;

        EnsureWaveZoneObjects();
        MaybeApplyDefaultAbilitiesIfUnset();

        if (crowd != null)
        {
            crowd.ConfigureCityPopulationFromWaveZones(waves);
            crowd.EnsureCityPlayableBounds();
        }
    }

    void EnsureWaveZoneObjects()
    {
        if (waves == null) return;

        for (int i = 0; i < waves.Length; i++)
        {
            if (waves[i] == null)
                waves[i] = new WaveZone();
        }
    }

    void MaybeApplyDefaultAbilitiesIfUnset()
    {
        if (!autoDistinctAbilitiesWhenUnset || waves == null || waves.Length == 0)
            return;

        bool allPlayerNone = true;
        bool allEnemyNone = true;

        for (int i = 0; i < waves.Length; i++)
        {
            WaveZone w = waves[i];
            if (w == null) continue;
            if (w.playerAbility != PlayerWaveAbility.None) allPlayerNone = false;
            if (w.enemyAbility != EnemyWaveAbility.None) allEnemyNone = false;
        }

        int n = Mathf.Min(3, waves.Length);

        if (allPlayerNone)
        {
            for (int i = 0; i < n; i++)
            {
                if (waves[i] != null)
                    waves[i].playerAbility = (PlayerWaveAbility)(i + 1);
            }
        }

        if (allEnemyNone)
        {
            for (int i = 0; i < n; i++)
            {
                if (waves[i] != null)
                    waves[i].enemyAbility = (EnemyWaveAbility)(i + 1);
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (crowd == null || waves == null || waves.Length == 0)
            return;

        BeginWave(0);
    }

    /// <summary>All enemy leaders eliminated for this wave.</summary>
    public void OnEnemyWaveDefeated()
    {
        _lastClearByDefeatingAllLeaders = true;
        NotifyWaveCleared();
    }

    /// <summary>Wave timer reached zero and the player was not beaten in battle.</summary>
    public void OnWaveSurvived()
    {
        _lastClearByDefeatingAllLeaders = false;
        NotifyWaveCleared();
    }

    /// <summary>Defeat the enemy or survive until time runs out to advance.</summary>
    public void NotifyWaveCleared()
    {
        if (_runComplete || _waveTransitionPending || crowd == null) return;

        _waveTransitionPending = true;
        crowd.MarkGameOver();
        ShowWaveClearedAnnouncement();
        StartCoroutine(CoAdvanceAfterDelay());
    }

    IEnumerator CoAdvanceAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBeforeNextWave);

        _waveIndex++;

        if (_waveIndex >= waves.Length)
        {
            _runComplete = true;
            SceneManager.LoadScene("WinScene");
            yield break;
        }

        _waveTransitionPending = false;
        BeginWave(_waveIndex);
    }

    void ApplyStandardWaveProgression(int index)
    {
        if (!applyStandardWaveProgression || waves == null || index < 0 || index >= waves.Length)
            return;

        WaveZone w = waves[index];
        if (w == null) return;

        w.waveTimeSeconds = 60f;
        w.neutralCount = index switch
        {
            0 => 45,
            1 => 55,
            _ => 65
        };
        w.enemyLeaderCount = index switch
        {
            0 => 3,
            1 => 5,
            _ => 7
        };
        w.enemyFollowerCount = index switch
        {
            0 => 1,
            1 => 1,
            _ => 2
        };
        w.shockwavesPerWave = 2;

        w.enemyMoveSpeedMultiplier = 0.93f;
        w.enemyChaseSpeedMultiplier = 0.88f;

        int abilitySlot = Mathf.Clamp(index + 1, 1, 3);
        w.playerAbility = (PlayerWaveAbility)abilitySlot;
        w.enemyAbility = (EnemyWaveAbility)abilitySlot;
    }

    void BeginWave(int index)
    {
        _waveIndex = index;
        WaveZone w = waves[_waveIndex];

        ApplyStandardWaveProgression(index);

        crowd.ResetRunStateForNewWave();
        crowd.ClearPlayerCrowdForNewWave();

        crowd.playAreaCenter = w.playAreaCenter;
        crowd.spawnRange = w.spawnRange;
        crowd.neutralNPCCount = Mathf.Max(0, w.neutralCount);
        crowd.enemyStartCount = Mathf.Clamp(w.enemyFollowerCount, 1, 2);
        crowd.waveEnemyLeaderCount = Mathf.Max(1, w.enemyLeaderCount);
        crowd.SetRunTimerForWave(w.waveTimeSeconds);

        crowd.RebuildGameplayFloor();

        ApplyPlayerModifiers(w);
        TeleportPlayerAndFollowers(w);
        crowd.DestroyNeutralNPCsInScene();
        crowd.DestroyActiveEnemyCrowd();
        crowd.SpawnNeutralNPCs();
        crowd.SpawnEnemyCrowd();
        crowd.ResnapPlayerCrowdToGround();
        crowd.RefreshPlayerFormation();
        SnapCameraToPlayer();
        crowd.UpdateUI();

        ShowWaveAnnouncement();
    }

    void ShowWaveAnnouncement()
    {
        WaveAnnouncementUI ui = WaveAnnouncementUI.GetOrCreate();
        if (ui == null) return;

        if (crowd != null && crowd.timerText != null)
            ui.SetReferenceFont(crowd.timerText);

        ui.ShowWaveStart(CurrentWaveNumber);
    }

    void ShowWaveClearedAnnouncement()
    {
        WaveAnnouncementUI ui = WaveAnnouncementUI.GetOrCreate();
        if (ui == null) return;

        if (crowd != null && crowd.timerText != null)
            ui.SetReferenceFont(crowd.timerText);

        bool hasNext = _waveIndex + 1 < waves.Length;
        string message = _lastClearByDefeatingAllLeaders
            ? "WAVE CLEARED"
            : "TIME'S UP";

        if (hasNext)
            message += "\n<size=55%>Next district...</size>";

        ui.ShowMessage(message, holdDuration: 1.15f);
    }

    void ApplyPlayerModifiers(WaveZone w)
    {
        if (crowd.player == null) return;

        PlayerController pc = crowd.player.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.waveMoveSpeedMultiplier = w.playerMoveSpeedMultiplier;
        pc.waveShockwaveCooldownMultiplier = w.playerShockwaveCooldownMultiplier;
        pc.maxShockwavesPerWave = w.shockwavesPerWave;
        pc.activeWaveAbility = w.playerAbility;
        pc.ResetWaveAbilityState();
    }

    void TeleportPlayerAndFollowers(WaveZone w)
    {
        Transform spawn = w.playerSpawn != null ? w.playerSpawn : w.playAreaCenter;
        if (spawn == null || crowd.player == null)
            return;

        crowd.PlaceActorAtWaveSpawn(crowd.player, spawn);
    }

    void SnapCameraToPlayer()
    {
        if (crowd == null || crowd.player == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        follow.player = crowd.player;
        follow.SnapToPlayerImmediately();
    }
}

[System.Serializable]
public class WaveZone
{
    [Tooltip("Shown in logs / optional UI hooks.")]
    public string title = "Wave";

    public Transform playerSpawn;
    public Transform playAreaCenter;

    [Min(0)] public int neutralCount = 28;

    [Min(1)] public int enemyLeaderCount = 3;

    [Tooltip("Total enemy followers spawned at wave start (1–2), shared across all enemy leaders. Leaders can recruit more neutrals during play.")]
    [Range(1, 2)] public int enemyFollowerCount = 1;
    public float spawnRange = 45f;
    [Min(1f)] public float waveTimeSeconds = 60f;

    [Tooltip("Shockwave uses per wave for the player and each enemy leader.")]
    [Min(0)] public int shockwavesPerWave = 2;

    [Header("Player modifiers")]
    [Tooltip("Multiplies move speed for this wave.")]
    public float playerMoveSpeedMultiplier = 1f;

    [Tooltip("Multiplies shockwave cooldown (below 1 = faster shockwaves).")]
    public float playerShockwaveCooldownMultiplier = 1f;

    public PlayerWaveAbility playerAbility = PlayerWaveAbility.None;

    [Header("Enemy modifiers")]
    public float enemyMoveSpeedMultiplier = 1f;
    public float enemyChaseSpeedMultiplier = 1f;

    [Tooltip("Below 1 = enemy shockwaves more often.")]
    public float enemyShockwaveCooldownMultiplier = 1f;

    public EnemyWaveAbility enemyAbility = EnemyWaveAbility.None;
}
