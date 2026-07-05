using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class LotTaBehavior : GrabbableObject
    {
        private AudioSource AudioSrc;
        public AudioClip ScratchSound;
        public float countdownSec;

        public NetworkVariable<int> scrapVal = new NetworkVariable<int>(0);

        private void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
            grabbable = true;
            isInFactory = true;
            grabbableToEnemies = true;
            itemProperties = ItemManager.GetItem("LotTa");
            countdownSec = 1f;
            ScratchSound = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/LotTa/sounds/LottaActivate.ogg");

            scrapVal.OnValueChanged += (_,val) => SetScrapValue(val);
        }

        public override void Start()
        {
            // @TODO: Determine if the item is spawned during oribit (mark as non-scratchable) or on the moon
            base.Start();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            // Scratch Ticket
            if (!buttonDown) return;
            if (!base.IsOwner) return;
            if (scrapVal.Value != 0) return;
            playerHeldBy.activatingItem = true;
            StartScratchServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void StartScratchServerRpc(RpcParams rpcParam = default)
        {
            StartCoroutine(ExecuteReq());
        }

        private IEnumerator ExecuteReq()
        {
            if (!NetworkManager.Singleton.IsServer) yield break;
            if (scrapVal.Value != 0) yield break;
            LottaOutcome.Opts selOpt = LottaOutcome.getRandItem();
            switch (selOpt)
            {
                case LottaOutcome.Opts.Explosion:
                    yield return StartClientRpc(selOpt);
                    yield return new WaitForSeconds(999); // Replace 999 with time needed for explosion to execute
                    GetComponent<NetworkObject>().Despawn();
                    break;
                case LottaOutcome.Opts.RandEnemy:
                    yield return StartClientRpc(selOpt);
                    //@TODO: Spawn emeny code here
                    GetComponent<NetworkObject>().Despawn();
                    break;
                case LottaOutcome.Opts.RandTrap:
                    yield return StartClientRpc(selOpt);
                    //@TODO: Spawn trap code here
                    GetComponent<NetworkObject>().Despawn();
                    break;
                default:
                    yield return StartClientRpc(selOpt);
                    break;
            }
        }

        IEnumerator StartClientRpc(LottaOutcome.Opts opt)
        {
            if (!NetworkManager.Singleton.IsServer) yield break;
            ExecuteStageClientRpc(opt);
            yield return new WaitForSeconds(countdownSec + 2);
            switch(opt)
            {
                case LottaOutcome.Opts.x1Multi:
                    scrapVal.Value = scrapValue;
                    break;
                case LottaOutcome.Opts.x1_5Multi:
                    scrapVal.Value = Mathf.RoundToInt(scrapValue * 1.5f);
                    break;
                case LottaOutcome.Opts.x2Multi:
                    scrapVal.Value = Mathf.RoundToInt(scrapValue * 2f);
                    break;
                case LottaOutcome.Opts.x5Multi:
                    scrapVal.Value = Mathf.RoundToInt(scrapValue * 5f);
                    break;
                case LottaOutcome.Opts.x10Multi:
                    scrapVal.Value = Mathf.RoundToInt(scrapValue * 10f);
                    break;
                default:
                    scrapVal.Value = 0;
                    break;
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ExecuteStageClientRpc(LottaOutcome.Opts opt)
        {
            StartCoroutine(CountdownRoutine(opt));
        }
        private IEnumerator CountdownRoutine(LottaOutcome.Opts SelOpt) {
            
            //@ TODO: IMPLEMENT Activation Process
            //
            //if (!NetworkManager.Singleton.IsClient)
            //    yield break;

            //// Pre-Explosion sound
            //BombSrc.PlayOneShot(preExplodeSound);
            //yield return new WaitForSeconds(countdownSec);

            //GameObject Explosion = UnityEngine.Object.Instantiate(StartOfRound.Instance.explosionPrefab, GetComponent<Transform>().position, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
            //GameObject LargeExplosion = Instantiate(ItemManager.LargeExplosion, GetComponent<Transform>().position, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
            //LargeExplosion.SetActive(value: true);
            //HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            //yield return new WaitForSeconds(beforeKillcdSec);

            //// Kill all the players and wrap-up things
            //GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Blast);
            //Utils.HideNetObject(gameObject);
        }
    }

    public class LottaOutcome
    {
        // Global
        public enum Opts
        {
            ZeroValue,
            Explosion,
            RandEnemy,
            RandTrap,
            x1Multi,
            x1_5Multi,
            x2Multi,
            x5Multi,
            x10Multi
        }
        public readonly static HashSet<LottaOutcome> StatTable = new HashSet<LottaOutcome>()
        {
            // Final Calculation: Percentage spawn * 100 to allow two decimal place for percentages
            new LottaOutcome(Opts.ZeroValue, 15),
            new LottaOutcome(Opts.Explosion, 15),
            new LottaOutcome(Opts.RandEnemy, 5),
            new LottaOutcome(Opts.RandTrap, 5),
            new LottaOutcome(Opts.x1Multi, 25),
            new LottaOutcome(Opts.x1_5Multi, 20),
            new LottaOutcome(Opts.x2Multi, 10),
            new LottaOutcome(Opts.x5Multi, 3.5),
            new LottaOutcome(Opts.x10Multi, 1.5),
        };

        public static Opts getRandItem()
        {
            int SelectedNum = RandomNumberGenerator.GetInt32(0, totalVal);
            int tempVal = 0;
            foreach (LottaOutcome item in StatTable)
            {
                if (item.chance + tempVal > SelectedNum)
                    return item.option;
                tempVal += item.chance;
            }

            throw new Exception("getRandItem: Huh, this is here for control-flow purposes. If you see this, something is terribly wrong...");
        }

        private static int totalVal = 0;
        private LottaOutcome(Opts opt, double chance)
        {
            option = opt;
            this.chance = (int)(chance * 100);
            totalVal += this.chance;
        }

        // Stat Trackings
        private Opts option { get; set; }
        private int chance { get; set; }

        [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
        [HarmonyPrefix]
        public static void MarkAllLottoUnscratchable()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            LotTaBehavior[] tickets = UnityEngine.Object.FindObjectsByType<LotTaBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (LotTaBehavior ticket in tickets)
                if (ticket.IsSpawned && ticket.scrapVal.Value == 0)
                    ticket.scrapVal.Value = ticket.scrapValue;
        }
    }
}
