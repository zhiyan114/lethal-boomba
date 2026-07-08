using BepInEx;
using BepInEx.Configuration;
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
        public const string Version = "1.1.1";

        public static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(Guid);
        private static readonly Harmony harmony = new Harmony(Guid);
        public static ConfigFile config { get; private set; }

        private void Awake()
        {
            // Must Run First stuff here
            Init.config = this.Config;
            Utils.ExecuteRuntimeInitAttr(typeof(ItemManager));
            Utils.ExecuteRuntimeInitAttr(typeof(ConfigManager));

            // Stuff here that should be executed early, but breaks if way too early
            Utils.ExecuteRuntimeInitAttr(typeof(LottaOutcome));
            
            // Setup Items
            ItemManager.AddItem<BoomBaBehavior>("Assets/AssetBundles/BombToolkit/BoomBa/B00mbaProp.asset", 60);
            ItemManager.AddItem<NuKaBehavior>("Assets/AssetBundles/BombToolkit/NukeKa/NuKaProp.asset", 30);
            ItemManager.AddItem<LotTaBehavior>("Assets/AssetBundles/BombToolkit/LotTa/LottaProp.asset", 50);

            // Patch Code
            harmony.PatchAll(typeof(ItemManager));
            harmony.PatchAll(typeof(LottaOutcome));
            harmony.PatchAll(typeof(UtilHelper));
            logger.LogInfo("Patch Done...");

            // Setup NetVariable
            Utils.ExecuteRuntimeInitAttr(Assembly.GetExecutingAssembly().GetType("__GEN.NetworkVariableSerializationHelper"));
            logger.LogInfo("NetVariable Init Done...");
        }
    }
}
