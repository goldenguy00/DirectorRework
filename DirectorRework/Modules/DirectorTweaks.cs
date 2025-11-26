using System;
using HG;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class DirectorTweaks : MonoBehaviour
    {
        private static event Action<bool> onEnabledChanged;
        private static bool _enabled;
        public static bool ModuleEnabled
        {
            get => _enabled;
            set
            {
                if (value != _enabled)
                {
                    _enabled = value;
                    onEnabledChanged?.Invoke(_enabled);
                }
            }
        }

        public static void Init()
        {
            _enabled = PluginConfig.enableDirectorTweaks.Value;

            On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
        }

        private static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            if (NetworkServer.active)
                self.EnsureComponent<DirectorTweaks>();

            orig(self);
        }

        private CombatDirector director;
        private bool eventHandlersSet = false;

        private float _internalCreditMult;
        private float CreditMultiplier
        {
            get => director.creditMultiplier;
            set
            {
                if (value != _internalCreditMult)
                {
                    if (_internalCreditMult > 0)
                        director.creditMultiplier /= _internalCreditMult;

                    _internalCreditMult = value;

                    if (_internalCreditMult > 0)
                        director.creditMultiplier *= _internalCreditMult;
                }
            }
        }

        public void Awake()
        {
            director = GetComponent<CombatDirector>();
            onEnabledChanged += SetEventHandlers;
            SetEventHandlers(ModuleEnabled);
        }

        public void OnDestroy()
        {
            SetEventHandlers(false);
        }

        public void SetEventHandlers(bool newEnabled)
        {
            if (newEnabled == eventHandlersSet)
                return;

            eventHandlersSet = newEnabled;
            if (eventHandlersSet)
            {
                PluginConfig.useRecommendedValues.SettingChanged += OnSettingValuesChanged;
                PluginConfig.creditMultiplier.SettingChanged += CreditMultiplier_SettingChanged;

                PluginConfig.minRerollSpawnInterval.SettingChanged += MinRerollSpawnInterval_SettingChanged;
                PluginConfig.maxRerollSpawnInterval.SettingChanged += MaxRerollSpawnInterval_SettingChanged;

                PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged += MaximumNumberToSpawnBeforeSkipping_SettingChanged;
                PluginConfig.maxConsecutiveCheapSkips.SettingChanged += MaxConsecutiveCheapSkips_SettingChanged;
            }
            else
            {
                PluginConfig.useRecommendedValues.SettingChanged -= OnSettingValuesChanged;
                PluginConfig.creditMultiplier.SettingChanged -= CreditMultiplier_SettingChanged;

                PluginConfig.minRerollSpawnInterval.SettingChanged -= MinRerollSpawnInterval_SettingChanged;
                PluginConfig.maxRerollSpawnInterval.SettingChanged -= MaxRerollSpawnInterval_SettingChanged;

                PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged -= MaximumNumberToSpawnBeforeSkipping_SettingChanged;
                PluginConfig.maxConsecutiveCheapSkips.SettingChanged -= MaxConsecutiveCheapSkips_SettingChanged;
            }

            OnSettingValuesChanged(null, null);
        }

        private void OnSettingValuesChanged(object _, EventArgs __)
        {
            CreditMultiplier = PluginConfig.creditMultiplier.GetValueOrDefault();

            director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValueOrDefault();
            director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValueOrDefault();

            director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValueOrMax();
            director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValueOrMax();
        }

        private void CreditMultiplier_SettingChanged(object _, EventArgs __)
        {
            CreditMultiplier = PluginConfig.creditMultiplier.GetValueOrDefault();
        }

        private void MinRerollSpawnInterval_SettingChanged(object _, EventArgs __)
        {
            director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValueOrDefault();
        }

        private void MaxRerollSpawnInterval_SettingChanged(object _, EventArgs __)
        {
            director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValueOrDefault();
        }

        private void MaximumNumberToSpawnBeforeSkipping_SettingChanged(object _, EventArgs __)
        {
            director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValueOrDefault();
        }

        private void MaxConsecutiveCheapSkips_SettingChanged(object _, EventArgs __)
        {
            director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault();
        }
    }
}
