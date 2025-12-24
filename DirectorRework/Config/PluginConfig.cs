using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using MiscFixes.Modules;
using UnityEngine;

namespace DirectorRework.Config
{
    public static class PluginConfig
    {
        public static ConfigEntry<bool> enableEliteStacking;
        public static ConfigEntry<bool> enableSpawnDiversity;
        public static ConfigEntry<bool> enableDirectorTweaks;
        public static ConfigEntry<bool> enableScalingTweaks;
        public static ConfigEntry<bool> enableDebugLogs;
        public static ConfigEntry<bool> enableMoreDebugLogs;

        public static Dictionary<string, ConfigEntry<bool>> useRecommendedValues;
        public static ConfigEntry<float> creditMultiplier;
        public static ConfigEntry<float> spawnDistanceMultiplier;
        public static ConfigEntry<bool> useSlowDirector;
        public static ConfigEntry<float> fastSpawnInterval;
        public static ConfigEntry<float> slowSpawnInterval;
        public static ConfigEntry<float> spawnIntervalVariance;
        public static ConfigEntry<int> maximumNumberToSpawnBeforeSkipping;
        public static ConfigEntry<int> maxConsecutiveCheapSkips;

        public static ConfigEntry<bool> rampTyphoonCredits;
        public static ConfigEntry<bool> enableLinearScaling;
        public static ConfigEntry<float> linearScalingMultiplier;
        public static ConfigEntry<int> creditRefundMultiplier;

        public static T GetValueOrDefault<T>(this ConfigEntry<T> entry)
        {
            if (useRecommendedValues.TryGetValue(entry.Definition.Section, out var recValuesEnabled) && recValuesEnabled.Value)
                return (T)entry.DefaultValue;
            return entry.Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init(ConfigFile cfg)
        {
            if (DirectorReworkPlugin.RooInstalled)
                InitRoO();

            BindModules(MainConfig, "Main");
            BindAffixStacking(cfg, "1. Elite Stacking");
            BindMain(cfg, "2. Spawn Diversity");
            BindDirectorTweaks(cfg, "3. Director Tweaks");
            BindScalingTweaks(cfg, "4. Scaling Tweaks");
        }

        private static void BindModules(ConfigFile cfg, string section)
        {
            useRecommendedValues[section] = cfg.BindOption(section,
                "Use Recommended Values",
                "If enabled, then the recommended values for this section will be used instead of the ones set in the config." + Environment.NewLine +
                "Disable this setting if you want to change this section.",
                true);

            enableEliteStacking = cfg.BindOption(section,
                "Enable Module: Elite Stacking",
                "Enables Elite Affix Stacking, highly inspired by Artifact of Cruelty in RiskyArtifacts." + Environment.NewLine + "Disable to prevent all modifications in 'Elite Stacking' from loading.",
                true);

            enableSpawnDiversity = cfg.BindOption(section,
                "Enable Module: Spawn Diversity",
                "Enables Enemy Variety and all 'Director Main' config options." + Environment.NewLine + "Disable to prevent all modifications in 'Director Main' from loading.",
                true);

            enableDirectorTweaks = cfg.BindOption(section,
                "Enable Module: Director Tweaks",
                "Enables a variety of configurable Combat Director options, intended for fine tuning the pacing of spawns." + Environment.NewLine + "Disable to prevent all modifications in 'Director Tweaks' from loading.",
                true);

            enableScalingTweaks = cfg.BindOption(section,
                "Enable Module: Scaling Tweaks",
                "Enables run scaling tweaks." + Environment.NewLine + "Disable to prevent all modifications in 'Scaling Tweaks' from loading.",
                true);

            enableDebugLogs = cfg.BindOption(section,
                "Enable Debug Logs",
                "Prints some stuff that might be useful. turn this on before reporting a problem :)",
                false,
                Extensions.ConfigFlags.ClientSided);

            enableMoreDebugLogs = cfg.BindOption(section,
                "Enable Even More Debug Logs",
                "Nerd.",
                false,
                Extensions.ConfigFlags.ClientSided);
        }

        private static void BindMain(ConfigFile cfg, string section)
        {
            useRecommendedValues[section] = cfg.BindOption(section,
                "Use Recommended Values",
                "If enabled, then the recommended values for this section will be used instead of the ones set in the config." + Environment.NewLine +
                "Disable this setting if you want to change this section.",
                true);

            enableBossDiversity = cfg.BindOption(section,
                "Enable Boss Diversity",
                "Spawns multiple boss types during teleporter events.",
                true);

            enableWorldDiversity = cfg.BindOption(section,
                "Enable Spawn Diversity",
                "Spawns multiple enemy types per wave.",
                true);

            enableVieldsDiversity = cfg.BindOption(section,
                "Enable Void Fields Spawn Diversity",
                "Spawns multiple enemy types in void fields. Selection is limited to the enemy types that can spawn on the final wave.",
                true);

            hordeOfManyChance = cfg.BindOptionSlider(section,
                "Horde of Many Chance",
                "Chance to replace the teleporter event with a Horde of Many. Vanilla is 0 (only used when it can't spawn normal bosses)",
                5,
                0, 100);

            maxBossSpawns = cfg.BindOptionSlider(section,
                "Max Boss/Horde Spawns",
                "Maximum number of enemies that can be spawned as the teleporter boss, scaled with playercount. Affects normal bosses and horde of many. Vanilla is 6.",
                12,
                0, 100);
        }

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
                Log.Info(e.ToString());
            }
        }
    }
}
