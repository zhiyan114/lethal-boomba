using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class DruGaBehavior : GrabbableObject
    {
        private AudioSource AudioSrc;
        [SerializeField]
        private AudioClip EatSfx;
        private float eatCountDown;

        void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
            eatCountDown = EatSfx.length + 0.250f;
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {

        }
    }

    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerPatcher
    {
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
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
                if (codes[i].Calls(clampMethod) && i - 1 >= 0)
                {
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
            }
            return codes;
        }
    }
}
