using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;
using static LethalBoomba.Behaviors.LottaOutcome;

namespace LethalBoomba.Behaviors
{
    public class LotTaBehavior : GrabbableObject
    {
        public static List<LotTaBehavior> UnscratchedTickets = new List<LotTaBehavior>();
        private AudioSource AudioSrc;
        public AudioClip ScratchSound;
        public AudioClip WinnerSFX;
        public float countdownSec;

        public NetworkVariable<int> scrapVal = new NetworkVariable<int>(-1);
        public NetworkVariable<LottaOutcome.Opts> Result = new NetworkVariable<Opts>(LottaOutcome.Opts.Unselected);
        public bool isScratched { get => Result.Value != LottaOutcome.Opts.Unselected; }

        private void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
            grabbable = true;
            isInFactory = true;
            grabbableToEnemies = true;
            itemProperties = ItemManager.GetItem("LotTa");
            countdownSec = 1f;
            ScratchSound = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/LotTa/sounds/LottaActivate.ogg");
            WinnerSFX = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/LotTa/sounds/LottaWon.ogg");
            scrapVal.OnValueChanged += (_, val) => SetScrapValue(val);
            Result.OnValueChanged += serverResUpdate;
        }

        private void serverResUpdate(LottaOutcome.Opts old, LottaOutcome.Opts val)
        {
            if (old == Opts.Unselected && val != Opts.Unselected)
                UnscratchedTickets.Remove(this);
        }

        public override void OnNetworkSpawn()
        {
            UnscratchedTickets.Add(this);
            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            UnscratchedTickets.Remove(this);
            base.OnNetworkDespawn();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (isScratched) return;
            if (playerHeldBy.activatingItem) return;
            if (base.IsOwner)
                playerHeldBy.activatingItem = true;

            StartCoroutine(HandleTicketActivation());
        }

        private IEnumerator HandleTicketActivation()
        {
            if (!NetworkManager.Singleton.IsClient)
                yield break;

            // Scratch sfx
            AudioSrc.PlayOneShot(ScratchSound);
            WalkieTalkie.TransmitOneShotAudio(AudioSrc, ScratchSound);
            yield return new WaitForSeconds(1);
            if(base.IsOwner) StartScratchServerRpc();
            yield return new WaitUntil(() => isScratched);

            switch (Result.Value)
            {
                case Opts.NoState:
                    HUDManager.Instance.DisplayTip("LotTa Outcome", "Invalid State Detected (The item was spawned by a mod while in an invalid state :p)");
                    playerHeldBy.activatingItem = false;
                    yield break;
                case LottaOutcome.Opts.Explosion:
                    if (this.IsOwner)
                    {
                        HUDManager.Instance.DisplayTip("LotTa Outcome", "Aww, you got explosion!");
                        playerHeldBy.activatingItem = false;
                        if (NetworkObject.IsSpawned)
                            playerHeldBy.DiscardHeldObject();

                    }
                    Utils.Explode(transform.position, 10);
                    Utils.HideNetObject(gameObject);
                    yield break;
                case LottaOutcome.Opts.RandEnemy:
                    if (this.IsOwner)
                    {
                        HUDManager.Instance.DisplayTip("LotTa Outcome", "Aww, you got random enemy spawned on you!");
                        playerHeldBy.activatingItem = false;
                        if (NetworkObject.IsSpawned)
                            playerHeldBy.DiscardHeldObject();
                    }
                    Utils.HideNetObject(gameObject);
                    yield break;
                case LottaOutcome.Opts.RandTrap:
                    if (this.IsOwner)
                    {
                        HUDManager.Instance.DisplayTip("LotTa Outcome", "Aww, you got random trap spawned on you!");
                        playerHeldBy.activatingItem = false;
                        if (NetworkObject.IsSpawned)
                            playerHeldBy.DiscardHeldObject();
                    }
                    Utils.HideNetObject(gameObject);
                    yield break;
                case LottaOutcome.Opts.ZeroValue:
                    if (this.IsOwner)
                    {
                        HUDManager.Instance.DisplayTip("LotTa Outcome", "Well, at least you only lose all the value of the ticket!");
                        playerHeldBy.activatingItem = false;
                    }
                    yield break;
                case LottaOutcome.Opts.x1Multi:
                    if (this.IsOwner)
                        HUDManager.Instance.DisplayTip("LotTa Outcome", "Kinda lucky that you didnt lose any ticket value!");
                    break;
                case LottaOutcome.Opts.x1_5Multi:
                    if (this.IsOwner)
                        HUDManager.Instance.DisplayTip("LotTa Outcome", $"Congrat, you got x1.5 more value on the ticket! Total Value: {(isScratched ? scrapValue : scrapValue * 1.5f)}");
                    break;
                case LottaOutcome.Opts.x2Multi:
                    if (this.IsOwner)
                        HUDManager.Instance.DisplayTip("LotTa Outcome", $"Wow, you got x2 more value on the ticket! Total Value: {(isScratched ? scrapValue : scrapValue * 2f)}");
                    break;
                case LottaOutcome.Opts.x5Multi:
                    if (this.IsOwner)
                        HUDManager.Instance.DisplayTip("LotTa Outcome", $"Crazy luck, you got x5 more value on the ticket! Total Value: {(isScratched ? scrapValue : scrapValue * 5f)}");
                    break;
                case LottaOutcome.Opts.x10Multi:
                    if (this.IsOwner)
                        HUDManager.Instance.DisplayTip("LotTa Outcome", $"Insane luck, you got x10 more value on the ticket! Total Value: {(isScratched ? scrapValue : scrapValue * 10f)}");
                    break;
            }
            //@TODO: The code below should only be accessable to multiplier x1.5 and above AND add custom party sfx to it as well
            // Handle post-multiplier selection
            if (this.IsOwner)
                playerHeldBy.activatingItem = false;
            if(Utils.confettiPrefab && (byte)Result.Value > (byte)Opts.x1Multi)
            {
                UnityEngine.Object.Instantiate(
                    Utils.confettiPrefab,
                    transform.position,
                    Quaternion.identity,
                    (!isInElevator) ? RoundManager.Instance.mapPropsContainer.transform : StartOfRound.Instance.elevatorTransform);
                AudioSrc.PlayOneShot(WinnerSFX);
                WalkieTalkie.TransmitOneShotAudio(AudioSrc, WinnerSFX);
            }
                
        }

