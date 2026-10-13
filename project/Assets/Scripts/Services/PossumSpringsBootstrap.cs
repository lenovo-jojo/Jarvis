using System.IO;
using UnityEngine;

namespace AICompanion.Services
{
    [DefaultExecutionOrder(-100)]
    public sealed class PossumSpringsBootstrap :
        MonoBehaviour
    {
        private void Start()
        {
            Transform spawn =
                transform.Find("ParkingLotSpawn");
            if (spawn == null)
            {
                spawn =
                    transform.Find("PlayerSpawn");
            }
            if (spawn == null)
            {
                Debug.LogError(
                    "[PossumSprings] PlayerSpawn is missing.");
                return;
            }

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogError(
                    "[PossumSprings] Main camera is missing.");
                return;
            }

            CompanionCamera companionCamera =
                worldCamera.GetComponent<CompanionCamera>();
            if (companionCamera == null)
            {
                companionCamera =
                    worldCamera.gameObject.AddComponent<
                        CompanionCamera>();
            }

            Bounds worldBounds =
                CalculateWorldBounds();
            companionCamera.ConfigureWorldNavigation(
                worldBounds.min.y,
                worldBounds.max.y + 20f,
                6f);

            GameObject serviceObject =
                new GameObject("PossumSpringsVRMService");
            serviceObject.transform.SetParent(
                transform,
                false);
            VRMService vrmService =
                serviceObject.AddComponent<VRMService>();

            string playerPath =
                Path.Combine(
                    Application.dataPath,
                    "Characters",
                    "jojo4.vrm");
            vrmService.LoadPlayerVRM(
                playerPath,
                onLoaded: player =>
                {
                    player.transform.SetPositionAndRotation(
                        spawn.position,
                        Quaternion.identity);
                    companionCamera.SetPlayerAvatar(
                        player.transform);
                    companionCamera.SetTrackedTarget(
                        player.transform);
                    companionCamera.SetPlayerViewStyle(
                        CompanionCamera.PlayerViewStyle
                            .OverTheShoulder);
                    companionCamera.SetExploreMode(true);
                    PossumSpringsMiniMap miniMap =
                        gameObject.AddComponent<
                            PossumSpringsMiniMap>();
                    miniMap.Initialize(
                        player.transform,
                        worldBounds,
                        companionCamera);
                    PossumSpringsConversationController
                        conversation =
                            gameObject.AddComponent<
                                PossumSpringsConversationController>();
                    conversation.Initialize(
                        player.transform,
                        vrmService,
                        miniMap);
                    PossumSpringsMaeController mae =
                        gameObject.AddComponent<
                            PossumSpringsMaeController>();
                    mae.Initialize(
                        player.transform,
                        conversation);
                    Debug.Log(
                        "[PossumSprings] Jojo loaded at " +
                        $"the town spawn: {spawn.position}.");
                },
                onError: error =>
                    Debug.LogError(
                        "[PossumSprings] " + error));
        }

        private Bounds CalculateWorldBounds()
        {
            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds =
                new Bounds(
                    transform.position,
                    Vector3.one);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            return bounds;
        }
    }
}
