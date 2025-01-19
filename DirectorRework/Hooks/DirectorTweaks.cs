using System;
using DirectorRework.Config;
using RoR2;

namespace DirectorRework.Hooks
{
    internal class DirectorTweaks
    {
        public bool HooksEnabled { get; set; }
        private float prevCreditMult = PluginConfig.creditMultiplier.GetValue();
        private float prevEliteBias = PluginConfig.eliteBias.GetValue();

        public static DirectorTweaks instance;

        public static void Init() => instance ??= new DirectorTweaks();

        private DirectorTweaks()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableDirectorTweaks.SettingChanged += OnSettingChanged;
            PluginConfig.useRecommendedValues.SettingChanged += OnSettingValuesChanged;

            PluginConfig.minRerollSpawnInterval.SettingChanged += OnSettingValuesChanged;
            PluginConfig.maxRerollSpawnInterval.SettingChanged += OnSettingValuesChanged;
            PluginConfig.maxConsecutiveCheapSkips.SettingChanged += OnSettingValuesChanged;
            PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged += OnSettingValuesChanged;

            PluginConfig.creditMultiplierForEachMountainShrine.SettingChanged += OnSettingValuesChanged;
            PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.SettingChanged += OnSettingValuesChanged;

            PluginConfig.creditMultiplier.SettingChanged += OnSettingValuesChanged;
            PluginConfig.eliteBias.SettingChanged += OnSettingValuesChanged;
        }

        public void OnSettingChanged(object sender, EventArgs args)
        {
            if (PluginConfig.enableDirectorTweaks.Value)
                SetHooks();
            else
                UnsetHooks();
        }

        public void SetHooks()
        {
            if (!HooksEnabled)
            {
                On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
                On.RoR2.TeleporterInteraction.ChargingState.OnEnter += ChargingState_OnEnter;

                HooksEnabled = true;
            }
        }

        public void UnsetHooks()
        {
            if (HooksEnabled)
            {
                On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;
                On.RoR2.TeleporterInteraction.ChargingState.OnEnter -= ChargingState_OnEnter;

                HooksEnabled = false;
            }
        }

        public void OnSettingValuesChanged(object sender, EventArgs args)
        {
            if (HooksEnabled)
            {
                foreach (var director in CombatDirector.instancesList)
                {
                    director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValue() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValue();
                    director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValue();
                    director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValue();
                    director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValue();

                    director.creditMultiplier /= prevCreditMult;
                    director.creditMultiplier *= PluginConfig.creditMultiplier.GetValue();
                    prevCreditMult = PluginConfig.creditMultiplier.GetValue();

                    director.eliteBias /= prevEliteBias;
                    director.eliteBias *= PluginConfig.eliteBias.GetValue();
                    prevEliteBias = PluginConfig.eliteBias.GetValue();
                }
            }
        }

        private void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            self.creditMultiplier *= PluginConfig.creditMultiplier.GetValue();
            self.eliteBias *= PluginConfig.eliteBias.GetValue();

            self.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValue() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValue();
            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValue();
            self.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValue();
            self.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValue();

            orig(self);
        }

        private void ChargingState_OnEnter(On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig, EntityStates.BaseState self)
        {
            if (self is TeleporterInteraction.ChargingState state && state.teleporterInteraction)
            {
                var stacks = state.teleporterInteraction.shrineBonusStacks;
                if (stacks > 0)
                {
                    var dir = state.bossDirector;
                    if (dir)
                    {
                        dir.creditMultiplier += dir.creditMultiplier * (1f - (stacks * PluginConfig.creditMultiplierForEachMountainShrine.GetValue()));
                        dir.expRewardCoefficient += dir.expRewardCoefficient * (1f - (stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.GetValue()));
                        dir.goldRewardCoefficient += dir.goldRewardCoefficient * (1f - (stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.GetValue()));
                    }

                    dir = state.bonusDirector;
                    if (dir)
                    {
                        dir.creditMultiplier += dir.creditMultiplier * (1f - (stacks * PluginConfig.creditMultiplierForEachMountainShrine.GetValue()));
                        dir.expRewardCoefficient += dir.expRewardCoefficient * (1f - (stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.GetValue()));
                        dir.goldRewardCoefficient += dir.goldRewardCoefficient * (1f - (stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.GetValue()));
                    }
                }
            }

            orig(self);
        }
    }
}
