using System.Collections.Generic;
using System.Linq;
using DirectorRework.Config;
using RoR2;
using UnityEngine;

namespace DirectorRework.Hooks
{
    internal class DirectorMain
    {
        public static DirectorMain instance;

        public static void Init()
        {
            instance ??= new DirectorMain();
        }

        private DirectorMain()
        {
            On.RoR2.CombatDirector.Spawn += CombatDirector_Spawn;

            // enemy variety
            On.RoR2.CombatDirector.Simulate += CombatDirector_Simulate;
            On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += ChangeMessage;
            On.RoR2.BossGroup.UpdateBossMemories += UpdateTitle;
        }

        private void CombatDirector_Simulate(On.RoR2.CombatDirector.orig_Simulate orig, CombatDirector self, float deltaTime)
        {
            orig(self, deltaTime);

            if (PluginConfig.enableSpawnDiversity.Value && self.currentMonsterCard != null)
            {
                float monsterSpawnTimer = self.monsterSpawnTimer;
                int spawnCountInCurrentWave = self.spawnCountInCurrentWave;

                if (self == TeleporterInteraction.instance?.bossDirector)
                    self.SetNextSpawnAsBoss();
                else if (PluginConfig.enableVieldsDiversity.Value && ArenaMissionController.instance && ArenaMissionController.instance.activeMonsterCards?.Any() == true)
                    self.PrepareNewMonsterWave(self.rng.NextElementUniform(ArenaMissionController.instance.activeMonsterCards));
                else if (self.finalMonsterCardsSelection != null && self.finalMonsterCardsSelection.Count > 0)
                    self.PrepareNewMonsterWave(self.finalMonsterCardsSelection.Evaluate(self.rng.nextNormalizedFloat));

                self.monsterSpawnTimer = monsterSpawnTimer;
                self.spawnCountInCurrentWave = spawnCountInCurrentWave;
            }
        }

        private void ChangeMessage(On.RoR2.Chat.orig_SendBroadcastChat_ChatMessageBase orig, ChatMessageBase message)
        {
            if (PluginConfig.enableSpawnDiversity.Value && message is Chat.SubjectFormatChatMessage chat && chat.paramTokens?.Any() is true && chat.baseToken is "SHRINE_COMBAT_USE_MESSAGE")
                chat.paramTokens[0] = Language.GetString("LOGBOOK_CATEGORY_MONSTER").ToLower();

            // Replace with generic message since shrine will have multiple enemy types
            orig(message);
        }

        private void UpdateTitle(On.RoR2.BossGroup.orig_UpdateBossMemories orig, BossGroup self)
        {
            orig(self);

            if (!PluginConfig.enableSpawnDiversity.Value)
                return;

            var health = new Dictionary<(string, string), float>();
            float maximum = 0;

            for (int i = 0; i < self.bossMemoryCount; ++i)
            {
                var body = self.bossMemories[i].cachedBody;
                if (!body)
                    continue;

                var component = body.healthComponent;
                if (!component || !component.alive)
                    continue;

                string name = Util.GetBestBodyName(body.gameObject);
                string subtitle = body.GetSubtitle();

                var key = (name, subtitle);
                if (!health.ContainsKey(key))
                    health[key] = 0;

                health[key] += component.combinedHealth + component.missingCombinedHealth * 4;

                // Use title for enemy with the most total health and damage received
                if (health[key] > maximum)
                    maximum = health[key];
                else
                    continue;

                if (string.IsNullOrEmpty(subtitle))
                    subtitle = Language.GetString("NULL_SUBTITLE");

                self.bestObservedName = name;
                self.bestObservedSubtitle = $"<sprite name=\"CloudLeft\" tint=1> {subtitle} <sprite name=\"CloudRight\" tint=1>";
            }
        }

        private bool CombatDirector_Spawn(On.RoR2.CombatDirector.orig_Spawn orig, RoR2.CombatDirector self, SpawnCard spawnCard, EliteDef eliteDef, Transform spawnTarget,
            DirectorCore.MonsterSpawnDistance spawnDistance, bool preventOverhead, float valueMultiplier, DirectorPlacementRule.PlacementMode placementMode)
        {
            var res = orig(self, spawnCard, eliteDef, spawnTarget, spawnDistance, preventOverhead, valueMultiplier, placementMode);
            if (res && PluginConfig.enableCreditRefund.Value)
            {
                var refund = PluginConfig.creditRefundMultiplier.Value * 0.01f * spawnCard.directorCreditCost * valueMultiplier;

                self.monsterCredit += refund;
                self.totalCreditsSpent += refund;
            }

            return res;
        }
    }
}
