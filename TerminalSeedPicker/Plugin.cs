using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalNetworkAPI;
using System;
using System.Collections.Generic;
using TerminalApi;
using TerminalApi.Classes;
using UnityEngine;
using static TerminalApi.TerminalApi;

namespace TerminalSeedPicker
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(LethalNetworkAPI.MyPluginInfo.PLUGIN_GUID)]
    [BepInDependency("atomic.terminalapi")]
    public class Plugin : BaseUnityPlugin
    {
        public enum TerminalSounds
        {
            PurchaseSFX = 0,
            TypoError = 1,
            LoadImage = 2,
            Alarm = 3,
        }

        public const string modGUID = "thej01.lc.TerminalSeedPicker";
        public const string modName = "TerminalSeedPicker";
        public const string modVersion = "2.0.0";

        public static LNetworkMessage<SeedInfo> syncNextMoonSeedInfo = LNetworkMessage<SeedInfo>.Connect($"{modGUID}.syncNextMoonSeedInfo", onClientReceived: SyncNextMoonSeedInfo, 
                                                                                                           onClientReceivedFromClient: SyncNextMoonSeedInfo, onServerReceived: SyncNextMoonSeedInfo);

        public static LNetworkEvent requestSeedFromClient = LNetworkEvent.Connect($"{modGUID}.requestSeedFromClient", onServerReceived: RequestSeedFromClient);

        public const int minSeed = 1;

        public const int maxSeed = 100_000_000;

        public static readonly Harmony harmony = new Harmony(modGUID);

        public static ManualLogSource Log = new ManualLogSource(modGUID);

        public static TerminalNode seedNode = CreateTerminalNode("this shouldn't appear\n\n", true);
        public static TerminalKeyword seedKeyword = CreateTerminalKeyword("seed");

        public static Terminal? terminal = null;

        /// <summary>
        /// The variables of the current moon (if landed)
        /// </summary>
        public static SeedInfo currentMoonSeedInfo = new SeedInfo();
        /// <summary>
        /// The variables to apply to the next moons
        /// </summary>
        public static SeedInfo nextMoonSeedInfo = new SeedInfo();

        public static class SeedVerbs
        {
            public static List<TerminalKeyword> setVerbs = new List<TerminalKeyword>
            {
                CreateTerminalKeyword("set", true),
                CreateTerminalKeyword("s", true),
                CreateTerminalKeyword("change", true),
                CreateTerminalKeyword("modify", true),
                CreateTerminalKeyword("alter", true),
            };

            public static List<TerminalKeyword> getVerbs = new List<TerminalKeyword>
            {
                CreateTerminalKeyword("get", true),
                CreateTerminalKeyword("g", true),
                CreateTerminalKeyword("recieve", true),
                CreateTerminalKeyword("check", true),
                CreateTerminalKeyword("see", true),
                CreateTerminalKeyword("obtain", true),
                CreateTerminalKeyword("print", true),
            };

            public static List<TerminalKeyword> randomVerbs = new List<TerminalKeyword>
            {
                CreateTerminalKeyword("random", true),
                CreateTerminalKeyword("rand", true),
                CreateTerminalKeyword("r", true),
                CreateTerminalKeyword("randomize", true),
                CreateTerminalKeyword("randomise", true),
                CreateTerminalKeyword("generate", true),
                CreateTerminalKeyword("gen", true),
                CreateTerminalKeyword("choose", true),
            };
        }

        public enum SeedType
        {
            /// <summary>
            /// Completely random, auto determined by LC.
            /// </summary>
            Random,
            /// <summary>
            /// Set by typing "set seed [num]" in the terminal
            /// </summary>
            SetNum,
            /// <summary>
            /// Set by typing "set seed [string]" in the terminal
            /// </summary>
            SetString,
            /// <summary>
            /// Set by typing "randomize seed" in the terminal
            /// </summary>
            SetRandomized,
            /// <summary>
            /// Set by challenge moon.
            /// </summary>
            SetChallengeMoon,
        }

        /// <summary>
        /// Info on a seed
        /// </summary>
        [Serializable]
        public class SeedInfo
        {
            [SerializeField]
            public int seedNum = 0;

            [SerializeField]
            public SeedType seedType = SeedType.Random;

            [SerializeField]
            public string seedWord = "";

            public SeedInfo(bool fromCurrentMoon = false)
            {
                if (fromCurrentMoon)
                    InitFromCurrentMoon();
                else
                    Reset();
            }

            public SeedInfo(int seedNum, SeedType seedType, string seedWord = "")
            {
                this.seedNum = seedNum;
                this.seedWord = seedWord;
                this.seedType = seedType;
            }

            public void Reset()
            {
                seedNum = 0;
                seedWord = "";
                seedType = SeedType.Random;
            }

            public void InitFromCurrentMoon()
            {
                Reset();
                seedNum = StartOfRound.Instance.randomMapSeed;
                seedType = SeedType.Random;
                if (StartOfRound.Instance.isChallengeFile)
                    seedType = SeedType.SetChallengeMoon;
            }

            /// <summary>
            /// Get the second line of the terminal when using any seed related terminal command.
            /// </summary>
            /// <returns></returns>
            public string GetTerminalSecondLine()
            {
                string secondLine = "";

                switch (seedType)
                {
                    case SeedType.SetString:
                        secondLine = $"[Generated from '{seedWord}']";
                        break;

                    case SeedType.SetRandomized:
                        secondLine = "[Randomly generated]";
                        break;

                    case SeedType.SetNum:
                        break;

                    case SeedType.SetChallengeMoon:
                        secondLine = "[Challenge Moon]";
                        break;

                    case SeedType.Random:
                        break;

                    default:
                        secondLine = $"UNDEFINED SEEDTYPE: {seedType}";
                        break;
                }

                return secondLine;
            }

            /// <summary>
            /// Displays when doing "Set seed [SEED]"
            /// OR when doing "Randomize seed"
            /// </summary>
            /// <returns></returns>
            public string TerminalTextWhenSet()
            {
                string secondLine = GetTerminalSecondLine();
                string text = $"[Seed has been set to {seedNum}.]";

                if (!secondLine.IsNullOrWhiteSpace())
                    text += "\n";
                text += secondLine + "\n\n";

                return text;
            }

            /// <summary>
            /// Displays when doing "Get seed"
            /// </summary>
            /// <returns></returns>
            public string GetTerminalEntry()
            {
                string secondLine = GetTerminalSecondLine();
                string text = $"[Random moon seed is {seedNum}]";
                if (seedType != SeedType.Random)
                    text = $"[Set moon seed is {seedNum}]";

                if (!secondLine.IsNullOrWhiteSpace())
                    text += "\n";
                text += secondLine + "\n\n";

                return text;
            }

            /// <summary>
            /// Text when loading into a moon
            /// </summary>
            /// <returns></returns>
            public string GetLoadingText()
            {
                string text = $"Set Seed: {seedNum}";

                string origin = "";

                switch (seedType)
                {
                    case SeedType.SetNum:
                        break;

                    case SeedType.SetString:
                        origin = $"(Generated from: '{seedWord}')";
                        break;

                    case SeedType.SetRandomized:
                        origin = "(Randomly Generated)";
                        break;

                    case SeedType.SetChallengeMoon:
                        origin = "(Challenge Moon)";
                        break;

                    default:
                        origin = "(Unknown Origin)";
                        break;
                }

                if (!origin.IsNullOrWhiteSpace())
                    text += " " + origin;

                return text;
            }

            public SeedInfo Copy()
            {
                return new SeedInfo(seedNum, seedType, seedWord);
            }
        }

        public static void AddVerbToSeed(List<TerminalKeyword> keywords)
        {
            foreach (TerminalKeyword verb in keywords)
            {
                verb.AddCompatibleNoun(seedKeyword, seedNode);
                AddTerminalKeyword(verb);
            }
        }

        public void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{modName} {modVersion} is loading...");

            Log.LogInfo("Loading patches...");

            harmony.PatchAll();

            Log.LogInfo("Loaded patches.");

            Log.LogInfo("Adding terminal commands...");

            AddVerbToSeed(SeedVerbs.setVerbs);
            AddVerbToSeed(SeedVerbs.getVerbs);
            AddVerbToSeed(SeedVerbs.randomVerbs);

            seedKeyword.defaultVerb = SeedVerbs.getVerbs[0];

            AddTerminalKeyword(seedKeyword, new CommandInfo()
            {
                Title = "[Set/Get/Random] SEED [Seed]",
                TriggerNode = seedNode,
                DisplayTextSupplier = RunSeedCommand,
                Category = "Other",
                Description = "To modify the current moon seed or get information about it. Defaults to \"Get\"" +
                              "\nType \"Set SEED [Seed]\" to set a new moon seed. [Seed] can be text OR numbers." +
                              "\nType \"Get SEED\" to have the terminal print info on the current seed." +
                              "\nType \"Random SEED\" to set the moon seed to a completely random one."
            });

            Log.LogInfo("Added terminal commands.");

            Log.LogInfo($"{modName} {modVersion} is loaded.");
        }

        // Commmands

        // We ran ONE of the commands, we are unsure which exactly as to my knowledge there is no way to see which
        // verbs were used in a command with TerminalAPI
        public static string RunSeedCommand()
        {
            // Split input by spaces (removing whitespace at start and end because we don't need it)
            string[] inputSplit = GetTerminalInput().Trim().Split(' ');

            // find first instance of word "seed", and start parsing from the input before that
            bool startCopy = false;
            List<string> inputSplitShortened = new List<string>();
            for (int i = 0; i < inputSplit.Length; i++)
            {
                if (startCopy)
                    inputSplitShortened.Add(inputSplit[i]);
                else
                {
                    if (inputSplit[i].ToLower() == seedKeyword.word)
                    {
                        // Begin parsing input before "seed"
                        startCopy = true;
                        i -= 2;
                        if (i < -1)
                            i = -1;
                    }
                }
            }

            inputSplit = inputSplitShortened.ToArray();

            string specifiedCommand = inputSplit[0].ToLower();
            // Check every keyword, see if we can find a correlating command.
            foreach (TerminalKeyword keyword in SeedVerbs.setVerbs)
            {
                if (keyword.word == specifiedCommand)
                    return SetSeedCommand(inputSplit);
            }

            foreach (TerminalKeyword keyword in SeedVerbs.getVerbs)
            {
                if (keyword.word == specifiedCommand)
                    return GetSeedCommand();
            }

            foreach (TerminalKeyword keyword in SeedVerbs.randomVerbs)
            {
                if (keyword.word == specifiedCommand)
                    return RandomSeedCommand();
            }

            // Default to get seed
            return GetSeedCommand();
        }

        public static string SetSeedCommand(string[] inputSplitOriginal)
        {
            string[] inputSplit = new string[3];
            bool foundSeed = false;
            bool foundSeedThisIteration = false;
            string seedString = "";
            inputSplit[0] = inputSplitOriginal[0];
            inputSplit[1] = inputSplitOriginal[1];
            for (int i = 2; i < inputSplitOriginal.Length; i++)
            {
                foundSeedThisIteration = false;

                // Find first input after the first two that isn't blank
                if (!inputSplitOriginal[i].IsNullOrWhiteSpace() && !foundSeed)
                {
                    // Start building seed string
                    foundSeed = true;
                    foundSeedThisIteration = true;
                    inputSplit[2] = inputSplitOriginal[i];
                }

                if (foundSeed)
                {
                    // Append to seed string, adding a space if needed.
                    // This removes any extra spaces in the string, to reduce confusion.
                    if (inputSplitOriginal[i].IsNullOrWhiteSpace())
                        continue;

                    if (!foundSeedThisIteration)
                        seedString += " ";
                    seedString += inputSplitOriginal[i];
                }
                    
            }

            // Remove extra whitespace at start and end of the seed string, as it's unintuitive
            seedString.Trim();

            // Remove certain characters that the loading screen font doesn't quite like
            seedString = seedString.Replace("\"", "");
            seedString = seedString.Replace("&", "");
            seedString = seedString.Replace("^", "");
            seedString = seedString.Replace("@", "");
            seedString = seedString.Replace("+", "");
            seedString = seedString.Replace("~", "");

            // Missing seed
            if (inputSplitOriginal.Length < 3 || !foundSeed || seedString.IsNullOrWhiteSpace())
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return "[Seed is empty!]\n\n";
            }

            bool isNumSeed = false;
            int numSeed;
            isNumSeed = int.TryParse(seedString, out numSeed);

            if (isNumSeed && numSeed > maxSeed)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return $"[Seed cannot be greater than {maxSeed.ToString("N0")}!]\n\n";
            }

            if (isNumSeed && numSeed < minSeed)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return $"[Seed cannot be less than {minSeed.ToString("N0")}!]\n\n";
            }

            if (!RoundManager.Instance.playersManager.inShipPhase)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return "[Seed cannot be changed after landing.]\n\n";
            }

            if (StartOfRound.Instance.isChallengeFile)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return "[Seed cannot be changed on a Challenge Moon.]\n\n";
            }
                

            if (!isNumSeed)
                numSeed = StringToSeed(seedString);

            // Apply the seed information to next moon

            nextMoonSeedInfo.Reset();
            nextMoonSeedInfo.seedNum = numSeed;

            if (isNumSeed)
                nextMoonSeedInfo.seedType = SeedType.SetNum;
            else
            {
                nextMoonSeedInfo.seedType = SeedType.SetString;
                nextMoonSeedInfo.seedWord = seedString;
            }

            SendSeedToAll();

            PlayTerminalAudio(TerminalSounds.LoadImage);
            return nextMoonSeedInfo.TerminalTextWhenSet();
        }

        public static int StringToSeed(string seedString)
        {
            int seed = seedString.GetHashCode();
            // Ensure seed isn't negative, or out of range.
            seed = Math.Abs(seed);
            seed = seed % maxSeed;
            if (seed < minSeed)
                seed += minSeed;
            return seed;
        }

        public static string GetSeedCommand()
        {
            if (StartOfRound.Instance.inShipPhase)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                // my friend told me to add this
                if (UnityEngine.Random.Range(1, 100) == 1)
                    return "[No.]\n\n";
                return "[Must land on a moon first before checking seed!]\n\n";
            }

            return currentMoonSeedInfo.GetTerminalEntry();
        }

        public static string RandomSeedCommand()
        {
            if (!RoundManager.Instance.playersManager.inShipPhase)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return "[Seed cannot be changed after landing.]\n\n";
            }

            if (StartOfRound.Instance.isChallengeFile)
            {
                PlayTerminalAudio(TerminalSounds.TypoError);
                return "[Seed cannot be changed on a Challenge Moon.]\n\n";
            }

            PlayTerminalAudio(TerminalSounds.LoadImage);
            nextMoonSeedInfo.seedNum = UnityEngine.Random.Range(minSeed, maxSeed);
            nextMoonSeedInfo.seedType = SeedType.SetRandomized;

            SendSeedToAll();

            return nextMoonSeedInfo.TerminalTextWhenSet();
        }

        // Netcode

        // Sending seed from any
        public static void SendSeedToAll()
        {
            if (GameNetworkManager.Instance.localPlayerController.IsServer)
                syncNextMoonSeedInfo.SendClients(nextMoonSeedInfo);
            else
            {
                syncNextMoonSeedInfo.SendOtherClients(nextMoonSeedInfo);
                syncNextMoonSeedInfo.SendServer(nextMoonSeedInfo);
            }
        }


        // Receiving seed from server
        public static void SyncNextMoonSeedShared(SeedInfo info)
        {
            Log.LogInfo("Recieved seed info sync, values:");
            nextMoonSeedInfo = info;
            Log.LogInfo($"seedNum: {nextMoonSeedInfo.seedNum} seedType: {nextMoonSeedInfo.seedType} seedWord: {nextMoonSeedInfo.seedWord}");
        }

        public static void SyncNextMoonSeedInfo(SeedInfo info)
        {
            SyncNextMoonSeedShared(info);
        }

        public static void SyncNextMoonSeedInfo(SeedInfo info, ulong clientID)
        {
            SyncNextMoonSeedShared(info);
        }

        // Requesting seed from server
        public static void RequestSeedFromClient(ulong clientID)
        {
            syncNextMoonSeedInfo.SendClient(nextMoonSeedInfo, clientID);
        }

        // Utils

        public static Terminal GetTerminal()
        {
            if (terminal == null)
                terminal = FindObjectOfType<Terminal>();
            return terminal;
        }

        public static void PlayTerminalAudio(TerminalSounds audio)
        {
            GetTerminal().PlayTerminalAudioServerRpc((int)audio);
        }
    }
}
