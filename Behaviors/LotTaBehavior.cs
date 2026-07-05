using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
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
            uint SelectedNum = checked((uint)RandomNumberGenerator.GetInt32(0, checked((int)totalVal)));
            uint tempVal = 0;
            foreach (LottaOutcome item in StatTable)
            {
                if (item.chance + tempVal > SelectedNum)
                    return item.option;
                tempVal += item.chance;
            }

            throw new Exception("getRandItem: Huh, this is here for control-flow purposes. If you see this, something is terribly wrong...");
        }

        private static uint totalVal = 0;
        private LottaOutcome(Opts opt, double chance)
        {
            option = opt;
            this.chance = (uint)(chance * 100);
            totalVal += this.chance;
        }

        // Stat Trackings
        private Opts option { get; set; }
        private uint chance { get; set; }

    }
    
    public class LotTaBehavior : GrabbableObject
    {
        private AudioSource AudioSrc;
        public AudioClip ScratchSound;
        public float countdownSec;
        public float beforeKillcdSec;

        NetworkVariable<int> scrapValue = new NetworkVariable<int>(0);

        private void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
            grabbable = true;
            isInFactory = true;
            grabbableToEnemies = true;
            itemProperties = ItemManager.GetItem("LotTa");
            countdownSec = 1f;
            ScratchSound = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/LotTa/sounds/LottaActivate.ogg");

            scrapValue.OnValueChanged += (_,val) => SetScrapValue(val);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            // Activate Bomb
            if (!buttonDown) return;
            if (!isDetonated.Value)
            {
                StartBoomServerRpc();
                return;
            }
        }

        [Rpc(SendTo.Server)]
        private void StartBoomServerRpc(RpcParams rpcParam = default)
        {
            if (isDetonated.Value)
                return;
            isDetonated.Value = true;
            StartCoroutine(ManagedExecution());
        }

        private IEnumerator ManagedExecution()
        {
            if(!NetworkManager.Singleton.IsServer)
                yield break;
            ExecuteStageClientRpc();

            // 1s buffer for client to finish explosion process (accounted for things such as net latency)
            yield return new WaitForSeconds(countdownSec + beforeKillcdSec + 1);
            GetComponent<NetworkObject>().Despawn();
        }

        private IEnumerator CountdownRoutine()
        {
            if (!NetworkManager.Singleton.IsClient)
                yield break;

            //// It cant be thrown so just drop it when activate ig lol
            //if (IsOwner)
            //    playerHeldBy?.DiscardHeldObject();
            //grabbable = false;

            // Pre-Explosion sound
            BombSrc.PlayOneShot(preExplodeSound);
            yield return new WaitForSeconds(countdownSec);

            // Show dramatic nuke effect here (if there's one)
            //Utils.Explode(GetComponent<Transform>().position, blastRadius);
            //@TODO: Custom Explosion and stuff here
            // GameObject Explosion = UnityEngine.Object.Instantiate(StartOfRound.Instance.explosionPrefab, GetComponent<Transform>().position, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
            GameObject LargeExplosion = Instantiate(ItemManager.LargeExplosion, GetComponent<Transform>().position, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
            LargeExplosion.SetActive(value: true);
            HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            yield return new WaitForSeconds(beforeKillcdSec);

            // Kill all the players and wrap-up things
            GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Blast);
            Utils.HideNetObject(gameObject);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ExecuteStageClientRpc()
        {
            StartCoroutine(CountdownRoutine());
        }
    }
}
