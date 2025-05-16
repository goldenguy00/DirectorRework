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

        private static System.Diagnostics.Stopwatch s = new();
        private static System.Diagnostics.Stopwatch s1 = new();
        private static void AddEliteBuffs(CombatDirector director, CharacterBody body, Inventory inventory)
        {
            s.Start();
            //Check amount of elite buffs the target has
            var currentEliteBuffs = HG.CollectionPool<BuffIndex, HashSet<BuffIndex>>.RentCollection();
            if (body.eliteBuffCount > 0)
            {
                for (int i = 0; i < body.activeBuffsListCount; i++)
                {
                    if (BuffCatalog.GetBuffDef(body.activeBuffsList[i])?.isElite is true)
                        currentEliteBuffs.Add(body.activeBuffsList[i]);
                }
            }

            uint xp = 0, gold = 0;
            var deathRewards = body.GetComponent<DeathRewards>();
            if (deathRewards)
            {
                xp = deathRewards.expReward;
                gold = deathRewards.goldReward;
            }

            while (director.monsterCredit > 0 && currentEliteBuffs.Count < PluginConfig.maxAffixes.Value &&
                GetRandom(director.monsterCredit, director.currentMonsterCard, director.rng, currentEliteBuffs, out var eliteDef, out float cost))
            {
                CrueltyManager.GiveAffix(body, inventory, eliteDef.eliteEquipmentDef);

                currentEliteBuffs.Add(eliteDef.eliteEquipmentDef.passiveBuffDef.buffIndex);
                director.monsterCredit -= cost;
                body.cost += cost;

                CrueltyManager.GiveItemBoosts(inventory, eliteDef, currentEliteBuffs.Count);
                CrueltyManager.GiveDeathReward(deathRewards, xp, gold, currentEliteBuffs.Count);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            HG.CollectionPool<BuffIndex, HashSet<BuffIndex>>.ReturnCollection(currentEliteBuffs);
            s.Stop();
            Log.Debug("Random " + ((int)(s1.Elapsed.TotalMilliseconds * 100f) / 100));
            Log.Debug("Time " + ((int)(s.Elapsed.TotalMilliseconds * 100f) / 100));
            s.Reset();
            s1.Reset();
        }

        private static bool GetRandom(float availableCredits, DirectorCard card, Xoroshiro128Plus rng, HashSet<BuffIndex> currentBuffs, out EliteDef eliteDef, out float cost)
        {
            s1.Start();
            eliteDef = null;
            cost = 0f;

            var tiers = CombatDirector.eliteTiers;
            if (tiers is null || tiers.Length == 0)
                return false;

            // +1 is the cost once the affix is applied
            var cardCost = (card?.cost ?? 0) / (currentBuffs.Count + 1);

            var availableDefs =
                from etd in tiers
                where IsValid(etd, card, cardCost, availableCredits)
                from ed in etd.eliteTypes
                where CrueltyManager.IsValid(ed, currentBuffs)
                select new { def = ed.eliteIndex, eliteCost = etd.costMultiplier * cardCost };

            if (!availableDefs.Any())
                return false;

            // Move down the collection one element at a time.
            // When index is -1 we are at the random element location
            var rngIndex = rng.RangeInt(0, availableDefs.Count());
            using var enumerator = availableDefs.GetEnumerator();

            while (rngIndex >= 0 && enumerator.MoveNext())
                rngIndex--;

            // Return the current element
            eliteDef = EliteCatalog.GetEliteDef(enumerator.Current.def);
            cost = enumerator.Current.eliteCost;

            s1.Stop();
            return true;
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
