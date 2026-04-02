using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerControllerBPatch
    {
        // Reset seed info when joining a new game
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyPostfix]
        public static void ResetSeedInfo()
        {
            Plugin.currentMoonSeedInfo.Reset();
            Plugin.nextMoonSeedInfo.Reset();

            StartOfRound.Instance.overrideRandomSeed = false;

            // Sync seed when joining
            if (!GameNetworkManager.Instance.localPlayerController.IsServer)
                Plugin.requestSeedFromClient.InvokeServer();
        }
    }
}
