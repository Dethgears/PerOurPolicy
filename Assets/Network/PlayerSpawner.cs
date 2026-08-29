using Core.Events;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Network
{
    /// <summary>
    /// PURPOSE: Spawns each connected client's player object at a distinct, preset spawn
    ///          point (a child of this component's transform). 
    /// DEPENDENCIES: NetworkManager's own "Player Prefab" field must be left EMPTY, and
    ///               Connection Approval left off. 
    /// PUBLIC API: none - runs automatically once the scene loads.
    /// </summary>
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        
        private void Start()
        {
            if (NetworkManager.Singleton == null || !IsServer)
                return;

            SpawnAllConnectedPlayers();

            // Covers a client connecting after this scene is already loaded
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
        }

        public override void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }

        private void SpawnAllConnectedPlayers()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                SpawnPlayer(client.ClientId);
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] No player prefab assigned.");
                return;
            }

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                return; // already has one

            Transform spawnPoint = GetSpawnPointFor(clientId);
            NetworkObject instance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            instance.SpawnAsPlayerObject(clientId);
        }

        /// <summary>Assigns spawn points by connection order, wrapping around if there are more players than points.</summary>
        private Transform GetSpawnPointFor(ulong clientId)
        {
            int childCount = transform.childCount;
            if (childCount == 0)
            {
                Debug.LogWarning("[PlayerSpawner] No spawn points assigned - spawning at the origin.");
                return transform;
            }

            int index = 0;
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id == clientId) break;
                index++;
            }

            return transform.GetChild(index % childCount);
        }
    }
}