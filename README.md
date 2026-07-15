# BTD6 Unlocker Deluxe

[![Requires BTD6 Mod Helper](https://raw.githubusercontent.com/gurrenm3/BTD-Mod-Helper/master/banner.png)](https://github.com/gurrenm3/BTD-Mod-Helper#readme)

All-in-one account unlocker and profile editor for Bloons TD 6, with a full in-game menu. A heavily extended fork of [btd6-unlocker](https://github.com/Interesting-exe/btd6-unlocker) by Interesting.

This mod depends on [BTD-Mod-Helper](https://github.com/gurrenm3/BTD-Mod-Helper). <br />
Make sure you follow the [install guide](https://github.com/gurrenm3/BTD-Mod-Helper/wiki/Install-Guide) when installing mod helper.

Everything is available from the mod's settings page: **Mods → BTD6 Unlocker Deluxe → settings**. Every action reports what it did to the console section at the bottom of the page and to the MelonLoader console.

# Features

### UNLOCK EVERYTHING
One button that runs every unlock below, plus monkey money, trophies, realistic medals and all achievements.

### Unlocks
- **Unlock Pop-Gated & Locked Towers** - unlocks every tower and hero through the game's own unlock path, maxes the pop-progress meters, flips Ninja Kiwi's internal debug unlock switches and flags all game modes unlocked. Covers the towers locked behind pop milestones.
- **Unlock All Tower Upgrades** - acquires every upgrade on every path of every tower at zero XP cost.
- **Unlock All Monkey Knowledge** - every point in every tree.
- **Unlock All Trophy Store Items** - including exclusives.
- **Give All Insta Monkeys** - 50 of every tower and crosspath combination.
- **Claim All Achievements** - marks every achievement complete and claims it through the game's own achievement manager, so the reward loot (monkey money, insta monkeys, powers) actually lands on your profile - same as pressing Claim on each one by hand.
- **Max Player Level (155)** - sets rank and XP directly, with no level-up screens to click through.
- **+100 Veteran Levels** - prestige levels, added directly.
- **Bot Realistic Map Medals** - resets map records, then completes a believable spread: nearly all Easy Standards tapering down to a handful of CHIMPS, scaled by map difficulty, with mixed black/normal borders and sparse co-op. Deterministic, so re-running gives the same result.
- **Max All Map Medals (100%)** - the loud version: every mode on every map, black-border quality, single player and co-op.

### Currency
Editable amounts with one-click buttons for **monkey money**, **trophies** and **tower XP**.

### Stat Editor
Around 60 editable lifetime stats across two pages - every pop type (bloons, camo, lead, purple, regrow, ceramic, fortified, MOAB/BFB/ZOMG/DDT/BAD, bosses, golden), cash and eco earned, games played, highest rounds, towers placed and sold, upgrades purchased, abilities used, hero stats, boss damage, trophies (balance, lifetime and spent), monkey money and knowledge point balances, continues, races, challenges, dailies, co-op stats, crits, glue, shimmer reveals, MOAB takedowns and more.

- **Load** - reads your profile's real values into the fields.
- **Fill** - loads a strong-but-believable veteran preset.
- **Apply** - writes every field to your profile and saves.
- **Bot Top Monkeys & Usage Stats** - fills per-monkey placement counts, per-monkey wins, hero usage and levels, and upgrade-tier purchases with a weighted realistic spread scaled to your totals, and makes Ninja Monkey your most experienced tower.

### Console
The last five actions are shown with timestamps at the bottom of the settings page, and everything is mirrored to the MelonLoader console.

### Hotkeys
| Key | Action |
| --- | ------ |
| F1 | Give all insta monkeys |
| F2 | Unlock all monkey knowledge |
| F3 | Add monkey money + unlock all upgrades |
| F4 | Add trophies |
| F5 | Unlock all trophy store items |
| F6 | Max player level, then +100 veteran levels per press |
| F7 | Apply the veteran stat preset |
| F8 | Unlock all towers + bot realistic medals |

# Installing

1. Install [MelonLoader and BTD Mod Helper](https://github.com/gurrenm3/BTD-Mod-Helper/wiki/Install-Guide).
2. Download `BTD6UnlockerDeluxe.dll` from [releases](../../releases) and put it in your `BloonsTD6/Mods` folder.
3. Launch the game.

# Building

Open `BTD6UnlockerDeluxe.sln` with the .NET 6 SDK installed. If your BTD6 install is not in the default Steam location, pass `-p:BloonsTD6="path\to\BloonsTD6"` to `dotnet build`. The DLL is copied to your Mods folder automatically after building.

# Disclaimer

Everything this mod does writes to your Ninja Kiwi profile and **cannot be undone by removing the mod**. Modded unlocks, stats and medals can flag your account - use an alt if you care about that. Use at your own risk.

# Credits

- Original [btd6-unlocker](https://github.com/Interesting-exe/btd6-unlocker) by Interesting - MIT licensed.
- Deluxe fork by **ekruges**.
- Built on [BTD Mod Helper](https://github.com/gurrenm3/BTD-Mod-Helper) by gurrenm3 and doombubbles.
