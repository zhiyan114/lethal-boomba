using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class DruGaBehavior : GrabbableObject
    {
        private AudioSource AudioSrc;
        [SerializeField]
        private AudioClip EatSfx;
        private float eatCountDown;

        private NetworkVariable<ushort> RawState = new NetworkVariable<ushort>(0);
        private DruGaHelper.ActionType ActionState = DruGaHelper.ActionType.Unused;
        private DruGaHelper.MultiplierType MultiplierState = DruGaHelper.MultiplierType.Unused;

        void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
            eatCountDown = EatSfx.length + 0.250f;
            RawState.OnValueChanged += (_, k) =>
            {
                ActionState = (DruGaHelper.ActionType)(k & 0xFF);
                MultiplierState = (DruGaHelper.MultiplierType)(k >> 8);
            };
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (playerHeldBy.activatingItem) return;
            if (base.IsOwner)
                playerHeldBy.activatingItem = true;
            StartCoroutine(ProcessActivation());
        }

        IEnumerator ProcessActivation()
        {
            if (!NetworkManager.Singleton.IsClient)
                yield break;
            AudioSrc.PlayOneShot(EatSfx);
            WalkieTalkie.TransmitOneShotAudio(AudioSrc, EatSfx);
            yield return new WaitForSeconds(eatCountDown);

            RequestUsageServerRpc();
            yield return new WaitUntil(() => ActionState != DruGaHelper.ActionType.Unused);

            // Handle Explosion
            if(ActionState == DruGaHelper.ActionType.Explosion)
            {
                if (base.IsOwner)
                {
                    HUDManager.Instance.DisplayTip("DruGa Outcome", "Oops, you got unlucky :(");
                    playerHeldBy.activatingItem = false;
                }
                Utils.Explode(transform.position, 10);
                Utils.HideNetObject(gameObject);
                yield break;
            }

            // Fetch Multiplier Value
            float multiVal = 0;
            switch(MultiplierState)
            {
                case DruGaHelper.MultiplierType.Multi0_5: multiVal = 0.5f; break;
                case DruGaHelper.MultiplierType.Multi0_75: multiVal = 0.75f; break;
                case DruGaHelper.MultiplierType.Multi1: multiVal = 1f; break;
                case DruGaHelper.MultiplierType.Multi1_5: multiVal = 1.5f; break;
                case DruGaHelper.MultiplierType.Multi2: multiVal = 2f; break;
                case DruGaHelper.MultiplierType.Multi3: multiVal = 3f; break;
            }

            // Apply the stat changes
            switch(ActionState)
            {
                case DruGaHelper.ActionType.Health:
                    playerHeldBy.health *= Mathf.RoundToInt((float)playerHeldBy.health * multiVal);
                    if (base.IsOwner)
                        HUDManager.Instance.DisplayTip("DruGa Outcome", $"You received a health multiplier of {multiVal}x");
                    break;
                case DruGaHelper.ActionType.Speed:
                    playerHeldBy.movementSpeed *= multiVal;
                    if (base.IsOwner)
                        HUDManager.Instance.DisplayTip("DruGa Outcome", $"You received a speed multiplier of {multiVal}x");
                    break;
                case DruGaHelper.ActionType.JumpPower:
                    playerHeldBy.jumpForce *= multiVal;
                    if (base.IsOwner)
                        HUDManager.Instance.DisplayTip("DruGa Outcome", $"You received a jump power multiplier of {multiVal}x");
                    break;
            }

            if (base.IsOwner)
                playerHeldBy.activatingItem = false;
            Utils.HideNetObject(gameObject);
            yield break;
        }

        [Rpc(SendTo.Server)]
        void RequestUsageServerRpc()
        {
            StartCoroutine(HandleCleanup());
        }

        IEnumerator HandleCleanup()
        {
            // Surprisingly, that's it...
            RawState.Value = DruGaHelper.GetRNGState();
            yield return new WaitForSeconds(1.5f);
            GetComponent<NetworkObject>().Despawn();
        }
    }

    public class DruGaHelper
    {
        public enum ActionType: byte
        {
            Unused,
            Explosion,
            Health,
            Speed,
            JumpPower
        }
        public enum MultiplierType: byte
        {
            Unused,
            Multi0_5,
            Multi0_75,
            Multi1,
            Multi1_5,
            Multi2,
            Multi3
        }
        public static IReadOnlyList<DruGaHelper> statsWeights;
        public static int totalWeights = 0;


        public static ushort GetRNGState()
        {
            int explosionWeight = Mathf.RoundToInt(ConfigManager.Druga_ExplodeChance.Value * 100f);
            int selectedVal = RandomNumberGenerator.GetInt32(0, 10000); // 100 percent w. 2 dec place support

            // Explosion is selected
            if (selectedVal < explosionWeight)
                return (ushort)ActionType.Explosion;

            // Select action state other than explosion
            ActionType actionState = ActionType.Unused;
            selectedVal = RandomNumberGenerator.GetInt32(0, 3);
            switch(selectedVal)
            {
                case 0: actionState = ActionType.Health; break;
                case 1: actionState = ActionType.Speed; break;
                case 2: actionState = ActionType.JumpPower; break;
            }

            // Select the multiplier value
            selectedVal = RandomNumberGenerator.GetInt32(0, totalWeights);
            int tempWColl = 0;
            for(int i = 0; i < statsWeights.Count; i++)
            {
                int curWeight = statsWeights[i].weights;
                if (tempWColl + curWeight > selectedVal)
                    return (ushort)((ushort)actionState | (ushort)(statsWeights[i].state) << 8);
                tempWColl += curWeight;
            }

            throw new Exception("GetRNGState: Huh, this is here for control-flow purposes. If you see this, something is terribly wrong...");
        }

        [RuntimeInitializeOnLoadMethod]
        private static void SetupWeights()
        {
            statsWeights = new List<DruGaHelper>()
            {
                new DruGaHelper(MultiplierType.Multi0_5, ConfigManager.Druga_x0_5MultiChance.Value),
                new DruGaHelper(MultiplierType.Multi0_75, ConfigManager.Druga_x0_75MultiChance.Value),
                new DruGaHelper(MultiplierType.Multi1, ConfigManager.Druga_x1MultiChance.Value),
                new DruGaHelper(MultiplierType.Multi1_5, ConfigManager.Druga_x1_5MultiChance.Value),
                new DruGaHelper(MultiplierType.Multi2, ConfigManager.Druga_x2MultiChance.Value),
                new DruGaHelper(MultiplierType.Multi3, ConfigManager.Druga_x3MultiChance.Value),
            };
            totalWeights = statsWeights.Sum(k => k.weights);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PatchHealth(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo clampMethod = typeof(Mathf).GetMethod(
                "Clamp",
                new[] { typeof(int), typeof(int), typeof(int) }
            );

            for (int i = 0; i < codes.Count; i++)
            {
                // Find instruction with clamp call
                if (!codes[i].Calls(clampMethod) || i - 1 < 0)
                    continue;

                // i - 1 pulls the last param, which is the maxValue
                CodeInstruction instruct = codes[i - 1];
                if (
                    instruct.opcode == OpCodes.Ldc_I4_S &&
                    instruct.operand is sbyte val &&
                    val == 100
                  )
                {
                    // Replace the param 100 with int limit to increase max health limit
                    instruct.opcode = OpCodes.Ldc_I4;
                    instruct.operand = int.MaxValue;
                    break;
                }
            }
            return codes;
        }

        // Probability trackings
        public MultiplierType state;
        public int weights;
        public DruGaHelper(MultiplierType multiState, float percentage) 
        {
            this.state = multiState;
            this.weights = Mathf.RoundToInt(percentage * 100f);
        }
    }
}
