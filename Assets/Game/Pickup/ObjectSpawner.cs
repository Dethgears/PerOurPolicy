using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Pickup
{
    public class ObjectSpawner : NetworkBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public override void OnNetworkSpawn()
        {
            SpawnObjectsServerRpc();
        }

        [ServerRpc]
        private void SpawnObjectsServerRpc()
        {
            List<Transform> spawns = new List<Transform>();
            spawns.AddRange(gameObject.GetComponentsInChildren<Transform>());
            spawns.RemoveAt(0); // Don't include this
            var numObjects = Random.Range(3, spawns.Count/2);
            GameObject[] objectPrefabs = Resources.LoadAll<GameObject>("Items");
            
            for (int i = 0; i < numObjects; i++)
            {
                var randSpawn = spawns[Random.Range(0, spawns.Count)];
                var obj = Instantiate(objectPrefabs[Random.Range(0, objectPrefabs.Length)], randSpawn.position, randSpawn.rotation);
                obj.GetComponent<NetworkObject>().Spawn();
                spawns.Remove(randSpawn);
            }
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
