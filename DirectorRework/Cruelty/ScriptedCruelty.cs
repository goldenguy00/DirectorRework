using System;
using System.Collections.Generic;
using System.Linq;
using DirectorRework.Modules;
using RoR2;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public static class ScriptedCruelty
    {
        public static void ScriptedCombatEncounter_Awake(On.RoR2.ScriptedCombatEncounter.orig_Awake orig, ScriptedCombatEncounter self)
        {
            orig(self);

            if (NetworkServer.active && self.combatSquad)
            {
                var rng = self.rng;
                self.combatSquad.onMemberAddedServer += OnMemberAddedServer;

                void OnMemberAddedServer(CharacterMaster master)
                {
                    if (!PluginConfig.enableCruelty.Value)
                        return;

                    if (master && master.inventory && master.inventory.GetItemCount(RoR2Content.Items.HealthDecay) <= 0)
                    {
                        var body = master.GetBody();
                        if (body)
                        {
                            if (!PluginConfig.guaranteeSpecialBoss.Value)
                            {
                                if (!Util.CheckRoll(PluginConfig.triggerChance.Value))
                                    return;

                                var isBoss = master.isBoss || body.isChampion;
                                if (!PluginConfig.allowBosses.Value && isBoss)
                                    return;
                            }

                            ScriptedCruelty.AddEliteBuffs(body, master.inventory, rng);
                        }
                    }
                }
            }
        }
        private static void AddEliteBuffs(CharacterBody body, Inventory inventory, Xoroshiro128Plus rng)
        {
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

            while (currentEliteBuffs.Count < PluginConfig.maxScriptedAffixes.Value && GetScriptedRandom(rng, currentEliteBuffs, out var result))
            {
                CrueltyManager.GiveAffix(body, inventory, result.eliteEquipmentDef);

                currentEliteBuffs.Add(result.eliteEquipmentDef.passiveBuffDef.buffIndex);

                int affixes = currentEliteBuffs.Count;
                CrueltyManager.GiveItemBoosts(inventory, result, affixes);
                CrueltyManager.GiveDeathReward(deathRewards, xp, gold, affixes);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            HG.CollectionPool<BuffIndex, HashSet<BuffIndex>>.ReturnCollection(currentEliteBuffs);
        }

        private static bool GetScriptedRandom(Xoroshiro128Plus rng, HashSet<BuffIndex> currentBuffs, out EliteDef result)
        {
            result = null;

            var tiers = CombatDirector.eliteTiers;
            if (tiers is null || tiers.Length == 0)
                return false;
            
            var availableDefs = 
                from etd in tiers
                where etd?.canSelectWithoutAvailableEliteDef == false
                from ed in etd.eliteTypes
                where CrueltyManager.IsValid(ed, currentBuffs)
                select ed.eliteIndex;

            if (!availableDefs.Any())
                return false;

            // Move down the collection one element at a time.
            // When index is -1 we are at the random element location
            var rngIndex = rng.RangeInt(0, availableDefs.Count());
            using var enumerator = availableDefs.GetEnumerator();

            while (rngIndex >= 0 && enumerator.MoveNext())
                rngIndex--;

            // Return the current element
            result = EliteCatalog.GetEliteDef(enumerator.Current);
            return true;
        }
    }
}
