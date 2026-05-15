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
    [Tooltip("Each wave: 60s, escalating enemy followers (6 → 10 → 14), and abilities TurboDash/RallyCry/SurgePulse vs Harrier/StubbornSnap/EchoBlast.")]
    public bool applyStandardWaveProgression = true;

    [Tooltip("If every wave still has None for player or enemy ability, assign distinct ones for the first three waves (TurboDash → RallyCry → SurgePulse, Harrier → StubbornSnap → EchoBlast).")]
    public bool autoDistinctAbilitiesWhenUnset = true;

    [Header("Waves (exactly 3 recommended)")]
    public WaveZone[] waves = new WaveZone[3];

    int _waveIndex;
    bool _runComplete;
    bool _waveTransitionPending;

    public int CurrentWaveNumber => Mathf.Clamp(_waveIndex + 1, 1, Mathf.Max(1, waves != null ? waves.Length : 1));
    public WaveZone CurrentWaveDefinition => waves != null && _waveIndex >= 0 && _waveIndex < waves.Length ? waves[_waveIndex] : null;

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
            crowd.ConfigureCityPopulationFromWaveZones(waves);
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

    /// <summary>Enemy leader eliminated — same outcome as surviving the wave timer.</summary>
    public void OnEnemyWaveDefeated()
    {
        NotifyWaveCleared();
    }

    /// <summary>Wave timer reached zero and the player was not beaten in battle.</summary>
    public void OnWaveSurvived()
    {
        NotifyWaveCleared();
    }

    /// <summary>Defeat the enemy or survive until time runs out to advance.</summary>
    public void NotifyWaveCleared()
    {
        if (_runComplete || _waveTransitionPending || crowd == null) return;

        _waveTransitionPending = true;
        crowd.MarkGameOver();
        StartCoroutine(CoAdvanceAfterDelay());
    }

    IEnumerator CoAdvanceAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeNextWave);

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
        w.enemyFollowerCount = 0;

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

        crowd.playAreaCenter = w.playAreaCenter;
        crowd.spawnRange = w.spawnRange;
        crowd.neutralNPCCount = Mathf.Max(0, w.neutralCount);
        crowd.enemyStartCount = Mathf.Max(0, w.enemyFollowerCount);
        crowd.waveEnemyLeaderCount = Mathf.Max(1, w.enemyLeaderCount);
        crowd.SetRunTimerForWave(w.waveTimeSeconds);

        ApplyPlayerModifiers(w);
        TeleportPlayerAndFollowers(w);
        crowd.PrunePlayerFollowerList();
        crowd.RefreshPlayerFormation();
        crowd.DestroyNeutralNPCsInScene();
        crowd.DestroyActiveEnemyCrowd();
        crowd.RebuildGameplayFloor();
        crowd.SpawnNeutralNPCs();
        crowd.SpawnEnemyCrowd();
        crowd.UpdateUI();

        ShowWaveAnnouncement();
    }

    void ShowWaveAnnouncement()
    {
        WaveAnnouncementUI ui = WaveAnnouncementUI.GetOrCreate();
        if (ui == null) return;

        if (crowd != null && crowd.timerText != null)
            ui.SetReferenceFont(crowd.timerText);

        ui.Show(CurrentWaveNumber);
    }

    void ApplyPlayerModifiers(WaveZone w)
    {
        if (crowd.player == null) return;

        PlayerController pc = crowd.player.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.waveMoveSpeedMultiplier = w.playerMoveSpeedMultiplier;
        pc.waveShockwaveCooldownMultiplier = w.playerShockwaveCooldownMultiplier;
        pc.activeWaveAbility = w.playerAbility;
        pc.ResetWaveAbilityState();
    }

    void TeleportPlayerAndFollowers(WaveZone w)
    {
        Transform spawn = w.playerSpawn != null ? w.playerSpawn : crowd.playAreaCenter;
        if (spawn == null) return;

        Quaternion rot = spawn.rotation;
        Vector3 p = new Vector3(spawn.position.x, 0f, spawn.position.z);

        Rigidbody prb = crowd.player.GetComponent<Rigidbody>();
        if (prb != null)
        {
            prb.position = p;
            prb.rotation = rot;
        }
        else
        {
            crowd.player.SetPositionAndRotation(p, rot);
        }

        crowd.SnapRigidbodyToWalkable(crowd.player);

        crowd.PrunePlayerFollowerList();

        for (int i = 0; i < crowd.playerFollowers.Count; i++)
        {
            GameObject go = crowd.playerFollowers[i];
            if (go == null) continue;

            Vector3 off = new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
            Vector3 t = crowd.player.position + off;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.position = t;
            else
                go.transform.position = t;

            crowd.SnapRigidbodyToWalkable(go.transform);

            FollowerUnit unit = go.GetComponent<FollowerUnit>();
            if (unit != null)
                unit.SetFollower(FollowerUnit.CrowdTeam.Player, crowd.player, i);
        }

        crowd.RefreshPlayerFormation();
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

    [Min(0)] public int enemyFollowerCount = 0;
    public float spawnRange = 45f;
    [Min(1f)] public float waveTimeSeconds = 60f;

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
