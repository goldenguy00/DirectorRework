using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DirectorRework.Config;
using DirectorRework.Cruelty;
using EntityStates;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Modules
{
    public class CrueltyManager
    {
        private readonly HashSet<EquipmentIndex> _blacklistedElites = [];
        public bool ModuleEnabled { get; private set; }
        public static CrueltyManager Instance { get; private set; }
        public static void Init() => Instance ??= new CrueltyManager();
        public CrueltyManager()
        {
            EquipmentCatalog.availability.CallWhenAvailable(OnLoad);

            OnSettingChanged();

            PluginConfig.enableEliteStacking.SettingChanged += OnSettingChanged;
            PluginConfig.useRecommendedValues[PluginConfig.enableEliteStacking.Definition.Section].SettingChanged += OnSettingChanged;
        }

        public void OnSettingChanged(object _ = null, EventArgs __ = null)
        {
            if (PluginConfig.enableEliteStacking.GetValueOrDefault())
                SetHooks();
            else
                UnsetHooks();
        }

        public void SetHooks()
        {
            if (ModuleEnabled) return;
            ModuleEnabled = true;

            GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;

            On.RoR2.CombatDirector.Awake += CombatCruelty.Instance.CombatDirector_Awake;
            On.RoR2.ScriptedCombatEncounter.Awake += ScriptedCruelty.Instance.ScriptedCombatEncounter_Awake;

            IL.EntityStates.VoidInfestor.Infest.FixedUpdate += Infest_FixedUpdate;
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        public void UnsetHooks()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath -= GlobalEventManager_OnCharacterDeath;
            IL.EntityStates.VoidInfestor.Infest.FixedUpdate -= Infest_FixedUpdate;

            On.RoR2.ScriptedCombatEncounter.Awake -= ScriptedCruelty.Instance.ScriptedCombatEncounter_Awake;
            On.RoR2.CombatDirector.Awake -= CombatCruelty.Instance.CombatDirector_Awake;

            GlobalEventManager.onCharacterDeathGlobal -= GlobalEventManager_onCharacterDeathGlobal;

            ModuleEnabled = false;
        }

        private void OnLoad()
        {
            BlacklistEquipment("AffixBlightedMoffein");
            BlacklistEquipment("EliteLunarEquipment");
        }

        private void BlacklistEquipment(string name)
        {
            var equipmentIndex = EquipmentCatalog.FindEquipmentIndex(name);
            if (equipmentIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                if (ed?.passiveBuffDef?.isElite == true && ed.passiveBuffDef.buffIndex != BuffIndex.None)
                {
                    Log.Info("Blacklisting " + name);
                    _blacklistedElites.Add(equipmentIndex);
                    return;
                }
            }

            Log.Info("Failed to blacklist " + name);
        }

        private void GlobalEventManager_onCharacterDeathGlobal(DamageReport report)
        {
            if (!NetworkServer.active)
                return;

            if (!report.victimIsElite || !report.victimBody)
                return;

            var inventory = report.victimMaster ? report.victimMaster.inventory : null;
            if (!inventory)
                return;

            var baseLuck = report.attackerMaster ? report.attackerMaster.luck : 0;
            var currentEquipment = inventory.currentEquipmentIndex;
            for (uint i = 0; i < inventory.GetEquipmentSlotCount(); i++)
            {
                var equipmentDef = inventory.GetEquipment(i, 0).equipmentDef;
                if (equipmentDef == null || equipmentDef.dropOnDeathChance <= 0f)
                    continue;

                if (equipmentDef.equipmentIndex == EquipmentIndex.None || equipmentDef.equipmentIndex == currentEquipment)
                    continue;

                if (Util.CheckRoll(equipmentDef.dropOnDeathChance * 100f, baseLuck + i, report.attackerMaster))
                {
                    var ray = report.victimBody.inputBank
                        ? report.victimBody.inputBank.GetAimRay()
                        : new Ray(report.victimBody.corePosition, report.victimBody.transform.rotation * Vector3.forward);

                    ray.origin += Vector3.up * 1.5f;
                    ray.direction = (ray.direction * 2f) + (Vector3.up * 20f);

                    if (PluginConfig.enableMoreDebugLogs.GetValueOrDefault())
                        Log.Debug("dropping affix " + equipmentDef.nameToken + " woo " + i + "x luck!");
                    PickupDropletController.CreatePickupDroplet(new UniquePickup(PickupCatalog.FindPickupIndex(equipmentDef.equipmentIndex)), ray.origin, ray.direction, false);
                }
            }
        }

        private static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            var c = new ILCursor(il);

            var victimBodyLoc = 0;
            var infestorMasterLoc = 0;
            if (c.TryGotoNext(
                    x => x.MatchLdloc(out victimBodyLoc),
                    x => x.MatchLdsfld(AccessTools.Field(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.EliteVoid))),
                    x => x.MatchCallOrCallvirt(AccessTools.Method(typeof(CharacterBody), nameof(CharacterBody.HasBuff), [typeof(BuffDef)]))
                ) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchLdloc(out infestorMasterLoc),
                    x => x.MatchCallOrCallvirt<CharacterMaster>(nameof(CharacterMaster.SpawnBodyHere))
                ))
            {
                c.Emit(OpCodes.Ldloc, victimBodyLoc);
                c.Emit(OpCodes.Ldloc, infestorMasterLoc);
                c.Emit(OpCodes.Call, AccessTools.Method(typeof(CrueltyManager), nameof(TransferAffixes), [typeof(CharacterBody), typeof(CharacterMaster)]));
            }
            else
                Log.Error("Director Rework: GlobalEventManager_OnCharacterDeath IL Hook failed");
        }

        private static void Infest_FixedUpdate(ILContext il)
        {
            var c = new ILCursor(il);

            var loc = 0;
            if (c.TryGotoNext(
                    x => x.MatchLdloc(out loc),
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.inventory)))) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.SetEquipmentIndex))
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(EntityState), nameof(EntityState.characterBody)));
                c.Emit(OpCodes.Ldloc, loc);
                c.Emit(OpCodes.Call, AccessTools.Method(typeof(CrueltyManager), nameof(TransferAffixes), [typeof(CharacterBody), typeof(CharacterBody)]));
            }
            else
                Log.Error("Director Rework: Infest_FixedUpdate IL Hook failed");
        }

        internal static void TransferAffixes(CharacterBody sourceBody, CharacterMaster targetMaster) => TransferAffixes(sourceBody ? sourceBody.inventory : null, targetMaster ? targetMaster.GetBody() : null);
        internal static void TransferAffixes(CharacterBody sourceBody, CharacterBody targetBody) => TransferAffixes(sourceBody ? sourceBody.inventory : null, targetBody);
        internal static void TransferAffixes(Inventory sourceInventory, CharacterBody targetBody)
        {
            if (!sourceInventory)
                return;

            var targetInventory = targetBody ? targetBody.inventory : null;
            if (!targetInventory)
                return;

            // first empty slot
            uint targetIndex = 0;
            for (uint i = 0; i <= targetInventory.GetEquipmentSlotCount(); i++)
            {
                var indexForSlot = targetInventory.GetEquipment(i, 0).equipmentIndex;
                if (indexForSlot == EquipmentIndex.None)
                {
                    targetIndex = i;
                    break;
                }
            }

            for (uint i = 0; i < sourceInventory.GetEquipmentSlotCount(); i++)
            {
                var affixEquipDef = sourceInventory.GetEquipment(i, 0).equipmentDef;
                if (affixEquipDef == null || affixEquipDef.passiveBuffDef == null)
                    continue;

                if (affixEquipDef.equipmentIndex == EquipmentIndex.None || affixEquipDef.passiveBuffDef.buffIndex == BuffIndex.None)
                    continue;

                if (affixEquipDef.passiveBuffDef.isElite == false || targetBody.HasBuff(affixEquipDef.passiveBuffDef.buffIndex))
                    continue;

                targetInventory.SetEquipmentIndexForSlot(affixEquipDef.equipmentIndex, targetIndex, 0);
                targetBody.AddBuff(affixEquipDef.passiveBuffDef);
                targetIndex++;
            }
        }

        internal void GiveAffix(CharacterBody body, Inventory inventory, EquipmentDef equipment)
        {
            // Fill in first empty equipment slot
            // Or existing affix equipment
            for (uint i = 0; i <= inventory.GetEquipmentSlotCount(); i++)
            {
                var indexForSlot = inventory.GetEquipment(i, 0).equipmentIndex;
                if (indexForSlot == EquipmentIndex.None || indexForSlot == equipment.equipmentIndex)
                {
                    inventory.SetEquipmentIndexForSlot(equipment.equipmentIndex, i, 0);
                    break;
                }
            }
            body.SetBuffCount(equipment.passiveBuffDef.buffIndex, 1);
        }

        internal void GiveItemBoosts(Inventory inventory, EliteDef def, int affixes)
        {
            inventory.GiveItemPermanent(RoR2Content.Items.BoostHp, Mathf.RoundToInt(def.healthBoostCoefficient * 10f / (affixes + 2)));
            inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((def.damageBoostCoefficient - 1f) * 10f / (affixes + 1)));
        }

        internal void GiveItemBoostsVerbose(Inventory inventory, EliteDef def, int affixes)
        {
            var boost = Mathf.RoundToInt(def.healthBoostCoefficient * 10f / (affixes + 2));
            Log.Debug("hp " + boost);
            inventory.GiveItemPermanent(RoR2Content.Items.BoostHp, boost);

            boost = Mathf.RoundToInt((def.damageBoostCoefficient - 1f) * 10f / (affixes + 1));
            Log.Debug("dmg " + boost);
            inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, boost);
        }

        internal void GiveDeathReward(DeathRewards deathReward, uint xp, uint gold, int affixes)
        {
            if (deathReward)
            {
                if (xp != 0)
                    deathReward.expReward += Convert.ToUInt32(xp / affixes);
                if (gold != 0)
                    deathReward.goldReward += Convert.ToUInt32(gold / affixes);
            }
        }

        internal bool IsEliteDefValid(EliteDef ed, List<BuffIndex> currentBuffs)
        {
            return ed && ed.IsAvailable() && ed.eliteEquipmentDef?.passiveBuffDef?.isElite is true &&
                   ed.eliteEquipmentDef.equipmentIndex != EquipmentIndex.None &&
                   ed.eliteEquipmentDef.passiveBuffDef.buffIndex != BuffIndex.None &&
                   !_blacklistedElites.Contains(ed.eliteEquipmentDef.equipmentIndex) &&
                   !currentBuffs.Contains(ed.eliteEquipmentDef.passiveBuffDef.buffIndex);
        }

        internal bool IsTierAvailable(CombatDirector.EliteTierDef etd) => etd?.canSelectWithoutAvailableEliteDef == false;

        internal bool IsTierAffordable([NotNull] CombatDirector.EliteTierDef etd, DirectorCard card, int cardCost, float availableCredits)
        {
            if (card?.GetSpawnCard() && etd.CanSelect(card.GetSpawnCard().eliteRules))
            {
                return availableCredits >= cardCost * etd.costMultiplier;
            }
            return false;
        }
    }
}