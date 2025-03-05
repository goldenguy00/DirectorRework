using System.Collections.Generic;
using System.Linq;
using DirectorRework.Modules;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public static class CombatCruelty
    {
        public readonly struct EliteWithCost(EliteDef def, float cost)
        {
            public readonly EliteDef eliteDef = def;
            public readonly float cost = cost;
        }

        public static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                self.onSpawnedServer.AddListener(OnSpawnedServer);

                void OnSpawnedServer(GameObject masterObject)
                {
                    if (PluginConfig.enableCruelty.Value && Util.CheckRoll(PluginConfig.triggerChance.Value))
                    {
                        var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
                        if (master && master.inventory && master.inventory.GetItemCount(RoR2Content.Items.HealthDecay) <= 0)
                        {
                            var body = master.GetBody();
                            if (body)
                            {
                                var isBoss = master.isBoss || body.isChampion;
                                if (!PluginConfig.allowBosses.Value && isBoss)
                                    return;

                                var isElite = body.eliteBuffCount > 0 || (PluginConfig.bossesAreElite.Value && isBoss);
                                if (PluginConfig.onlyApplyToElites.Value && !isElite)
                                    return;

                                CombatCruelty.AddEliteBuffs(self, body, master.inventory);
                            }
                        }
                    }
                };
            }
        }

        private static void AddEliteBuffs(CombatDirector director, CharacterBody body, Inventory inventory)
        {
            //Check amount of elite buffs the target has
            List<BuffIndex> currentEliteBuffs = HG.ListPool<BuffIndex>.RentCollection();
            foreach (var b in BuffCatalog.eliteBuffIndices)
            {
                if (body.HasBuff(b) && !currentEliteBuffs.Contains(b))
                    currentEliteBuffs.Add(b);
            }

            uint xp = 0, gold = 0;
            if (body.TryGetComponent<DeathRewards>(out var deathRewards))
            {
                xp = deathRewards.expReward;
                gold = deathRewards.goldReward;
            }

            while (director.monsterCredit > 0 && currentEliteBuffs.Count < PluginConfig.maxAffixes.Value && GetRandom(director.monsterCredit, director.currentMonsterCard, director.rng, currentEliteBuffs, out EliteWithCost result))
            {
                CrueltyManager.GiveAffix(body, inventory, result.eliteDef.eliteEquipmentDef);

                currentEliteBuffs.Add(result.eliteDef.eliteEquipmentDef.passiveBuffDef.buffIndex);

                int affixes = currentEliteBuffs.Count;
                director.monsterCredit -= result.cost;
                body.cost += result.cost;

                CrueltyManager.GiveItemBoosts(inventory, result.eliteDef, affixes);
                CrueltyManager.GiveDeathReward(deathRewards, xp, gold, affixes);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            HG.ListPool<BuffIndex>.ReturnCollection(currentEliteBuffs);
        }


        private static bool GetRandom(float availableCredits, DirectorCard card, Xoroshiro128Plus rng, List<BuffIndex> currentBuffs, out EliteWithCost result)
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
                select new EliteWithCost(ed, etd.costMultiplier * cost);


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
            if (etd?.canSelectWithoutAvailableEliteDef == false && (!card?.spawnCard || etd.CanSelect(card.spawnCard.eliteRules)))
            {
                return availableCredits >= cost * etd.costMultiplier;
            }
            return false;
        }
    }
}
