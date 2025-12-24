using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MiscFixes.Modules;

namespace DirectorRework.Config
{
    public class CrueltyConfig
    {
        private readonly ConfigFile cfg;

        public static CrueltyConfig Instance { get; private set; }
        internal static void Init() => Instance ??= new CrueltyConfig();
        private CrueltyConfig()
        {
            BepInPlugin metadata = DirectorReworkPlugin.Instance.Info.Metadata;
            string basePath = Chainloader.IsEditor ? "." : Paths.ConfigPath;
            cfg = new ConfigFile(Utility.CombinePaths(basePath, $"{metadata.GUID}-EliteStacking.cfg"), saveOnInit: true, metadata);
        }

        public ConfigEntry<int> worldMaxAffixes;
        public ConfigEntry<int> scriptedMaxAffixes;
        public ConfigEntry<bool> alwaysEliteEvent;
        public ConfigEntry<bool> allowBosses;
        public ConfigEntry<bool> worldOnlyPromote;
        public ConfigEntry<bool> bossesAreElite;
        public ConfigEntry<bool> scriptedStats;
        public ConfigEntry<int> scriptedTriggerChance;
        public ConfigEntry<int> worldAdditionalChance;
        public ConfigEntry<string> worldBlacklistedElites;
        private static void BindAffixStacking(ConfigFile cfg, string section)
        {
            useRecommendedValues[section] = cfg.BindOption(section,
                "Use Recommended Values",
                "If enabled, then the recommended values for this section will be used instead of the ones set in the config." + Environment.NewLine +
                "Disable this setting if you want to change this section.",
                true);
            #region World
            worldMaxAffixes = cfg.BindOptionSlider(section,
                "World: Max Affixes",
                "Maximum Affixes that a world spawned enemy can have. Combat Director will still need to afford the combined credit cost of the new enemy.",
                3,
                0, 10);

            worldBossStats = cfg.BindOption(section,
                "World: Boss Bonus Stats",
                "When enabled, world spawned and teleporter bosses recieve bonus health and damage buffs from each affix applied.",
                false);

            worldOnlyPromote = cfg.BindOption(section,
                "World: Only Promote Elites",
                "When enabled, all world and teleporter spawns must already be elite to be considered for elite affix stacking. Disabling this will increase the occurrance of elites as a whole.",
                true);

            worldTriggerChance = cfg.BindOptionSlider(section,
                "World: First Promotion Chance",
                "Percent chance to give a second elite affix to a world or teleporter spawned enemy, if affordable.",
                20,
                0, 100);

            worldAdditionalChance = cfg.BindOptionSlider(section,
                "World: Additional Promotion Chance",
                "Percent chance for each additional elite affix after the first, if it can afford to. Set to 100 to make it always attempt to add as many affixes as possible.",
                10,
                0, 100);

            worldBlacklistedElites = cfg.BindOption(section,
                "World: Blacklist",
                "Blacklisted elite equipment names. Use internal names, like EliteLunarEquipment",
                "EliteLunarEquipment, AffixBlightedMoffein");
            #endregion

            #region Tele
            worldMaxAffixes = cfg.BindOptionSlider(section,
                "World: Max Affixes",
                "Maximum Affixes that a world spawned enemy can have. Combat Director will still need to afford the combined credit cost of the new enemy.",
                3,
                0, 10);

            worldBossStats = cfg.BindOption(section,
                "World: Boss Bonus Stats",
                "When enabled, world spawned and teleporter bosses recieve bonus health and damage buffs from each affix applied.",
                false);

            worldOnlyPromote = cfg.BindOption(section,
                "World: Only Promote Elites",
                "When enabled, all world and teleporter spawns must already be elite to be considered for elite affix stacking. Disabling this will increase the occurrance of elites as a whole.",
                true);

            worldTriggerChance = cfg.BindOptionSlider(section,
                "World: First Promotion Chance",
                "Percent chance to give a second elite affix to a world or teleporter spawned enemy, if affordable.",
                20,
                0, 100);

            worldAdditionalChance = cfg.BindOptionSlider(section,
                "World: Additional Promotion Chance",
                "Percent chance for each additional elite affix after the first, if it can afford to. Set to 100 to make it always attempt to add as many affixes as possible.",
                10,
                0, 100);

            worldBlacklistedElites = cfg.BindOption(section,
                "World: Blacklist",
                "Blacklisted elite equipment names. Use internal names, like EliteLunarEquipment",
                "EliteLunarEquipment, AffixBlightedMoffein");
            #endregion

            #region Scripted
            scriptedMaxAffixes = cfg.BindOptionSlider(section,
                "Scripted: Max Affixes",
                "Maximum Affixes that an enemy from a scripted event (Mithrix, void cradle) can have. Set to 0 to disable.",
                1,
                0, 10);

            scriptedStats = cfg.BindOption(section,
                "Scripted: Bonus Stats",
                "When enabled, scripted event enemies recieve bonus health and damage buffs from each affix applied.",
                false);

            scriptedTriggerChance = cfg.BindOptionSlider(section,
                "Scripted Event: Elite Promotion Chance",
                "Chance to promote a non-elite encounter to an elite. Set to 100 to make it always apply.",
                100,
                0, 100);

            scriptedAdditionalChance = cfg.BindOptionSlider(section,
                "Scripted: Elite Stacking Chance",
                "Chance to add an additional affix after the first. Set to 100 to make it always attempt to add as many affixes as possible.",
                25,
                0, 100);

            scriptedBlacklistedElites = cfg.BindOption(section,
                "Scripted: Blacklist",
                "Blacklisted elite equipment names. Use internal names, like EliteLunarEquipment",
                "EliteLunarEquipment, AffixBlightedMoffein");
            #endregion
        }
    }
}