        [Rpc(SendTo.Server)]
        private void StartScratchServerRpc(RpcParams rpcParam = default)
        {
            if (isScratched) return;

            // Handle scenario where player activates ticket that's spawned through "cheat" and potentially breaks the game state
            // This should silently update the ticket to the correct state and prevent breaking change form happening
            if (!StartOfRound.Instance.shipHasLanded)
            {
                scrapVal.Value = scrapValue;
                Result.Value = Opts.NoState;
                return;
            }
            ulong senderClientId = rpcParam.Receive.SenderClientId;
            PlayerControllerB owner = StartOfRound.Instance.allPlayerScripts
    .FirstOrDefault(p => p.OwnerClientId == senderClientId);
            StartCoroutine(ExecuteReq(owner));
        }

        private IEnumerator ExecuteReq(PlayerControllerB owner)
        {
            if (!NetworkManager.Singleton.IsServer) yield break;
            if (isScratched) yield break;
            LottaOutcome.Opts selOpt = LottaOutcome.getRandItem();
            switch (selOpt)
            {
                case LottaOutcome.Opts.Explosion:
                    yield return ProcessState(selOpt);
                    yield return new WaitForSeconds(1); // 1s of buffers
                    GetComponent<NetworkObject>().Despawn();
                    break;
                case LottaOutcome.Opts.RandEnemy:
                    yield return ProcessState(selOpt);
                    LottaOutcome.spawnRandEnemy(owner, !owner.isInsideFactory);
                    yield return new WaitForSeconds(1);
                    GetComponent<NetworkObject>().Despawn();
                    break;
                case LottaOutcome.Opts.RandTrap:
                    yield return ProcessState(selOpt);
                    LottaOutcome.spawnRandTrap(owner);
                    yield return new WaitForSeconds(1);
                    GetComponent<NetworkObject>().Despawn();
                    break;
                default:
                    yield return ProcessState(selOpt);
                    break;
            }
        }

        IEnumerator ProcessState(LottaOutcome.Opts opt)
        {
            if (!NetworkManager.Singleton.IsServer) yield break;
            // yield return new WaitForSeconds(countdownSec+1);
            switch (opt)
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
            Result.Value = opt;
        }

