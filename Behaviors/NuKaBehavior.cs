using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class NuKaBehavior : GrabbableObject
    {
        [SerializeField]
        private AudioClip preExplodeSound;
        [SerializeField]
        private float countdownSec;
        [SerializeField]
        private float beforeKillcdSec;

        private AudioSource BombSrc;
        NetworkVariable<bool> isDetonated = new NetworkVariable<bool>(false);

        private void Awake()
        {
            BombSrc = GetComponents<AudioSource>()[1];
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
            if (!NetworkManager.Singleton.IsServer)
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

            // Show dramatic nuke effect here
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
