using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class BoomBaBehavior : GrabbableObject
    {
        public AudioClip preExplodeSound;
        public float countdownSec;
        public AudioClip preExplodeFinalSound;
        public float preExplodeFinalCD;
        private AudioSource BombSrc;
        private float blastRadius = 5f;
        NetworkVariable<bool> isDetonated = new NetworkVariable<bool>(false);

        public AnimationCurve grenadeFallCurve;
        public AnimationCurve grenadeVerticalFallCurve;
        public AnimationCurve grenadeVerticalFallCurveNoBounce;
        private int grenadeThrowMask = 268437761;

        private void Awake()
        {
            BombSrc = GetComponent<AudioSource>();
            grabbable = true;
            isInFactory = true;
            itemProperties = ItemManager.GetItem("B00mba");
            countdownSec = 5f;
            preExplodeFinalCD = 3.5f;
            preExplodeSound = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/BoomBa/Sounds/BoomBaCountdown.ogg");
            preExplodeFinalSound = ItemManager.bundle.LoadAsset<AudioClip>("Assets/AssetBundles/BombToolkit/BoomBa/Sounds/B00mbaBoom.ogg");

            grenadeFallCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            grenadeVerticalFallCurve = new AnimationCurve(
                new Keyframe(0f,    0f,     0f,  0f),
                new Keyframe(0.35f, -0.35f, 0f,  0f),
                new Keyframe(1f,    1f,     0f,  0f)
            );
            grenadeVerticalFallCurveNoBounce = new AnimationCurve(
                new Keyframe(0f,   0f,     0f, 0f),
                new Keyframe(0.4f, -0.2f,  0f, 0f),
                new Keyframe(1f,   1f,     0f, 0f)
            );
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

            // Throw the activated explosive immediately (no server roundtrip), matching egg behaviour
            if (playerHeldBy != null)
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetGrenadeThrowDestination());
        }

        public override void SetControlTipsForItem()
        {
            HUDManager.Instance.ChangeControlTipMultiple(new string[] {
                isDetonated.Value ? "Throw Bomb : [LMB]" : "Detonate Bomb : [LMB]"
            }, holdingItem: true, itemProperties);
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
            if (!NetworkManager.Singleton.IsServer)
                yield break;
            ExecuteStageClientRpc();

            // 3s buffers (mostly to let audio play out and buffers)
            yield return new WaitForSeconds(countdownSec + preExplodeFinalCD + 3);
            GetComponent<NetworkObject>().Despawn();
        }

        private IEnumerator CountdownRoutine()
        {
            if(!NetworkManager.Singleton.IsClient)
                yield break;

            if (!StartOfRound.Instance.shipHasLanded)
            {
                // Prevent inventory bug when item is despawn while in pocket
                if(IsOwner)
                    playerHeldBy?.DiscardHeldObject();
                    
                grabbable = false;
            }

            BombSrc.PlayOneShot(preExplodeSound);
            yield return new WaitForSeconds(countdownSec);
            BombSrc.PlayOneShot(preExplodeFinalSound);
            yield return new WaitForSeconds(preExplodeFinalCD);
            Utils.Explode(GetComponent<Transform>().position, blastRadius);
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            Utils.HideNetObject(gameObject);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ExecuteStageClientRpc()
        {
            StartCoroutine(CountdownRoutine());
        }

        private Vector3 GetGrenadeThrowDestination()
        {
            Camera plrCam = playerHeldBy.gameplayCamera;
            Ray grenadeThrowRay = new Ray(plrCam.transform.position, plrCam.transform.forward);
            Vector3 position = (!Physics.Raycast(grenadeThrowRay, out RaycastHit grenadeHit, 12f, grenadeThrowMask, QueryTriggerInteraction.Ignore))
                ? grenadeThrowRay.GetPoint(10f)
                : grenadeThrowRay.GetPoint(grenadeHit.distance - 0.05f);
            grenadeThrowRay = new Ray(position, Vector3.down);
            if (Physics.Raycast(grenadeThrowRay, out grenadeHit, 30f, grenadeThrowMask, QueryTriggerInteraction.Ignore))
                return grenadeHit.point + Vector3.up * (itemProperties.verticalOffset + 0.05f);
            return grenadeThrowRay.GetPoint(30f);
        }

        public override void FallWithCurve()
        {
            float magnitude = (startFallingPosition - targetFloorPosition).magnitude;
            base.transform.rotation = Quaternion.Lerp(base.transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, base.transform.eulerAngles.y, itemProperties.restingRotation.z), 14f * Time.deltaTime / magnitude);
            base.transform.localPosition = Vector3.Lerp(startFallingPosition, targetFloorPosition, grenadeFallCurve.Evaluate(fallTime));
            if (magnitude > 5f)
                base.transform.localPosition = Vector3.Lerp(new Vector3(base.transform.localPosition.x, startFallingPosition.y, base.transform.localPosition.z), new Vector3(base.transform.localPosition.x, targetFloorPosition.y, base.transform.localPosition.z), grenadeVerticalFallCurveNoBounce.Evaluate(fallTime));
            else
                base.transform.localPosition = Vector3.Lerp(new Vector3(base.transform.localPosition.x, startFallingPosition.y, base.transform.localPosition.z), new Vector3(base.transform.localPosition.x, targetFloorPosition.y, base.transform.localPosition.z), grenadeVerticalFallCurve.Evaluate(fallTime));
            fallTime += Mathf.Abs(Time.deltaTime * 12f / magnitude);
        }
    }
}
