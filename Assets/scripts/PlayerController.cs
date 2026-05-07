using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public Animator animator; // 👈 DRAG YOUR HUMAN MODEL HERE

    [Header("Shockwave")]
    public float shockwaveRadius = 8f;
    public float shockwaveForce = 12f;
    public float shockwaveCooldown = 5f;
    public GameObject shockwavePrefab;
    public TextMeshProUGUI shockwaveCooldownText;

    private Rigidbody rb;
    private float shockwaveTimer = 0f;
    private Vector3 moveDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
    }

    void Update()
    {
        // 🔹 Movement input
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        moveDir = new Vector3(h, 0f, v).normalized;

        // 🔹 Animation control
        if (animator != null)
        {
            bool isMoving = moveDir.magnitude > 0.1f;
            animator.SetBool("isMoving", isMoving);
        }

        // 🔹 Shockwave cooldown UI
        UpdateCooldownUI();

        // 🔹 Activate shockwave
        if (Input.GetKeyDown(KeyCode.E) && shockwaveTimer <= 0f)
        {
            ActivateShockwave();
            shockwaveTimer = shockwaveCooldown;
        }
    }

    void FixedUpdate()
    {
        // 🔹 Move player
        rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);

        // 🔹 Rotate player toward movement
        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
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

    void ActivateShockwave()
    {
        if (shockwavePrefab != null)
        {
            Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius);

        foreach (Collider hit in hits)
        {
            EnemyLeader enemy = hit.GetComponent<EnemyLeader>();
            FollowerUnit follower = hit.GetComponent<FollowerUnit>();

            // Push enemies
            if (enemy != null)
            {
                PushObject(enemy.transform);
            }

            // Push enemy followers ONLY (no conversion)
            if (follower != null && follower.team == FollowerUnit.CrowdTeam.Enemy)
            {
                PushObject(follower.transform);
            }
        }
    }

    void PushObject(Transform target)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir != Vector3.zero)
        {
            target.position += dir.normalized * shockwaveForce;
        }
    }
}