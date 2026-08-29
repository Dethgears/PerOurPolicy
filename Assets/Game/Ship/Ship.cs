using System.Collections.Generic;
using Core.Events;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Ship
{
    public class Ship : NetworkBehaviour
    {
        [Header("Events")]
        [SerializeField] private GameEvent onTeleporterActivated;
        [SerializeField] private GameEvent onExitFacility;
        [SerializeField] private GameEvent onExitShop;
        
        private List<Transform> carriedItems = new List<Transform>();
        private List<Transform> carriedPlayers = new List<Transform>();
        
        private bool isWaitingToTeleport;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            DontDestroyOnLoad(this);
            
            if (!IsServer) return;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    
        private void OnTriggerEnter(Collider collision)
        {
            if (!IsServer) return;
            var hit = collision.gameObject;
            if (hit.TryGetComponent(out Pickup.Pickup pickup))
            {
                carriedItems.Add(pickup.transform);
            }

            if (hit.CompareTag("Player"))
            {
                carriedPlayers.Add(hit.transform);
                TryTeleportNow();
            }
        }

        private void OnTriggerExit(Collider collision)
        {
            if (!IsServer) return;
            var hit = collision.gameObject;
            if (hit.TryGetComponent(out Pickup.Pickup pickup))
            {
                carriedItems.Remove(pickup.transform);
            }
            
            if (hit.CompareTag("Player"))
            {
                carriedPlayers.Remove(hit.transform);
            }
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;
            foreach (var item in carriedItems)
            {
                item.parent = null;
            }
        }
        
        public void OnButtonPressed()
        {
            if (!IsServer) return;
            onTeleporterActivated.Raise();
            isWaitingToTeleport = true;
            TryTeleportNow();
        }

        private void TryTeleportNow()
        {
            if (!IsServer) return;
            var state = GameManager.Instance.GetComponent<GameStateManager>();

            Debug.Log("Alive players: " + state.numAlivePlayers + ", In Teleporter: " + carriedPlayers.Count);

            if (carriedPlayers.Count == state.numAlivePlayers && isWaitingToTeleport)
            {
                OnBeginTeleport();
            }
        }

        public void OnBeginTeleport()
        {
            if (!IsServer) return;
            
            foreach (var item in carriedItems)
            {
                item.parent = transform; // To survive scene transition
            }
            
            var activeScene = SceneManager.GetActiveScene();
            switch (activeScene.name)
            {
                case "Facility":
                    onExitFacility.Raise();
                    NetworkManager.Singleton.SceneManager.LoadScene("Shop", LoadSceneMode.Single);
                    break;
                case "Shop":
                    onExitShop.Raise();
                    NetworkManager.Singleton.SceneManager.LoadScene("Facility", LoadSceneMode.Single);
                    break;
            }
            
            isWaitingToTeleport = false;
        }
    }
}