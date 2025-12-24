using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MiscFixes.Modules;

namespace DirectorRework.Config
{
    public class DirectorConfig
    {
        private readonly ConfigFile cfg;

        public static DirectorConfig Instance { get; private set; }
        internal static void Init() => Instance ??= new DirectorConfig();
        private DirectorConfig()
        {
            BepInPlugin metadata = DirectorReworkPlugin.Instance.Info.Metadata;
            string basePath = Chainloader.IsEditor ? "." : Paths.ConfigPath;
            cfg = new ConfigFile(Utility.CombinePaths(basePath, $"{metadata.GUID}-EliteStacking.cfg"), saveOnInit: true, metadata);
        }

        public static ConfigEntry<bool> enableBossDiversity;
        public static ConfigEntry<bool> enableWorldDiversity;
        public static ConfigEntry<bool> enableVieldsDiversity;
        public static ConfigEntry<bool> enableCreditRefund;
        public static ConfigEntry<int> hordeOfManyChance;
        public static ConfigEntry<int> maxBossSpawns;


        private static void BindDirectorTweaks(ConfigFile cfg, string section)
        {
            useRecommendedValues[section] = cfg.BindOption(section,
                "Use Recommended Values",
                "If enabled, then the default values for this section will be used instead of the ones set in the config." + Environment.NewLine +
                "Disable this setting if you want to change this section. Set any value to -1 to retain the default vanilla value.",
                true);

            creditMultiplier = cfg.BindOptionSlider(section,
                "Credit Multiplier",
                "Multiplies the monster credit accumulation rate.",
                1f,
                -1f, 5f);

            spawnDistanceMultiplier = cfg.BindOptionSlider(section,
                "Spawn Distance Multiplier",
                "Multiplier to the max spawn range from the desired target.",
                1.2f,
                -1f, 5f);

            spawnIntervalVariance = cfg.BindOptionSlider(section,
                "Spawn Interval Multiplier",
                "Adds a degree of randomness to the spawn timings, max delay determined by the interval times this value.",
                1.4f,
                1f, 5f);

            fastSpawnInterval = cfg.BindOptionSlider(section,
                "Fast Spawn Interval",
                "Minimum delay before attempting to spawn a new wave.",
                5f,
                -1f, 20f);

            slowSpawnInterval = cfg.BindOptionSlider(section,
                "Slow Spawn Interval",
                "Minimum delay before attempting to spawn a new expensive wave. Set to -1 to make all directors fast.",
                12f,
                -1f, 30f);

            maximumNumberToSpawnBeforeSkipping = cfg.BindOptionSlider(section,
                "Maximum Number To Spawn Before Skipping",
                "Maximum number of enemies in a single wave. If the director can afford more than this, it'll reroll the spawncard and try again later.",
                5,
                -1, 20);

            maxConsecutiveCheapSkips = cfg.BindOptionSlider(section,
                "Max Consecutive Cheap Skips",
                "If skipSpawnIfTooCheap is true, we'll behave as though it's not set after this many consecutive skips.",
                4,
                -1, 20);
        }

        private static void BindScalingTweaks(ConfigFile cfg, string section)
        {
            useRecommendedValues[section] = cfg.BindOption(section,
                "Use Recommended Values",
                "If enabled, then the recommended values for this section will be used instead of the ones set in the config. " +
                "Disable this setting if you want to change this section. Set any value to -1 to retain the default vanilla value.",
                true);

            rampTyphoonCredits = cfg.BindOption(section,
                "Ramp Overscaled Director Credits",
                "When other mods add credit scaling, it generally is unintentionally difficult on early stages. Enable this option to start stage 1 with 50% of the multipler and ramp up to the full credit multiplier on stage 3",
                true);

            enableLinearScaling = cfg.BindOption(section,
                "Enable Linear Stage Scaling",
                "Enables linear scaling for the stages cleared coefficient. Doesn't affect stage 1 or time scaling. Swaps back when exponential scaling is greater.",
                false);

            linearScalingMultiplier = cfg.BindOptionSlider(section,
                "Linear Scaling Per Stage",
                "Will be applied to the formula (1 + {VALUE} * StagesCleared). A value of 0.2 will make stages 2-5 a bit harder. A value of 0.25 will make stages 2-7 significantly harder.",
                0.2f,
                0.15f, 0.5f);

            enableCreditRefund = cfg.BindOption(section,
                "Enable Credit Refund",
                "Gives combat director back a percent of credits spent on spawns when they are killed.",
                false);

            creditRefundMultiplier = cfg.BindOptionSlider(section,
                "Percent Refund",
                "Amount to refund the combat director when an enemy is killed, in percent. 100 is a bad idea, but its technically possible.",
                15,
                0, 100);
        }
    }
}
