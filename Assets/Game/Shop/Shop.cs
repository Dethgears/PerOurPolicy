using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shop
{
    public class Shop : NetworkBehaviour
    {
        [Header("UI")] 
        [SerializeField] private TMP_Text deathsText;
        [SerializeField] private TMP_Text deathsCost;
        [SerializeField] private TMP_Text balance;
        [SerializeField] private TMP_Text quota;
        [SerializeField] private TMP_Text itemsWorth;
        [SerializeField] private TMP_Text newBalance;

        private ShopEconomy shopEconomy;

        private readonly List<Pickup.Pickup> currentItems = new();

        private int currentValue;

        private void Start()
        { 
            if (!IsServer)
                return;

            shopEconomy = GameManager.Instance.GetComponentInChildren<ShopEconomy>();

            if (shopEconomy == null)
            {
                Debug.LogError("Shop: Could not find ShopEconomy.");
                return;
            }

            UpdateShopUI();
            UpdateValue();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
                return;

            if (!other.TryGetComponent<Pickup.Pickup>(out var pickup))
                return;

            if (currentItems.Contains(pickup))
                return;

            currentItems.Add(pickup);

            UpdateValue();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer)
                return;

            if (!other.TryGetComponent<Pickup.Pickup>(out var pickup))
                return;

            if (!currentItems.Remove(pickup))
                return;

            UpdateValue();
        }
        
        private void UpdateValue()
        {
            if (!IsServer)
                return;

            if (shopEconomy == null)
                return;

            currentValue = 0;

            foreach (var pickup in currentItems)
            {
                if (pickup != null)
                {
                    currentValue += Mathf.RoundToInt(shopEconomy.GetPrice(pickup));
                }
            }

            UpdateShopUI();
        }
        
        public void OnButtonPressed()
        {
            SellItemsServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void SellItemsServerRpc()
        {
            if (shopEconomy == null)
                return;
            
            GameManager.Instance.money += currentValue;
            
            foreach (var pickup in currentItems)
            {
                if (pickup == null)
                    continue;

                shopEconomy.RecordSale(pickup);
                pickup.GetComponent<NetworkObject>().Despawn(true); 
            }

            currentItems.Clear();
            currentValue = 0;

            UpdateShopUI();
        }
        
        private void UpdateShopUI()
        {
            if (!IsServer)
                return;

            int deaths = GameManager.Instance.roundDeaths;
            int money = GameManager.Instance.money;
            int quotaValue = GameManager.Instance.quota;
            int itemValue = currentValue;

            int projectedBalance =
                money - quotaValue + itemValue;

            UpdateShopUIClientRpc(
                deaths,
                money,
                quotaValue,
                itemValue,
                projectedBalance);
        }
        
        [Rpc(SendTo.Everyone)]
        private void UpdateShopUIClientRpc(
            int deaths,
            int money,
            int quotaValue,
            int itemValue,
            int projectedBalance)
        {
            if (deathsText != null)
                deathsText.text = $"Deaths ({deaths}):";

            if (deathsCost != null)
                deathsCost.text = $"-${deaths * 50}";

            if (balance != null)
            {
                balance.text = FormattedBalance(money);
                balance.color =
                    money >= 0 ? Color.green : Color.red;
            }

            if (quota != null)
                quota.text = $"-${quotaValue}";

            if (itemsWorth != null)
                itemsWorth.text = $"+${itemValue}";

            if (newBalance != null)
            {
                newBalance.text =
                    FormattedBalance(projectedBalance);

                newBalance.color =
                    projectedBalance >= 0
                        ? Color.green
                        : Color.red;
            }
        }

        private string FormattedBalance(int value)
        {
            return value >= 0
                ? $"${value}"
                : $"-${-value}";
        }
    }
}