using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TerminalApi;
using TerminalApi.Classes;
using TerminalSeedPicker.Patches;
using UnityEngine.Windows;
using UnityEngine;
using Unity;
using static TerminalApi.Events.Events;
using static TerminalApi.TerminalApi;
using static TerminalSeedPicker.Logger;
using static TerminalSeedPicker.TerminalSeedPickerMod;
using static TerminalSeedPicker.Patches.RoundManagerPatch;
using LethalNetworkAPI;
using System.Xml.Linq;

namespace TerminalSeedPicker
{
    public class Logger
    {
        internal ManualLogSource MLS;

        public string modName = "No-Name";
        public string modVersion = "No-Ver";

        public enum LogLevelConfig
        {
            None,
            Important,
            Everything
        }

        public void Init(string modGUID = "")
        {
            MLS = BepInEx.Logging.Logger.CreateLogSource(modGUID);
        }

        public bool LogLevelAllow(LogLevelConfig severity = LogLevelConfig.Important, LogLevelConfig severity2 = LogLevelConfig.Everything)
        {
            if (severity2 == LogLevelConfig.None)
                return false;

            if (severity == LogLevelConfig.Everything)
            {
                return severity2 == LogLevelConfig.Everything;
            }

            return true;
        }

        public void Log(string text = "", BepInEx.Logging.LogLevel level = BepInEx.Logging.LogLevel.Info, LogLevelConfig severity = LogLevelConfig.Important)
        {
            bool allowed = true; // ConfigValues.logLevel == null;
            /*if (!allowed)
            {
                allowed = LogLevelAllow(severity, ConfigValues.logLevel);
            }*/

            if (allowed)
            {
                string resultText = string.Format("[{0} v{1}] - {2}", modName, modVersion, text);
                MLS.Log(level, resultText);
            }
        }
    }


    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency("LethalNetworkAPI")]
    [BepInDependency("atomic.terminalapi")]
    public class TerminalSeedPickerMod : BaseUnityPlugin
    {
        private const string modGUID = "thej01.lc.TerminalSeedPicker";
        private const string modName = "TerminalSeedPicker";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static TerminalSeedPickerMod Instance;

        public static Logger seedLogger = new Logger();

        public static LethalClientMessage<SeedNetworkData> sendSeedClientMessage = new LethalClientMessage<SeedNetworkData>("thej01.lc.TerminalSeedPicker.sendSeedClientMessage");
        public static LethalClientMessage<string> askSeedClientMessage = new LethalClientMessage<string>("thej01.lc.TerminalSeedPicker.askSeedClientMessage");
        public static LethalClientEvent replaceRandomSeedText = new LethalClientEvent("thej01.lc.TerminalSeedPicker.replaceRandomSeedText");

        public static int maxSeed = 100_000_000;

        public static List<TerminalKeyword> seedCommandGet = new List<TerminalKeyword>();

        public static List<TerminalKeyword> seedCommandSet = new List<TerminalKeyword>();

        public static SeedNetworkData seedNetworkData = new SeedNetworkData();

        public class SeedNetworkData
        {
            public int? seedToForce = null;
            public string seedWordToForce = null;
            public bool seedWasString = false;
        }

        // these are *currently* not needed to be sent on the server
        public static int? lastSeedToForce = null;
        public static string lastSeedWordToForce = null;
        public static bool lastSeedWasString = false;

        public void InitConfigValues(Logger seedLogger)
        {
            seedLogger.Log("Initialising config values...");

            seedLogger.Log("Config values initialised.");
        }

        public void InitAllConfigBinds(Logger seedLogger)
        {

        }

        public void InitConfig(Logger seedLogger)
        {
            seedLogger.Log("Initialising config...", BepInEx.Logging.LogLevel.Message, LogLevelConfig.Important);
            InitAllConfigBinds(seedLogger);
            InitConfigValues(seedLogger);
            seedLogger.Log("Config initialised.", BepInEx.Logging.LogLevel.Message, LogLevelConfig.Important);
        }

        public static string terminalMsg = "";

        public static class TerminalKeywords
        {
            public static TerminalKeyword set;
            public static TerminalKeyword s;

            public static TerminalKeyword seed;

            public static TerminalKeyword get;
            public static TerminalKeyword check;
            public static TerminalKeyword g;
            public static TerminalKeyword c;
        }

        public static class TerminalNodes
        {
            public static TerminalNode setSeed;
            public static TerminalNode getSeed;
        }

        public static string GetNumWord(string text = "", int targetWordNum = 1)
        {
            int curWord = 1;
            int curChar = 0;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in text)
            {
                curChar++;
                if (char.IsWhiteSpace(c))
                {
                    if (curWord >= targetWordNum)
                        return stringBuilder.ToString();

                    curWord++;
                    stringBuilder.Clear();
                }
                else
                {
                    stringBuilder.Append(c);
                }

                if (curChar >= text.Length && curWord < targetWordNum)
                {
                    seedLogger.Log("Reached the end of word before reaching targetWordNum, returning blank.", LogLevel.Warning, LogLevelConfig.Important);
                    return "";
                }
            }
            return stringBuilder.ToString();
        }

        public static string StartStringAtWord(string text = "", string targetWordCut = "")
        {
            int curChar = 0;
            int lastWhiteSpaceIndex = 0;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in text)
            {
                curChar++;
                if (char.IsWhiteSpace(c))
                {
                    if (targetWordCut == stringBuilder.ToString())
                        return text.Substring(lastWhiteSpaceIndex);

                    stringBuilder.Clear();
                    lastWhiteSpaceIndex = curChar;
                }
                else
                {
                    stringBuilder.Append(c);
                }

                if (curChar >= text.Length)
                {
                    seedLogger.Log("Reached the end of word before reaching targetWordCut, returning blank.", LogLevel.Warning, LogLevelConfig.Important);
                    return "";
                }
            }
            return stringBuilder.ToString();
        }

        public static bool IsStringOnlyNumbers(string text = "")
        {
            foreach (char c in text)
            {
                if (!char.IsNumber(c))
                {
                    return false;
                }
            }
            return true;
        }

        public static void LogTerminalAudioClips()
        {
            Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            int i = 0;
            foreach (AudioClip audioClip in terminal.syncedAudios)
            {
                string msg = string.Format("TERMINAL AUDIO CLIP {0}: {1}", i, audioClip.name);
                seedLogger.Log(msg, LogLevel.Message, LogLevelConfig.Everything);
                i++;
            }

            /* Output for v49:
             * TERMINAL AUDIO CLIP 0: PurchaseSFX
             * TERMINAL AUDIO CLIP 1: TerminalTypoError
             * TERMINAL AUDIO CLIP 2: TerminalLoadImage
             * TERMINAL AUDIO CLIP 3: TerminalAlarm
             * */
            
        }

        public static void PlayTerminalAudio(int audioIndex)
        {
            Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();

            terminal.PlayTerminalAudioClientRpc(audioIndex);
        }

        public static string RunSeedCommand()
        {
            string firstWord = GetNumWord(terminalMsg, 1);
            seedLogger.Log("Trying get keywords...", LogLevel.Message, LogLevelConfig.Important);
            foreach (TerminalKeyword keyword in seedCommandGet)
            {
                seedLogger.Log($"CUR GET KEYWORD: {keyword.word}", LogLevel.Message, LogLevelConfig.Important);
                if (keyword.word == firstWord)
                    return RunGetSeedCommand();
            }
            seedLogger.Log("Didn't find a get keyword! Trying set...", LogLevel.Message, LogLevelConfig.Important);
            foreach (TerminalKeyword keyword in seedCommandSet)
            {
                seedLogger.Log($"CUR SET KEYWORD: {keyword.word}", LogLevel.Message, LogLevelConfig.Important);
                if (keyword.word == firstWord)
                    return RunSetSeedCommand();
            }

            PlayTerminalAudio(1);
            return $"['{firstWord}' cannot be applied to seed.]\n\n";
        }

        public static string RunSetSeedCommand()
        {
            StartMatchLever lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();

            LogTerminalAudioClips();

            int audioClipIndex = 1;

            if (GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile")
            {
                PlayTerminalAudio(audioClipIndex);
                return "[Seed cannot be changed on a Challenge Moon.]\n\n";
            }

            if (!lever.triggerScript.interactable && GameNetworkManager.Instance.gameHasStarted)
            {
                PlayTerminalAudio(audioClipIndex);
                return "[Ship cannot be leaving or landing!]\n\n";
            }

            if (!lever.playersManager.inShipPhase)
            {
                PlayTerminalAudio(audioClipIndex);
                return "[Seed can only be changed while in orbit.]\n\n";
            }

            string commandStart = StartStringAtWord(terminalMsg, "seed");
            seedLogger.Log($"Parsed Command: {commandStart}", LogLevel.Message, LogLevelConfig.Important);

            string seedWord = GetNumWord(commandStart, 2);
            seedLogger.Log($"Seed word: {seedWord}", LogLevel.Message, LogLevelConfig.Important);

            if (seedWord == "" || seedWord == " ")
            {
                seedLogger.Log("Seed word was invalid! Denying.", LogLevel.Message, LogLevelConfig.Important);
                PlayTerminalAudio(audioClipIndex);
                return "[Please input a valid seed.]\n\n";
            }

            if (IsStringOnlyNumbers(seedWord))
            {
                seedNetworkData.seedWasString = false;
                seedNetworkData.seedWordToForce = null;
                string msg = "[Failed to set seed!]\n[Please try again.]\n\n";
                try
                {
                    bool bad = false;
                    seedNetworkData.seedToForce = Int32.Parse(seedWord);
                    if (seedNetworkData.seedToForce > maxSeed)
                    {
                        seedNetworkData.seedToForce = null;
                        msg = "[Seed cannot be greater than 100,000,000!]\n\n";
                        bad = true;
                    }
                    if (seedNetworkData.seedToForce < 0)
                    {
                        seedNetworkData.seedToForce = null;
                        msg = "[Seed cannot be less than 0!]\n\n";
                        bad = true;
                    }
                    if (!bad)
                    {
                        seedNetworkData.seedWordToForce = "Raw Number";
                        audioClipIndex = 2;
                        msg = $"[Moon seed has been set to {seedNetworkData.seedToForce}]\n[Raw number.]\n\n";
                        sendSeedClientMessage.SendAllClients(seedNetworkData);
                    }
                }
                catch(Exception e)
                {
                    seedLogger.Log($"Unable to parse '{seedWord}', err: {e.Message}", LogLevel.Error, LogLevelConfig.Important);
                }
                PlayTerminalAudio(audioClipIndex);
                return msg;
            }
            else
            {
                seedNetworkData.seedToForce = GenerateSeedFromString(seedWord);

                // ensure its not negative, and below max seed
                seedNetworkData.seedToForce = ((seedNetworkData.seedToForce % maxSeed + maxSeed) % maxSeed);
                seedNetworkData.seedWordToForce = seedWord;
                seedNetworkData.seedWasString = true;

                audioClipIndex = 2;
                PlayTerminalAudio(audioClipIndex);
                sendSeedClientMessage.SendAllClients(seedNetworkData);
                return $"[Moon seed has been set to {seedNetworkData.seedToForce}]\n[Generated from '{seedWord}'.]\n\n";
            }

        }

        public static string RunGetSeedCommand()
        {
            StartMatchLever lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();

            int audioClipIndex = 1;

            if (lever.playersManager.inShipPhase)
            {
                PlayTerminalAudio(audioClipIndex);
                return "[Must land on a planet first!]\n\n";
            }

            string msgSeedNum = $"[Random Seed: {StartOfRound.Instance.randomMapSeed}]";
            string msgSeedType = "\n\n";

            string logMsg = string.Format("lastSeedToForce: {0}, lastSeedWasString: {1}, lastSeedWordToForce: {2}", lastSeedToForce, lastSeedWasString, lastSeedWordToForce);

            seedLogger.Log(logMsg, LogLevel.Message, LogLevelConfig.Important);

            if (lastSeedToForce != null || GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile")
            {
                msgSeedNum = $"[Set Seed: {StartOfRound.Instance.randomMapSeed}]";
                if (lastSeedWasString)
                    msgSeedType = $"\n[Generated from: '{lastSeedWordToForce}']\n\n";
                else
                    msgSeedType = "\n[Raw Number]\n\n";

                if (GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile")
                    msgSeedType = "\n[Challenge Moon]\n\n";
            }

            return msgSeedNum + msgSeedType;

        }


        // chatgpt lol im notsmart enough to do this im sorry i just wanted to be like minecraft
        static int GenerateSeedFromString(string input)
        {
            // Convert the string into bytes
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] hashBytes = hash.ComputeHash(bytes);

                // Convert the hash bytes into an integer seed
                int seed = BitConverter.ToInt32(hashBytes, 0);
                return seed;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            seedLogger.Init(modGUID);

            seedLogger.modName = modName;
            seedLogger.modVersion = modVersion;

            seedLogger.Log("seedLogger Initialised!", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            InitConfig(seedLogger);

            seedLogger.Log("Patching TerminalSeedPickerMod...", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);
            harmony.PatchAll(typeof(TerminalSeedPickerMod));
            seedLogger.Log("Patched TerminalSeedPickerMod.", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            seedLogger.Log("Patching TerminalPatch...", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);
            harmony.PatchAll(typeof(TerminalPatch));
            seedLogger.Log("Patched TerminalPatch.", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            seedLogger.Log("Patching StartOfRoundPatch...", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);
            harmony.PatchAll(typeof(StartOfRoundPatch));
            seedLogger.Log("Patched StartOfRoundPatch.", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            seedLogger.Log("Patching RoundManagerPatch...", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);
            harmony.PatchAll(typeof(RoundManagerPatch));
            seedLogger.Log("Patched RoundManagerPatch.", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            seedLogger.Log("Patching PlayerControllerBPatch...", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);
            harmony.PatchAll(typeof(PlayerControllerBPatch));
            seedLogger.Log("Patched PlayerControllerBPatch.", BepInEx.Logging.LogLevel.Info, LogLevelConfig.Everything);

            seedLogger.Log("Initialising terminal commands...", BepInEx.Logging.LogLevel.Message, LogLevelConfig.Important);

            TerminalNodes.setSeed = CreateTerminalNode("Hello world!!!\n", true);
            TerminalNodes.getSeed = CreateTerminalNode("Hello world 2!!!\n", true);

            TerminalKeywords.set = CreateTerminalKeyword("set", true);
            TerminalKeywords.s = CreateTerminalKeyword("s", true);

            TerminalKeywords.seed = CreateTerminalKeyword("seed", false, TerminalNodes.setSeed);

            TerminalKeywords.get = CreateTerminalKeyword("get", true);
            TerminalKeywords.g = CreateTerminalKeyword("g", true);
            TerminalKeywords.check = CreateTerminalKeyword("check", true);
            TerminalKeywords.c = CreateTerminalKeyword("c", true);



            TerminalKeywords.set = TerminalKeywords.set.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.setSeed);
            TerminalKeywords.s = TerminalKeywords.s.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.setSeed);

            TerminalKeywords.get = TerminalKeywords.get.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.getSeed);
            TerminalKeywords.g = TerminalKeywords.g.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.getSeed);
            TerminalKeywords.check = TerminalKeywords.check.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.getSeed);
            TerminalKeywords.c = TerminalKeywords.c.AddCompatibleNoun(TerminalKeywords.seed, TerminalNodes.getSeed);

            TerminalKeywords.seed.defaultVerb = TerminalKeywords.set;

            AddTerminalKeyword(TerminalKeywords.seed, new CommandInfo()
            {
                Title = "[Set/Get] SEED [Seed]",
                TriggerNode = TerminalNodes.setSeed,
                DisplayTextSupplier = RunSeedCommand,
                Category = "Other",
                Description = "Type set to set seed, Text or numbers allowed.\nType get to get info on the current seed."
            });

            seedCommandSet.Add(TerminalKeywords.set);
            seedCommandSet.Add(TerminalKeywords.s);

            seedCommandGet.Add(TerminalKeywords.get);
            seedCommandGet.Add(TerminalKeywords.g);
            seedCommandGet.Add(TerminalKeywords.check);
            seedCommandGet.Add(TerminalKeywords.c);

            seedLogger.Log("Initialised terminal commands!", BepInEx.Logging.LogLevel.Message, LogLevelConfig.Important);

            sendSeedClientMessage.OnReceivedFromClient += ReceiveFromClient;
            askSeedClientMessage.OnReceivedFromClient += ReceiveAskSeed;
            replaceRandomSeedText.OnReceivedFromClient += ReceiveRandomSeedEvent;
        }

        private void ReceiveFromClient(SeedNetworkData data, ulong clientId)
        {
            seedNetworkData = data;
            seedLogger.Log($"Recieved seedNetworkData Client MSG from {clientId}", BepInEx.Logging.LogLevel.Message, LogLevelConfig.Important);
        }

        private void ReceiveAskSeed(string data, ulong clientId)
        {
            // send it to all clients but the server is the only one that sends back... kinda silly, idk if theres a better way of doing this.
            if (data == "sendSeed" && GameNetworkManager.Instance.localPlayerController.IsServer)
                sendSeedClientMessage.SendAllClients(seedNetworkData);
        }

        private void ReceiveRandomSeedEvent(ulong clientId)
        {
            seedLogger.Log($"Replacing random seed text! Sent from {clientId}", LogLevel.Message, LogLevelConfig.Important);
            
            seedLogger.Log("Level loaded, initialising variables.", LogLevel.Message, LogLevelConfig.Important);

            newLoadingText = "";

            if (seedNetworkData.seedToForce != null || GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile")
                newLoadingText = GetText();
            else
                seedLogger.Log("seedNetworkData.seedToForce was null or not on a challenge moon! Not changing text.", LogLevel.Message, LogLevelConfig.Important);

            lastSeedWordToForce = seedNetworkData.seedWordToForce;
            lastSeedToForce = seedNetworkData.seedToForce;
            lastSeedWasString = seedNetworkData.seedWasString;

            seedNetworkData.seedWordToForce = null;
            seedNetworkData.seedToForce = null;
            seedNetworkData.seedWasString = false;
        }
    }
}
