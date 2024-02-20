using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using static TerminalSeedPicker.TerminalSeedPickerMod;
using static TerminalSeedPicker.Logger;
using static System.Net.Mime.MediaTypeNames;
using System.CodeDom;
using System.Collections;
using UnityEngine;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch
    {
        public static string newLoadingText = "";

        [HarmonyPatch(nameof(RoundManager.GenerateNewLevelClientRpc))]
        [HarmonyPostfix]
        public static void PostGenerateNewLevel()
        {
            /* mfw i tried to add this method 6 hours ago not realising seedNetworkData.seedToForce became null
             * before it was call, not adding a log statement for the null check like a dumbass
             * so i thought it was because the text was like being overwritten or something
             * so i tried changing it through IL code then i remembered i dont know IL code
             * go insane trying to figure IL code for a few hours then just try this again out of desperation with
             * a string that says "please work" that is set no matter what
             * 
             * it works
             * i realise that seedNetworkData.seedToForce was becoming null before this method was being called because im stupid
             * 
             * i cry
             * */

            if (newLoadingText != "" && HUDManager.Instance.loadingText.text.Contains("Random"))
            {
                HUDManager.Instance.loadingText.text = newLoadingText;
                seedLogger.Log($"Random Seed text succesfully replaced with: '{newLoadingText}'!", LogLevel.Message, LogLevelConfig.Important);
            }
        }

        [HarmonyPatch(nameof(RoundManager.LoadNewLevel))]
        [HarmonyPostfix]
        public static void PostLoadLevel()
        {

            replaceRandomSeedText.InvokeAllClients();

            StartOfRound.Instance.overrideRandomSeed = false;
        }

        public static string GetText()
        {
            string msg = $"Set Seed: {seedNetworkData.seedToForce}";
            string seedOrigin = "";

            if (seedNetworkData.seedWasString)
                seedOrigin = $"\n(Generated from: {seedNetworkData.seedWordToForce})";
            else
                seedOrigin = $"\n{seedNetworkData.seedWordToForce}";

            if (GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile")
                seedOrigin = "\n(Challenge Moon)";

            msg = msg + seedOrigin;
            seedLogger.Log($"Intended seed text: {msg}", LogLevel.Message, LogLevelConfig.Important);

            return msg;

        }
    }
}
