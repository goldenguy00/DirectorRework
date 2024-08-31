using System;
using System.Collections.Generic;
using System.Linq;
using DirectorRework.Config;
using RoR2;
using UnityEngine;

namespace DirectorRework.Cruelty
{
    public static class CombatCruelty
    {
        public static void OnSpawnedServer(CombatDirector director, GameObject masterObject)
        {
            if (!Util.CheckRoll(PluginConfig.triggerChance.Value))
                return;

            var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
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

            if (PluginConfig.onlyApplyToElites.Value && !currentEliteBuffs.Any())
                return;

            var dr = body.GetComponent<DeathRewards>();
            uint xp = 0, gold = 0;
            if (dr)
            {
                xp = dr.expReward;
                gold = dr.goldReward;
            }

            while (director.monsterCredit > 0 && currentEliteBuffs.Count < PluginConfig.maxAffixes.Value && GetRandom(director.monsterCredit, director.currentMonsterCard, director.rng, currentEliteBuffs, out var result))
            {
                //Fill in equipment slot if it isn't filled
                if (inventory.currentEquipmentIndex == EquipmentIndex.None)
                    inventory.SetEquipmentIndex(result.def.eliteEquipmentDef.equipmentIndex);

                //Apply Elite Bonus
                var buff = result.def.eliteEquipmentDef.passiveBuffDef.buffIndex;
                currentEliteBuffs.Add(buff);
                body.AddBuff(buff);

                // set the affix count to higher than the actual count to reduce the impact of "raidbosses"
                // also prevents  director from pouring all credits onto a single enemy since elite affordability is still compared to the true affix count
                // math is a lil funky but it feels fine.
                float affixes = currentEliteBuffs.Count;
                director.monsterCredit -= result.cost / affixes;
                inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt((result.def.healthBoostCoefficient - 1f) * 10f / affixes));
                inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((result.def.damageBoostCoefficient - 1f) * 10f / affixes));

                if (dr)
                {
                    dr.expReward += Convert.ToUInt32(xp / affixes);
                    dr.goldReward += Convert.ToUInt32(gold / affixes);
                }

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }
        }


        private static bool GetRandom(float availableCredits, DirectorCard card, Xoroshiro128Plus rng, List<BuffIndex> currentBuffs, out (EliteDef def, float cost) result)
        {
            result = default;

            var tiers = CombatDirector.eliteTiers;
            if (tiers == null || tiers.Length == 0)
                return false;

            var cost = card?.cost ?? 0;

            var availableDefs =
                from etd in tiers
                where IsValid(etd, card, cost, availableCredits, currentBuffs.Count)
                from ed in etd.availableDefs
                where CrueltyManager.IsValid(ed, currentBuffs)
                select (ed, etd.costMultiplier * cost);


            if (availableDefs.Any())
            {
                var rngIndex = rng.RangeInt(0, availableDefs.Count());
                result = availableDefs.ElementAt(rngIndex);
                return true;
            }

            return false;
        }


        private static bool IsValid(CombatDirector.EliteTierDef etd, DirectorCard card, int cost, float availableCredits, int affixes)
        {
            // +1 is the cost once the affix is applied
            var canAfford = availableCredits >= cost * etd.costMultiplier / (affixes + 1);

            return etd != null && !etd.canSelectWithoutAvailableEliteDef && canAfford &&
                   (card == null || etd.CanSelect(card.spawnCard.eliteRules));
        }
    }
}
