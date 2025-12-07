using System;
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
            On.RoR2.CombatDirector.Awake += CombatDirector_Awake;

            _enabled = PluginConfig.enableDirectorTweaks.Value;
            PluginConfig.enableDirectorTweaks.SettingChanged += (_, __) =>
            {
                ModuleEnabled = PluginConfig.enableDirectorTweaks.Value;
            };
        }

        private static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            if (NetworkServer.active)
                self.gameObject.AddComponent<DirectorTweaks>().SetDirector(self);

            orig(self);
        }

        private CombatDirector _director;
        private bool eventHandlersSet;
        private bool isFastDirector;

        private int _vanillaMaxSpawns;
        private int _vanillaMaxCheapSkips;
        private float _vanillaMinSpawnInterval;
        private float _vanillaMaxSpawnInterval;
        private float _internalCreditMult;
        private float _internalSpawnDistMult;

        private void SetDirector(CombatDirector director)
        {
            if (PluginConfig.enableDebugLogs.Value)
                CombatDirector.cvDirectorCombatEnableInternalLogs.SetBool(true);

            _director = director;
            _vanillaMaxSpawns = _director.maximumNumberToSpawnBeforeSkipping;
            _vanillaMaxCheapSkips = _director.maxConsecutiveCheapSkips;
            _vanillaMinSpawnInterval = _director.minRerollSpawnInterval;
            _vanillaMaxSpawnInterval = _director.maxRerollSpawnInterval;
            isFastDirector = true;

            if (_vanillaMinSpawnInterval != _vanillaMaxSpawnInterval && _vanillaMinSpawnInterval > 5f)
            {
                var allDirectors = GetComponents<DirectorTweaks>();
                foreach (var otherDirector in allDirectors)
                {
                    if (otherDirector == this || !otherDirector._director)
                        continue;

                    if (otherDirector._vanillaMinSpawnInterval >= _vanillaMinSpawnInterval)
                    {
                        // we are at least tied with another. multiple slow directors isnt supported, i dont care.
                        isFastDirector = true;
                        break;
                    }

                    if (otherDirector._vanillaMinSpawnInterval < _vanillaMinSpawnInterval)
                    {
                        // maybe the fat kid
                        // continue on to check if we can pass that to someone else
                        isFastDirector = false;
                        // they are fast but dont know it yet
                        if (otherDirector.isFastDirector == false)
                        {
                            Log.Info("Resetting sibling from " + gameObject.name);
                            otherDirector.isFastDirector = true;
                            otherDirector.OverwriteSpawnInterval_SettingChanged();
                        }
                    }
                }
            }

            SetEventHandlers(ModuleEnabled);
            onEnabledChanged += SetEventHandlers;
        }

        private void SetEventHandlers(bool newEnabled)
        {
            if (newEnabled == eventHandlersSet)
                return;

            eventHandlersSet = newEnabled;
            if (eventHandlersSet)
            {
                PluginConfig.useRecommendedValues.SettingChanged += OnSettingValuesChanged;

                PluginConfig.creditMultiplier.SettingChanged += CreditMultiplier_SettingChanged;
                PluginConfig.spawnDistanceMultiplier.SettingChanged += SpawnDistanceMultiplier_SettingChanged;

                PluginConfig.useSlowDirector.SettingChanged += OverwriteSpawnInterval_SettingChanged;
                PluginConfig.spawnIntervalVariance.SettingChanged += OverwriteSpawnInterval_SettingChanged;
                PluginConfig.fastSpawnInterval.SettingChanged += OverwriteSpawnInterval_SettingChanged;
                PluginConfig.slowSpawnInterval.SettingChanged += OverwriteSpawnInterval_SettingChanged;

                PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged += MaximumNumberToSpawnBeforeSkipping_SettingChanged;
                PluginConfig.maxConsecutiveCheapSkips.SettingChanged += MaxConsecutiveCheapSkips_SettingChanged;
            }
            else
            {
                PluginConfig.useRecommendedValues.SettingChanged -= OnSettingValuesChanged;

                PluginConfig.creditMultiplier.SettingChanged -= CreditMultiplier_SettingChanged;
                PluginConfig.spawnDistanceMultiplier.SettingChanged -= SpawnDistanceMultiplier_SettingChanged;

                PluginConfig.useSlowDirector.SettingChanged -= OverwriteSpawnInterval_SettingChanged;
                PluginConfig.spawnIntervalVariance.SettingChanged -= OverwriteSpawnInterval_SettingChanged;
                PluginConfig.fastSpawnInterval.SettingChanged -= OverwriteSpawnInterval_SettingChanged;
                PluginConfig.slowSpawnInterval.SettingChanged -= OverwriteSpawnInterval_SettingChanged;

                PluginConfig.maximumNumberToSpawnBeforeSkipping.SettingChanged -= MaximumNumberToSpawnBeforeSkipping_SettingChanged;
                PluginConfig.maxConsecutiveCheapSkips.SettingChanged -= MaxConsecutiveCheapSkips_SettingChanged;
            }

            OnSettingValuesChanged();
        }

        private void OnSettingValuesChanged(object _ = null, EventArgs __ = null)
        {
            CreditMultiplier_SettingChanged();
            SpawnDistanceMultiplier_SettingChanged();

            OverwriteSpawnInterval_SettingChanged();

            MaximumNumberToSpawnBeforeSkipping_SettingChanged();
            MaxConsecutiveCheapSkips_SettingChanged();
        }

        private void CreditMultiplier_SettingChanged(object _ = null, EventArgs __ = null)
        {
            var newMult = PluginConfig.creditMultiplier.GetValueOrDefault();
            var oldValue = _director.creditMultiplier;
            if (newMult != _internalCreditMult)
            {
                if (_internalCreditMult > 0)
                    _director.creditMultiplier /= _internalCreditMult;

                _internalCreditMult = newMult;

                if (_internalCreditMult > 0)
                    _director.creditMultiplier *= _internalCreditMult;
            }

            if (!PluginConfig.enableMoreDebugLogs.Value || oldValue == _director.creditMultiplier)
                return;
            Log.Debug(gameObject.name + " | creditMult\r\n\t" +
                "prv: " + oldValue + "\r\n\t" +
                "new: " + _director.creditMultiplier);
        }

        private void SpawnDistanceMultiplier_SettingChanged(object _ = null, EventArgs __ = null)
        {
            var newMult = PluginConfig.spawnDistanceMultiplier.GetValueOrDefault();
            var oldValue = _director.spawnDistanceMultiplier;
            if (newMult != _internalSpawnDistMult)
            {
                if (_internalSpawnDistMult > 0)
                    _director.spawnDistanceMultiplier /= _internalSpawnDistMult;

                _internalSpawnDistMult = newMult;

                if (_internalSpawnDistMult > 0)
                    _director.spawnDistanceMultiplier *= _internalSpawnDistMult;
            }

            if (!PluginConfig.enableMoreDebugLogs.Value || oldValue == _director.spawnDistanceMultiplier)
                return;

            Log.Debug(gameObject.name + " | spawnMult\r\n\t" +
                "prv: " + oldValue + "\r\n\t" +
                "new: " + _director.spawnDistanceMultiplier);
        }

        private void OverwriteSpawnInterval_SettingChanged(object _ = null, EventArgs __ = null)
        {
            if (_vanillaMinSpawnInterval == _vanillaMaxSpawnInterval)
            {
                Log.Info(gameObject.name + " Intervals " + _vanillaMaxSpawnInterval + " are equal, skipping special director.");
                return;
            }

            var newMin = _vanillaMinSpawnInterval;
            var newMax = _vanillaMaxSpawnInterval;

            var fastSpawnTimer = PluginConfig.fastSpawnInterval.GetValueOrDefault();
            var slowSpawnTimer = PluginConfig.slowSpawnInterval.GetValueOrDefault();
            var timerMult = PluginConfig.spawnIntervalVariance.GetValueOrDefault();
            if (fastSpawnTimer < 0 || slowSpawnTimer < 0 || timerMult < 1)
            {
                Log.Info(gameObject.name + " resetting to vanilla spawn timers due to invalid config value");
            }
            else
            {
                if (this.isFastDirector || !PluginConfig.useSlowDirector.GetValueOrDefault())
                {
                    newMin = fastSpawnTimer;
                }
                else
                {
                    newMin = slowSpawnTimer;
                }
                newMax = newMin * timerMult;
            }

            if (_director.minRerollSpawnInterval == newMin && _director.maxRerollSpawnInterval == newMax)
                return;

            _director.minRerollSpawnInterval = newMin;
            _director.maxRerollSpawnInterval = newMax;

            if (!PluginConfig.enableMoreDebugLogs.Value)
                return;

            Log.Debug(gameObject.name + " | spawnInterval\r\n\t" +
                "prv: " + _vanillaMinSpawnInterval + " and " + _vanillaMaxSpawnInterval + "\r\n\t" +
                "new: " + newMin + " and " + newMax);
        }

        private void MaximumNumberToSpawnBeforeSkipping_SettingChanged(object _ = null, EventArgs __ = null)
        {
            var newValue = PluginConfig.maximumNumberToSpawnBeforeSkipping.GetValueOrDefault();
            if (newValue <= 0)
                newValue = _vanillaMaxSpawns;

            if (_director.maximumNumberToSpawnBeforeSkipping == newValue)
                return;

            _director.maximumNumberToSpawnBeforeSkipping = newValue;

            if (!PluginConfig.enableMoreDebugLogs.Value)
                return;

            Log.Debug(gameObject.name + " | maxBeforeSkip\r\n\t" +
                "prv: " + _vanillaMaxSpawns + "\r\n\t" +
                "new: " + newValue);
        }

        private void MaxConsecutiveCheapSkips_SettingChanged(object _ = null, EventArgs __ = null)
        {
            var newValue = PluginConfig.maxConsecutiveCheapSkips.GetValueOrDefault();
            if (newValue <= 0)
                newValue = _vanillaMaxCheapSkips;

            if (_director.maxConsecutiveCheapSkips == newValue)
                return;

            _director.maxConsecutiveCheapSkips = newValue;

            if (!PluginConfig.enableMoreDebugLogs.Value)
                return;

            Log.Debug(gameObject.name + " | consecutive skips\r\n\t" +
                "prv: " + _director.maxConsecutiveCheapSkips + "\r\n\t" +
                "new: " + newValue);
        }
    }
}
