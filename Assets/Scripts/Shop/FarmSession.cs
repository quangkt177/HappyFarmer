using System.Collections.Generic;
using HappyFarmer.Gameplay;
using UnityEngine;

namespace HappyFarmer.Shop
{
    public class BoughtAnimal
    {
        public string key;
        public AnimalGrowth.Species species;
        public Vector2 position;
    }

    // Tiến trình sống qua các lần đổi map (chưa ghi ra file, thoát game là mất)
    public static class FarmSession
    {
        public static readonly HashSet<string> UnlockedParcels = new HashSet<string>();
        public static readonly HashSet<string> RemovedAnimalKeys = new HashSet<string>();
        public static readonly List<BoughtAnimal> BoughtAnimals = new List<BoughtAnimal>();

        private static int nextAnimalId;

        public static string NewAnimalKey() => $"bought_{nextAnimalId++}";
    }
}
