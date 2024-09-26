using System;
using System.Collections.Generic;
using DirectorRework.Config;
using RoR2;

namespace DirectorRework.Cruelty
{
    /// <summary>
    /// Fixed it.
    /// </summary>
    public class CrueltyManager : IHookProvider
    {
        private HashSet<EquipmentIndex> BlacklistedElites { get; set; } = [];

        public static CrueltyManager Instance { get; private set; }
        public bool HooksEnabled { get; set; }

        public static void Init() => Instance ??= new CrueltyManager();

        private CrueltyManager()
        {
            RoR2Application.onLoad += OnLoad;

            PluginConfig.enableCruelty.SettingChanged += OnSettingChanged;
            OnSettingChanged(null, null);
        }

        public void OnSettingChanged(object sender, EventArgs args)
        {
            if (PluginConfig.enableCruelty.Value)
                SetHooks();
            else
                UnsetHooks();
        }

        public void SetHooks()
        {
            if (!HooksEnabled)
            {
                On.RoR2.CombatDirector.Awake += CombatCruelty.CombatDirector_Awake;
                On.RoR2.ScriptedCombatEncounter.Awake += ScriptedCruelty.ScriptedCombatEncounter_Awake;
                HooksEnabled = true;
            }
        }

        public void UnsetHooks()
        {
            if (HooksEnabled)
            {
                On.RoR2.CombatDirector.Awake -= CombatCruelty.CombatDirector_Awake;
                On.RoR2.ScriptedCombatEncounter.Awake -= ScriptedCruelty.ScriptedCombatEncounter_Awake;

                HooksEnabled = false;
            }
        }

        private void OnLoad()
        {
            var blightIndex = EquipmentCatalog.FindEquipmentIndex("AffixBlightedMoffein");
            if (blightIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(blightIndex);
                if (ed && ed.passiveBuffDef && ed.passiveBuffDef.eliteDef)
                    BlacklistedElites.Add(blightIndex);
            }

            var perfectedIndex = EquipmentCatalog.FindEquipmentIndex("EliteLunarEquipment");
            if (perfectedIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(perfectedIndex);
                if (ed && ed.passiveBuffDef && ed.passiveBuffDef.eliteDef)
                    BlacklistedElites.Add(perfectedIndex);
            }
        }


        internal static bool IsValid(EliteDef ed, List<BuffIndex> currentBuffs)
        {
            return ed && ed.IsAvailable() && ed.eliteEquipmentDef &&
                                ed.eliteEquipmentDef.passiveBuffDef &&
                                ed.eliteEquipmentDef.passiveBuffDef.isElite &&
                                !CrueltyManager.Instance.BlacklistedElites.Contains(ed.eliteEquipmentDef.equipmentIndex) &&
                                !currentBuffs.Contains(ed.eliteEquipmentDef.passiveBuffDef.buffIndex);
        }
    }
}