using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using static TerminalSeedPicker.TerminalSeedPickerMod;
using static TerminalSeedPicker.Logger;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class StartOfRoundPatch
    {
        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPrefix]
        public static void PreStartGame(ref StartOfRound __instance)
        {
            if (seedNetworkData.seedToForce != null)
            {
                string seedOriginMsg = seedNetworkData.seedWasString ? $"Generated from: {seedNetworkData.seedWordToForce}" : seedNetworkData.seedWordToForce;
                string msg = string.Format("Seed **should** be overridden by {0} ({1})", seedNetworkData.seedToForce, seedOriginMsg);
                seedLogger.Log(msg, LogLevel.Message, LogLevelConfig.Important);

                __instance.overrideRandomSeed = true;
                __instance.overrideSeedNumber = (int)seedNetworkData.seedToForce;
            }
        }
    }
}
