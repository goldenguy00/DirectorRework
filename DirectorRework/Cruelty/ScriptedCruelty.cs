using System;
using System.Collections.Generic;
using System.Linq;
using DirectorRework.Config;
using RoR2;
using UnityEngine;

namespace DirectorRework.Cruelty
{
    public static class ScriptedCruelty
    {
        public static void OnMemberAddedServer(CharacterMaster master, Xoroshiro128Plus rng)
        {
            if (!PluginConfig.guaranteeSpecialBoss.Value && !Util.CheckRoll(PluginConfig.triggerChance.Value))
                return;

            if (!master || !master.hasBody)
                return;

            var body = master.GetBody();
            var inventory = master.inventory;

            if (!body || !inventory || inventory.GetItemCount(RoR2Content.Items.HealthDecay) > 0)
                return;

            //Check amount of elite buffs the target has
            List<BuffIndex> currentEliteBuffs = [];
            foreach (var b in BuffCatalog.eliteBuffIndices)
            {
                if (body.HasBuff(b) && !currentEliteBuffs.Contains(b))
                    currentEliteBuffs.Add(b);
            }

            var dr = body.GetComponent<DeathRewards>();
            uint xp = 0, gold = 0;
            if (dr)
            {
                xp = dr.expReward;
                gold = dr.goldReward;
            }

            while (currentEliteBuffs.Count < PluginConfig.maxAffixes.Value && GetScriptedRandom(rng, currentEliteBuffs, out var result))
            {
                //Fill in equipment slot if it isn't filled
                if (inventory.currentEquipmentIndex == EquipmentIndex.None)
                    inventory.SetEquipmentIndex(result.eliteEquipmentDef.equipmentIndex);

                //Apply Elite Bonus
                var buff = result.eliteEquipmentDef.passiveBuffDef.buffIndex;
                currentEliteBuffs.Add(buff);
                body.AddBuff(buff);

                float affixes = currentEliteBuffs.Count;
                inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt((result.healthBoostCoefficient - 1f) * 10f / affixes));
                inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((result.damageBoostCoefficient - 1f) * 10f / affixes));
                if (dr)
                {
                    dr.expReward += Convert.ToUInt32(xp / affixes);
                    dr.goldReward += Convert.ToUInt32(gold / affixes);
                }

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }
        }

        private static bool GetScriptedRandom(Xoroshiro128Plus rng, List<BuffIndex> currentBuffs, out EliteDef result)
        {
            result = null;

            var tiers = CombatDirector.eliteTiers;
            if (tiers == null || tiers.Length == 0)
                return false;

            var availableDefs =
                from etd in tiers
                where etd != null && !etd.canSelectWithoutAvailableEliteDef
                from ed in etd.availableDefs
                where CrueltyManager.IsValid(ed, currentBuffs)
                select ed;


            if (availableDefs.Any())
            {
                var rngIndex = rng.RangeInt(0, availableDefs.Count());
                result = availableDefs.ElementAt(rngIndex);
                return true;
            }

            return false;
        }
    }
}
