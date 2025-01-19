using System;
using System.Linq;
using DirectorRework.Config;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Hooks
{
    internal class DirectorMain
    {
        public bool HooksEnabled { get; set; }
        public bool RefundEnabled { get; set; }
        public static bool VarietyEnabled => PluginConfig.enableSpawnDiversity.Value || PluginConfig.enableVieldsDiversity.Value || PluginConfig.enableBossDiversity.Value;

        private const string SUBTITLE_FORMAT = "<sprite name=\"CloudLeft\" tint=1> {0} <sprite name=\"CloudRight\" tint=1>";

        public static DirectorMain Instance { get; private set; }
        public static void Init() => Instance ??= new DirectorMain();

        private DirectorMain()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableDirectorMain.SettingChanged += OnSettingChanged;
            PluginConfig.enableSpawnDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableCreditRefund.SettingChanged += OnSettingChanged;
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
                On.RoR2.CombatDirector.Awake += CombatDirector_Awake;

                RefundEnabled = true;
            }
            else if (RefundEnabled && !PluginConfig.enableCreditRefund.Value)
            {
                On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;

                RefundEnabled = false;
            }

            // enemy variety
            if (!HooksEnabled && VarietyEnabled)
            {
                IL.RoR2.CombatDirector.AttemptSpawnOnTarget += CombatDirector_AttemptSpawnOnTarget;
                On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += ChangeMessage;
                On.RoR2.BossGroup.UpdateBossMemories += UpdateTitle;

                HooksEnabled = true;
            }
            else if (HooksEnabled && !VarietyEnabled)
            {
                IL.RoR2.CombatDirector.Simulate -= CombatDirector_AttemptSpawnOnTarget;
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
                On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;

                RefundEnabled = false;
            }

            // enemy variety
            if (HooksEnabled)
            {
                IL.RoR2.CombatDirector.AttemptSpawnOnTarget -= CombatDirector_AttemptSpawnOnTarget;
                On.RoR2.Chat.SendBroadcastChat_ChatMessageBase -= ChangeMessage;
                On.RoR2.BossGroup.UpdateBossMemories -= UpdateTitle;

                HooksEnabled = false;
            }
        }

        private static void CombatDirector_AttemptSpawnOnTarget(ILContext il)
        {
            var c = new ILCursor(il)
            {
                Index = il.Instrs.Count - 1
            };

            if (c.TryGotoPrev(
                    x => x.MatchLdcI4(1),
                    x => x.MatchRet()
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(Simulate);
            }
            else Log.Error("IL Hook failed for CombatDirector.Simulate");
        }

        public static void Simulate(CombatDirector self)
        {
            if (!VarietyEnabled)
                return;

            if (ArenaMissionController.instance?.activeMonsterCards?.Any() == true)
            {
                if (PluginConfig.enableVieldsDiversity.Value)
                    self.PrepareNewMonsterWave(self.rng.NextElementUniform(ArenaMissionController.instance.activeMonsterCards));
            }
            else if (self.finalMonsterCardsSelection?.Count > 0)
            {
                if (self == TeleporterInteraction.instance?.bossDirector)
                {
                    if (PluginConfig.enableBossDiversity.Value)
                    {
                        SetNextSpawn(self, true);
                    }
                }
                else if (self.spawnCountInCurrentWave < self.maximumNumberToSpawnBeforeSkipping)
                {
                    if (PluginConfig.enableSpawnDiversity.Value)
                    {
                        SetNextSpawn(self, false);
                    }
                }
            }
        }

        private static void SetNextSpawn(CombatDirector self, bool isBoss)
        {
            int mostExpensive = self.mostExpensiveMonsterCostInDeck;
            bool spawnChampion = isBoss && self.currentMonsterCard.spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion;

            WeightedSelection<DirectorCard> weightedSelection = new WeightedSelection<DirectorCard>();
            int i = 0;
            for (int count = self.finalMonsterCardsSelection.Count; i < count; i++)
            {
                WeightedSelection<DirectorCard>.ChoiceInfo choice = self.finalMonsterCardsSelection.GetChoice(i);
                if (choice.value.IsAvailable() && choice.value.cost <= self.monsterCredit)
                {
                    if (isBoss)
                    {
                        SpawnCard spawnCard = choice.value.spawnCard;
                        bool isChampion = spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion;
                        bool forbiddenAsBoss = (spawnCard as CharacterSpawnCard)?.forbiddenAsBoss ?? false;

                        if (isChampion == spawnChampion && !forbiddenAsBoss && ValidateBossCard(self, choice.value, mostExpensive))
                        {
                            weightedSelection.AddChoice(choice);
                        }
                    }
                    else
                    {
                        weightedSelection.AddChoice(choice);
                    }
                }
            }

            if (weightedSelection.Count > 0)
            {
                self.currentMonsterCard = weightedSelection.Evaluate(self.rng.nextNormalizedFloat);
                self.currentActiveEliteTier = GetBestEliteTier(self, self.currentMonsterCard);
                self.currentActiveEliteDef = self.currentActiveEliteTier.GetRandomAvailableEliteDef(self.rng);
                self.lastAttemptedMonsterCard = self.currentMonsterCard;
            }
        }

        private static bool ValidateBossCard(CombatDirector self, DirectorCard spawnCard, int mostExpensive)
        {
            float costMultiplier = GetBestEliteTier(self, spawnCard).costMultiplier;
            int cost = spawnCard.cost;
            int eliteCost = (int)(cost * costMultiplier);
            if (eliteCost <= self.monsterCredit)
            {
                cost = eliteCost;
            }

            if (self.skipSpawnIfTooCheap && (cost * self.maximumNumberToSpawnBeforeSkipping) < self.monsterCredit)
            {
                if (mostExpensive > cost)
                {
                    return false;
                }
            }

            return true;
        }

        private static CombatDirector.EliteTierDef GetBestEliteTier(CombatDirector self, DirectorCard currentMonsterCard)
        {
            // default tier
            var currentActiveEliteTier = CombatDirector.eliteTiers[0];
            for (int i = 0; i < CombatDirector.eliteTiers.Length; i++)
            {
                if (CombatDirector.eliteTiers[i].CanSelect(currentMonsterCard.spawnCard.eliteRules))
                {
                    currentActiveEliteTier = CombatDirector.eliteTiers[i];
                    break;
                }
            }

            // find most expensive tier
            if (!(currentMonsterCard.spawnCard as CharacterSpawnCard).noElites)
            {
                for (int i = 1; i < CombatDirector.eliteTiers.Length; i++)
                {
                    var eliteTierDef = CombatDirector.eliteTiers[i];
                    if (eliteTierDef.CanSelect(currentMonsterCard.spawnCard.eliteRules))
                    {
                        float eliteCost = currentMonsterCard.cost * eliteTierDef.costMultiplier * self.eliteBias;
                        if (eliteCost <= self.monsterCredit)
                        {
                            currentActiveEliteTier = eliteTierDef;
                        }
                    }
                }
            }

            return currentActiveEliteTier;
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

            float maxMissingHp = float.MinValue;
            CharacterBody cachedBody = null;

            for (int i = 0; i < self.bossMemoryCount; i++)
            {
                ref BossGroup.BossMemory memory = ref self.bossMemories[i];

                if (!memory.cachedBody || memory.lastObservedHealth <= 0f)
                    continue;

                var missingHp = memory.maxObservedMaxHealth + (4 * Mathf.Max(0f, memory.maxObservedMaxHealth - memory.lastObservedHealth));
                if (missingHp > maxMissingHp)
                {
                    maxMissingHp = missingHp;
                    cachedBody = memory.cachedBody;
                }
            }

            if (cachedBody)
            {
                self.bestObservedName = Util.GetBestBodyName(cachedBody.gameObject);
                var bestObservedSubtitle = cachedBody.GetSubtitle();
                if (string.IsNullOrEmpty(bestObservedSubtitle))
                {
                    bestObservedSubtitle = Language.GetString("NULL_SUBTITLE");
                }

                self.bestObservedSubtitle = string.Format(SUBTITLE_FORMAT, bestObservedSubtitle);
            }
        }

        private void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            orig(self);

            if (!NetworkServer.active || self == TeleporterInteraction.instance?.bossDirector)
                return;

            self.onSpawnedServer.AddListener(OnSpawnedServer);

            void OnSpawnedServer(GameObject masterObject)
            {
                var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
                if (!master)
                    return;

                var body = master.GetBody();
                if (body && !body.isBoss && !body.isChampion && body.cost > 0f && body.teamComponent.teamIndex == TeamIndex.Monster)
                {
                    master.onBodyDestroyed += OnBodyDestroyed;
                }
            }

            void OnBodyDestroyed(CharacterBody body)
            {
                if (PluginConfig.creditRefundMultiplier.Value <= 0)
                    return;

                if (self && self.isActiveAndEnabled && self.monsterCredit > 0)
                {
                    var refund = body?.cost * PluginConfig.creditRefundMultiplier.Value * 0.01f;
                    if (refund.HasValue)
                        self.monsterCredit += refund.Value;
                }
            }
        }
    }
}
