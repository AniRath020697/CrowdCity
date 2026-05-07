using UnityEngine;
using System.Collections;

public class EnemyLeader : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float chaseSpeed = 4f;
    public float wanderRange = 40f;
    public float chaseDistance = 35f;

    [Header("Animation")]
    public Animator animator;

    [Header("Enemy Shockwave")]
    public float shockwaveRadius = 8f;
    public float shockwaveForce = 10f;
    public float shockwaveCooldown = 6f;
    public GameObject shockwavePrefab;

    private Vector3 targetPos;
    private Transform player;
    private float shockwaveTimer = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (CrowdManager.Instance != null && CrowdManager.Instance.player != null)
            player = CrowdManager.Instance.player;

        lastPosition = transform.position;
        SetNewTarget();
    }

    void Update()
    {
        shockwaveTimer -= Time.deltaTime;

        if (CrowdManager.Instance == null) return;

        bool playerIsStronger =
            CrowdManager.Instance.playerFollowers.Count + 1 >
            CrowdManager.Instance.enemyFollowers.Count + 1;

        bool enemyIsStronger =
            CrowdManager.Instance.enemyFollowers.Count + 1 >
            CrowdManager.Instance.playerFollowers.Count + 1;

        if (playerIsStronger && player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= shockwaveRadius && shockwaveTimer <= 0f)
            {
                ActivateEnemyShockwave();
                shockwaveTimer = shockwaveCooldown;
            }
        }

        if (enemyIsStronger && player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= chaseDistance)
            {
                ChasePlayer();
                UpdateAnimation();
                return;
            }
        }

        Wander();
        UpdateAnimation();
    }

    void UpdateAnimation()
    {
        if (animator == null) return;

        float movementAmount = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        bool isMoving = movementAmount > 0.05f;

        animator.SetBool("isMoving", isMoving);

        lastPosition = transform.position;
    }

    void ActivateEnemyShockwave()
    {
        if (CrowdManager.Instance != null)
            StartCoroutine(ShockwaveBattleLock());

        if (shockwavePrefab != null)
            Instantiate(shockwavePrefab, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius);

        foreach (Collider hit in hits)
        {
            PlayerController playerController = hit.GetComponent<PlayerController>();

            if (playerController != null)
                PushObject(playerController.transform);

            FollowerUnit unit = hit.GetComponent<FollowerUnit>();

            if (unit != null && unit.team == FollowerUnit.CrowdTeam.Player)
                PushObject(unit.transform);
        }
    }

    IEnumerator ShockwaveBattleLock()
    {
        CrowdManager.Instance.battleLocked = true;
        yield return new WaitForSeconds(1.5f);
        CrowdManager.Instance.battleLocked = false;
    }

    void PushObject(Transform target)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir != Vector3.zero)
            target.position += dir.normalized * shockwaveForce;
    }

    void ChasePlayer()
    {
        Vector3 playerPos = player.position;
        playerPos.y = transform.position.y;

        MoveTo(playerPos, chaseSpeed);
    }

    void Wander()
    {
        MoveTo(targetPos, moveSpeed);

        if (Vector3.Distance(transform.position, targetPos) < 1f)
            SetNewTarget();
    }

    void MoveTo(Vector3 destination, float speed)
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );

        Vector3 dir = destination - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void SetNewTarget()
    {
        float x = Random.Range(-wanderRange, wanderRange);
        float z = Random.Range(-wanderRange, wanderRange);

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