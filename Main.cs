using System;
using System.Collections.Generic;
using System.Globalization;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Data.TrophyStore;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Knowledge;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppAssets.Scripts.Utils;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(UnlockerDeluxe.Main), "BTD6 Unlocker Deluxe", "4.1.0", "ekruges")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace UnlockerDeluxe
{
    public static class ModHelperData
    {
        public const string Version = "4.1.0";
        public const string Name = "BTD6 Unlocker Deluxe";
        public const string RepoOwner = "ekruges";
        public const string RepoName = "BTD6Unlocker_Deluxe";

        public const string Description =
            "All-in-one account unlocker and profile editor. One-click UNLOCK EVERYTHING, or pick individually: " +
            "all tower upgrades (zero XP cost), all monkey knowledge, all trophy store items, insta monkeys, " +
            "every achievement claimed with its real reward loot, " +
            "instant max level 155 and veteran levels with no level-up screens. Currency tab adds any amount of " +
            "monkey money, trophies and tower XP. Full lifetime stat editor: load your real stats, tweak any field " +
            "(pops, cash, games, rounds, trophies...), apply, or use the believable veteran preset. " +
            "Console at the bottom shows what every action did.\n\n" +
            "Hotkeys: F1 instas | F2 knowledge | F3 money+upgrades | F4 trophies | F5 trophy store | " +
            "F6 max level / +100 veteran | F7 veteran stat preset | F8 unlock pop-gated & locked towers | " +
            "F9 bot realistic map medals (asks first, never touches medals you already have).\n\n" +
            "Changes write to your Ninja Kiwi profile and cannot be undone by removing the mod. Use on an alt if you care about flags.";
    }

    public class Main : BloonsTD6Mod
    {
        // ============================== UI PLUMBING ==============================

        private static readonly Dictionary<ModSetting, ModHelperInputField> Inputs = new Dictionary<ModSetting, ModHelperInputField>();

        private static void WireInt(ModSettingInt s, bool readOnly = false) =>
            s.modifyInput = (Action<ModHelperInputField>)(input => Capture(s, input, readOnly));

        private static void WireNum(ModSettingDouble s) =>
            s.modifyInput = (Action<ModHelperInputField>)(input => Capture(s, input, false));

        private static void WireStr(ModSettingString s, bool readOnly = false) =>
            s.modifyInput = (Action<ModHelperInputField>)(input => Capture(s, input, readOnly));

        private static void Capture(ModSetting s, ModHelperInputField input, bool readOnly)
        {
            Inputs[s] = input;
            if (readOnly)
            {
                try { input.InputField.readOnly = true; } catch { }
            }
        }

        private static void Repaint(ModSetting s, string text)
        {
            if (Inputs.TryGetValue(s, out ModHelperInputField input))
            {
                try { input.SetText(text, false); } catch { }
            }
        }

        private static void SetInt(ModSettingInt s, int v)
        {
            s.SetValue(v);
            Repaint(s, v.ToString(CultureInfo.InvariantCulture));
        }

        private static void SetNum(ModSettingDouble s, double v)
        {
            s.SetValue(v);
            Repaint(s, v.ToString("F0", CultureInfo.InvariantCulture));
        }

        // ============================== CONSOLE ==============================

        public static readonly ModSettingCategory CatConsole = new ModSettingCategory("Console") { order = 5 };

        public static readonly ModSettingString Console1 = new ModSettingString("ready.") { displayName = "•", category = CatConsole };
        public static readonly ModSettingString Console2 = new ModSettingString("") { displayName = "•", category = CatConsole };
        public static readonly ModSettingString Console3 = new ModSettingString("") { displayName = "•", category = CatConsole };
        public static readonly ModSettingString Console4 = new ModSettingString("") { displayName = "•", category = CatConsole };
        public static readonly ModSettingString Console5 = new ModSettingString("") { displayName = "•", category = CatConsole };

        private static readonly List<string> ConsoleLines = new List<string>();

        private static void Log(string msg)
        {
            MelonLogger.Msg(msg);
            ConsoleLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            while (ConsoleLines.Count > 5) ConsoleLines.RemoveAt(5);
            ModSettingString[] slots = { Console1, Console2, Console3, Console4, Console5 };
            for (int i = 0; i < slots.Length; i++)
            {
                string text = i < ConsoleLines.Count ? ConsoleLines[i] : "";
                try { slots[i].SetValue(text); } catch { }
                Repaint(slots[i], text);
            }
        }

        private static Btd6Player P()
        {
            Btd6Player p = GameExt.GetBtd6Player(Game.instance);
            if (p == null) Log("Player not ready - wait for the main menu.");
            return p;
        }

        // Anything that writes map medals asks first: a stray key press must never be the reason
        // someone's medal collection changed. Non-dismissable, so the only exits are the two buttons.
        private static bool confirmOpen;

        private static void Confirm(string title, string body, string okText, Action onConfirm)
        {
            PopupScreen screen = PopupScreen.instance;
            if (confirmOpen && screen != null && screen.IsPopupActiveOrLoading()) return;
            if (screen == null)
            {
                Log($"{title} needs a confirmation dialog, which can't open right now - nothing was changed.");
                return;
            }
            confirmOpen = true;
            var placement = InGame.instance != null ? PopupScreen.Placement.inGameCenter : PopupScreen.Placement.menuCenter;
            try
            {
                screen.ShowPopup(placement, title, body,
                    new Action(() => { confirmOpen = false; onConfirm(); }), okText,
                    new Action(() => { confirmOpen = false; Log($"{title} cancelled - nothing was changed."); }), "Cancel",
                    Popup.TransitionAnim.Scale, PopupScreen.BackGround.GreyNonDismissable);
            }
            catch (Exception e)
            {
                confirmOpen = false;
                Log($"{title}: the confirmation dialog failed ({e.Message}) - nothing was changed.");
            }
        }

        private const string MedalPromiseText =
            "Medals you already have are never touched - this only fills in modes you haven't completed. " +
            "It writes to your Ninja Kiwi profile and can't be undone by removing the mod.";

        // ============================== UNLOCKS ==============================

        public static readonly ModSettingCategory CatUnlocks = new ModSettingCategory("Unlocks") { order = 1 };

        public static readonly ModSettingButton BtnEverything = new ModSettingButton(() => Confirm(
            "Unlock everything?",
            "Runs every unlock, adds monkey money and trophies, claims all achievements and bots realistic map medals. " + MedalPromiseText,
            "Unlock", UnlockEverything))
        {
            displayName = "UNLOCK EVERYTHING",
            description = "Runs every unlock below plus monkey money, trophies, achievements and realistic map medals. Asks before running; medals you already have are never touched.",
            buttonText = "GO",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnUpgrades = new ModSettingButton(() => UnlockAllUpgrades())
        {
            displayName = "Unlock All Tower Upgrades",
            description = "Acquires every upgrade on every path of every tower at zero XP cost, and tops up tower XP.",
            buttonText = "Unlock",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnKnowledge = new ModSettingButton(() => UnlockAllKnowledge())
        {
            displayName = "Unlock All Monkey Knowledge",
            description = "Every point in every knowledge tree.",
            buttonText = "Unlock",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnTrophyItems = new ModSettingButton(() => UnlockTrophyItems())
        {
            displayName = "Unlock All Trophy Store Items",
            description = "Every cosmetic in the trophy store, including exclusives.",
            buttonText = "Unlock",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnInstas = new ModSettingButton(() => GiveAllInstas())
        {
            displayName = "Give All Insta Monkeys",
            description = "50 of every tower and crosspath combination.",
            buttonText = "Give",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnMaxLevel = new ModSettingButton(() => MaxPlayerLevel())
        {
            displayName = "Max Player Level (155)",
            description = "Sets rank and XP directly - no level-up screens.",
            buttonText = "Max",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnVeteran = new ModSettingButton(() => AddVeteranLevels(100))
        {
            displayName = "+100 Veteran Levels",
            description = "Adds prestige levels directly, no ceremonies.",
            buttonText = "Add",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnLockedTowers = new ModSettingButton(() => UnlockAllTowers())
        {
            displayName = "Unlock Pop-Gated & Locked Towers (F8)",
            description = "Adds every tower and hero to your profile's unlocked sets and maxes the pops-progress meters - covers the towers locked behind pop milestones.",
            buttonText = "Unlock",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnAchievements = new ModSettingButton(() => ClaimAllAchievements())
        {
            displayName = "Claim All Achievements",
            description = "Marks every achievement complete and claims its reward - the monkey money, insta monkeys and power loot all land on your profile, same as claiming each one by hand.",
            buttonText = "Claim",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnMedals = new ModSettingButton(() => Confirm(
            "Bot realistic map medals?",
            "Adds a believable spread of medals: nearly all Easy Standards, tapering down to a handful of CHIMPS. " + MedalPromiseText,
            "Add Medals", BotRealisticMedals))
        {
            displayName = "Bot Realistic Map Medals (F9)",
            description = "Adds a believable spread: nearly all Easy Standards, tapering down to a handful of CHIMPS, scaled by map difficulty, with mixed borders and sparse co-op. Only fills modes you don't have a medal on yet - existing medals and black borders are never touched. Asks first.",
            buttonText = "Bot",
            category = CatUnlocks,
        };

        public static readonly ModSettingButton BtnMedalsMax = new ModSettingButton(() => Confirm(
            "Max all map medals?",
            "Black-borders every mode on every map, single player and co-op. Existing black borders are left as they are. " +
            "It writes to your Ninja Kiwi profile and can't be undone by removing the mod.",
            "Max Medals", BotAllMedals))
        {
            displayName = "Max All Map Medals (100%)",
            description = "The loud version: every mode on every map, black-border quality, SP and co-op. Only upgrades modes that aren't black-bordered yet. Asks first.",
            buttonText = "Max",
            category = CatUnlocks,
        };

        // ============================== CURRENCY ==============================

        public static readonly ModSettingCategory CatCurrency = new ModSettingCategory("Currency") { order = 2 };

        public static readonly ModSettingInt MoneyAmount = new ModSettingInt(1000000)
        {
            displayName = "Monkey Money Amount",
            min = 0,
            max = 2000000000,
            category = CatCurrency,
        };

        public static readonly ModSettingButton BtnMoney = new ModSettingButton(() => AddMoney(MoneyAmount))
        {
            displayName = "Add Monkey Money",
            description = "Adds the amount above to your balance.",
            buttonText = "Add",
            category = CatCurrency,
        };

        public static readonly ModSettingInt TrophyAmount = new ModSettingInt(10000)
        {
            displayName = "Trophies Amount",
            min = 0,
            max = 1000000,
            category = CatCurrency,
        };

        public static readonly ModSettingButton BtnTrophies = new ModSettingButton(() => AddTrophies(TrophyAmount))
        {
            displayName = "Add Trophies",
            description = "Adds the amount above and reports your new balance in the console.",
            buttonText = "Add",
            category = CatCurrency,
        };

        public static readonly ModSettingInt TowerXpAmount = new ModSettingInt(5000000)
        {
            displayName = "Tower XP Amount",
            min = 0,
            max = 2000000000,
            category = CatCurrency,
        };

        public static readonly ModSettingButton BtnTowerXp = new ModSettingButton(() => SetAllTowerXp(TowerXpAmount))
        {
            displayName = "Set XP On All Towers",
            description = "Force-sets every tower's XP counter to the amount above.",
            buttonText = "Set",
            category = CatCurrency,
        };

        // ============================== STAT EDITOR ==============================

        public static readonly ModSettingCategory CatStats = new ModSettingCategory("Stat Editor") { order = 3 };

        public static readonly ModSettingButton BtnLoadStats = new ModSettingButton(() => LoadStats())
        {
            displayName = "Load Current Stats Into Fields",
            description = "Reads your profile's real values into the fields below so you can tweak from there.",
            buttonText = "Load",
            category = CatStats,
        };

        public static readonly ModSettingButton BtnApplyStats = new ModSettingButton(() => ApplyStats())
        {
            displayName = "Apply Fields To Profile",
            description = "Writes every field below into your profile and saves.",
            buttonText = "Apply",
            category = CatStats,
        };

        public static readonly ModSettingButton BtnPreset = new ModSettingButton(() => VeteranPreset())
        {
            displayName = "Fill Fields With Veteran Preset",
            description = "Strong but believable lifetime stats. Press Apply afterwards to write them.",
            buttonText = "Fill",
            category = CatStats,
        };

        public static readonly ModSettingButton BtnTopMonkeys = new ModSettingButton(() => BotTopMonkeys())
        {
            displayName = "Bot Top Monkeys & Usage Stats",
            description = "Fills per-monkey placement counts, per-monkey wins, hero usage/levels and upgrade-tier purchases with a weighted realistic spread scaled to your totals, and makes Ninja Monkey your most experienced tower. This is what feeds the Top Monkeys / Top Heroes panels.",
            buttonText = "Bot",
            category = CatStats,
        };

        public static readonly ModSettingDouble StBloons = new ModSettingDouble(487234116) { displayName = "Bloons Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StCamos = new ModSettingDouble(9412887) { displayName = "Camos Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StLeads = new ModSettingDouble(11203442) { displayName = "Leads Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StPurples = new ModSettingDouble(6871559) { displayName = "Purples Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StRegrows = new ModSettingDouble(31246018) { displayName = "Regrows Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StCerams = new ModSettingDouble(27904773) { displayName = "Ceramics Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StForts = new ModSettingDouble(8133205) { displayName = "Fortified Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StMoabs = new ModSettingInt(3118406) { displayName = "MOABs Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StBfbs = new ModSettingInt(741283) { displayName = "BFBs Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StZomgs = new ModSettingInt(158447) { displayName = "ZOMGs Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StDdts = new ModSettingInt(94102) { displayName = "DDTs Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StBads = new ModSettingInt(36518) { displayName = "BADs Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StBosses = new ModSettingInt(342) { displayName = "Bosses Popped", min = 0, category = CatStats };
        public static readonly ModSettingInt StGolden = new ModSettingInt(1247) { displayName = "Golden Bloons Popped", min = 0, category = CatStats };
        public static readonly ModSettingDouble StLeaked = new ModSettingDouble(1842970) { displayName = "Bloons Leaked", min = 0, category = CatStats };
        public static readonly ModSettingDouble StCash = new ModSettingDouble(1914377208) { displayName = "Cash Earned", min = 0, category = CatStats };
        public static readonly ModSettingInt StMonkeyMoney = new ModSettingInt(611240) { displayName = "Monkey Money Earned", min = 0, category = CatStats };
        public static readonly ModSettingInt StGames = new ModSettingInt(6847) { displayName = "Games Played", min = 0, category = CatStats };
        public static readonly ModSettingInt StHighRound = new ModSettingInt(263) { displayName = "Highest Round", min = 0, category = CatStats };
        public static readonly ModSettingInt StHighRoundCur = new ModSettingInt(187) { displayName = "Highest Round (This Version)", min = 0, category = CatStats };
        public static readonly ModSettingInt StPlaced = new ModSettingInt(412809) { displayName = "Towers Placed", min = 0, category = CatStats };
        public static readonly ModSettingInt StSold = new ModSettingInt(118344) { displayName = "Towers Sold", min = 0, category = CatStats };
        public static readonly ModSettingInt StUpgrades = new ModSettingInt(987542) { displayName = "Upgrades Purchased", min = 0, category = CatStats };
        public static readonly ModSettingInt StAbilities = new ModSettingInt(391706) { displayName = "Abilities Used", min = 0, category = CatStats };
        public static readonly ModSettingInt StHeroes = new ModSettingInt(5912) { displayName = "Heroes Placed", min = 0, category = CatStats };
        public static readonly ModSettingInt StInstasUsed = new ModSettingInt(1206) { displayName = "Insta Monkeys Used", min = 0, category = CatStats };
        public static readonly ModSettingDouble StBossDmg = new ModSettingDouble(92481336904) { displayName = "Damage To Bosses", min = 0, category = CatStats };
        public static readonly ModSettingInt StTrophyBalance = new ModSettingInt(12480) { displayName = "Trophies (Balance)", min = 0, category = CatStats };
        public static readonly ModSettingInt StLifetimeTrophies = new ModSettingInt(31750) { displayName = "Lifetime Trophies Earned", min = 0, category = CatStats };
        public static readonly ModSettingInt StPowersUsed = new ModSettingInt(38904) { displayName = "Powers Used", min = 0, category = CatStats };
        public static readonly ModSettingInt StNecro = new ModSettingInt(2507443) { displayName = "Necro Bloons Reanimated", min = 0, category = CatStats };
        public static readonly ModSettingInt StDailies = new ModSettingInt(412) { displayName = "Daily Challenges Completed", min = 0, category = CatStats };
        public static readonly ModSettingInt StTeamsWins = new ModSettingInt(87) { displayName = "Monkey Teams Wins", min = 0, category = CatStats };
        public static readonly ModSettingInt StCrates = new ModSettingInt(356) { displayName = "Collection Crates Opened", min = 0, category = CatStats };
        public static readonly ModSettingInt StOdysseys = new ModSettingInt(124) { displayName = "Odysseys Completed", min = 0, category = CatStats };
        public static readonly ModSettingInt StVetRank = new ModSettingInt(0) { displayName = "Veteran Rank", min = 0, category = CatStats };

        // ============================== STAT EDITOR: MORE ==============================

        public static readonly ModSettingCategory CatStats2 = new ModSettingCategory("Stat Editor: Economy & Misc") { order = 4, collapsed = true };

        public static readonly ModSettingInt StMmBalance = new ModSettingInt(184620) { displayName = "Monkey Money (Balance)", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StKnowledgePts = new ModSettingInt(4213) { displayName = "Knowledge Points (Balance)", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StTrophiesSpent = new ModSettingInt(19270) { displayName = "Trophies Spent", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StContinues = new ModSettingInt(231) { displayName = "Continues Used", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StRaces = new ModSettingInt(148) { displayName = "Races Entered", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StChallengesPlayed = new ModSettingInt(371) { displayName = "Challenges Played", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StChallengesShared = new ModSettingInt(14) { displayName = "Challenges Shared", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StHostedCoop = new ModSettingInt(412) { displayName = "Hosted Co-op Games", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StConsecDailies = new ModSettingInt(23) { displayName = "Consecutive Dailies Streak", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StUniqueDailyWins = new ModSettingInt(264) { displayName = "Unique Daily Challenge Wins", min = 0, category = CatStats2 };
        public static readonly ModSettingDouble StEco = new ModSettingDouble(8412776) { displayName = "Eco Earned", min = 0, category = CatStats2 };
        public static readonly ModSettingDouble StCoopPops = new ModSettingDouble(61244903) { displayName = "Co-op Bloons Popped", min = 0, category = CatStats2 };
        public static readonly ModSettingDouble StCoopCashGiven = new ModSettingDouble(18441209) { displayName = "Co-op Cash Given", min = 0, category = CatStats2 };
        public static readonly ModSettingDouble StGlued = new ModSettingDouble(14208331) { displayName = "Bloons Glued", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StCrits = new ModSettingInt(92441) { displayName = "Critical Hits", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StRainbowMagic = new ModSettingInt(84112) { displayName = "Rainbows Popped With Magic", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StShimmer = new ModSettingInt(391244) { displayName = "Bloons Revealed By Shimmer", min = 0, category = CatStats2 };
        public static readonly ModSettingDouble StDartlingPops = new ModSettingDouble(22481336) { displayName = "Dartling Gunner Pops", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StMoabTakedowns = new ModSettingInt(1204118) { displayName = "MOAB Takedowns", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StTier5Instas = new ModSettingInt(96) { displayName = "Tier 5 Instas Used", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StHeroSold = new ModSettingInt(1884) { displayName = "Heroes Sold", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StRestarts = new ModSettingInt(3912) { displayName = "Games Restarted", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StHero3rd = new ModSettingInt(11208) { displayName = "Lv3 Hero Abilities Used", min = 0, category = CatStats2 };
        public static readonly ModSettingInt StHero10th = new ModSettingInt(4417) { displayName = "Lv10 Hero Abilities Used", min = 0, category = CatStats2 };

        private static ModSetting[] AllStatFields => new ModSetting[]
        {
            StBloons, StCamos, StLeads, StPurples, StRegrows, StCerams, StForts,
            StMoabs, StBfbs, StZomgs, StDdts, StBads, StBosses, StGolden, StLeaked,
            StCash, StMonkeyMoney, StGames, StHighRound, StHighRoundCur, StPlaced,
            StSold, StUpgrades, StAbilities, StHeroes, StInstasUsed, StBossDmg,
            StTrophyBalance, StLifetimeTrophies, StPowersUsed, StNecro, StDailies,
            StTeamsWins, StCrates, StOdysseys, StVetRank,
            StMmBalance, StKnowledgePts, StTrophiesSpent, StContinues, StRaces,
            StChallengesPlayed, StChallengesShared, StHostedCoop, StConsecDailies,
            StUniqueDailyWins, StEco, StCoopPops, StCoopCashGiven, StGlued, StCrits,
            StRainbowMagic, StShimmer, StDartlingPops, StMoabTakedowns, StTier5Instas,
            StHeroSold, StRestarts, StHero3rd, StHero10th,
        };

        // ============================== ACTIONS ==============================

        private static void UnlockEverything()
        {
            UnlockAllTowers();
            UnlockAllUpgrades();
            UnlockAllKnowledge();
            UnlockTrophyItems();
            GiveAllInstas();
            MaxPlayerLevel();
            AddMoney(MoneyAmount);
            AddTrophies(TrophyAmount);
            BotRealisticMedals();
            ClaimAllAchievements();
            Log("UNLOCK EVERYTHING finished.");
        }

        // Achievements live on the runtime AchievementManager, not just the profile: setting progress
        // to the goal and claiming through the manager grants the real reward loot (money/instas/powers),
        // exactly like pressing Claim on each one.
        private static void ClaimAllAchievements()
        {
            Btd6Player player = P();
            if (player == null) return;
            var mgr = Game.instance.achievementManager;
            if (mgr == null) { Log("Achievement manager not ready."); return; }
            var list = mgr.GetAchievements();
            int claimed = 0, had = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                try
                {
                    if (a == null) continue;
                    if (a.Claimed) { had++; continue; }
                    mgr.SetAchievementProgress(a.Id, a.Goal);
                    mgr.ClaimAchievementReward(a);
                    claimed++;
                }
                catch { }
            }
            player.SaveNow();
            Log($"Achievements: {claimed} claimed with rewards ({had} already claimed).");
        }

        private static void UnlockAllTowers()
        {
            Btd6Player player = P();
            if (player == null) return;

            var d = player.Data;
            int newTowers = 0, newHeroes = 0;
            foreach (var td in Game.instance.model.towerSet)
            {
                string id = td.towerId;
                SetK(d.towerUnlockProgresses, id, 100000000);
                bool had = d.unlockedTowers.Contains(id);
                try { player.UnlockTower(id); } catch { if (!had) d.unlockedTowers.Add(id); }
                if (!had) newTowers++;
            }
            foreach (var td in Game.instance.model.heroSet)
            {
                string id = td.towerId;
                bool had = d.unlockedHeroes.Contains(id);
                try { player.UnlockHero(id); } catch { if (!had) d.unlockedHeroes.Add(id); }
                if (!had) newHeroes++;
            }

            try
            {
                player.debugUnlockAllTowers = true;
                player.debugUnlockAllUpgrades = true;
                player.debugUnlockAllPowersPro = true;
            }
            catch { }

            try { d.UnlockAllModes.Value = 1; } catch { }

            player.SaveNow();
            Log($"HARD unlock: {newTowers} towers + {newHeroes} heroes via UnlockTower/UnlockHero, pop-progress maxed, all modes flagged, NK debug unlock switches ON for this session.");
        }

        private static readonly Dictionary<string, string[]> ModesByDifficulty = new Dictionary<string, string[]>
        {
            { "Easy", new[] { "Standard", "PrimaryOnly", "Deflation" } },
            { "Medium", new[] { "Standard", "MilitaryOnly", "Apopalypse", "Reverse" } },
            { "Hard", new[] { "Standard", "MagicOnly", "DoubleMoabHealth", "HalfCash", "AlternateBloonsRounds", "Impoppable", "Clicks" } },
        };

        private static int H(string input)
        {
            unchecked
            {
                uint h = 5381;
                foreach (char c in input) h = h * 33 + c;
                return (int)(h % 100);
            }
        }

        private static readonly Dictionary<string, int> ModeChance = new Dictionary<string, int>
        {
            { "Easy/Standard", 96 }, { "Easy/PrimaryOnly", 82 }, { "Easy/Deflation", 78 },
            { "Medium/Standard", 90 }, { "Medium/MilitaryOnly", 68 }, { "Medium/Apopalypse", 55 }, { "Medium/Reverse", 62 },
            { "Hard/Standard", 76 }, { "Hard/MagicOnly", 48 }, { "Hard/DoubleMoabHealth", 44 }, { "Hard/HalfCash", 33 },
            { "Hard/AlternateBloonsRounds", 38 }, { "Hard/Impoppable", 26 }, { "Hard/Clicks", 18 },
        };

        private static int MapFactor(string difficultyName)
        {
            switch (difficultyName)
            {
                case "Beginner": return 100;
                case "Intermediate": return 85;
                case "Advanced": return 65;
                case "Expert": return 45;
                default: return 70;
            }
        }

        // True when the player already has a medal on this mode (blackBorder: specifically a black
        // border). When the check itself fails we report "owned", so an unreadable record is skipped
        // rather than written over.
        private static bool HasMedal(MapInfoManager mapInfo, string map, string difficulty, string mode, bool coop, bool blackBorder)
        {
            try { return mapInfo.HasCompletedMode(map, difficulty, mode, coop, blackBorder); }
            catch { return true; }
        }

        // Additive only: modes that already have a medal are never passed to CompleteMode, so real
        // completions and their black borders survive. (Before 4.1 this cleared every map record first.)
        private static void BotRealisticMedals()
        {
            Btd6Player player = P();
            if (player == null) return;

            var mapInfo = player.Data.mapInfo;
            int sp = 0, coop = 0, chimps = 0, kept = 0, maps = 0;
            foreach (var map in GameData.Instance.mapSet.Maps.items)
            {
                if (map == null || map.isDebug) continue;
                string id = map.id;
                try { mapInfo.UnlockMap(id); } catch { }
                int factor = MapFactor(map.difficulty.ToString());
                foreach (var diff in ModesByDifficulty)
                {
                    foreach (string mode in diff.Value)
                    {
                        int chance = ModeChance[$"{diff.Key}/{mode}"] * factor / 100;
                        if (H($"{id}|{diff.Key}|{mode}|sp") >= chance) continue;

                        if (HasMedal(mapInfo, id, diff.Key, mode, false, false)) kept++;
                        else
                        {
                            bool noSave = H($"{id}|{diff.Key}|{mode}|ns") < (mode == "Clicks" ? 45 : 30);
                            try
                            {
                                mapInfo.CompleteMode(id, diff.Key, mode, noSave, false);
                                sp++;
                                if (mode == "Clicks") chimps++;
                            }
                            catch { }
                        }

                        if (H($"{id}|{diff.Key}|{mode}|coop") < 38)
                        {
                            if (HasMedal(mapInfo, id, diff.Key, mode, true, false)) kept++;
                            else
                            {
                                try { mapInfo.CompleteMode(id, diff.Key, mode, false, true); coop++; } catch { }
                            }
                        }
                    }
                }
                maps++;
            }
            player.SaveNow();
            Log($"Realistic medals: added {sp} SP ({chimps} CHIMPS) + {coop} co-op across {maps} maps. {kept} medals you already had were left untouched.");
        }

        private static void BotAllMedals()
        {
            Btd6Player player = P();
            if (player == null) return;

            var mapInfo = player.Data.mapInfo;
            int maps = 0, added = 0, kept = 0;
            foreach (var map in GameData.Instance.mapSet.Maps.items)
            {
                if (map == null || map.isDebug) continue;
                string id = map.id;
                try { mapInfo.UnlockMap(id); } catch { }
                foreach (var diff in ModesByDifficulty)
                {
                    foreach (string mode in diff.Value)
                    {
                        foreach (bool coop in new[] { false, true })
                        {
                            if (HasMedal(mapInfo, id, diff.Key, mode, coop, true)) { kept++; continue; }
                            try { mapInfo.CompleteMode(id, diff.Key, mode, true, coop); added++; } catch { }
                        }
                    }
                }
                maps++;
            }
            player.SaveNow();
            Log($"Medals: {added} modes black-bordered across {maps} maps (SP+co-op). {kept} were already black-bordered and left untouched.");
        }

        private static void UnlockAllUpgrades()
        {
            Btd6Player player = P();
            if (player == null) return;

            Il2CppSystem.Collections.Generic.List<string> monkes = Helpers.ValidBaseTowerNames();
            int xpCount = 0;
            foreach (string monke in monkes)
            {
                player.AddTowerXP(monke, TowerXpAmount);
                if (player.Data.towerXp.ContainsKey(monke))
                    player.Data.towerXp[monke].Value = TowerXpAmount;
                xpCount++;
            }

            int acquired = 0;
            foreach (var tm in Game.instance.model.towers)
            {
                if (tm.appliedUpgrades == null) continue;
                foreach (string up in tm.appliedUpgrades)
                {
                    try
                    {
                        if (player.HasUpgrade(up)) continue;
                        player.AcquireUpgrade(tm.baseId, up, 0f);
                        acquired++;
                    }
                    catch { }
                }
            }

            player.SaveNow();
            Log($"Upgrades: {acquired} newly acquired. Tower XP set to {(int)TowerXpAmount:N0} on {xpCount} towers.");
        }

        private static void UnlockAllKnowledge()
        {
            Btd6Player player = P();
            if (player == null) return;

            GameModel gameModel = Game.instance.model;
            KnowledgeModel[] models = gameModel.allKnowledge.ToArray();
            int unlocked = 0, had = 0;
            foreach (KnowledgeModel model in models)
            {
                try
                {
                    string m = model.name;
                    if (m.StartsWith("KnowledgeModel_"))
                        m = m.Substring("KnowledgeModel_".Length);
                    if (player.HasKnowledge(m)) { had++; continue; }
                    player.AcquireKnowledge(m);
                    unlocked++;
                }
                catch { }
            }
            player.SaveNow();
            Log($"Monkey knowledge: {unlocked} unlocked ({had} already owned).");
        }

        private static void UnlockTrophyItems()
        {
            Btd6Player player = P();
            if (player == null) return;

            TrophyStoreItems items = GameData.Instance.trophyStoreItems;
            int count = 0;
            foreach (TrophyStoreItem item in items.storeItems.ToList())
            {
                try { player.AddTrophyStoreItem(item.name); count++; } catch { }
            }
            player.SaveNow();
            Log($"Trophy store: {count} items unlocked.");
        }

        private static void GiveAllInstas()
        {
            Btd6Player player = P();
            if (player == null) return;

            int given = 0;
            foreach (string monke in Helpers.ValidBaseTowerNames())
            {
                var seen = new HashSet<string>();
                for (int main = 0; main <= 5; main++)
                {
                    for (int second = 0; second <= 2; second++)
                    {
                        int[][] combos =
                        {
                            new[] { main, second, 0 }, new[] { main, 0, second },
                            new[] { second, main, 0 }, new[] { 0, main, second },
                            new[] { second, 0, main }, new[] { 0, second, main },
                        };
                        foreach (int[] c in combos)
                        {
                            string key = $"{c[0]}{c[1]}{c[2]}";
                            if (!seen.Add(key)) continue;
                            try { player.AddInstaTower(monke, c, 50); given++; } catch { }
                        }
                    }
                }
            }
            player.SaveNow();
            Log($"Insta monkeys: 50 each of {given} tower/crosspath combos.");
        }

        private static void MaxPlayerLevel()
        {
            Btd6Player player = P();
            if (player == null) return;

            double before = player.Data.rank.Value;
            player.Data.xp.Value = 500000000;
            player.Data.rank.Value = 155;
            player.SaveNow();
            Log($"Player level {before} -> 155. No level-up screens.");
        }

        private static void AddVeteranLevels(int amount)
        {
            Btd6Player player = P();
            if (player == null) return;

            double before = player.Data.veteranRank.Value;
            player.Data.veteranRank.Value = before + amount;
            player.SaveNow();
            Log($"Veteran rank {before} -> {before + amount}.");
        }

        private static void AddMoney(int amount)
        {
            Btd6Player player = P();
            if (player == null) return;
            GameExt.AddMonkeyMoney(Game.instance, amount);
            player.SaveNow();
            Log($"Monkey money +{amount:N0}.");
        }

        private static void AddTrophies(int amount)
        {
            Btd6Player player = P();
            if (player == null) return;

            double before = player.Data.trophies.Value;
            player.GainTrophies(amount, "event", null);
            double after = player.Data.trophies.Value;
            if (after < before + amount)
            {
                player.Data.trophies.Value = before + amount;
                player.Data.lifetimeTrophies.Value += amount;
                after = player.Data.trophies.Value;
            }
            player.SaveNow();
            Log($"Trophies +{amount:N0}. Balance now {(long)after:N0}.");
        }

        private static void SetAllTowerXp(int amount)
        {
            Btd6Player player = P();
            if (player == null) return;

            int count = 0;
            foreach (string monke in Helpers.ValidBaseTowerNames())
            {
                player.AddTowerXP(monke, amount);
                if (player.Data.towerXp.ContainsKey(monke))
                    player.Data.towerXp[monke].Value = amount;
                count++;
            }
            player.SaveNow();
            Log($"Tower XP set to {amount:N0} on {count} towers.");
        }

        private static Il2CppAssets.Scripts.Models.Profile.BasicStats BS(Il2CppAssets.Scripts.Models.Profile.ProfileModel d)
        {
            var s = d.analyticsKonFuze;
            var bs = s.basicStats;
            if (bs == null)
            {
                bs = s.basicStatsFiller;
                if (s.basicStats != null) bs = s.basicStats;
                else if (bs != null) s.basicStats = bs;
            }
            return bs;
        }

        private static void LoadStats()
        {
            Btd6Player player = P();
            if (player == null) return;

            var d = player.Data;
            var bs = BS(d);
            if (bs == null) { Log("Could not find basicStats on the profile."); return; }

            SetNum(StBloons, bs.bloonsPopped.Value);
            SetNum(StCamos, bs.camosPopped.Value);
            SetNum(StLeads, bs.leadPopped.Value);
            SetNum(StPurples, bs.purplesPopped.Value);
            SetNum(StRegrows, bs.regrowPopped.Value);
            SetNum(StCerams, bs.ceramicsPopped.Value);
            SetNum(StForts, bs.fortifiedPopped.Value);
            SetInt(StMoabs, ToInt(bs.moabsPopped.Value));
            SetInt(StBfbs, ToInt(bs.bfbsPopped.Value));
            SetInt(StZomgs, ToInt(bs.zomgsPopped.Value));
            SetInt(StDdts, ToInt(bs.ddtsPopped.Value));
            SetInt(StBads, ToInt(bs.badsPopped.Value));
            SetInt(StBosses, ToInt(bs.bossesPopped.Value));
            SetInt(StGolden, d.goldenBloonsPopped);
            SetNum(StLeaked, bs.bloonsLeaked.Value);
            SetNum(StCash, bs.cashEarned.Value);
            SetInt(StMonkeyMoney, ToInt(bs.monkeyMoneyEarned.Value));
            SetInt(StGames, ToInt(bs.gamesPlayed.Value));
            SetInt(StHighRound, d.highestSeenRound);
            SetInt(StHighRoundCur, d.highestSeenRoundCurrentVersion);
            SetInt(StPlaced, ToInt(bs.totalTowersPlaced.Value));
            SetInt(StSold, ToInt(bs.totalTowersSold.Value));
            SetInt(StUpgrades, ToInt(bs.totalUpgradesPurchased.Value));
            SetInt(StAbilities, ToInt(bs.totalAbilitiesActivated.Value));
            SetInt(StHeroes, ToInt(bs.timesHeroPlaced.Value));
            SetInt(StInstasUsed, ToInt(bs.instaMonkeysUsed.Value));
            SetNum(StBossDmg, bs.damageDoneToBosses.Value);
            SetInt(StTrophyBalance, ToInt(d.trophies.Value));
            SetInt(StLifetimeTrophies, ToInt(d.lifetimeTrophies.Value));
            SetInt(StPowersUsed, ToInt(bs.totalPowersActivated.Value));
            SetInt(StNecro, ToInt(d.analyticsKonFuze.necroBloonsReanimated.Value));
            SetInt(StDailies, d.totalDailyChallengesCompleted);
            SetInt(StTeamsWins, d.monkeyTeamsWins);
            SetInt(StCrates, d.collectionEventCratesOpened);
            SetInt(StOdysseys, ToInt(d.totalCompletedOdysseys.Value));
            SetInt(StVetRank, ToInt(d.veteranRank.Value));

            var sa = d.analyticsKonFuze;
            SetInt(StMmBalance, ToInt(d.monkeyMoney.Value));
            SetInt(StKnowledgePts, ToInt(d.knowledgePoints.Value));
            SetInt(StTrophiesSpent, d.trophiesSpent);
            SetInt(StContinues, ToInt(d.continuesUsed.Value));
            SetInt(StRaces, ToInt(d.totalRacesEntered.Value));
            SetInt(StChallengesPlayed, ToInt(d.challengesPlayed.Value));
            SetInt(StChallengesShared, ToInt(d.challengesShared.Value));
            SetInt(StHostedCoop, d.hostedCoopGames);
            SetInt(StConsecDailies, d.consecutiveDailyChallengesCompleted);
            SetInt(StUniqueDailyWins, d.numWonUniqueDailyChallenges);
            SetNum(StEco, bs.ecoEarned.Value);
            SetNum(StCoopPops, sa.coopBloonsPopped.Value);
            SetNum(StCoopCashGiven, sa.coopCashGiven.Value);
            SetNum(StGlued, sa.gluedBloons.Value);
            SetInt(StCrits, ToInt(sa.criticalHits.Value));
            SetInt(StRainbowMagic, ToInt(sa.rainbowBloonsPoppedWithMagic.Value));
            SetInt(StShimmer, ToInt(sa.bloonsRevealedByShimmer.Value));
            SetNum(StDartlingPops, sa.dartlingGunnerPops.Value);
            SetInt(StMoabTakedowns, ToInt(sa.moabTakedownsCount.Value));
            SetInt(StTier5Instas, ToInt(sa.tier5InstasUsed.Value));
            SetInt(StHeroSold, ToInt(bs.timesHeroSold.Value));
            SetInt(StRestarts, ToInt(bs.timesGameRestarted.Value));
            SetInt(StHero3rd, ToInt(bs.thirdLevelHeroAbilityUsed.Value));
            SetInt(StHero10th, ToInt(bs.tenthLevelHeroAbilityUsed.Value));

            Log("Loaded current profile stats into the editor fields (both pages).");
        }

        private static int ToInt(double v) => (int)Math.Max(0, Math.Min(v, int.MaxValue));

        private static void ApplyStats()
        {
            Btd6Player player = P();
            if (player == null) return;

            var d = player.Data;
            var s = d.analyticsKonFuze;
            var bs = BS(d);
            if (bs == null) { Log("Could not find basicStats on the profile."); return; }

            bs.bloonsPopped.Value = StBloons;
            bs.camosPopped.Value = StCamos;
            bs.leadPopped.Value = StLeads;
            bs.purplesPopped.Value = StPurples;
            bs.regrowPopped.Value = StRegrows;
            bs.ceramicsPopped.Value = StCerams;
            bs.fortifiedPopped.Value = StForts;
            bs.moabsPopped.Value = StMoabs;
            bs.bfbsPopped.Value = StBfbs;
            bs.zomgsPopped.Value = StZomgs;
            bs.ddtsPopped.Value = StDdts;
            bs.badsPopped.Value = StBads;
            bs.bossesPopped.Value = StBosses;
            bs.bloonsLeaked.Value = StLeaked;
            bs.cashEarned.Value = StCash;
            bs.monkeyMoneyEarned.Value = StMonkeyMoney;
            bs.gamesPlayed.Value = StGames;
            bs.totalTowersPlaced.Value = StPlaced;
            bs.totalTowersSold.Value = StSold;
            bs.totalUpgradesPurchased.Value = StUpgrades;
            bs.totalAbilitiesActivated.Value = StAbilities;
            bs.timesHeroPlaced.Value = StHeroes;
            bs.instaMonkeysUsed.Value = StInstasUsed;
            bs.damageDoneToBosses.Value = StBossDmg;

            s.bloonsPopped.Value = StBloons;
            s.camosPopped.Value = StCamos;
            s.leadPopped.Value = StLeads;
            s.purplesPopped.Value = StPurples;
            s.regrowPopped.Value = StRegrows;
            s.ceramicsPopped.Value = StCerams;
            s.fortifiedPopped.Value = StForts;
            s.moabsPopped.Value = StMoabs;
            s.bfbsPopped.Value = StBfbs;
            s.zomgsPopped.Value = StZomgs;
            s.ddtsPopped.Value = StDdts;
            s.badsPopped.Value = StBads;
            s.bossesPopped.Value = StBosses;
            s.bloonsLeaked.Value = StLeaked;
            s.cashEarned.Value = StCash;
            s.monkeyMoneyEarned.Value = StMonkeyMoney;
            s.gamesPlayed.Value = StGames;
            s.totalTowersPlaced.Value = StPlaced;
            s.totalTowersSold.Value = StSold;
            s.totalUpgradesPurchased.Value = StUpgrades;
            s.totalAbilitiesActivated.Value = StAbilities;
            s.timesHeroPlaced.Value = StHeroes;
            s.instaMonkeysUsed.Value = StInstasUsed;
            s.damageDoneToBosses.Value = StBossDmg;

            bs.totalPowersActivated.Value = StPowersUsed;
            s.totalPowersActivated.Value = StPowersUsed;
            s.necroBloonsReanimated.Value = StNecro;

            d.goldenBloonsPopped = StGolden;
            d.highestSeenRound = StHighRound;
            d.highestSeenRoundCurrentVersion = StHighRoundCur;
            d.trophies.Value = StTrophyBalance;
            d.lifetimeTrophies.Value = StLifetimeTrophies;
            d.totalDailyChallengesCompleted = StDailies;
            d.monkeyTeamsWins = StTeamsWins;
            d.collectionEventCratesOpened = StCrates;
            d.totalCompletedOdysseys.Value = StOdysseys;
            if ((int)StVetRank > 0) d.veteranRank.Value = StVetRank;

            d.monkeyMoney.Value = StMmBalance;
            d.knowledgePoints.Value = StKnowledgePts;
            d.trophiesSpent = StTrophiesSpent;
            d.continuesUsed.Value = StContinues;
            d.totalRacesEntered.Value = StRaces;
            d.challengesPlayed.Value = StChallengesPlayed;
            d.challengesShared.Value = StChallengesShared;
            d.hostedCoopGames = StHostedCoop;
            d.consecutiveDailyChallengesCompleted = StConsecDailies;
            d.numWonUniqueDailyChallenges = StUniqueDailyWins;
            bs.ecoEarned.Value = StEco;
            s.ecoEarned.Value = StEco;
            s.coopBloonsPopped.Value = StCoopPops;
            s.coopCashGiven.Value = StCoopCashGiven;
            s.gluedBloons.Value = StGlued;
            s.criticalHits.Value = StCrits;
            s.rainbowBloonsPoppedWithMagic.Value = StRainbowMagic;
            s.bloonsRevealedByShimmer.Value = StShimmer;
            s.dartlingGunnerPops.Value = StDartlingPops;
            s.moabTakedownsCount.Value = StMoabTakedowns;
            s.tier5InstasUsed.Value = StTier5Instas;
            bs.timesHeroSold.Value = StHeroSold;
            s.timesHeroSold.Value = StHeroSold;
            bs.timesGameRestarted.Value = StRestarts;
            s.timesGameRestarted.Value = StRestarts;
            bs.thirdLevelHeroAbilityUsed.Value = StHero3rd;
            s.thirdLevelHeroAbilityUsed.Value = StHero3rd;
            bs.tenthLevelHeroAbilityUsed.Value = StHero10th;
            s.tenthLevelHeroAbilityUsed.Value = StHero10th;

            player.SaveNow();
            Log("Applied all stat fields (both pages) to the profile and saved.");
        }

        private static void VeteranPreset()
        {
            foreach (ModSetting f in AllStatFields)
            {
                if (f is ModSettingDouble dbl) SetNum(dbl, (double)dbl.GetDefaultValue());
                else if (f is ModSettingInt it) SetInt(it, (int)it.GetDefaultValue());
            }
            Log("Veteran preset loaded into fields. Press Apply to write it.");
        }

        private static void SetK(Il2CppSystem.Collections.Generic.Dictionary<string, KonFuze> dict, string key, double val)
        {
            if (dict == null) return;
            if (dict.ContainsKey(key)) dict[key].Value = val;
            else { var k = new KonFuze(); k.Value = val; dict.Add(key, k); }
        }

        private static void SetKi(Il2CppSystem.Collections.Generic.Dictionary<int, KonFuze> dict, int key, double val)
        {
            if (dict == null) return;
            if (dict.ContainsKey(key)) dict[key].Value = val;
            else { var k = new KonFuze(); k.Value = val; dict.Add(key, k); }
        }

        private static double Share(int index, int count, double total)
        {
            double weightSum = 0;
            for (int i = 0; i < count; i++) weightSum += 1.0 / (i + 2);
            double share = total * (1.0 / (index + 2)) / weightSum;
            return Math.Floor(share) + (index * 7919) % 977;
        }

        private static void BotTopMonkeys()
        {
            Btd6Player player = P();
            if (player == null) return;

            var d = player.Data;
            var s = d.analyticsKonFuze;
            var bs = BS(d);
            if (bs == null) { Log("Could not find basicStats on the profile."); return; }

            var towers = new List<string>();
            foreach (string t in Helpers.ValidBaseTowerNames()) towers.Add(t);
            towers.Remove("NinjaMonkey"); towers.Insert(0, "NinjaMonkey");
            towers.Remove("SuperMonkey"); towers.Insert(1, "SuperMonkey");
            towers.Remove("BananaFarm"); towers.Insert(2, "BananaFarm");

            var heroes = new List<string>();
            var seenHero = new HashSet<string>();
            foreach (var td in Game.instance.model.heroSet)
            {
                if (td == null) continue;
                if (seenHero.Add(td.towerId)) heroes.Add(td.towerId);
            }

            double totalPlaced = StPlaced;
            double heroPlaced = StHeroes;
            double totalWins = (int)StGames * 0.61;

            for (int i = 0; i < towers.Count; i++)
            {
                double placed = Share(i, towers.Count, totalPlaced);
                double wins = Share(i, towers.Count, totalWins);
                SetK(bs.towersPlacedByBaseName, towers[i], placed);
                SetK(s.towersPlacedByBaseName, towers[i], placed);
                SetK(bs.monkeyTypeWins, towers[i], wins);
                SetK(s.monkeyTypeWins, towers[i], wins);
            }

            for (int i = 0; i < heroes.Count; i++)
            {
                double placed = Share(i, heroes.Count, heroPlaced);
                double wins = Share(i, heroes.Count, totalWins * 0.4);
                SetK(bs.heroesPlacedByName, heroes[i], placed);
                SetK(s.heroesPlacedByName, heroes[i], placed);
                SetK(bs.heroWonCount, heroes[i], wins);
                SetK(s.heroWonCount, heroes[i], wins);
                SetK(bs.heroLevelsByName, heroes[i], 20);
                SetK(s.heroLevelsByName, heroes[i], 20);
            }

            double totalUpgrades = StUpgrades;
            for (int tier = 1; tier <= 5; tier++)
            {
                double n = Share(tier - 1, 5, totalUpgrades);
                SetKi(bs.upgradesPurchasedByTier, tier, n);
                SetKi(s.upgradesPurchasedByTier, tier, n);
            }

            player.AddTowerXP("NinjaMonkey", 1);
            if (d.towerXp.ContainsKey("NinjaMonkey"))
                d.towerXp["NinjaMonkey"].Value = 13247590;

            player.SaveNow();
            Log($"Botted usage stats for {towers.Count} towers and {heroes.Count} heroes. Ninja Monkey is now your most experienced tower.");
        }

        // ============================== LIFECYCLE ==============================

        public override void OnApplicationStart()
        {
            foreach (ModSetting f in AllStatFields)
            {
                if (f is ModSettingDouble dbl) WireNum(dbl);
                else if (f is ModSettingInt it) WireInt(it);
            }
            WireInt(MoneyAmount);
            WireInt(TrophyAmount);
            WireInt(TowerXpAmount);
            WireStr(Console1, true);
            WireStr(Console2, true);
            WireStr(Console3, true);
            WireStr(Console4, true);
            WireStr(Console5, true);

            MelonLogger.Msg($"BTD6 Unlocker Deluxe v{ModHelperData.Version} loaded. Open Mods -> BTD6 Unlocker Deluxe for the menu. Hotkeys: F1 instas, F2 knowledge, F3 money+upgrades, F4 trophies, F5 trophy store, F6 max level/veteran, F7 veteran stat preset, F8 unlock pop-gated & locked towers, F9 bot realistic map medals (asks first).");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F1)) GiveAllInstas();
            if (Input.GetKeyDown(KeyCode.F2)) UnlockAllKnowledge();
            if (Input.GetKeyDown(KeyCode.F3)) { AddMoney(MoneyAmount); UnlockAllUpgrades(); }
            if (Input.GetKeyDown(KeyCode.F4)) AddTrophies(TrophyAmount);
            if (Input.GetKeyDown(KeyCode.F5)) UnlockTrophyItems();
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Btd6Player player = GameExt.GetBtd6Player(Game.instance);
                if (player == null) { Log("Player not ready."); return; }
                if (player.Data.rank.Value < 155) MaxPlayerLevel();
                else AddVeteranLevels(100);
            }
            if (Input.GetKeyDown(KeyCode.F7)) { VeteranPreset(); ApplyStats(); }
            if (Input.GetKeyDown(KeyCode.F8)) UnlockAllTowers();
            if (Input.GetKeyDown(KeyCode.F9))
                Confirm("Bot realistic map medals?",
                    "Adds a believable spread of medals: nearly all Easy Standards, tapering down to a handful of CHIMPS. " + MedalPromiseText,
                    "Add Medals", BotRealisticMedals);
        }
    }
}