        public override void SetControlTipsForItem()
        {
            bool allowScratch = !isScratched && StartOfRound.Instance?.shipHasLanded == true;
            HUDManager.Instance.ChangeControlTipMultiple(allowScratch ? itemProperties.toolTips : new string[] { }, holdingItem: true, itemProperties);
        }
    }

    public class LottaOutcome
    {
        // Global
        public enum Opts : byte
        {
            // General Purpose Lotta states
            Unselected,
            ZeroValue,
            Explosion,
            RandEnemy,
            RandTrap,
            // Multiplier with value > 0 must and only go below here (NoState is an exception)
            x1Multi,
            x1_5Multi,
            x2Multi,
            x5Multi,
            x10Multi,
            NoState, // Mark item as activated without any activation process (Includes invalid activation states)
        }
        public readonly static IReadOnlyList<LottaOutcome> StatTable = new List<LottaOutcome>()
        {
            // Final Calculation: Percentage spawn * 100 to allow two decimal place for percentages
            new LottaOutcome(Opts.ZeroValue, 15f),
            new LottaOutcome(Opts.Explosion, 15f),
            new LottaOutcome(Opts.RandEnemy, 5f),
            new LottaOutcome(Opts.RandTrap, 5f),
            new LottaOutcome(Opts.x1Multi, 25f),
            new LottaOutcome(Opts.x1_5Multi, 20f),
            new LottaOutcome(Opts.x2Multi, 10f),
            new LottaOutcome(Opts.x5Multi, 3.5f),
            new LottaOutcome(Opts.x10Multi, 1.5f),
        };
        private static readonly int totalVal = StatTable.Sum(k => k.chance);

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

        private LottaOutcome(Opts opt, float chance)
        {
            option = opt;
            this.chance = Mathf.FloorToInt(chance * 100f);
        }

        public static void spawnRandEnemy(PlayerControllerB owner, bool outdoorEnemy = true)
        {
            EnemyType[] whitelistEnemy = Utils.globalEnemies.Where(k => k.isOutsideEnemy == outdoorEnemy).ToArray();
            GameObject spawnObj = UnityEngine.Object.Instantiate(
                whitelistEnemy[RandomNumberGenerator.GetInt32(0, whitelistEnemy.Length)].enemyPrefab,
                Physics.Raycast(owner.transform.position, Vector3.down, out RaycastHit hit, 10f) ? hit.point : owner.transform.position,
                Quaternion.identity
            );

            spawnObj.GetComponent<NetworkObject>().Spawn(true);
            EnemyAI AIState = spawnObj.GetComponent<EnemyAI>();
            RoundManager.Instance.SpawnedEnemies.Add(AIState);
            AIState.enemyType.numberSpawned++;
            AIState.enemyType.hasSpawnedAtLeastOne = true;

        }

        public static void spawnRandTrap(PlayerControllerB owner)
        {
            GameObject spawnObj = UnityEngine.Object.Instantiate(
                Utils.globalTraps[RandomNumberGenerator.GetInt32(0, Utils.globalTraps.Count)].prefabToSpawn,
                Physics.Raycast(owner.transform.position, Vector3.down, out RaycastHit hit, 10f) ? hit.point : owner.transform.position,
                Quaternion.identity
            );
            spawnObj.GetComponent<NetworkObject>().Spawn(true);
        }

        // Stat Trackings
        private Opts option { get; set; }
        private int chance { get; set; }

        [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
        [HarmonyPostfix]
        public static void MarkAllLottoUnscratchable()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            // LotTaBehavior[] tickets = UnityEngine.Object.FindObjectsByType<LotTaBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (LotTaBehavior ticket in LotTaBehavior.UnscratchedTickets.ToArray())
                if (ticket.IsSpawned && !ticket.isScratched)
                {
                    ticket.scrapVal.Value = ticket.scrapValue;
                    ticket.Result.Value = Opts.NoState;
                }
            if (LotTaBehavior.UnscratchedTickets.Count > 0)
                Init.logger.LogError($"UnscratchedTickets expected zero item, but got {LotTaBehavior.UnscratchedTickets.Count}");
        }
    }
}
