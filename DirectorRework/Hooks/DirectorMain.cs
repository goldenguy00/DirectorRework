using System;
using System.Collections.Generic;
using System.Linq;
using DirectorRework.Config;
using RoR2;
using UnityEngine.Networking;

namespace DirectorRework.Hooks
{
    internal class DirectorMain
    {
        public bool HooksEnabled { get; set; }
        public bool RefundEnabled { get; set; }
        public bool VarietyEnabled => PluginConfig.enableSpawnDiversity.Value || PluginConfig.enableVieldsDiversity.Value || PluginConfig.enableBossDiversity.Value;

        public static DirectorMain Instance { get; private set; }
        public static void Init() => Instance ??= new DirectorMain();

        private DirectorMain()
        {
            PluginConfig.enableDirectorMain.SettingChanged += OnSettingChanged;
            PluginConfig.enableSpawnDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableCreditRefund.SettingChanged += OnSettingChanged;

            OnSettingChanged(null, null);
        }

        // fuck i hate this, worth a try tho
        public void OnSettingChanged(object sender, EventArgs a)
        {
            if (PluginConfig.enableDirectorMain.Value)
                SetHooks();
            else
                UnsetHooks();
        }

        public void SetHooks()
        {
            // refund
            if (!RefundEnabled && PluginConfig.enableCreditRefund.Value)
            {
                GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;

                RefundEnabled = true;
            }
            else if (RefundEnabled && !PluginConfig.enableCreditRefund.Value)
            {
                GlobalEventManager.onCharacterDeathGlobal -= GlobalEventManager_onCharacterDeathGlobal;

                RefundEnabled = false;
            }

            // enemy variety
            if (!HooksEnabled && VarietyEnabled)
            {
                On.RoR2.CombatDirector.Simulate += CombatDirector_Simulate;
                On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += ChangeMessage;
                On.RoR2.BossGroup.UpdateBossMemories += UpdateTitle;

                HooksEnabled = true;
            }
            else if (HooksEnabled && !VarietyEnabled)
            {
                On.RoR2.CombatDirector.Simulate -= CombatDirector_Simulate;
                On.RoR2.Chat.SendBroadcastChat_ChatMessageBase -= ChangeMessage;
                On.RoR2.BossGroup.UpdateBossMemories -= UpdateTitle;

                HooksEnabled = false;
            }
        }

        public void UnsetHooks()
        {
            // refund
            if (RefundEnabled)
            {
                GlobalEventManager.onCharacterDeathGlobal -= GlobalEventManager_onCharacterDeathGlobal;

                RefundEnabled = false;
            }

            // enemy variety
            if (HooksEnabled)
            {
                On.RoR2.CombatDirector.Simulate -= CombatDirector_Simulate;
                On.RoR2.Chat.SendBroadcastChat_ChatMessageBase -= ChangeMessage;
                On.RoR2.BossGroup.UpdateBossMemories -= UpdateTitle;

                HooksEnabled = false;
            }
        }
        private void CombatDirector_Simulate(On.RoR2.CombatDirector.orig_Simulate orig, CombatDirector self, float deltaTime)
        {
            orig(self, deltaTime);

            if (VarietyEnabled && self.currentMonsterCard != null)
            {
                float monsterSpawnTimer = self.monsterSpawnTimer;
                int spawnCountInCurrentWave = self.spawnCountInCurrentWave;

                if (self == TeleporterInteraction.instance?.bossDirector)
                {
                    if (PluginConfig.enableBossDiversity.Value)
                        self.SetNextSpawnAsBoss();
                }
                else if (ArenaMissionController.instance?.activeMonsterCards?.Any() == true)
                {
                    if (PluginConfig.enableVieldsDiversity.Value)
                        self.PrepareNewMonsterWave(self.rng.NextElementUniform(ArenaMissionController.instance.activeMonsterCards));
                }
                else if (self.finalMonsterCardsSelection != null && self.finalMonsterCardsSelection.Count > 0)
                {
                    if (PluginConfig.enableSpawnDiversity.Value)
                        self.PrepareNewMonsterWave(self.finalMonsterCardsSelection.Evaluate(self.rng.nextNormalizedFloat));
                }

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

            if (!PluginConfig.enableBossDiversity.Value)
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

        private void GlobalEventManager_onCharacterDeathGlobal(DamageReport report)
        {
            if (!NetworkServer.active || !PluginConfig.enableCreditRefund.Value || PluginConfig.creditRefundMultiplier.Value <= 0 || CombatDirector.instancesList.Count <= 0)
                return;

            var body = report?.victimBody;
            if (body && !body.isBoss && !body.isChampion && body.cost > 0f && body.teamComponent.teamIndex == TeamIndex.Monster)
            {
                var combatDirector = CombatDirector.instancesList.ElementAtOrDefault(UnityEngine.Random.Range(0, CombatDirector.instancesList.Count));
                if (combatDirector)
                {
                    var refund = PluginConfig.creditRefundMultiplier.Value * 0.01f * body.cost;
                    if (body.isElite || CombatDirector.IsEliteOnlyArtifactActive())
                    {
                        combatDirector.monsterCredit += refund;
                    }
                    else
                    {
                        combatDirector.refundedMonsterCredit += refund;
                    }
                }
            }
        }
    }
}
