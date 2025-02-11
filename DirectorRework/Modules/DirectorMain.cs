using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class DirectorMain
    {
        public static bool ConfigVarietyEnabled => PluginConfig.enableBossDiversity.Value || PluginConfig.enableSpawnDiversity.Value || PluginConfig.enableVieldsDiversity.Value;

        private const string SUBTITLE_FORMAT = "<sprite name=\"CloudLeft\" tint=1> {0} <sprite name=\"CloudRight\" tint=1>";

        private bool varietyEnabled, refundEnabled;

        public static DirectorMain Instance { get; private set; }
        public static void Init() => Instance ??= new DirectorMain();

        private DirectorMain()
        {
            OnSettingChanged(null, null);

            PluginConfig.enableDirectorMain.SettingChanged += OnSettingChanged;
            PluginConfig.enableSpawnDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableBossDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableVieldsDiversity.SettingChanged += OnSettingChanged;
            PluginConfig.enableCreditRefund.SettingChanged += OnSettingChanged;
        }

        // fuck i hate this, worth a try tho
        public void OnSettingChanged(object sender, EventArgs a)
        {
            var enabled = PluginConfig.enableDirectorMain.Value;

            ApplyRefund(enabled && PluginConfig.enableCreditRefund.Value);
            ApplyVariety(enabled && ConfigVarietyEnabled);
        }

        private void ApplyRefund(bool enabled)
        {
            if (refundEnabled != enabled)
            {
                refundEnabled = enabled;

                if (enabled)
                    On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
                else
                    On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;
            }
        }

        private void ApplyVariety(bool enabled)
        {
            if (varietyEnabled != enabled)
            {
                varietyEnabled = enabled;

                if (enabled)
                {
                    On.RoR2.CombatDirector.SetNextSpawnAsBoss += CombatDirector_SetNextSpawnAsBoss;
                    IL.RoR2.CombatDirector.SetNextSpawnAsBoss += IL_CombatDirector_SetNextSpawnAsBoss;
                    IL.RoR2.CombatDirector.AttemptSpawnOnTarget += CombatDirector_AttemptSpawnOnTarget;
                    On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += ChangeMessage;
                    On.RoR2.BossGroup.UpdateBossMemories += UpdateTitle;
                }
                else
                {
                    On.RoR2.CombatDirector.SetNextSpawnAsBoss -= CombatDirector_SetNextSpawnAsBoss;
                    IL.RoR2.CombatDirector.SetNextSpawnAsBoss -= IL_CombatDirector_SetNextSpawnAsBoss;
                    IL.RoR2.CombatDirector.AttemptSpawnOnTarget -= CombatDirector_AttemptSpawnOnTarget;
                    On.RoR2.Chat.SendBroadcastChat_ChatMessageBase -= ChangeMessage;
                    On.RoR2.BossGroup.UpdateBossMemories -= UpdateTitle;
                }
            }
        }

        private static void CombatDirector_SetNextSpawnAsBoss(On.RoR2.CombatDirector.orig_SetNextSpawnAsBoss orig, CombatDirector self)
        {
            var additionalPlayerCount = Run.instance?.participatingPlayerCount ?? 0;
            if (additionalPlayerCount > 0)
                additionalPlayerCount--;

            self.maximumNumberToSpawnBeforeSkipping = PluginConfig.maxBossSpawns.Value + (PluginConfig.maxBossSpawns.Value * additionalPlayerCount / 4);
            self.skipSpawnIfTooCheap = false;

            orig(self);
        }

        private static void IL_CombatDirector_SetNextSpawnAsBoss(ILContext il)
        {
            var c = new ILCursor(il)
            {
                Index = il.Instrs.Count - 1
            };

            var selectionLoc = 0;
            if (c.TryGotoPrev(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdloc(out selectionLoc),
                    x => x.MatchCallOrCallvirt<CombatDirector>(nameof(CombatDirector.PrepareNewMonsterWave))
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, selectionLoc);
                c.EmitDelegate<Func<CombatDirector, DirectorCard, DirectorCard>>((self, selection) =>
                {
                    if (self.rng.RangeInt(0, 100) < PluginConfig.hordeOfManyChance.Value)
                        return GetNextSpawnCard(self, isBoss: true, spawnChampion: false) ?? selection;
                    return selection;
                });
                c.Emit(OpCodes.Stloc, selectionLoc);
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
                        if ((choice.value.spawnCard as CharacterSpawnCard)?.forbiddenAsBoss == true)
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

            var maxMissingHp = float.MinValue;
            CharacterBody cachedBody = null;

            for (var i = 0; i < self.bossMemoryCount; i++)
            {
                ref BossGroup.BossMemory memory = ref self.bossMemories[i];

                if (!memory.cachedBody || memory.lastObservedHealth <= 0f)
                    continue;

                var missingHp = memory.maxObservedMaxHealth + 4 * Mathf.Max(0f, memory.maxObservedMaxHealth - memory.lastObservedHealth);
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
                    bestObservedSubtitle = Language.GetString("NULL_SUBTITLE");

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
                    master.onBodyDestroyed += OnBodyDestroyed;
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
