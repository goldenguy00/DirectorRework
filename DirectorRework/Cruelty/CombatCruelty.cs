using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DirectorRework.Config;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public static class CombatCruelty
    {
        public static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                self.onSpawnedServer.AddListener((masterObject) =>
                {
                    if (!PluginConfig.enableCruelty.Value || !Util.CheckRoll(PluginConfig.triggerChance.Value))
                        return;

                    var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
                    if (master && master.inventory && master.inventory.GetItemCount(RoR2Content.Items.HealthDecay) <= 0)
                    {
                        var body = master.GetBody();
                        if (body)
                        {
                            if (!PluginConfig.allowBosses.Value && (master.isBoss || body.isChampion))
                                return;

                            //Check amount of elite buffs the target has
                            HashSet<BuffIndex> currentEliteBuffs = [];
                            foreach (var b in BuffCatalog.eliteBuffIndices)
                            {
                                if (body.HasBuff(b) && !currentEliteBuffs.Contains(b))
                                    currentEliteBuffs.Add(b);
                            }

                            if (PluginConfig.onlyApplyToElites.Value && !currentEliteBuffs.Any())
                                return;

                            CombatCruelty.OnSpawnedServer(self, body, master.inventory, currentEliteBuffs);
                        }
                    }
                });
            }
        }

        private static void OnSpawnedServer(CombatDirector director, CharacterBody body, Inventory inventory, HashSet<BuffIndex> currentEliteBuffs)
        {
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

                var buff = result.def.eliteEquipmentDef.passiveBuffDef.buffIndex;
                currentEliteBuffs.Add(buff);
                body.AddBuff(buff);

                // some fuckery here
                float affixes = currentEliteBuffs.Count;
                director.monsterCredit -= result.cost / affixes;
                body.cost += result.cost / affixes;
                inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt((result.def.healthBoostCoefficient - 1f) * 10f / (affixes + 1)));
                inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((result.def.damageBoostCoefficient - 1f) * 10f / (affixes + 1)));

                if (dr)
                {
                    dr.expReward += Convert.ToUInt32(xp / affixes);
                    dr.goldReward += Convert.ToUInt32(gold / affixes);
                }

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }
        }


        private static bool GetRandom(float availableCredits, DirectorCard card, Xoroshiro128Plus rng, HashSet<BuffIndex> currentBuffs, out (EliteDef def, float cost) result)
        {
            result = default;

            var tiers = CombatDirector.eliteTiers;
            if (tiers is null || tiers.Length == 0)
                return false;

            // +1 is the cost once the affix is applied
            var cost = (card?.cost ?? 0) / (currentBuffs.Count + 1);

            var availableDefs =
                from etd in tiers
                where IsValid(etd, card, cost, availableCredits)
                from ed in etd.eliteTypes
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


        private static bool IsValid(CombatDirector.EliteTierDef etd, DirectorCard card, int cost, float availableCredits)
        {
            if (etd?.canSelectWithoutAvailableEliteDef == false && (card is null || etd.CanSelect(card.spawnCard.eliteRules)))
            {
                return availableCredits >= cost * etd.costMultiplier;
            }
            return false;
        }
    }
}
