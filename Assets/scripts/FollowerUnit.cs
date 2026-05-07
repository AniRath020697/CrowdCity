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
    public float blobRandomness = 0.45f;
    public float blobWaveAmount = 0.25f;
    public float blobWaveSpeed = 2.5f;

    [Header("Neutral Roaming")]
    public float roamSpeed = 2.5f;
    public float roamRange = 35f;
    public float roamChangeTime = 2f;

    private Transform leader;
    private int indexInCrowd;

    private Vector3 roamTarget;
    private float roamTimer;

    private float randomOffsetX;
    private float randomOffsetZ;
    private float randomPhase;

    void Start()
    {
        MakeRandomOffset();
        SetNewRoamTarget();
    }

    public void SetNeutral()
    {
        team = CrowdTeam.Neutral;
        leader = null;
        indexInCrowd = 0;
        SetNewRoamTarget();
    }

    public void SetFollower(CrowdTeam newTeam, Transform newLeader, int index)
    {
        team = newTeam;
        leader = newLeader;
        indexInCrowd = index;
        MakeRandomOffset();
    }

    void Update()
    {
        if (team == CrowdTeam.Neutral)
        {
            Roam();
            return;
        }

        if (leader == null) return;

        Vector3 targetPos = GetBackBlobPosition();
        targetPos.y = transform.position.y;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            followSpeed * Time.deltaTime
        );

        Vector3 dir = targetPos - transform.position;

        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(dir.normalized),
                8f * Time.deltaTime
            );
        }
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

        Vector3 right = leader.right;
        Vector3 back = -leader.forward;

        return leader.position + right * xOffset + back * Mathf.Abs(zOffset);
    }

    void MakeRandomOffset()
    {
        randomOffsetX = Random.Range(-blobRandomness, blobRandomness);
        randomOffsetZ = Random.Range(-blobRandomness, blobRandomness);
        randomPhase = Random.Range(0f, 10f);
    }

    void Roam()
    {
        roamTimer += Time.deltaTime;

        if (roamTimer >= roamChangeTime || Vector3.Distance(transform.position, roamTarget) < 1f)
        {
            SetNewRoamTarget();
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            roamTarget,
            roamSpeed * Time.deltaTime
        );

        Vector3 dir = roamTarget - transform.position;

        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(dir.normalized),
                5f * Time.deltaTime
            );
        }
    }

    void SetNewRoamTarget()
    {
        roamTimer = 0f;

        float x = Random.Range(-roamRange, roamRange);
        float z = Random.Range(-roamRange, roamRange);

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
                CrowdManager.Instance.ResolveBattle();
                return;
            }

            if (otherUnit != null && otherUnit.team == CrowdTeam.Enemy)
            {
                CrowdManager.Instance.ResolveBattle();
                return;
            }
        }

        if (team == CrowdTeam.Enemy)
        {
            if (player != null)
            {
                CrowdManager.Instance.ResolveBattle();
                return;
            }

            if (otherUnit != null && otherUnit.team == CrowdTeam.Player)
            {
                CrowdManager.Instance.ResolveBattle();
                return;
            }
        }
    }
}