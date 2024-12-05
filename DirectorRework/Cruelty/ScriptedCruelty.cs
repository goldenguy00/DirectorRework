using System.Collections.Generic;
using System.Linq;
using DirectorRework.Config;
using DirectorRework.Hooks;
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
                self.combatSquad.onMemberAddedServer += (master) =>
                {
                    if (PluginConfig.enableCruelty.Value && master && master.inventory && master.inventory.GetItemCount(RoR2Content.Items.HealthDecay) <= 0)
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

                            ScriptedCruelty.OnMemberAddedServer(body, master.inventory, rng);
                        }
                    }
                };
            }
        }

        public static void OnMemberAddedServer(CharacterBody body, Inventory inventory, Xoroshiro128Plus rng)
        {
            //Check amount of elite buffs the target has
            List<BuffIndex> currentEliteBuffs = HG.ListPool<BuffIndex>.RentCollection();
            foreach (var b in BuffCatalog.eliteBuffIndices)
            {
                if (body.HasBuff(b) && !currentEliteBuffs.Contains(b))
                    currentEliteBuffs.Add(b);
            }

            uint xp = 0, gold = 0;
            if (body.TryGetComponent<DeathRewards>(out var deathReward))
            {
                xp = deathReward.expReward;
                gold = deathReward.goldReward;
            }

            while (currentEliteBuffs.Count < PluginConfig.maxScriptedAffixes.Value && GetScriptedRandom(rng, currentEliteBuffs, out var result))
            {
                CrueltyManager.GiveAffix(body, inventory, result.eliteEquipmentDef);

                currentEliteBuffs.Add(result.eliteEquipmentDef.passiveBuffDef.buffIndex);

                int affixes = currentEliteBuffs.Count;
                CrueltyManager.GiveItemBoosts(inventory, result, affixes);
                CrueltyManager.GiveDeathReward(deathReward, xp, gold, affixes);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            HG.ListPool<BuffIndex>.ReturnCollection(currentEliteBuffs);
        }

        private static bool GetScriptedRandom(Xoroshiro128Plus rng, List<BuffIndex> currentBuffs, out EliteDef result)
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
