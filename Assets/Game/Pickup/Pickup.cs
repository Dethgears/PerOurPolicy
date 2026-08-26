using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Pickup
{
    public class Pickup : MonoBehaviour
    {
        [SerializeField] public string objectName;
        [SerializeField] public int value;
        [SerializeField] public float variation = 0.25f;

        private void Start()
        {
            var multiplier = 1 - variation + Random.value * 2 * variation; // From 1-variation to 1+variation
            value = (int)Math.Round(value * multiplier);
        }
    }
}