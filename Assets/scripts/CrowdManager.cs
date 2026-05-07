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

    [Header("Colors")]
    public Color neutralColor = Color.gray;
    public Color playerColor = Color.cyan;
    public Color enemyColor = Color.red;

    public List<GameObject> playerFollowers = new List<GameObject>();
    public List<GameObject> enemyFollowers = new List<GameObject>();

    [HideInInspector] public bool battleLocked = false;

    private EnemyLeader enemyLeader;
    private float timeLeft;
    private bool gameOver;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        timeLeft = gameTime;
        SpawnNeutralNPCs();
        SpawnEnemyCrowd();
        UpdateUI();
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

    void SpawnNeutralNPCs()
    {
        for (int i = 0; i < neutralNPCCount; i++)
        {
            GameObject npc = Instantiate(followerPrefab, GetRandomPosition(), Quaternion.identity);

            FollowerUnit unit = npc.GetComponent<FollowerUnit>();
            if (unit == null)
                unit = npc.AddComponent<FollowerUnit>();

            unit.SetNeutral();
            SetColor(npc, neutralColor);
            SetupFollowerPhysics(npc);
        }
    }

    void SpawnEnemyCrowd()
    {
        if (enemyLeaderPrefab == null) return;

        enemyFollowers.Clear();

        GameObject enemyObj = Instantiate(enemyLeaderPrefab, GetRandomPosition(), Quaternion.identity);

        enemyLeader = enemyObj.GetComponent<EnemyLeader>();
        if (enemyLeader == null)
            enemyLeader = enemyObj.AddComponent<EnemyLeader>();

        enemyLeader.wanderRange = spawnRange;
        SetupLeaderPhysics(enemyObj);

        for (int i = 0; i < enemyStartCount; i++)
        {
            Vector3 pos = enemyObj.transform.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            GameObject follower = Instantiate(followerPrefab, pos, Quaternion.identity);

            FollowerUnit unit = follower.GetComponent<FollowerUnit>();
            if (unit == null)
                unit = follower.AddComponent<FollowerUnit>();

            enemyFollowers.Add(follower);
            unit.SetFollower(FollowerUnit.CrowdTeam.Enemy, enemyLeader.transform, enemyFollowers.Count - 1);

            SetColor(follower, enemyColor);
            SetupFollowerPhysics(follower);
        }

        RefreshEnemyFormation();
        UpdateUI();
    }

    Vector3 GetRandomPosition()
    {
        float x = Random.Range(-spawnRange, spawnRange);
        float z = Random.Range(-spawnRange, spawnRange);

        return new Vector3(x, 1f, z);
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

        SetColor(obj, playerColor);
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

        SetColor(obj, enemyColor);
        SetupFollowerPhysics(obj);

        RefreshPlayerFormation();
        RefreshEnemyFormation();
        UpdateUI();
    }

    public void ResolveBattle()
    {
        if (battleLocked || gameOver) return;

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

            Invoke(nameof(SpawnEnemyCrowd), 3f);
        }
        else
        {
            gameOver = true;
            SceneManager.LoadScene("LoseScene");
        }

        UpdateUI();
    }

    void RefreshPlayerFormation()
    {
        for (int i = 0; i < playerFollowers.Count; i++)
        {
            if (playerFollowers[i] == null) continue;

            FollowerUnit unit = playerFollowers[i].GetComponent<FollowerUnit>();

            if (unit != null)
            {
                unit.SetFollower(FollowerUnit.CrowdTeam.Player, player, i);
            }
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
            {
                unit.SetFollower(FollowerUnit.CrowdTeam.Enemy, enemyLeader.transform, i);
            }
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

    void UpdateUI()
    {
        if (playerCrowdText != null)
            playerCrowdText.text = "PLAYER CROWD: " + (playerFollowers.Count + 1);

        if (enemyCrowdText != null)
            enemyCrowdText.text = "ENEMY CROWD: " + (enemyFollowers.Count + 1);

        if (timerText != null)
            timerText.text = "TIME LEFT: " + Mathf.CeilToInt(timeLeft);
    }

    void SetColor(GameObject obj, Color color)
    {
        Renderer r = obj.GetComponent<Renderer>();

        if (r != null)
            r.material.color = color;
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
    }
}