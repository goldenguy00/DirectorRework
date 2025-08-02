using System;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class DirectorTweaks : MonoBehaviour
    {
        private static bool _hooksEnabled;
        public static bool Enabled
        {
            get => _hooksEnabled;
            set
            {
                if (value != _hooksEnabled)
                {
                    _hooksEnabled = value;
                    OnEnabledChanged();
                }
            }
        }

        public static void Init()
        {
            Enabled = PluginConfig.enableDirectorTweaks.Value;

            On.RoR2.CombatDirector.Awake += CombatDirector_Awake;

            PluginConfig.useRecommendedValues.SettingChanged += OnSettingValuesChanged;

            PluginConfig.creditMultiplier.SettingChanged += CreditMultiplier_SettingChanged;

            PluginConfig.minRerollSpawnInterval.SettingChanged += MinRerollSpawnInterval_SettingChanged;
            PluginConfig.maxRerollSpawnInterval.SettingChanged += MaxRerollSpawnInterval_SettingChanged;

            PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged += MaximumNumberToSpawnBeforeSkipping_SettingChanged;
            PluginConfig.maxConsecutiveCheapSkips.SettingChanged += MaxConsecutiveCheapSkips_SettingChanged;
        }

        private static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            if (NetworkServer.active && !self.GetComponent<DirectorTweaks>())
                self.gameObject.AddComponent<DirectorTweaks>();

            orig(self);
        }

        #region Event Handlers
        private static void OnEnabledChanged()
        {
            if (Enabled)
            {
                OnSettingValuesChanged(null, null);
            }
            else
            {
                foreach (var director in CombatDirector.instancesList)
                {
                    if (_prevCreditMult != 1f)
                        director.creditMultiplier /= _prevCreditMult;

                    director.minRerollSpawnInterval = 2.33333325f;
                    director.maxRerollSpawnInterval = 4.33333349f;

                    director.maximumNumberToSpawnBeforeSkipping = 6;
                    director.maxConsecutiveCheapSkips = int.MaxValue;
                }
            }
        }
        #endregion
        private float _prevCreditMult = 1f;
        private CombatDirector director;

        public void Awake()
        {
            director = GetComponent<CombatDirector>();
            _prevCreditMult = PluginConfig.creditMultiplier.GetValueOrDefault();
            self.creditMultiplier *= _prevCreditMult;

            self.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValueOrDefault();
            self.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValueOrDefault();

            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValueOrDefault();
            self.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault();
        }

        private void OnSettingValuesChanged(object _, EventArgs __)
        {
            var newCreditMult = PluginConfig.creditMultiplier.GetValueOrDefault();

            if (newCreditMult != _prevCreditMult)
            {
                director.creditMultiplier /= _prevCreditMult;
                director.creditMultiplier *= newCreditMult;
            }

            director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValueOrDefault();
            director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValueOrDefault();

            director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValueOrDefault();
            director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault();

            _prevCreditMult = newCreditMult;
        }

        private void CreditMultiplier_SettingChanged(object _, EventArgs __)
        {
            var newCreditMult = PluginConfig.creditMultiplier.GetValueOrDefault();

            if (newCreditMult != _prevCreditMult)
            {
                director.creditMultiplier /= _prevCreditMult;
                director.creditMultiplier *= newCreditMult;
            }
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
