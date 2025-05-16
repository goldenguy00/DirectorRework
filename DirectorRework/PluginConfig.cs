using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

namespace DirectorRework
{
    public static class PluginConfig
    {
        public static ConfigFile MainConfig;

        public static ConfigEntry<bool> enableCruelty;
        public static ConfigEntry<bool> enableDirectorMain;
        public static ConfigEntry<bool> enableDirectorTweaks;
        public static ConfigEntry<bool> enableScalingTweaks;

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
        public static ConfigEntry<int> hordeOfManyChance;
        public static ConfigEntry<int> maxBossSpawns;

        public static ConfigEntry<bool> useRecommendedValues;
        public static ConfigEntry<float> minRerollSpawnInterval;
        public static ConfigEntry<float> maxRerollSpawnInterval;
        public static ConfigEntry<float> creditMultiplier;
        public static ConfigEntry<int> maximumNumberToSpawnBeforeSkipping;
        public static ConfigEntry<int> maxConsecutiveCheapSkips;

        public static ConfigEntry<bool> rampTyphoonCredits;
        public static ConfigEntry<bool> enableLinearScaling;
        public static ConfigEntry<float> linearScalingMultiplier;

        public static T GetValue<T>(this ConfigEntry<T> entry)
        {
            if (useRecommendedValues.Value)
                return (T)entry.DefaultValue;
            return entry.Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init(ConfigFile cfg)
        {
            MainConfig = cfg;

            if (DirectorReworkPlugin.RooInstalled)
                InitRoO();

            BindModules(cfg, "1. Modules");
            BindCruelty(cfg, "2. Affixes/Cruelty");
            BindMain(cfg, "3. Director Main");
            BindDirectorTweaks(cfg, "4. Director Tweaks");
            BindScalingTweaks(cfg, "5. Scaling Tweaks");
        }

        private static void BindModules(ConfigFile cfg, string section)
        {
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
                "Enables a variety of configurable Combat Director options. Intended for fine tuning the pacing of spawns. Disable to prevent all modifications in 'Director Tweaks' from loading.");

            enableScalingTweaks = cfg.BindOption(section,
                "Enable Scaling Tweaks",
                true,
                "Enables run scaling tweaks. Disable to prevent all modifications in 'Scaling Tweaks' from loading.");
        }

        private static void BindCruelty(ConfigFile cfg, string section)
        {
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
                true,
                "Always apply additional affixes to special bosses in scripted events. Applies to void cradles, Mithrix, Alloy Worship Unit, etc");

            allowBosses = cfg.BindOption(section,
                "Allow Boss Affix Stacking",
                true,
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
                25,
                "Chance to apply the first additional affix to an enemy. Set to 100 to make it always apply.",
                0, 100);

            successChance = cfg.BindOptionSlider(section,
                "Additional Affix Chance",
                10,
                "Chance to add an additional affix after the first. Set to 100 to make it always attempt to add as many affixes as possible.",
                0, 100);
        }

        private static void BindMain(ConfigFile cfg, string section)
        {
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
                true,
                "Spawns multiple enemy types in void fields. Selection is limited to the enemy types that can spawn on the final wave.");

            enableCreditRefund = cfg.BindOption(section,
                "Enable Credit Refund",
                false,
                "Gives combat director back a percent of credits spent on spawns when they are killed.");

            creditRefundMultiplier = cfg.BindOptionSlider(section,
                "Percent Refund",
                15,
                "Amount to refund the combat director when an enemy is killed, in percent. 100 is a bad idea, but its technically possible.",
                0, 100);

            hordeOfManyChance = cfg.BindOptionSlider(section,
                "Horde of Many Chance",
                5,
                "Chance to replace the teleporter event with a Horde of Many. Vanilla is 0 (only used when it can't spawn normal bosses)",
                0, 100);

            maxBossSpawns = cfg.BindOptionSlider(section,
                "Max Boss/Horde Spawns",
                12,
                "Maximum number of enemies that can be spawned as the teleporter boss, scaled with playercount. Affects normal bosses and horde of many. Vanilla is 6.",
                0, 100);
        }

        private static void BindDirectorTweaks(ConfigFile cfg, string section)
        {
            useRecommendedValues = cfg.BindOption(section,
                "Use Recommended Values",
                true,
                "If enabled, then the recommended values for this section will be used instead of the ones set in the config. " +
                "Disable this setting if you want to change this section.");

            creditMultiplier = cfg.BindOptionSlider(section,
                "Credit Multiplier",
                1f,
                "How much to multiply money wave yield by. Vanilla is 1.",
                0.1f, 5f);

            minRerollSpawnInterval = cfg.BindOptionSlider(section,
                "Minimum Reroll Spawn Interval",
                4f,
                "Used when a spawn is rejected and the director needs to wait to build more credits. Vanilla is 2.33333",
                0.1f, 20f);

            maxRerollSpawnInterval = cfg.BindOptionSlider(section,
                "Maximum Reroll Spawn Interval",
                6f,
                "Used when a spawn is rejected and the director needs to wait to build more credits. Vanilla is 4.33333",
                0.1f, 20f);

            maximumNumberToSpawnBeforeSkipping = cfg.BindOptionSlider(section,
                "Maximum Number To Spawn Before Skipping",
                6,
                "Maximum number of enemies in a single wave. If the director can afford more than this, it'll reroll the spawncard. Vanilla is 6",
                1, 20);

            maxConsecutiveCheapSkips = cfg.BindOptionSlider(section,
                "Max Consecutive Cheap Skips",
                6,
                "If skipSpawnIfTooCheap is true, we'll behave as though it's not set after this many consecutive skips. Vanilla is -1",
                -1, 20);
        }

        private static void BindScalingTweaks(ConfigFile cfg, string section)
        {
            rampTyphoonCredits = cfg.BindOption(section,
                "Ramp Director Credit Scaling",
                true,
                "When other mods add credit scaling, it generally is unintentionally difficult on early stages. Enable this option to start stage 1 with 50% of the multipler and ramp up to the full credit multiplier on stage 3");

            enableLinearScaling = cfg.BindOption(section,
                "Enable Linear Stage Scaling",
                false,
                "Enables linear scaling for the stages cleared coefficient. Doesn't affect stage 1 or time scaling. Swaps back when exponential scaling is greater.");

            linearScalingMultiplier = cfg.BindOptionSlider(section,
                "Linear Scaling Per Stage",
                0.2f,
                "Will be applied to the formula (1 + {VALUE} * StagesCleared). A value of 0.2 will make stages 2-5 a bit harder. A value of 0.25 will make stages 2-7 significantly harder.",
                0.15f, 0.5f);
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
            if (defaultValue is int or float)
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

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description));

            if (DirectorReworkPlugin.RooInstalled)
                TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (!(defaultValue is int or float))
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

            if (DirectorReworkPlugin.RooInstalled)
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

            if (DirectorReworkPlugin.RooInstalled)
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
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
            else if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
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
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
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
