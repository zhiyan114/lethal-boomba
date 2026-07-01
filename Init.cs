using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalBoomba.Behaviors;
using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba
{
    [BepInPlugin(Guid, Name, Version)]
    public class Init: BaseUnityPlugin
    {
        public const string Guid = "FURRYNET.BoomBa";
        public const string Name = "BoomBa";
        public const string Version = "1.0.0";

        public static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(Guid);
        private static readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            ItemManager.InitFirst();
            ItemManager.AddItem<BoomBaBehavior>("Assets/AssetBundles/BombToolkit/BoomBa/B00mbaProp.asset", 60);
            ItemManager.AddItem<NuKaBehavior>("Assets/AssetBundles/BombToolkit/NukeKa/NuKaProp.asset", 30);
            // ItemManager.AddItem("Assets/AssetBundles/BombToolkit/NukeKa/NuKaProp.asset", 100);

            // NetCode Initializer
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                        method.Invoke(null, null);
                }
            }
            logger.LogMessage("Called RuntimeInit on internal...");

            // Patch Code
            harmony.PatchAll(typeof(RoundPatch));
            harmony.PatchAll(typeof(NetworkHook));
            logger.LogMessage("RoundPatch Done...");
        }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    public class RoundPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPatch(StartOfRound __instance)
        {
            ItemManager.SetupItemSpawn(__instance);
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager))]
    public class NetworkHook
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void NetStart(GameNetworkManager __instance)
        {
            ItemManager.SetupNetPrefab(__instance.GetComponent<NetworkManager>());
        }
    }
}
