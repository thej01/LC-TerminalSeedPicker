using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TerminalSeedPicker.Logger;
using static TerminalSeedPicker.TerminalSeedPickerMod;
using BepInEx.Logging;
using UnityEngine;
using GameNetcodeStuff;

namespace TerminalSeedPicker.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PlayerControllerBPatch
    {
        // patching this method is kinda dumb, but it works
        [HarmonyPatch(nameof(PlayerControllerB.SpawnPlayerAnimation))]
        [HarmonyPostfix]
        static void PostSpawnIn()
        {
            seedLogger.Log("Spawned in! Initialising, then asking for seed data!", LogLevel.Message, LogLevelConfig.Important);

            seedNetworkData.seedToForce = null;
            lastSeedToForce = null;

            seedNetworkData.seedWasString = false;
            lastSeedWasString = false;

            seedNetworkData.seedWordToForce = null;
            lastSeedWordToForce = null;

            StartOfRound.Instance.overrideRandomSeed = false;

            seedLogger.Log("Asking now...", LogLevel.Message, LogLevelConfig.Important);

            // only the server host replies back to this
            askSeedClientMessage.SendAllClients("sendSeed");
        }
    }
}
