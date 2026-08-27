using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Ship
{
    public class Ship : MonoBehaviour
    {
        private List<Transform> carriedItems = new List<Transform>();
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            DontDestroyOnLoad(this);
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    
        private void OnCollisionEnter(Collision collision)
        {
            var hit = collision.gameObject;
            if (hit.TryGetComponent(out Pickup.Pickup pickup))
            {
                carriedItems.Add(pickup.transform);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            var hit = collision.gameObject;
            if (hit.TryGetComponent(out Pickup.Pickup pickup))
            {
                carriedItems.Remove(pickup.transform);
            }
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            foreach (var item in carriedItems)
            {
                item.parent = null;
            }
        }

        public void OnButtonPressed()
        {
            // todo: progress the game here
            
            foreach (var item in carriedItems)
            {
                item.parent = transform; // To survive scene transition
            }
            
            var activeScene = SceneManager.GetActiveScene();
            switch (activeScene.name)
            {
                case "Facility":
                    NetworkManager.Singleton.SceneManager.LoadScene("Shop", LoadSceneMode.Single);
                    break;
                case "Shop":
                    NetworkManager.Singleton.SceneManager.LoadScene("Facility", LoadSceneMode.Single);
                    break;
            }
        }
    }
}