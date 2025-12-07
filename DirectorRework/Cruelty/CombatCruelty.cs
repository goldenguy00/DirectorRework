using System.Collections.Generic;
using DirectorRework.Modules;
using HG;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public class CombatCruelty
    {
        public static CombatCruelty Instance { get; private set; }
        public static void Init() => Instance ??= new CombatCruelty();

        public void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            orig(self);

            if (!NetworkServer.active)
                return;

            self.onSpawnedWithDirectorServer.AddListener(OnSpawnedServer);
        }

        private void OnSpawnedServer(GameObject masterObject, CombatDirector self)
        {
            if (!PluginConfig.enableCruelty.Value || !Util.CheckRoll(PluginConfig.triggerChance.Value))
                return;

            if (!self || ReferenceEquals(self.currentMonsterCard, null) || ReferenceEquals(self.rng, null))
            {
                return;
            }

            if (!masterObject || !masterObject.TryGetComponent<CharacterMaster>(out var master))
                return;

            if (!master.inventory || master.inventory.GetItemCountEffective(RoR2Content.Items.HealthDecay) != 0)
                return;

            var body = master.GetBody();
            if (!body)
                return;

            if (!PluginConfig.allowBosses.Value && master.isBoss)
                return;

            bool isElite = body.eliteBuffCount > 0 || (PluginConfig.bossesAreElite.Value && master.isBoss);
            if (PluginConfig.onlyApplyToElites.Value && !isElite)
                return;

            AddEliteBuffs(self, body, master.inventory);
        }

        private void AddEliteBuffs(CombatDirector director, CharacterBody body, Inventory inventory)
        {
            //Check amount of elite buffs the target has
            var currentEliteBuffs = HG.ListPool<BuffIndex>.RentCollection();
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
                GetRandom(director, currentEliteBuffs, out var eliteDef, out float cost))
            {
                currentEliteBuffs.Add(eliteDef.eliteEquipmentDef.passiveBuffDef.buffIndex);
                director.monsterCredit -= cost;
                body.cost += cost;

                if (PluginConfig.enableMoreDebugLogs.Value)
                {
                    Log.Debug($"NORMAL {body}  |  isBoss? {body.isBoss}   |   affix #{currentEliteBuffs.Count} - {eliteDef.modifierToken}");
                }

                CrueltyManager.Instance.GiveAffix(body, inventory, eliteDef.eliteEquipmentDef);
                if (PluginConfig.enableMoreDebugLogs.Value)
                    CrueltyManager.Instance.GiveItemBoostsVerbose(inventory, eliteDef, currentEliteBuffs.Count);
                else
                    CrueltyManager.Instance.GiveItemBoosts(inventory, eliteDef, currentEliteBuffs.Count);
                CrueltyManager.Instance.GiveDeathReward(deathRewards, xp, gold, currentEliteBuffs.Count);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            HG.ListPool<BuffIndex>.ReturnCollection(currentEliteBuffs);
        }

        private bool GetRandom(CombatDirector director, List<BuffIndex> currentBuffs, out EliteDef result, out float cost)
        {
            result = null;
            cost = 0f;

            // +1 is the cost once the affix is applied
            var cardCost = director.currentMonsterCard.cost / (currentBuffs.Count + 1);

            var availableDefs = ListPool<EliteDef>.RentCollection();
            var availableCosts = ListPool<float>.RentCollection();

            for (int i = 0; i < CombatDirector.eliteTiers.Length; ++i)
            {
                var etd = CombatDirector.eliteTiers[i];
                if (CrueltyManager.Instance.IsTierAvailable(etd) && 
                    CrueltyManager.Instance.IsTierAffordable(etd, director.currentMonsterCard, cardCost, director.monsterCredit))
                {
                    for (int j = 0; j < etd.eliteTypes.Length; ++j)
                    {
                        var ed = etd.eliteTypes[j];
                        if (CrueltyManager.Instance.IsEliteDefValid(ed, currentBuffs))
                        {
                            availableDefs.Add(ed);
                            availableCosts.Add(etd.costMultiplier);
                        }
                    }
                }
            }

            if (availableDefs.Count > 0)
            {
                var nextIndex = director.rng.RangeInt(0, availableDefs.Count);
                result = availableDefs[nextIndex];
                cost = availableCosts[nextIndex] * cardCost;
            }

            ListPool<EliteDef>.ReturnCollection(availableDefs);
            ListPool<float>.ReturnCollection(availableCosts);

            return !ReferenceEquals(result, null);
        }
    }
}
