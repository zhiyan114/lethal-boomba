using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalBoomba.Behaviors;
using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using static LethalBoomba.Behaviors.LotTaBehavior;

namespace LethalBoomba
{
    [BepInPlugin(Guid, Name, Version)]
    public class Init: BaseUnityPlugin
    {
        public const string Guid = "FurryNet.BoomBa";
        public const string Name = "BoomBa";
        public const string Version = "1.1.0";

        public static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(Guid);
        private static readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            // Setup Items
            Utils.ExecuteRuntimeInitAttr(typeof(ItemManager));
            ItemManager.AddItem<BoomBaBehavior>("Assets/AssetBundles/BombToolkit/BoomBa/B00mbaProp.asset", 60);
            ItemManager.AddItem<NuKaBehavior>("Assets/AssetBundles/BombToolkit/NukeKa/NuKaProp.asset", 30);
            ItemManager.AddItem<LotTaBehavior>("Assets/AssetBundles/BombToolkit/LotTa/LottaProp.asset", 50);

            // Patch Code
            harmony.PatchAll(typeof(ItemManager));
            harmony.PatchAll(typeof(LottaOutcome));
            logger.LogInfo("Patch Done...");

            // Setup NetVariable
            Utils.ExecuteRuntimeInitAttr(Assembly.GetExecutingAssembly().GetType("__GEN.NetworkVariableSerializationHelper"));
            logger.LogInfo("NetVariable Init Done...");
        }
    }
}
