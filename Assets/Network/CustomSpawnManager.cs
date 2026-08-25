using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomSpawnManager : MonoBehaviour {
    public GameObject playerPrefab;

    private void Awake() {
        if (!NetworkManager.Singleton.IsServer) return;
        
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
    }

    private void OnSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode) {
        // Find available spawn points
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawn");
        
        if (spawnPoints.Length > 0) {
            // Example: Spawn at first available point
            Transform spawnTransform = spawnPoints[0].transform;
            
            // Instantiate player at custom position
            GameObject player = Instantiate(playerPrefab, spawnTransform.position, spawnTransform.rotation);
            
            // Assign ownership to the client
            player.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        }
    }
}