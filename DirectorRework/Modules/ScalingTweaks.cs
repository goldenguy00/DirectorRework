using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class ScalingTweaks
    {
        private bool linearScaling, rampTyphoonCredits;

        public static ScalingTweaks Instance { get; private set; }
        public static void Init() => Instance ??= new ScalingTweaks();

        private ScalingTweaks()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableScalingTweaks.SettingChanged += OnSettingChanged;
            PluginConfig.enableLinearScaling.SettingChanged += OnSettingChanged;
            PluginConfig.rampTyphoonCredits.SettingChanged += OnSettingChanged;
        }

        // fuck i hate this, worth a try tho
        public void OnSettingChanged(object sender, EventArgs a)
        {
            var enabled = PluginConfig.enableScalingTweaks.Value;

            ApplyLinearScaling(enabled && PluginConfig.enableLinearScaling.Value);
            ApplyTyphoonRamp(enabled && PluginConfig.rampTyphoonCredits.Value);
        }

        private void ApplyLinearScaling(bool enabled)
        {
            if (linearScaling != enabled)
            {
                linearScaling = enabled;

                if (enabled)
                    IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += Run_RecalculateDifficultyCoefficentInternal;
                else
                    IL.RoR2.Run.RecalculateDifficultyCoefficentInternal -= Run_RecalculateDifficultyCoefficentInternal;
            }
        }

        private void ApplyTyphoonRamp(bool enabled)
        {
            if (rampTyphoonCredits != enabled)
            {
                rampTyphoonCredits = enabled;

                if (enabled)
                    On.RoR2.CombatDirector.OnEnable += CombatDirector_OnEnable;
                else
                    On.RoR2.CombatDirector.OnEnable -= CombatDirector_OnEnable;
            }
        }

        private static void CombatDirector_OnEnable(On.RoR2.CombatDirector.orig_OnEnable orig, CombatDirector self)
        {
            orig(self);

            if (!NetworkServer.active || !Run.instance)
                return;

            self.creditMultiplier = GetNewCreditMultiplier(self.creditMultiplier);

            for (int i = 0; i < self.moneyWaves.Length; i++)
            {
                self.moneyWaves[i].multiplier = GetNewCreditMultiplier(self.moneyWaves[i].multiplier);
            }
        }

        // this is the dumbest shit ive ever written. so much arbitrary shit but it had to happen
        private static float GetNewCreditMultiplier(float creditMultiplier)
        {
            if (creditMultiplier is < 1.3f or > 2f)
                return creditMultiplier;

            // 50% base
            var baseFactor = (creditMultiplier - 1f) / 2f;

            // add a 3rd of the remaining value per stage idfk
            var stageFactor = (Run.instance.stageClearCount * baseFactor) / 3f;

            var newCreditMultiplier = Mathf.Clamp(1f + baseFactor + stageFactor, 1f, creditMultiplier);

            Log.Info($"Reducing credit multiplier from {creditMultiplier} to {newCreditMultiplier}");

            return newCreditMultiplier;
        }

        private static void Run_RecalculateDifficultyCoefficentInternal(ILContext il)
        {
            var c = new ILCursor(il);

            int stageMultLoc = 0;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<Run>(nameof(Run.stageClearCount)),
                    x => x.MatchConvR4(),
                    x => x.MatchCallOrCallvirt<Mathf>(nameof(Mathf.Pow))) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchStloc(out stageMultLoc)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit<Run>(OpCodes.Ldfld, nameof(Run.stageClearCount));
                c.Emit(OpCodes.Ldloc, stageMultLoc);
                c.EmitDelegate(GetStageMultiplier);
                c.Emit(OpCodes.Stloc, stageMultLoc);
            }
            else Log.Error("IL Hook failed for Run.RecalculateDifficultyCoefficentInternal");
        }

        public static float GetStageMultiplier(int stageClearCount, float currentScaling)
        {
            float linearScaling = 1f + (PluginConfig.linearScalingMultiplier.Value * stageClearCount);
            return Mathf.Max(currentScaling, linearScaling);
        }
    }
}
