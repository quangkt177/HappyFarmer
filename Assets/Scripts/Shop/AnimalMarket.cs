using System.Collections.Generic;
using HappyFarmer.Gameplay;
using UnityEngine;

namespace HappyFarmer.Shop
{
    public class AnimalKey : MonoBehaviour
    {
        public string key;

        // Lưu giai đoạn lớn khi rời map, để lần sau vào lại không bị reset về trạng thái trong scene
        private void OnDestroy()
        {
            var growth = GetComponent<AnimalGrowth>();
            if (growth == null || string.IsNullOrEmpty(key)) return;

            FarmSession.AnimalStates[key] = new AnimalState
            {
                stage = growth.SavedStage,
                growthTimer = growth.GrowthTimer,
                savedAt = Time.time
            };
        }

        public void ApplySavedState()
        {
            if (!FarmSession.AnimalStates.TryGetValue(key, out var state)) return;

            // Lúc người chơi ở map khác, con vật vẫn tiếp tục lớn
            GetComponent<AnimalGrowth>().RestoreState(state.stage, state.growthTimer, Time.time - state.savedAt);
        }
    }

    public static class AnimalMarket
    {
        // Vùng thả con mới mua, lấy theo chuồng gà và bãi bò trong PastureMapBuilder
        private static readonly Rect ChickenPenArea = new Rect(6.5f, 15.2f, 6f, 1.8f);
        private static readonly Rect CowPastureArea = new Rect(15.5f, 4.3f, 8f, 2.7f);

        // Bản sao ẩn của mỗi loài, để vẫn mua được khi đã bán hết con trong chuồng
        private static readonly Dictionary<AnimalGrowth.Species, GameObject> templates = new Dictionary<AnimalGrowth.Species, GameObject>();

        public static int CountAnimals(AnimalGrowth.Species species, AnimalGrowth.GrowthStage? stage = null)
        {
            int count = 0;
            foreach (var animal in Object.FindObjectsOfType<AnimalGrowth>())
            {
                if (animal.AnimalSpecies != species) continue;
                if (stage.HasValue && animal.CurrentStage != stage.Value) continue;
                count++;
            }
            return count;
        }

        public static Sprite GetIcon(AnimalGrowth.Species species)
        {
            // Bản mẫu ẩn chưa chạy Start nên chưa có sprite, lấy từ con đang có trong chuồng
            foreach (var animal in Object.FindObjectsOfType<AnimalGrowth>())
            {
                var renderer = animal.GetComponent<SpriteRenderer>();
                if (animal.AnimalSpecies == species && renderer != null && renderer.sprite != null) return renderer.sprite;
            }
            return null;
        }

        public static bool BuyBaby(AnimalGrowth.Species species)
        {
            var area = species == AnimalGrowth.Species.Chicken ? ChickenPenArea : CowPastureArea;
            var position = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));

            var record = new BoughtAnimal { key = FarmSession.NewAnimalKey(), species = species, position = position };
            if (!Spawn(record)) return false;

            FarmSession.BoughtAnimals.Add(record);
            return true;
        }

        public static bool SellOneAdult(AnimalGrowth.Species species)
        {
            foreach (var animal in Object.FindObjectsOfType<AnimalGrowth>())
            {
                if (animal.AnimalSpecies != species || animal.CurrentStage != AnimalGrowth.GrowthStage.Adult) continue;

                var tag = animal.GetComponent<AnimalKey>();
                if (tag != null)
                {
                    int removed = FarmSession.BoughtAnimals.RemoveAll(b => b.key == tag.key);
                    if (removed == 0) FarmSession.RemovedAnimalKeys.Add(tag.key);
                }

                // Ẩn ngay để danh sách trong shop đếm lại đúng trong cùng frame
                animal.gameObject.SetActive(false);
                Object.Destroy(animal.gameObject);
                return true;
            }
            return false;
        }

        // Gọi một lần sau khi load PastureScene: xoá con đã bán, thả lại con đã mua
        public static void RestoreSceneAnimals()
        {
            var animals = Object.FindObjectsOfType<AnimalGrowth>();
            if (animals.Length == 0 || Object.FindObjectOfType<AnimalKey>() != null) return;

            templates.Clear();
            foreach (var animal in animals)
            {
                var t = animal.transform;
                string key = $"{(t.parent != null ? t.parent.name : "root")}/{t.GetSiblingIndex()}";
                animal.gameObject.AddComponent<AnimalKey>().key = key;

                if (!templates.ContainsKey(animal.AnimalSpecies))
                {
                    var template = Object.Instantiate(animal.gameObject, t.parent);
                    template.name = $"{animal.AnimalSpecies} Template";
                    template.GetComponent<AnimalKey>().key = null; // bản mẫu không được ghi đè trạng thái con gốc
                    template.SetActive(false);
                    templates[animal.AnimalSpecies] = template;
                }
            }

            foreach (var animal in animals)
            {
                var tag = animal.GetComponent<AnimalKey>();
                if (FarmSession.RemovedAnimalKeys.Contains(tag.key))
                {
                    Object.Destroy(animal.gameObject);
                }
                else
                {
                    tag.ApplySavedState();
                }
            }

            foreach (var record in FarmSession.BoughtAnimals)
            {
                Spawn(record);
            }
        }

        private static bool Spawn(BoughtAnimal record)
        {
            var template = GetTemplate(record.species);
            if (template == null) return false;

            var clone = Object.Instantiate(template, record.position, Quaternion.identity, template.transform.parent);
            clone.transform.localScale = Vector3.one;
            clone.name = record.species == AnimalGrowth.Species.Chicken ? "Bought Chick" : "Bought Calf";
            var tag = clone.GetComponent<AnimalKey>();
            tag.key = record.key;
            clone.SetActive(true);
            clone.GetComponent<AnimalGrowth>().SetStage(AnimalGrowth.GrowthStage.Baby);
            tag.ApplySavedState();
            return true;
        }

        private static GameObject GetTemplate(AnimalGrowth.Species species)
        {
            return templates.TryGetValue(species, out var template) && template != null ? template : null;
        }
    }
}
