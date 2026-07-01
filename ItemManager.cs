using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba
{
    public class ItemManager
    {
        public static AssetBundle bundle { get; private set; }
        public static GameObject LargeExplosion { get; private set; }
        private static Dictionary<string, ItemManager> _items;
        public static ItemManager[] items { get { return _items.Values.ToArray(); } }
        public Item item { get; private set; }
        public int SpawnRate { get; private set; }

        private ItemManager() { }

        /// <summary>
        /// Setup asset and prepare for loading using default item behavior preconfigured in unity
        /// </summary>
        /// <param name="assetPath">Unity Item Asset Path</param>
        /// <param name="spawnRate">Item Spawn rate on moon</param>
        public static void AddItem(string assetPath, int spawnRate)
        {
            Item? mainItem = bundle.LoadAsset<Item>(assetPath);
            if (mainItem == null)
                throw new Exception($"Invalid asset bundle path: {assetPath}!");

            _items.Add(mainItem.itemName, new ItemManager
            {
                item = mainItem,
                SpawnRate = spawnRate
            });
            Init.logger.LogMessage($"ItemManager: Added item {mainItem.itemName} without custom behavior!");
        }
        /// <summary>
        /// Setup asset and prepare for loading using custom item behavior implementation
        /// </summary>
        /// <typeparam name="T">GrabbableObject class inheritance</typeparam>
        /// <param name="assetPath">Unity Item Asset Path</param>
        /// <param name="spawnRate">Item Spawn rate on moon</param>
        public static void AddItem<T>(string assetPath, int spawnRate) where T : GrabbableObject
        {
            Item mainItem = bundle.LoadAsset<Item>(assetPath);
            _items.Add(mainItem.itemName, new ItemManager
            {
                item = mainItem,
                SpawnRate = spawnRate
            });
            mainItem.spawnPrefab.AddComponent<T>();
            Init.logger.LogMessage($"ItemManager: Added item {mainItem.itemName} with custom behavior!");
        }
        /// <summary>
        /// Obtain the Item object given configured asset's itemName
        /// </summary>
        /// <param name="itemName">Configured name for your Item asset</param>
        /// <returns></returns>
        public static Item? GetItem(string itemName)
        {
            return _items.GetValueOrDefault(itemName)?.item;
        }

        /// <summary>
        /// Execute as early as possible (ONLY ONCE) to initialize
        /// AssetBundle
        /// </summary>
        public static void InitFirst()
        {
            string assetBundlePath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "bombpack"
            );
            if (!File.Exists(assetBundlePath))
                throw new Exception("bombpack asset missing!");
            _items = new Dictionary<string, ItemManager>();
            bundle = AssetBundle.LoadFromFile(assetBundlePath);
            LargeExplosion = bundle.LoadAsset<GameObject>("Assets/AssetBundles/BombToolkit/NukeKa/Effect/NuKaBoomEffect.prefab");
            Init.logger.LogMessage("ItemManager: Successfully Loaded Bundle...");

        }
        /// <summary>
        /// Run right before game starts but after NetworkManager is initialized (Only ONCE)
        /// </summary>
        /// <param name="netManager">Pass on NetworkManager instance</param>
        public static void SetupNetPrefab(NetworkManager netManager)
        {
            foreach (ItemManager itemMGR in _items.Values)
                netManager.AddNetworkPrefab(itemMGR.item.spawnPrefab);
            Init.logger.LogMessage("ItemManager: Configured Network Prefabs");
        }

        private static bool ItemConfigured = false;
        /// <summary>
        /// Run before the game is ready but after user loads the save file.
        /// Run ONLY once, though there is safety net to prevent this from execution multiple time
        /// </summary>
        /// <param name="instance">Pass on StartOfRound instance</param>
        public static void SetupItemSpawn(StartOfRound instance)
        {
            if (ItemConfigured) return;
            ItemConfigured = true;
            foreach(ItemManager itemMGR in _items.Values)
            {
                instance.allItemsList.itemsList.Add(itemMGR.item);
                foreach (SelectableLevel level in instance.levels)
                    level.spawnableScrap.Add(new SpawnableItemWithRarity(itemMGR.item, itemMGR.SpawnRate));
            }
            Init.logger.LogMessage("ItemManager: Successfully Loaded all items to all maps");

            //// Unit TestKit
            //Init.logger.LogMessage("Running Tests...");
            //GameObject obj = UnityEngine.Object.Instantiate(Init.mainAsset.spawnPrefab, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
            //Init.logger.LogMessage("Object Init...");

            //if(!obj.TryGetComponent<NetworkObject>(out NetworkObject netObj))
            //{
            //    Init.logger.LogError("Failed: NetworkObject missing from spawnPrefab...");
            //    return;
            //}
            //Init.logger.LogMessage("Obtained NetworkObject...");

            //if (!obj.TryGetComponent<GrabbableObject>(out GrabbableObject _))
            //{
            //    Init.logger.LogError("Failed: BoomBaBehavior Script Class missing from spawnPrefab...");
            //    return;
            //}

            //netObj.Spawn();
            //Init.logger.LogMessage("Passed: Object Spawned, if no exception occurs, test is completed!");
        }
    }
}
