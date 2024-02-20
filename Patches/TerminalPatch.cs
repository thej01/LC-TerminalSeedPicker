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
    [HarmonyPatch(typeof(Terminal))]
    internal class TerminalPatch
    {
        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPrefix]
        public static void PreParsePlayerSentence(ref Terminal __instance)
        {
            string text = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            text = RemovePunctuationWhyCantIUseItFromInstance(text);
            terminalMsg = text;

            string msg = string.Format("Terminal String: {0}", terminalMsg);
            seedLogger.Log(msg, LogLevel.Message, LogLevelConfig.Important);
        }

        public static string RemovePunctuationWhyCantIUseItFromInstance(string s)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in s)
            {
                if (!char.IsPunctuation(c))
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().ToLower();
        }
    }
}
