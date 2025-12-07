using System;
using System.Linq;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class DirectorMain
    {
        private const string SUBTITLE_FORMAT = "<sprite name=\"CloudLeft\" tint=1> {0} <sprite name=\"CloudRight\" tint=1>";

        public bool ConfigVarietyEnabled => PluginConfig.enableBossDiversity.Value || PluginConfig.enableSpawnDiversity.Value || PluginConfig.enableVieldsDiversity.Value;

        public bool RefundEnabled
        {
            get;
            set
            {
                if (field == value)
                    return;

                field = value;

                if (field)
                    On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
                else
                    On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;
            }
        }

        public bool VarietyEnabled
        {
            get;
            set
            {
                if (field == value)
                    return;

                field = value;

                if (field)
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
        private CombatDirector self;
        public static DirectorMain Instance { get; private set; }
        internal static void Init() => Instance ??= new DirectorMain();
        public DirectorMain()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableDirectorMain.SettingChanged += OnSettingChanged;

            PluginConfig.enableCreditRefund.SettingChanged += OnSettingChanged;

            PluginConfig.enableSpawnDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableBossDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableVieldsDiversity.SettingChanged += OnSettingChanged;
        }

        private void OnSettingChanged(object _, EventArgs __)
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

            self.onSpawnedWithDirectorServer.AddListener(OnSpawnedServer);
        }

        private static void OnSpawnedServer(GameObject masterObject, CombatDirector self)
        {
            var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
            if (!master)
                return;

            if (PluginConfig.creditRefundMultiplier.Value <= 0)
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
                c.Emit(OpCodes.Call, AccessTools.DeclaredMethod(typeof(DirectorMain), nameof(RollForHorde)));
            }
            else Log.Error("IL Hook failed for CombatDirector.SetNextSpawnAsBoss");
        }

        private static void CombatDirector_AttemptSpawnOnTarget(ILContext il)
        {
            var c = new ILCursor(il);
            if (c.TryGotoNext(
                    x => x.MatchCallOrCallvirt<CombatDirector>(nameof(CombatDirector.Spawn))) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchStfld<CombatDirector>(nameof(CombatDirector.consecutiveCheapSkips))
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Call, AccessTools.DeclaredMethod(typeof(DirectorMain), nameof(AttemptSpawnOnTarget)));
            }
            else
            {
                Log.Error("IL Hook failed for CombatDirector.Simulate");
            }
        }
        #endregion

        #region Director Modification
        public static DirectorCard RollForHorde(DirectorCard selection, CombatDirector self)
        {
            Instance.self = self;
            if (self.rng.RangeInt(0, 100) < PluginConfig.hordeOfManyChance.Value)
                selection = Instance.GetNextSpawnCard(isBoss: true, spawnChampion: false) ?? selection;
            Instance.self = null;
            return selection;
        }

        public static void AttemptSpawnOnTarget(CombatDirector self)
        {
            if (Instance.ConfigVarietyEnabled)
            {
                Instance.self = self;
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
                            Instance.SetNextSpawn(isBoss: true, GetIsChampion(self.currentMonsterCard));
                    }
                    else
                    {
                        if (PluginConfig.enableSpawnDiversity.Value)
                            Instance.SetNextSpawn(isBoss: false, spawnChampion: false);
                    }
                }
            }
            Instance.self = null;
        }

        private static bool GetIsChampion(DirectorCard currentMonsterCard) =>
            currentMonsterCard?.GetSpawnCard()?.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion ?? false;

        private void SetNextSpawn(bool isBoss, bool spawnChampion)
        {
            var nextMonsterCard = GetNextSpawnCard(isBoss, spawnChampion);
            if (nextMonsterCard?.GetSpawnCard())
            {
                var nextEliteTier = GetBestEliteTier(nextMonsterCard);
                var nextEliteDef = nextEliteTier.GetRandomAvailableEliteDef(self.rng);
                if (PluginConfig.enableMoreDebugLogs.Value)
                {
                    Log.Debug($"{self.name} overriding {self.currentMonsterCard?.GetSpawnCard()?.prefab?.name ?? "null"} {self.currentActiveEliteDef?.modifierToken} | {self.currentMonsterCard?.cost} * {self.currentActiveEliteTier?.costMultiplier}" +
                        $"\r\n\twith {nextMonsterCard.GetSpawnCard().prefab?.name} {nextEliteDef?.modifierToken} | {nextMonsterCard.cost} * {nextEliteTier.costMultiplier}");
                }
                self.currentMonsterCard = nextMonsterCard;
                self.lastAttemptedMonsterCard = nextMonsterCard;
                self.currentActiveEliteTier = nextEliteTier;
                self.currentActiveEliteDef = nextEliteDef;
            }
        }

        private DirectorCard GetNextSpawnCard(bool isBoss, bool spawnChampion = false)
        {
            if (self.finalMonsterCardsSelection?.Count == 0 || self.monsterCredit <= 0)
                return null;

            var weightedSelection = new WeightedSelection<DirectorCard>();
            var eliteSelection = new WeightedSelection<DirectorCard>();
            for (var i = 0; i < self.finalMonsterCardsSelection.Count; i++)
            {
                var choice = self.finalMonsterCardsSelection.GetChoice(i);
                if (choice.value?.IsAvailable() == true && choice.value.cost <= self.monsterCredit)
                {
                    if (isBoss)
                    {
                        if ((choice.value.GetSpawnCard() as CharacterSpawnCard)?.forbiddenAsBoss != false)
                            continue;

                        if (spawnChampion != GetIsChampion(choice.value))
                            continue;
                    }

                    var bestEliteTier = GetBestEliteTier(choice.value);
                    if (self.skipSpawnIfTooCheap && self.consecutiveCheapSkips < self.maxConsecutiveCheapSkips)
                    {
                        var eliteCost = bestEliteTier.costMultiplier * choice.value.cost;
                        if (eliteCost * self.maximumNumberToSpawnBeforeSkipping < self.monsterCredit && eliteCost < self.mostExpensiveMonsterCostInDeck)
                            continue;
                    }

                    weightedSelection.AddChoice(choice);

                    // if we have an elite selected, try to select another one
                    if (self.currentActiveEliteTier != CombatDirector.eliteTiers[0] && bestEliteTier == CombatDirector.eliteTiers[0])
                    {
                        continue;
                    }
                    eliteSelection.AddChoice(choice);
                }
            }
            if (eliteSelection.Count > 0)
                return eliteSelection.Evaluate(self.rng.nextNormalizedFloat);
            if (weightedSelection.Count > 0)
                return weightedSelection.Evaluate(self.rng.nextNormalizedFloat);

            return null;
        }

        private CombatDirector.EliteTierDef GetBestEliteTier(DirectorCard nextMonsterCard)
        {
            // default tier
            var currentActiveEliteTier = CombatDirector.eliteTiers[0];
            for (var i = 0; i < CombatDirector.eliteTiers.Length; i++)
            {
                if (CombatDirector.eliteTiers[i].CanSelect(nextMonsterCard.GetSpawnCard().eliteRules))
                {
                    currentActiveEliteTier = CombatDirector.eliteTiers[i];
                    break;
                }
            }

            // find most expensive tier
            if (!(nextMonsterCard.GetSpawnCard() as CharacterSpawnCard).noElites)
            {
                for (var i = 1; i < CombatDirector.eliteTiers.Length; i++)
                {
                    var eliteTierDef = CombatDirector.eliteTiers[i];
                    if (eliteTierDef.CanSelect(nextMonsterCard.GetSpawnCard().eliteRules))
                    {
                        var eliteCost = nextMonsterCard.cost * eliteTierDef.costMultiplier;
                        if (eliteCost <= self.monsterCredit)
                            currentActiveEliteTier = eliteTierDef;
                    }
                }
            }

            return currentActiveEliteTier;
        }
        #endregion
    }
}
