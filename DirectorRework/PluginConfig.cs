using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

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
        public static ConfigEntry<int> maxScriptedAffixes;
        public static ConfigEntry<bool> guaranteeSpecialBoss;
        public static ConfigEntry<bool> allowBosses;
        public static ConfigEntry<bool> onlyApplyToElites;
        public static ConfigEntry<bool> bossesAreElite;
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init(ConfigFile cfg)
        {
            MainConfig = cfg;

            if (DirectorReworkPlugin.RooInstalled)
                InitRoO();

            string section = "1. Modules";
            enableCruelty = cfg.BindOption(section,
                "Enable Affix Stacking",
                false,
                "Enables Affix Stacking (highly inspired by Artifact of Cruelty in RiskyArtifacts). Disable to prevent all modifications in 'Affixes/Cruelty' from loading.");

            enableDirectorMain = cfg.BindOption(section,
                "Enable Director Changes",
                true,
                "Enables Enemy Variety and all 'Director Main' config options. Disable to prevent all modifications in 'Director Main' from loading.");

            enableDirectorTweaks = cfg.BindOption(section,
                "Enable Combat Director Tweaks",
                true,
                "!!! Highly recommend to disable/zero out other mods that modify the combat director. Some options will stack exponentially !!!" +
                "\r\n\r\nEnables a variety of configurable Combat Director options. Intended for fine tuning the pacing of spawns. Disable to prevent all modifications in 'Director Tweaks' from loading.");

            section = "2. Affixes/Cruelty";
            maxAffixes = cfg.BindOptionSlider(section,
                "Max Additional Affixes",
                3,
                "Maximum Affixes that an enemy can have. Combat Director will still need to afford the combined credit cost of the new enemy.",
                0, 10);

            maxScriptedAffixes = cfg.BindOptionSlider(section,
                "Max Scripted Event Affixes",
                2,
                "Maximum Affixes that an enemy from a scripted event (Mithrix, void cradle) can have.",
                0, 10);

            guaranteeSpecialBoss = cfg.BindOption(section,
                "Guarantee Special Boss",
                false,
                "Always apply additional affixes to special bosses in scripted events. Applies to void cradles, Mithrix, Alloy Worship Unit, etc");

            allowBosses = cfg.BindOption(section,
                "Allow Boss Affix Stacking",
                false,
                "Allows any bosses to recieve additional affixes. This setting is ignored during scripted combat events if Guarantee Special Boss is true.");

            onlyApplyToElites = cfg.BindOption(section,
                "Only Apply to Elites",
                true,
                "Only applies additional affixes to enemies that are already elite. Setting this to false will increase the occurrance of elites as a whole.");

            bossesAreElite = cfg.BindOption(section,
                "Bosses Ignore Elite Setting",
                true,
                "When enabled, bosses do not have to be elite to receive affixes.");

            triggerChance = cfg.BindOptionSlider(section,
                "Trigger Chance",
                20,
                "Chance to apply the first additional affix to an enemy. Set to 100 to make it always apply.",
                0, 100);

            successChance = cfg.BindOptionSlider(section,
                "Additional Affix Chance",
                20,
                "Chance to add an additional affix after the first. Set to 100 to make it always attempt to add as many affixes as possible.",
                0, 100);


            section = "3. Director Main";
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

            creditRefundMultiplier = cfg.BindOptionSlider(section,
                "Percent Refund",
                10,
                "Amount to refund the combat director when an enemy is killed, in percent. 100 is a bad idea, but its technically possible.",
                0, 100);


            section = "Director Tweaks";

            creditMultiplier = cfg.BindOptionSlider(section,
                "Credit Multiplier",
                1f,
                "How much to multiply money wave yield by. Vanilla is 1. " +
                "\r\n\r\n!!! ITS RECOMMENDED TO SET ALL OTHER MODS TO 1 !!!",
                0, 5);

            eliteBiasMultiplier = cfg.BindOptionSlider(section,
                "Elite Bias Cost Multiplier",
                1f,
                "Multiplies the elite selection cost. Higher numbers result in higher cost and therefore less elites. Vanilla is 1",
                0, 5);

            minimumRerollSpawnIntervalMultiplier = cfg.BindOptionSlider(section,
                "Minimum Reroll Spawn Interval",
                4.3333333f,
                "Used when a spawn is rejected and the director needs to wait to build more credits. Vanilla is 2.33333",
                0, 20);

            maximumRerollSpawnIntervalMultiplier = cfg.BindOptionSlider(section,
                "Maximum Reroll Spawn Interval",
                6.3333335f,
                "Used when a spawn is rejected and the director needs to wait to build more credits. Vanilla is 4.33333",
                0, 20);

            creditMultiplierForEachMountainShrine = cfg.BindOptionSlider(section,
                "Credit Multiplier For Each Mountain Shrine",
                1f,
                "Credit multiplier for the teleporter director for each mountain shrine",
                0, 5);

            goldAndExperienceMultiplierForEachMountainShrine = cfg.BindOptionSlider(section,
                "Gold And Experience Multiplier For Each Mountain Shrine",
                1f,
                "Gold and Exp multiplier for the teleporter director for each mountain shrine. Vanilla is 1",
                0, 5);

            maximumNumberToSpawnBeforeSkipping = cfg.BindOptionSlider(section,
                "Maximum Number To Spawn Before Skipping",
                10,
                "Maximum number of enemies in a single wave. If the director can afford more than this, it'll reroll the spawncard. Vanilla is 6",
                0, 20);

            maxConsecutiveCheapSkips = cfg.BindOptionSlider(section,
                "Max Consecutive Cheap Skips",
                2,
                "If skipSpawnIfTooCheap is true, we'll behave as though it's not set after this many consecutive skips. Vanilla is -1",
                -1, 20);

        }

        #region Config Binding
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitRoO()
        {
            try
            {
                RiskOfOptions.ModSettingsManager.SetModDescription("Combat Director Tweaks and Elite Stacking", DirectorReworkPlugin.PluginGUID, DirectorReworkPlugin.PluginName);

                var iconStream = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(DirectorReworkPlugin.Instance.Info.Location), "icon.png"));
                var tex = new Texture2D(256, 256);
                tex.LoadImage(iconStream);
                var icon = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

                RiskOfOptions.ModSettingsManager.SetModIcon(icon);
            }
            catch (Exception e) 
            {
                Log.Debug(e.ToString()); 
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOption<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (defaultValue is int or float && !typeof(T).IsEnum)
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a numeric {typeof(T).Name} type, " +
                    $"but has been registered without using {nameof(BindOptionSlider)}. " +
                    $"Lower and upper bounds will be set to the defaults [0, 20]. Was this intentional?");
