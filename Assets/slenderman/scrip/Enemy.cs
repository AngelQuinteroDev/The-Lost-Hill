using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Enemy : MonoBehaviour
{
    [Header("Navegación")]
    public NavMeshAgent agent;
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 7f;

    [Header("Waypoints")]
    public string waypointTag = "Waypoint";
    private Transform[] waypoints;
    private int lastWaypointIndex = -1;

    [Header("Pausa en waypoints")]
    public float waitTimeAtWaypoint = 2f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    [Header("Detección")]
    public float alertRadius = 15f;
    public float chaseRadius = 10f;
    public float loseRadius = 20f;

    [Header("Captura")]
    public float catchRadius = 1.2f;

    private Transform targetPlayer;
    private Animator anim;

    private static readonly int AnimWalk = Animator.StringToHash("isWalking");
    private static readonly int AnimChase = Animator.StringToHash("isChasing");
    private static readonly int AnimIdle = Animator.StringToHash("isIdle");

    private enum State { Patrolling, WaitingAtWaypoint, Alert, Chasing }
    private State currentState = State.Patrolling;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        GameObject[] objs = GameObject.FindGameObjectsWithTag(waypointTag);
        waypoints = new Transform[objs.Length];
        for (int i = 0; i < objs.Length; i++)
            waypoints[i] = objs[i].transform;

        if (waypoints.Length == 0)
            Debug.LogWarning("[Enemy] No se encontraron waypoints con tag: " + waypointTag);

        GoToRandomWaypoint();
        SetState(State.Patrolling);
    }

    void Update()
    {
        Transform detected = FindNearest("Player", alertRadius);

        switch (currentState)
        {
            case State.Patrolling:
            case State.WaitingAtWaypoint:
                HandlePatrol();
                if (detected != null)
                {
                    targetPlayer = detected;
                    SetState(State.Alert);
                }
                break;

            case State.Alert:
                HandleAlert();
                break;

            case State.Chasing:
                HandleChase();
                break;
        }
    }

    void HandlePatrol()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                GoToRandomWaypoint();
                SetState(State.Patrolling);
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            waitTimer = waitTimeAtWaypoint;
            agent.ResetPath();
            SetState(State.WaitingAtWaypoint);
        }
    }

    void HandleAlert()
    {
        if (targetPlayer == null) { ReturnToPatrol(); return; }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        if (dist > loseRadius) { targetPlayer = null; ReturnToPatrol(); return; }

        if (dist <= chaseRadius) { SetState(State.Chasing); return; }

        agent.ResetPath();
        FaceTarget(targetPlayer.position);
    }

    void HandleChase()
    {
        if (targetPlayer == null) { ReturnToPatrol(); return; }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        if (dist > loseRadius) { targetPlayer = null; ReturnToPatrol(); return; }

        if (dist > chaseRadius) { agent.ResetPath(); SetState(State.Alert); return; }

        agent.SetDestination(targetPlayer.position);

        if (dist < catchRadius)
        {
            PlayerController player = targetPlayer.GetComponent<PlayerController>();
            if (player != null) player.OnCaughtByEnemy();
            targetPlayer = null;
            ReturnToPatrol();
        }
    }

    void SetState(State newState)
    {
        currentState = newState;

        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimChase, false);
        anim.SetBool(AnimIdle, false);

        switch (newState)
        {
            case State.Patrolling:
                agent.speed = patrolSpeed;
                anim.SetBool(AnimWalk, true);
                break;
            case State.WaitingAtWaypoint:
            case State.Alert:
                agent.speed = patrolSpeed;
                anim.SetBool(AnimIdle, true);
                break;
            case State.Chasing:
                agent.speed = chaseSpeed;
                anim.SetBool(AnimChase, true);
                break;
        }
    }

    void ReturnToPatrol()
    {
        isWaiting = false;
        GoToRandomWaypoint();
        SetState(State.Patrolling);
    }

    void GoToRandomWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        int index;
        do { index = Random.Range(0, waypoints.Length); }
        while (waypoints.Length > 1 && index == lastWaypointIndex);

        lastWaypointIndex = index;
        agent.SetDestination(waypoints[index].position);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 5f);
    }

    Transform FindNearest(string tag, float radius)
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
        Transform nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (GameObject obj in objs)
        {
            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist <= radius && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = obj.transform;
            }
        }
        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
    }
}