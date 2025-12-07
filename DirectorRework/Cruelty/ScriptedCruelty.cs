using System.Collections.Generic;
using DirectorRework.Modules;
using HG;
using RoR2;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public class ScriptedCruelty
    {
        public static ScriptedCruelty Instance { get; private set; }
        public static void Init() => Instance ??= new ScriptedCruelty();

        public void ScriptedCombatEncounter_Awake(On.RoR2.ScriptedCombatEncounter.orig_Awake orig, ScriptedCombatEncounter self)
        {
            orig(self);

            if (!NetworkServer.active || !self.combatSquad)
                return;

            var rng = self.rng;
            self.combatSquad.onMemberAddedServer += OnMemberAddedServer;
            void OnMemberAddedServer(CharacterMaster master)
            {
                if (!PluginConfig.enableCruelty.Value)
                    return;

                var body = master ? master.GetBody() : null;
                if (!body)
                    return;

                if (!master.inventory || master.inventory.GetItemCountEffective(RoR2Content.Items.HealthDecay) > 0)
                    return;

                if (!PluginConfig.guaranteeSpecialBoss.Value)
                {
                    if (!Util.CheckRoll(PluginConfig.triggerChance.Value))
                        return;

                    if (!PluginConfig.allowBosses.Value && master.isBoss)
                        return;
                }

                AddEliteBuffs(body, master.inventory, rng);
            }
        }

        private void AddEliteBuffs(CharacterBody body, Inventory inventory, Xoroshiro128Plus rng)
        {
            //Check amount of elite buffs the target has
            var currentEliteBuffs = ListPool<BuffIndex>.RentCollection();
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
                currentEliteBuffs.Add(result.eliteEquipmentDef.passiveBuffDef.buffIndex);

                if (PluginConfig.enableMoreDebugLogs.Value)
                    Log.Debug($"SCRIPTED {body}  |  isBoss? {body.isBoss}   |   affix #{currentEliteBuffs.Count} - {result.modifierToken}");

                CrueltyManager.Instance.GiveAffix(body, inventory, result.eliteEquipmentDef);
                if (PluginConfig.bossesGetStats.Value || !body.isBoss)
                {
                    if (PluginConfig.enableMoreDebugLogs.Value)
                        CrueltyManager.Instance.GiveItemBoostsVerbose(inventory, result, currentEliteBuffs.Count);
                    else
                        CrueltyManager.Instance.GiveItemBoosts(inventory, result, currentEliteBuffs.Count);
                }
                CrueltyManager.Instance.GiveDeathReward(deathRewards, xp, gold, currentEliteBuffs.Count);

                if (!Util.CheckRoll(PluginConfig.successChance.Value))
                    break;
            }

            ListPool<BuffIndex>.ReturnCollection(currentEliteBuffs);
        }

        private bool GetScriptedRandom(Xoroshiro128Plus rng, List<BuffIndex> currentBuffs, out EliteDef result)
        {
            result = null;

            var availableDefs = ListPool<EliteDef>.RentCollection();

            for (int i = 0; i < CombatDirector.eliteTiers.Length; ++i)
            {
                var etd = CombatDirector.eliteTiers[i];
                if (CrueltyManager.Instance.IsTierAvailable(etd))
                {
                    for (int j = 0; j < etd.eliteTypes.Length; ++j)
                    {
                        var ed = etd.eliteTypes[j];
                        if (CrueltyManager.Instance.IsEliteDefValid(ed, currentBuffs))
                            availableDefs.Add(ed);
                    }
                }
            }

            if (availableDefs.Count > 0)
                result = rng.NextElementUniform(availableDefs);

            ListPool<EliteDef>.ReturnCollection(availableDefs);
            return !ReferenceEquals(result, null);
        }
    }
}
