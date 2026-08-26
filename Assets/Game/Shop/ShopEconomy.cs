using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Shop
{
    public class ShopEconomy : MonoBehaviour
    {
        [Header("Economy")]
        [SerializeField] private int baseSupply = 100;

        [Tooltip("Percentage of the difference from base supply recovered each scene transition.")]
        [SerializeField, Range(0f, 1f)]
        private float supplyRecoveryRate = 0.05f;

        private readonly Dictionary<string, Dictionary<string, int>> items = new();
        private readonly Dictionary<string, int> pendingSales = new();

        private GameObject[] itemPrefabs;
        
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            itemPrefabs = Resources.LoadAll<GameObject>("Items");

            InitializeItems();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void InitializeItems()
        {
            foreach (GameObject item in itemPrefabs)
            {
                if (item == null)
                    continue;

                if (!item.TryGetComponent<Pickup.Pickup>(out var pickup))
                {
                    Debug.LogWarning(
                        $"ShopEconomy: {item.name} does not have a Pickup component. " +
                        "Skipping item.");

                    continue;
                }

                string itemName = pickup.objectName;

                if (string.IsNullOrEmpty(itemName))
                {
                    Debug.LogWarning(
                        $"ShopEconomy: {item.name} has no objectName. " +
                        "Skipping item.");

                    continue;
                }

                if (items.ContainsKey(itemName))
                    continue;

                AddItem(
                    itemName,
                    new Dictionary<string, int>
                    {
                        ["basePrice"] = pickup.value,
                        ["demand"] = baseSupply
                    });
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyEconomyUpdate();
        }

        /// <summary>
        /// Records an item sold during the current round.
        /// The supply change will not affect prices until the next scene transition.
        /// </summary>
        public void RecordSale(Pickup.Pickup pickup)
        {
            if (pickup == null)
                return;

            RecordSale(pickup.objectName);
        }

        /// <summary>
        /// Records a number of items of a specific type sold during the current round.
        /// </summary>
        private void RecordSale(string itemName, int amount = 1)
        {
            if (!items.ContainsKey(itemName))
            {
                Debug.LogWarning(
                    $"ShopEconomy: Tried to record a sale for unknown item '{itemName}'.");

                return;
            }

            if (amount <= 0)
                return;

            if (!pendingSales.ContainsKey(itemName))
                pendingSales[itemName] = 0;

            pendingSales[itemName] += amount;
        }

        private void ApplyEconomyUpdate()
        {
            foreach (var entry in items)
            {
                string itemName = entry.Key;
                Dictionary<string, int> data = entry.Value;

                int currentSupply = data["supply"];

                // Apply sales from the previous round.
                if (pendingSales.TryGetValue(itemName, out int sold))
                {
                    currentSupply += sold;
                }

                // Recover a fixed percentage toward the baseline.
                currentSupply = Mathf.RoundToInt(
                    Mathf.Lerp(
                        currentSupply,
                        baseSupply,
                        supplyRecoveryRate));

                data["supply"] = Mathf.Max(1, currentSupply);
            }

            pendingSales.Clear();
        }

        /// <summary>
        /// Gets the current market-adjusted price of an individual Pickup.
        /// The Pickup's quality-adjusted value is multiplied by the market factor.
        /// </summary>
        public float GetPrice(Pickup.Pickup pickup)
        {
            if (pickup == null)
                return 0f;

            string itemName = pickup.objectName;

            if (!items.TryGetValue(itemName, out var data))
            {
                Debug.LogWarning(
                    $"ShopEconomy: No economy data exists for '{itemName}'.");

                return pickup.value;
            }

            float factor = Mathf.Clamp(
                (float)data["demand"] /
                Mathf.Max(1, data["supply"]),
                0.5f,
                2f);

            return pickup.value * factor;
        }

        public void AddItem(
            string itemName,
            Dictionary<string, int> dict)
        {
            if (string.IsNullOrEmpty(itemName))
                return;

            if (!dict.TryGetValue("basePrice", out int basePrice))
            {
                Debug.LogWarning(
                    $"ShopEconomy: Item '{itemName}' is missing basePrice.");

                return;
            }

            int demand = dict.TryGetValue(
                "demand",
                out int suppliedDemand)
                ? suppliedDemand
                : baseSupply;

            items[itemName] = new Dictionary<string, int>
            {
                ["basePrice"] = basePrice,
                ["supply"] = baseSupply,
                ["demand"] = demand
            };

            pendingSales[itemName] = 0;
        }
    }
}