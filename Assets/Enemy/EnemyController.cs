using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float minStateTime = 0.5f;
        [SerializeField] private float maxStateTime = 2f;

        [Header("Detection")]
        [SerializeField] private float sightRange = 10f;
        [SerializeField] private float sightAngle = 90f;
        [SerializeField] private float loseTargetTime = 3f;

        [Header("Target")]
        [SerializeField] private Transform player;

        private enum EnemyState
        {
            Idle,
            Walking,
            Running
        }

        private NavMeshAgent agent;
        private EnemyState state;

        private float timeInState;
        private float stateTime;
        private float timeSincePlayerSeen;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            ChangeState();
        }

        private void Update()
        {
            UpdateDetection();

            if (state == EnemyState.Running && player != null)
            {
                agent.SetDestination(player.position);
            }

            timeInState += Time.deltaTime;

            if (state != EnemyState.Running &&
                timeInState >= stateTime)
            {
                ChangeState();
            }
        }

        private void UpdateDetection()
        {
            if (player == null)
                return;

            if (CanSeePlayer())
            {
                timeSincePlayerSeen = 0f;

                if (state != EnemyState.Running)
                    SetState(EnemyState.Running);
            }
            else if (state == EnemyState.Running)
            {
                timeSincePlayerSeen += Time.deltaTime;

                if (timeSincePlayerSeen >= loseTargetTime)
                    ChangeState();
            }
        }

        private bool CanSeePlayer()
        {
            Vector3 direction = player.position - transform.position;
            float distance = direction.magnitude;

            if (distance > sightRange)
                return false;

            direction.y = 0f;

            if (Vector3.Angle(transform.forward, direction) >
                sightAngle * 0.5f)
            {
                return false;
            }

            if (Physics.Raycast(
                    transform.position + Vector3.up,
                    player.position - (transform.position + Vector3.up),
                    out RaycastHit hit,
                    sightRange))
            {
                return hit.transform == player;
            }

            return false;
        }

        private void ChangeState()
        {
            EnemyState newState;

            do
            {
                newState = (EnemyState)Random.Range(0, 2);
            }
            while ((EnemyState)state == newState);

            SetState(newState);
        }

        private void SetState(EnemyState newState)
        {
            state = newState;
            timeInState = 0f;
            stateTime = Random.Range(minStateTime, maxStateTime);

            switch (state)
            {
                case EnemyState.Idle:
                    agent.ResetPath();
                    break;

                case EnemyState.Walking:
                    SetRandomDestination();
                    break;

                case EnemyState.Running:
                    agent.speed = 8f;
                    break;
            }
        }

        private void SetRandomDestination()
        {
            if (NavMesh.SamplePosition(
                    transform.position + Random.insideUnitSphere * 10f,
                    out NavMeshHit hit,
                    10f,
                    NavMesh.AllAreas))
            {
                agent.speed = 5f;
                agent.SetDestination(hit.position);
            }
        }
    }
}