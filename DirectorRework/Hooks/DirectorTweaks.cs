using System;
using DirectorRework.Config;
using RoR2;

namespace DirectorRework.Hooks
{
    internal class DirectorTweaks : IHookProvider
    {
        public bool HooksEnabled { get; set; }

        public static DirectorTweaks instance;

        public static void Init() => instance ??= new DirectorTweaks();

        private DirectorTweaks()
        {
            PluginConfig.enableDirectorTweaks.SettingChanged += OnSettingChanged;
            OnSettingChanged(null, null);
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
                On.RoR2.CombatDirector.OnEnable += CombatDirector_OnEnable;
                On.RoR2.TeleporterInteraction.ChargingState.OnEnter += ChargingState_OnEnter;
                HooksEnabled = true;
            }
        }

        public void UnsetHooks()
        {
            if (HooksEnabled)
            {
                On.RoR2.CombatDirector.OnEnable -= CombatDirector_OnEnable;
                On.RoR2.TeleporterInteraction.ChargingState.OnEnter -= ChargingState_OnEnter;

                HooksEnabled = false;
            }
        }

        private void ChargingState_OnEnter(On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig, EntityStates.BaseState self)
        {
            if (self is TeleporterInteraction.ChargingState state && state != null && state.teleporterInteraction)
            {
                var stacks = state.teleporterInteraction.shrineBonusStacks;
                if (stacks > 0)
                {
                    var dir = state.bossDirector;
                    if (dir)
                    {
                        dir.creditMultiplier *= stacks * PluginConfig.creditMultiplierForEachMountainShrine.Value;
                        dir.expRewardCoefficient *= stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.Value;
                        dir.goldRewardCoefficient *= stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.Value;
                    }

                    dir = state.bonusDirector;
                    if (dir)
                    {
                        dir.creditMultiplier *= stacks * PluginConfig.creditMultiplierForEachMountainShrine.Value;// * Mathf.Pow(Run.instance.participatingPlayerCount, 0.05f);
                        dir.expRewardCoefficient *= stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.Value;
                        dir.goldRewardCoefficient *= stacks * PluginConfig.goldAndExperienceMultiplierForEachMountainShrine.Value;
                    }
                }
            }
            orig(self);
        }

        private void CombatDirector_OnEnable(On.RoR2.CombatDirector.orig_OnEnable orig, CombatDirector self)
        {
            if (PluginConfig.maxConsecutiveCheapSkips.Value >= 0)
                self.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.Value;
            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.Value;
            self.minRerollSpawnInterval = PluginConfig.minimumRerollSpawnIntervalMultiplier.Value;
            self.maxRerollSpawnInterval = PluginConfig.maximumRerollSpawnIntervalMultiplier.Value;
            self.creditMultiplier = PluginConfig.creditMultiplier.Value;
            self.eliteBias = PluginConfig.eliteBiasMultiplier.Value;

            orig(self);
        }
    }
}
