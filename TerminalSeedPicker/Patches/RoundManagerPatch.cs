using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static TerminalSeedPicker.Plugin;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(RoundManager))]
    public class RoundManagerPatch
    {
        [HarmonyPatch(nameof(RoundManager.GenerateNewLevelClientRpc))]
        [HarmonyPostfix]
        public static void ChangeText(RoundManager __instance)
        {
            // Initialize current moon seed info
            if (nextMoonSeedInfo.seedType != SeedType.Random && !StartOfRound.Instance.isChallengeFile)
                currentMoonSeedInfo = nextMoonSeedInfo.Copy();
            else
                currentMoonSeedInfo.InitFromCurrentMoon();

            // Change the text if we modified the seed in any way
            if (currentMoonSeedInfo.seedType != SeedType.Random)
                HUDManager.Instance.loadingText.text = currentMoonSeedInfo.GetLoadingText();
        }

        [HarmonyPatch(nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        public static void ResetNextMoonInfo()
        {
            nextMoonSeedInfo.Reset();
        }

        [HarmonyPatch(nameof(RoundManager.LoadNewLevel))]
        [HarmonyPostfix]
        public static void DisableOverride()
        {
            StartOfRound.Instance.overrideRandomSeed = false;
        }
    }
}
