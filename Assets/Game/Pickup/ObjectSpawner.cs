using System.Collections.Generic;
using UnityEngine;

namespace Game.Pickup
{
    public class ObjectSpawner : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            List<Transform> spawns = new List<Transform>();
            spawns.AddRange(gameObject.GetComponentsInChildren<Transform>());
            var numObjects = Random.Range(3, spawns.Count/2);
            GameObject[] objectPrefabs = Resources.LoadAll<GameObject>("Items");
            
            for (int i = 0; i < numObjects; i++)
            {
                var randSpawn = spawns[Random.Range(0, spawns.Count)];
                Instantiate(objectPrefabs[Random.Range(0, objectPrefabs.Length)], randSpawn.position, randSpawn.rotation);
                spawns.Remove(randSpawn);
            }
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
