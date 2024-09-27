using BepInEx.Configuration;

namespace DirectorRework.Config
{
    public static class PluginConfig
    {
        public static ConfigFile MainConfig;

        public static ConfigEntry<bool> info;
        public static ConfigEntry<bool> enableCruelty;
        public static ConfigEntry<bool> enableDirectorMain;
        public static ConfigEntry<bool> enableDirectorTweaks;


        public static ConfigEntry<int> maxAffixes;
        public static ConfigEntry<bool> guaranteeSpecialBoss;
        public static ConfigEntry<bool> allowBosses;
        public static ConfigEntry<bool> onlyApplyToElites;
        public static ConfigEntry<int> triggerChance;
        public static ConfigEntry<int> successChance;

        public static ConfigEntry<bool> enableBossDiversity;
        public static ConfigEntry<bool> enableSpawnDiversity;
        public static ConfigEntry<bool> enableVieldsDiversity;
        public static ConfigEntry<bool> enableCreditRefund;
        public static ConfigEntry<int> creditRefundMultiplier;

        public static ConfigEntry<float> minimumRerollSpawnIntervalMultiplier;
        public static ConfigEntry<float> maximumRerollSpawnIntervalMultiplier;
        public static ConfigEntry<float> creditMultiplier;
        public static ConfigEntry<float> eliteBiasMultiplier;
        public static ConfigEntry<float> creditMultiplierForEachMountainShrine;
        public static ConfigEntry<float> goldAndExperienceMultiplierForEachMountainShrine;
        public static ConfigEntry<int> maximumNumberToSpawnBeforeSkipping;
        public static ConfigEntry<int> maxConsecutiveCheapSkips;

        public static void Init(ConfigFile cfg)
        {
            MainConfig = cfg;
            RiskOfOptions.ModSettingsManager.SetModDescription("Director Rework Plus Essentially.");

            string section = "Modules";
            enableCruelty = cfg.BindOption(section,
                "Enable Affix Stacking",
                true,
                "Enables Affix Stacking (highly inspired by Artifact of Cruelty in RiskyArtifacts). Disable to prevent all modifications in 'Affixes/Cruelty' from loading.");

            enableDirectorMain = cfg.BindOption(section,
                "Enable Director Changes",
                true,
                "Enables Enemy Variety and all 'Director Main' config options. Disable to prevent all modifications in 'Director Main' from loading.");

            enableDirectorTweaks = cfg.BindOption(section,
                "Enable Combat Director Tweaks",
                true,
                "Enables a variety of configurable Combat Director options. Intended for fine tuning the pacing of spawns. Disable to prevent all modifications in 'Director Tweaks' from loading.");

            section = "Affixes/Cruelty";
            maxAffixes = cfg.BindOptionSlider(section,
                "Max Additional Affixes",
                4,
                "Maximum Affixes that an enemy can have. Combat Director will still need to afford the combined credit cost of the new enemy.",
                1, 10);

            guaranteeSpecialBoss = cfg.BindOption(section,
                "Guarantee Special Boss",
                false,
                "Always apply additional affixes to special bosses. Applies to void cradles and ignores elite credit cost.");

            allowBosses = cfg.BindOption(section,
                "Allow Boss Affix Stacking",
                true,
                "Allows bosses to recieve additional affixes.");

            triggerChance = cfg.BindOptionSlider(section,
                "Trigger Chance",
                25,
                "Chance to apply the first additional affix to an enemy. Set to 100 to make it always apply.",
                0, 100);

            successChance = cfg.BindOptionSlider(section,
                "Additional Affix Chance",
                25,
                "Chance to add an additional affix after the first. Set to 100 to make it always attempt to add as many affixes as possible.",
                0, 100);

            onlyApplyToElites = cfg.BindOption(section,
                "Only Apply to Elites",
                true,
                "Only applies additional affixes to enemies that are already elite. Setting this to false will increase the occurrance of elites as a whole.");


            section = "Director Main";
            enableBossDiversity = cfg.BindOption(section,
                "Enable Boss Diversity",
                true,
                "Spawns multiple boss types during teleporter events.");

            enableSpawnDiversity = cfg.BindOption(section,
                "Enable Spawn Diversity",
                true,
                "Spawns multiple enemy types per wave.");

            enableVieldsDiversity = cfg.BindOption(section,
                "Enable Void Fields Spawn Diversity",
                false,
                "Spawns multiple enemy types in void fields. Selection is limited to the enemy types that can spawn on the final wave.");

            enableCreditRefund = cfg.BindOption(section,
                "Enable Credit Refund",
                false,
                "Gives combat director back a percent of credits spent on spawns when they are killed.");

            creditRefundMultiplier = cfg.BindOption(section,
                "Percent Refund",
                10,
                "Amount to refund the combat director when an enemy is killed, in percent. 100 is a bad idea, but its technically possible.");


            section = "Director Tweaks";

            creditMultiplier = cfg.BindOptionSlider(section,
                "Credit Multiplier",
                1f,
                "How much to multiply money wave yield by.");

            eliteBiasMultiplier = cfg.BindOptionSlider(section,
                "Elite Bias Cost Multiplier",
                1f,
                "Multiplies the elite selection cost. Higher numbers result in higher cost and therefore less elites.");

            minimumRerollSpawnIntervalMultiplier = cfg.BindOptionSlider(section,
                "Minimum Reroll Spawn Interval",
                2.3333333f,
                "Used when a spawn is rejected and the director needs to wait to build more credits.");

            maximumRerollSpawnIntervalMultiplier = cfg.BindOptionSlider(section,
                "Maximum Reroll Spawn Interval",
                4.3333335f,
                "Used when a spawn is rejected and the director needs to wait to build more credits.");

            creditMultiplierForEachMountainShrine = cfg.BindOptionSlider(section,
                "Credit Multiplier For Each Mountain Shrine",
                1f,
                "Credit multiplier for the teleporter director for each mountain shrine");

            goldAndExperienceMultiplierForEachMountainShrine = cfg.BindOptionSlider(section,
                "Gold And Experience Multiplier For Each Mountain Shrine",
                1f,
                "Gold and Exp multiplier for the teleporter director for each mountain shrine");

            maximumNumberToSpawnBeforeSkipping = cfg.BindOptionSlider(section,
                "Maximum Number To Spawn Before Skipping",
                6,
                "Maximum number of enemies in a single wave. If the director can afford more than this, it'll reroll the spawncard.");

            maxConsecutiveCheapSkips = cfg.BindOptionSlider(section,
                "Max Consecutive Cheap Skips",
                -1,
                "If skipSpawnIfTooCheap is true, we'll behave as though it's not set after this many consecutive skips.", -1, 20);

        }


        #region Config Binding
        public static ConfigEntry<T> BindOption<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);
            TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        public static ConfigEntry<T> BindOptionSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);

            TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = 0,
                    max = 20,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
                return;
            }
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
                return;
            }
            if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
                return;
            }
        }

        public static void TryRegisterOptionSlider<T>(ConfigEntry<T> entry, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, new RiskOfOptions.OptionConfigs.IntSliderConfig()
                {
                    min = (int)min,
                    max = (int)max,
                    formatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
                return;
            }

            if (entry is ConfigEntry<float> floatEntry)
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
        }
        #endregion
    }
}
