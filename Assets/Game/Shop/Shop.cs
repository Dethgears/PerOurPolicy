using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Shop
{
    public class Shop : MonoBehaviour
    {
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
            shopEconomy = GameManager.Instance.GetComponentInChildren<ShopEconomy>();

            if (shopEconomy == null)
            {
                Debug.LogError("Shop: Could not find ShopEconomy.");
            }
            
            SetText(deathsText,$"Deaths ({GameManager.Instance.roundDeaths.ToString()}):");
            SetText(deathsCost,$"-${(GameManager.Instance.roundDeaths*50).ToString()}");
            
            var money = GameManager.Instance.money;
            SetText(balance,FormattedBalance(money));
            balance.color = money >= 0 ? Color.green : Color.red;
            
            SetText(quota,$"-${GameManager.Instance.quota.ToString()}");
            UpdateNewBalance();

            UpdateValue();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<Pickup.Pickup>(out var pickup))
                return;

            if (currentItems.Contains(pickup))
                return;

            currentItems.Add(pickup);
            UpdateValue();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent<Pickup.Pickup>(out var pickup))
                return;

            if (!currentItems.Remove(pickup))
                return;

            UpdateValue();
        }

        private void UpdateValue()
        {
            if (shopEconomy == null)
                return;

            currentValue = 0;

            foreach (var pickup in currentItems)
            {
                if (pickup != null)
                    currentValue += Mathf.RoundToInt(shopEconomy.GetPrice(pickup));
            }

            SetText(itemsWorth,$"+${currentValue.ToString()}");
            UpdateNewBalance();
        }

        public void OnButtonPressed()
        {
            if (shopEconomy == null)
                return;
            
            GameManager.Instance.money += currentValue;
            SetText(balance,GameManager.Instance.money.ToString());

            // Sell everything currently in the shop.
            foreach (var pickup in currentItems)
            {
                if (pickup == null)
                    continue;

                shopEconomy.RecordSale(pickup);

                pickup.gameObject.SetActive(false);
            }

            currentItems.Clear();
            currentValue = 0;

            UpdateValue();
        }

        private void UpdateNewBalance()
        {
            var money = GameManager.Instance.money;
            money -= GameManager.Instance.quota;
            money += currentValue;
            
            SetText(newBalance,FormattedBalance(money));
            newBalance.color = money >= 0 ? Color.green : Color.red;
        }

        private string FormattedBalance(int value)
        {
            return value >= 0 ? $"${value.ToString()}" : $"-${-value}";
        }
        
        private void SetText(TMP_Text obj, string text)
        {
            if (obj != null) obj.text = text;
        }
    }
}