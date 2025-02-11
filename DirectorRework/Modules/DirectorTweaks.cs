using System;
using RoR2;

namespace DirectorRework.Modules
{
    public class DirectorTweaks
    {
        private float _prevCreditMult = 1f;

        private bool _hooksEnabled;
        public bool Enabled
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
        public static DirectorTweaks Instance { get; private set; }
        public static void Init() => Instance ??= new DirectorTweaks();

        private DirectorTweaks()
        {
            Enabled = PluginConfig.enableDirectorTweaks.Value;

            PluginConfig.useRecommendedValues.SettingChanged += OnSettingValuesChanged;

            PluginConfig.creditMultiplier.SettingChanged += CreditMultiplier_SettingChanged;

            PluginConfig.minRerollSpawnInterval.SettingChanged += MinRerollSpawnInterval_SettingChanged;
            PluginConfig.maxRerollSpawnInterval.SettingChanged += MaxRerollSpawnInterval_SettingChanged;

            PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged += MaximumNumberToSpawnBeforeSkipping_SettingChanged;
            PluginConfig.maxConsecutiveCheapSkips.SettingChanged += MaxConsecutiveCheapSkips_SettingChanged;
        }

        private void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            _prevCreditMult = PluginConfig.creditMultiplier.GetValue();
            self.creditMultiplier *= _prevCreditMult;

            self.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValue();
            self.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValue();

            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValue();
            self.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValue() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValue();

            orig(self);
        }

        #region Event Handlers
        private void OnEnabledChanged()
        {
            if (Enabled)
            {
                On.RoR2.CombatDirector.Awake += CombatDirector_Awake;

                OnSettingValuesChanged(null, null);
            }
            else
            {
                On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;

                foreach (var director in CombatDirector.instancesList)
                {
                    if (_prevCreditMult != 1f)
                        director.creditMultiplier /= _prevCreditMult;

                    director.minRerollSpawnInterval = 2.33333325f;
                    director.maxRerollSpawnInterval = 4.33333349f;

                    director.maximumNumberToSpawnBeforeSkipping = 6;
                    director.maxConsecutiveCheapSkips = int.MaxValue;
                }

                _prevCreditMult = 1f;
            }
        }

        private void OnSettingValuesChanged(object sender, EventArgs e)
        {
            var newCreditMult = PluginConfig.creditMultiplier.GetValue();

            foreach (var director in CombatDirector.instancesList)
            {
                if (newCreditMult != _prevCreditMult)
                {
                    director.creditMultiplier /= _prevCreditMult;
                    director.creditMultiplier *= newCreditMult;
                }

                director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValue();
                director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValue();

                director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValue();
                director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValue() <= 0 ? int.MaxValue : PluginConfig.maxConsecutiveCheapSkips.GetValue();
            }

            _prevCreditMult = newCreditMult;
        }

        private void CreditMultiplier_SettingChanged(object sender, EventArgs e)
        {
            if (!Enabled)
                return;

            var newCreditMult = PluginConfig.creditMultiplier.GetValue();

            if (newCreditMult != _prevCreditMult)
            {
                foreach (var director in CombatDirector.instancesList)
                {
                    director.creditMultiplier /= _prevCreditMult;
                    director.creditMultiplier *= newCreditMult;
                }

                _prevCreditMult = newCreditMult;
            }
        }

        private void MinRerollSpawnInterval_SettingChanged(object sender, EventArgs e)
        {
            foreach (var director in CombatDirector.instancesList)
                director.minRerollSpawnInterval = PluginConfig.minRerollSpawnInterval.GetValue();
        }

        private void MaxRerollSpawnInterval_SettingChanged(object sender, EventArgs e)
        {
            foreach (var director in CombatDirector.instancesList)
                director.maxRerollSpawnInterval = PluginConfig.maxRerollSpawnInterval.GetValue();
        }

        private void MaximumNumberToSpawnBeforeSkipping_SettingChanged(object sender, EventArgs e)
        {
            foreach (var director in CombatDirector.instancesList)
                director.maximumNumberToSpawnBeforeSkipping = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValue();
        }

        private void MaxConsecutiveCheapSkips_SettingChanged(object sender, EventArgs e)
        {
            foreach (var director in CombatDirector.instancesList)
                director.maxConsecutiveCheapSkips = PluginConfig.maxConsecutiveCheapSkips.GetValue();
        }
        #endregion
    }
}
