using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace LethalBoomba
{
    public static class Utils
    {
        public static void Explode(
        Vector3 position,
        float killRadius,
        int damage = 999,
        float force = 10f)
        {
            if (!NetworkManager.Singleton.IsClient) return;
            GameObject Explosion = UnityEngine.Object.Instantiate(StartOfRound.Instance.explosionPrefab, position, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
            Explosion.SetActive(value: true);

            Collider[] hits = Physics.OverlapSphere(position, killRadius, LayerMask.GetMask("Player", "Enemies"));

            foreach (Collider hit in hits)
            {
                // Player Eliminator
                PlayerControllerB? player = hit.GetComponentInParent<PlayerControllerB>();
                if (player != null)
                {
                    float dist = Vector3.Distance(player.transform.position, position);

                    Vector3 dir = (player.transform.position - position).normalized;
                    Vector3 knockback = dir * (force / Mathf.Max(dist, 0.1f));

                    if (dist <= killRadius)
                        player.KillPlayer(knockback, spawnBody: true, CauseOfDeath.Blast);
                }

                // Enemy Eliminator
                EnemyAI? enemy = hit.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    float dist = Vector3.Distance(enemy.transform.position, position);

                    if (dist <= killRadius)
                        enemy.KillEnemyServerRpc(enemy.enemyType.destroyOnDeath || !enemy.enemyType.canDie);
                }
            }

            // Optional: camera shake (client-side effect)
            if (GameNetworkManager.Instance.localPlayerController != null)
            {
                float localDist = Vector3.Distance(
                    GameNetworkManager.Instance.localPlayerController.transform.position,
                    position
                );

                HUDManager.Instance.ShakeCamera(
                    localDist < killRadius
                        ? ScreenShakeType.Big
                        : ScreenShakeType.Small
                );
            }
        }
        /// <summary>
        /// This is intended to hide networked GameObject from player until server invokes cleanup
        /// </summary>
        /// <param name="obj">GameObject to hide</param>
        public static void HideNetObject(GameObject obj)
        {
            if (obj.TryGetComponent<Renderer>(out Renderer render))
                render.enabled = false;
            if (obj.TryGetComponent<Collider>(out Collider collide))
                collide.enabled = false;
        }
    }
}
