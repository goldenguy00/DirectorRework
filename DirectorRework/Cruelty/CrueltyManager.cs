using System.Collections.Generic;
using RoR2;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    /// <summary>
    /// Fixed it.
    /// </summary>
    public class CrueltyManager
    {
        public static HashSet<EquipmentIndex> BlacklistedElites = [];
        public static CrueltyManager Instance { get; private set; }

        public static void Init() => Instance ??= new CrueltyManager();

        private CrueltyManager()
        {
            RoR2Application.onLoad += OnLoad;

            On.RoR2.CombatDirector.Awake += (orig, self) =>
            {
                orig(self);
                if (NetworkServer.active)
                {
                    self.onSpawnedServer.AddListener((masterObject) => CombatCruelty.OnSpawnedServer(self, masterObject));
                }
            };

            On.RoR2.ScriptedCombatEncounter.Awake += (orig, self) =>
            {
                orig(self);
                if (NetworkServer.active && self.combatSquad)
                {
                    self.combatSquad.onMemberAddedServer += (master) => ScriptedCruelty.OnMemberAddedServer(master, self.rng);
                }
            };
        }

        private static void OnLoad()
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
                                !CrueltyManager.BlacklistedElites.Contains(ed.eliteEquipmentDef.equipmentIndex) &&
                                !currentBuffs.Contains(ed.eliteEquipmentDef.passiveBuffDef.buffIndex);
        }
    }
}