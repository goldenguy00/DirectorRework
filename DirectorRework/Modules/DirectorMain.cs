using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public static class DirectorMain
    {
        private const string SUBTITLE_FORMAT = "<sprite name=\"CloudLeft\" tint=1> {0} <sprite name=\"CloudRight\" tint=1>";

        private static bool _varietyEnabled, _refundEnabled;

        public static bool ConfigVarietyEnabled => PluginConfig.enableBossDiversity.Value || PluginConfig.enableSpawnDiversity.Value || PluginConfig.enableVieldsDiversity.Value;

        public static bool RefundEnabled
        {
            get => _refundEnabled;
            set
            {
                if (_refundEnabled == value)
                    return;

                _refundEnabled = value;

                if (_refundEnabled)
                    On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
                else
                    On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;
            }
        }

        public static bool VarietyEnabled
        {
            get => _varietyEnabled;
            set
            {
                if (_varietyEnabled == value)
                    return;

                _varietyEnabled = value;

                if (_varietyEnabled)
                {
                    On.RoR2.CombatDirector.SetNextSpawnAsBoss += CombatDirector_SetNextSpawnAsBoss;

                    IL.RoR2.CombatDirector.SetNextSpawnAsBoss += IL_CombatDirector_SetNextSpawnAsBoss;
                    IL.RoR2.CombatDirector.AttemptSpawnOnTarget += CombatDirector_AttemptSpawnOnTarget;

                    On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += SendBroadcastChat_ChatMessageBase;
                    On.RoR2.BossGroup.UpdateBossMemories += UpdateBossMemories;
                }
                else
                {
                    On.RoR2.BossGroup.UpdateBossMemories -= UpdateBossMemories;
                    On.RoR2.Chat.SendBroadcastChat_ChatMessageBase -= SendBroadcastChat_ChatMessageBase;

                    IL.RoR2.CombatDirector.AttemptSpawnOnTarget -= CombatDirector_AttemptSpawnOnTarget;
                    IL.RoR2.CombatDirector.SetNextSpawnAsBoss -= IL_CombatDirector_SetNextSpawnAsBoss;

                    On.RoR2.CombatDirector.SetNextSpawnAsBoss -= CombatDirector_SetNextSpawnAsBoss;
                }
            }
        }

        internal static void Init()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableDirectorMain.SettingChanged += OnSettingChanged;

            PluginConfig.enableCreditRefund.SettingChanged += OnSettingChanged;

            PluginConfig.enableSpawnDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableBossDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableVieldsDiversity.SettingChanged += OnSettingChanged;
        }

        private static void OnSettingChanged(object _, EventArgs __)
        {
            RefundEnabled = PluginConfig.enableDirectorMain.Value && PluginConfig.enableCreditRefund.Value;
            VarietyEnabled = PluginConfig.enableDirectorMain.Value && ConfigVarietyEnabled;
        }

        #region Hooks

        private static void SendBroadcastChat_ChatMessageBase(On.RoR2.Chat.orig_SendBroadcastChat_ChatMessageBase orig, ChatMessageBase message)
        {
            if (PluginConfig.enableSpawnDiversity.Value && message is Chat.SubjectFormatChatMessage chat)
            {
                if (chat.baseToken is "SHRINE_COMBAT_USE_MESSAGE" && chat.paramTokens?.Length > 0)
                    chat.paramTokens[0] = Language.GetString("LOGBOOK_CATEGORY_MONSTER").ToLower();
            }

            orig(message);
        }

        private static void UpdateBossMemories(On.RoR2.BossGroup.orig_UpdateBossMemories orig, BossGroup self)
        {
            orig(self);

            if (!PluginConfig.enableBossDiversity.Value)
                return;

            var maxMissingHp = float.MinValue;
            CharacterBody cachedBody = null;

            for (var i = 0; i < self.bossMemoryCount; i++)
            {
                ref BossGroup.BossMemory memory = ref self.bossMemories[i];

                if (!memory.cachedBody || memory.lastObservedHealth <= 0f)
                    continue;

                var missingHp = memory.maxObservedMaxHealth + (2 * Mathf.Max(0f, memory.maxObservedMaxHealth - memory.lastObservedHealth));
                if (missingHp > maxMissingHp)
                {
                    maxMissingHp = missingHp;
                    cachedBody = memory.cachedBody;
                }
            }

            if (!cachedBody)
                return;

            var bestObservedSubtitle = cachedBody.GetSubtitle();
            if (string.IsNullOrEmpty(bestObservedSubtitle))
                bestObservedSubtitle = Language.GetString("NULL_SUBTITLE");

            self.bestObservedName = Util.GetBestBodyName(cachedBody.gameObject);
            self.bestObservedSubtitle = string.Format(SUBTITLE_FORMAT, bestObservedSubtitle);
        }

        private static void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
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

                master.onBodyDestroyed += (body) =>
                {
                    if (PluginConfig.creditRefundMultiplier.Value <= 0)
                        return;

                    if (self && self.isActiveAndEnabled && self.monsterCredit > 0)
                    {
                        var refund = body?.cost ?? 0f;
                        if (refund > 0f)
                            self.monsterCredit += refund * PluginConfig.creditRefundMultiplier.Value * 0.01f;
                    }
                };
            }
        }

        private static void CombatDirector_SetNextSpawnAsBoss(On.RoR2.CombatDirector.orig_SetNextSpawnAsBoss orig, CombatDirector self)
        {
            var additionalPlayerCount = Run.instance?.participatingPlayerCount ?? 0;
            if (additionalPlayerCount > 0)
                additionalPlayerCount--;

            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maxBossSpawns.Value + (additionalPlayerCount * (PluginConfig.maxBossSpawns.Value / 4));
            self.skipSpawnIfTooCheap = false;

            orig(self);
        }

        private static void IL_CombatDirector_SetNextSpawnAsBoss(ILContext il)
        {
            var c = new ILCursor(il)
            {
                Index = il.Instrs.Count - 1
            };

            if (c.TryGotoPrev(MoveType.Before,
                    x => x.MatchCallOrCallvirt<CombatDirector>(nameof(CombatDirector.PrepareNewMonsterWave))
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(RollForHorde);
            }
            else Log.Error("IL Hook failed for CombatDirector.SetNextSpawnAsBoss");
        }

        private static void CombatDirector_AttemptSpawnOnTarget(ILContext il)
        {
            var c = new ILCursor(il)
            {
                Index = il.Instrs.Count - 1
            };

            if (c.TryGotoPrev(
                    x => x.MatchLdcI4(1),
                    x => x.MatchRet()) ||
                c.TryGotoPrev(
                    x => x.MatchLdcI4(1),
                    x => x.MatchBr(out _)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(Simulate);
            }
            else Log.Error("IL Hook failed for CombatDirector.Simulate");
        }
        #endregion

        #region Director Modification
        private static DirectorCard RollForHorde(DirectorCard selection, CombatDirector self)
        {
            if (self.rng.RangeInt(0, 100) < PluginConfig.hordeOfManyChance.Value)
                return GetNextSpawnCard(self, isBoss: true, spawnChampion: false) ?? selection;

            return selection;
        }

        public static void Simulate(CombatDirector self)
        {
            if (!ConfigVarietyEnabled)
                return;

            if (ArenaMissionController.instance?.activeMonsterCards?.Any() == true)
            {
                if (PluginConfig.enableVieldsDiversity.Value)
                    self.PrepareNewMonsterWave(self.rng.NextElementUniform(ArenaMissionController.instance.activeMonsterCards));
            }
            else if (self.finalMonsterCardsSelection?.Count > 0 && self.spawnCountInCurrentWave < self.maximumNumberToSpawnBeforeSkipping)
            {
                if (self == TeleporterInteraction.instance?.bossDirector)
                {
                    if (PluginConfig.enableBossDiversity.Value)
                        SetNextSpawn(self, isBoss: true, GetIsChampion(self.currentMonsterCard));
                }
                else
                {
                    if (PluginConfig.enableSpawnDiversity.Value)
                        SetNextSpawn(self, isBoss: false);
                }
            }
        }

        private static bool GetIsChampion(DirectorCard currentMonsterCard) =>
            currentMonsterCard?.spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion ?? false;

        private static void SetNextSpawn(CombatDirector self, bool isBoss, bool spawnChampion = false)
        {
            var nextSpawn = GetNextSpawnCard(self, isBoss, spawnChampion);
            if (nextSpawn?.spawnCard)
            {
                self.currentMonsterCard = nextSpawn;
                self.lastAttemptedMonsterCard = nextSpawn;

                self.currentActiveEliteTier = GetBestEliteTier(nextSpawn, self.monsterCredit);
                self.currentActiveEliteDef = self.currentActiveEliteTier.GetRandomAvailableEliteDef(self.rng);
            }
        }

        private static DirectorCard GetNextSpawnCard(CombatDirector self, bool isBoss, bool spawnChampion = false)
        {
            var weightedSelection = new WeightedSelection<DirectorCard>();
            for (var i = 0; i < self.finalMonsterCardsSelection.Count; i++)
            {
                var choice = self.finalMonsterCardsSelection.GetChoice(i);
                if (choice.value.IsAvailable() && choice.value.cost <= self.monsterCredit)
                {
                    if (isBoss)
                    {
                        if (choice.value.spawnCard is CharacterSpawnCard { forbiddenAsBoss: true })
                            continue;

                        if (spawnChampion != GetIsChampion(choice.value))
                            continue;
                    }

                    if (self.skipSpawnIfTooCheap && self.consecutiveCheapSkips < self.maxConsecutiveCheapSkips)
                    {
                        var bestEliteTier = GetBestEliteTier(choice.value, self.monsterCredit);
                        var eliteCost = bestEliteTier.costMultiplier * choice.value.cost;
                        if (eliteCost * self.maximumNumberToSpawnBeforeSkipping < self.monsterCredit && eliteCost < self.mostExpensiveMonsterCostInDeck)
                            continue;
                    }

                    weightedSelection.AddChoice(choice);
                }
            }

            if (weightedSelection.Count > 0)
                return weightedSelection.Evaluate(self.rng.nextNormalizedFloat);

            return null;
        }

        private static CombatDirector.EliteTierDef GetBestEliteTier(DirectorCard currentMonsterCard, float monsterCredit)
        {
            // default tier
            var currentActiveEliteTier = CombatDirector.eliteTiers[0];
            for (var i = 0; i < CombatDirector.eliteTiers.Length; i++)
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
                for (var i = 1; i < CombatDirector.eliteTiers.Length; i++)
                {
                    var eliteTierDef = CombatDirector.eliteTiers[i];
                    if (eliteTierDef.CanSelect(currentMonsterCard.spawnCard.eliteRules))
                    {
                        var eliteCost = currentMonsterCard.cost * eliteTierDef.costMultiplier;
                        if (eliteCost <= monsterCredit)
                            currentActiveEliteTier = eliteTierDef;
                    }
                }
            }

            return currentActiveEliteTier;
        }
        #endregion
    }
}
