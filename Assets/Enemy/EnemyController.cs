using Core.Events;
using Network;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Enemy
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float minStateTime = 0.5f;
        [SerializeField] private float maxStateTime = 2f;

        [Header("Detection")]
        [SerializeField] private float sightRange = 100f;
        [SerializeField] private float sightAngle = 90f;
        [SerializeField] private float loseTargetTime = 3f;
        
        private GameObject[] players = {};
        private Transform target;
        
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
            UpdatePlayers();
            Component.FindAnyObjectByType<PlayerSpawner>().OnPlayerSpawned.AddListener(UpdatePlayers);
        }
        
        private void OnDestroy()
        {
            if (NetworkSessionManager.Instance != null)
            {
                FindAnyObjectByType<PlayerSpawner>().OnPlayerSpawned.RemoveListener(UpdatePlayers);
            }
        }

        private void Update()
        {
            UpdateDetection();
            
            if (state == EnemyState.Running && target != null)
            {
                agent.SetDestination(target.position);
            }

            timeInState += Time.deltaTime;
            
            switch (state)
            {
                case EnemyState.Idle:
                    if (timeInState >= stateTime/4) ChangeState();
                    break;
                case EnemyState.Walking:
                    if (timeInState >= stateTime) ChangeState();
                    break;
            }
        }

        private void UpdateDetection()
        {
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
            var foundPlayers = new System.Collections.Generic.List<Transform>();
            
            if (players.Length == 0) return false;

            foreach (var player in players)
            {
                Vector3 direction = player.transform.position - transform.position;
                float distance = direction.magnitude;

                if (distance > sightRange)
                    continue;

                direction.y = 0f;

                if (Vector3.Angle(transform.forward, direction) > sightAngle * 0.5f)
                    continue;

                Vector3 rayOrigin = transform.position + Vector3.up;
                Vector3 rayDirection = player.transform.position - rayOrigin;

                if (Physics.Raycast(
                        rayOrigin,
                        rayDirection,
                        out RaycastHit hit,
                        sightRange))
                {
                    if (hit.transform == player.transform)
                    {
                        foundPlayers.Add(player.transform);
                    }
                }
            }

            if (foundPlayers.Count == 0)
                return false;

            if (foundPlayers.Contains(target))
                return true;

            target = foundPlayers[Random.Range(0, foundPlayers.Count)];
            return true;
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

        public void UpdatePlayers()
        {
            players = GameObject.FindGameObjectsWithTag("Player");
        }
    }
}