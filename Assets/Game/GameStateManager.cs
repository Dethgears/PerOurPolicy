using Core.Events;
using Menu;
using Unity.Netcode;
using UnityEngine;

namespace Game
{
    public class GameStateManager : NetworkBehaviour
    {
        [SerializeField] private GameEvent onBeginTeleport;
    
        private NetworkVariable<float> teleportCountdown = new NetworkVariable<float>();
        private bool isCountdownActive = false;
        private int countdownDisplayValue;
        
        public int numAlivePlayers;

        public override void OnNetworkSpawn()
        {
            numAlivePlayers = GameObject.FindGameObjectsWithTag("Player").Length;
        }

        // Update is called once per frame
        void Update()
        {
            if (IsServer)
            {
                if (!isCountdownActive) return;
        
                teleportCountdown.Value -= Time.deltaTime;

                if (teleportCountdown.Value <= 0f)
                {
                    isCountdownActive = false;
                    countdownDisplayValue = 0;
                    onBeginTeleport.Raise(); 
                }
            }

            if (countdownDisplayValue != Mathf.CeilToInt(teleportCountdown.Value))
            {
                if (teleportCountdown.Value <= 0f)
                {
                    MenuManager.Instance.SetStatusText("");
                }
                else
                {
                    countdownDisplayValue = Mathf.CeilToInt(teleportCountdown.Value);
                    MenuManager.Instance.SetStatusText("Warning: Teleporter is being activated.\n" +
                                                       $"Survivors will be stranded in {countdownDisplayValue.ToString()}s");
                }
            }
        }
    
        [Rpc(SendTo.Server)]
        public void OnTeleporterActivatedServerRpc()
        { 
            if (isCountdownActive) return;
            teleportCountdown.Value = 120f;
            isCountdownActive = true;
        }
    
        [Rpc(SendTo.Server)]
        public void OnExitFacilityServerRpc()
        {
            isCountdownActive = false;
            GameManager.Instance.money -= GameManager.Instance.roundDeaths*50;
        }
    
        [Rpc(SendTo.Server)]
        public void OnExitShopServerRpc()
        {
            isCountdownActive = false;
            GameManager.Instance.money -= GameManager.Instance.quota;
            GameManager.Instance.quota = Mathf.RoundToInt(GameManager.Instance.quota*1.5f); 
            GameManager.Instance.roundDeaths = 0;
        }

        [Rpc(SendTo.Server)]
        public void OnPlayerSpawnedServerRpc()
        {
            numAlivePlayers++;
        }

        [Rpc(SendTo.Server)]
        public void OnPlayerDiedServerRpc()
        {
            numAlivePlayers--;
            GameManager.Instance.roundDeaths++;
        }
    }
}
