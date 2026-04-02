using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static TerminalSeedPicker.Plugin;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPatch
    {
        // This runs on host only
        [HarmonyPatch(nameof(StartOfRound.StartGame))]
        [HarmonyPrefix]
        public static void ApplyNewSeed(StartOfRound __instance)
        {
            // If we've modified the seed, and we aren't on a challenge moon, apply the new seed.
            if (nextMoonSeedInfo.seedType != SeedType.Random && !StartOfRound.Instance.isChallengeFile)
            {
                __instance.overrideRandomSeed = true;
                __instance.overrideSeedNumber = nextMoonSeedInfo.seedNum;
            }
        }
    }
}