#endif
                return myConfig.BindOptionSlider(section, name, defaultValue, description, 0, 20, restartRequired);
            }
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = null;
            if (typeof(T).IsEnum)
                range = new AcceptableValueList<string>(Enum.GetNames(typeof(T)));

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));
            TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (!(defaultValue is int or float && !typeof(T).IsEnum))
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a not a numeric {typeof(T).Name} type, " +
                    $"but has been registered as a slider option using {nameof(BindOptionSlider)}. Was this intentional?");
#endif
                return myConfig.BindOption(section, name, defaultValue, description, restartRequired);
            }

            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = typeof(T) == typeof(int)
                ? new AcceptableValueRange<int>((int)min, (int)max)
                : new AcceptableValueRange<float>(min, max);

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));

            TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSteppedSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, float increment = 1f, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            TryRegisterOptionSteppedSlider(configEntry, increment, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, new RiskOfOptions.OptionConfigs.InputFieldConfig()
                {
                    submitOn = RiskOfOptions.OptionConfigs.InputFieldConfig.SubmitEnum.OnExitOrSubmit,
                    restartRequired = restartRequired
                }));
            }
            else if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
            }
            else if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
            }
            else if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOption)}.");
#endif
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
            }
            else if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSlider)}.");
#endif
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TryRegisterOptionSteppedSlider<T>(ConfigEntry<T> entry, float increment, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(floatEntry, new RiskOfOptions.OptionConfigs.StepSliderConfig()
                {
                    increment = increment,
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSteppedSlider)}.");
#endif
            }
        }
        #endregion
    }
}
