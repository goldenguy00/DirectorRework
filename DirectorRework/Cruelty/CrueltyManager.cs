using System;
using System.Collections.Generic;
using EntityStates;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Cruelty
{
    public static class CrueltyManager
    {
        private static readonly HashSet<EquipmentIndex> _blacklistedElites = [];
        private static bool _hooksEnabled;

        public static void Init()
        {
            EquipmentCatalog.availability.CallWhenAvailable(OnLoad);

            PluginConfig.enableCruelty.SettingChanged += OnSettingChanged;
            OnSettingChanged(null, null);
        }

        public static void OnSettingChanged(object sender, EventArgs args)
        {
            if (PluginConfig.enableCruelty.Value)
                SetHooks();
            else
                UnsetHooks();
        }

        public static void SetHooks()
        {
            if (_hooksEnabled)
                return;

            GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;

            On.RoR2.CombatDirector.Awake += CombatCruelty.CombatDirector_Awake;
            On.RoR2.ScriptedCombatEncounter.Awake += ScriptedCruelty.ScriptedCombatEncounter_Awake;

            IL.EntityStates.VoidInfestor.Infest.FixedUpdate += Infest_FixedUpdate;
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;

            _hooksEnabled = true;
        }

        public static void UnsetHooks()
        {
            GlobalEventManager.onCharacterDeathGlobal -= GlobalEventManager_onCharacterDeathGlobal;

            On.RoR2.CombatDirector.Awake -= CombatCruelty.CombatDirector_Awake;
            On.RoR2.ScriptedCombatEncounter.Awake -= ScriptedCruelty.ScriptedCombatEncounter_Awake;

            IL.EntityStates.VoidInfestor.Infest.FixedUpdate -= Infest_FixedUpdate;
            IL.RoR2.GlobalEventManager.OnCharacterDeath -= GlobalEventManager_OnCharacterDeath;

            _hooksEnabled = false;
        }

        private static void OnLoad()
        {
            BlacklistEquipment("AffixBlightedMoffein");
            BlacklistEquipment("EliteLunarEquipment");

            void BlacklistEquipment(string name)
            {
                var equipmentIndex = EquipmentCatalog.FindEquipmentIndex(name);
                if (equipmentIndex != EquipmentIndex.None)
                {
                    var ed = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    if (ed?.passiveBuffDef?.eliteDef)
                        _blacklistedElites.Add(equipmentIndex);
                }
            }
        }

        private static void GlobalEventManager_onCharacterDeathGlobal(DamageReport report)
        {
            if (!NetworkServer.active)
                return;

            if (!report.victimIsElite || !report.victimBody)
                return;

            var inventory = report.victimMaster ? report.victimMaster.inventory : null;
            if (!inventory)
                return;

            var baseLuck = report.attackerMaster ? report.attackerMaster.luck : 0;
            for (uint i = 1; i < inventory.GetEquipmentSlotCount(); i++)
            {
                var equipmentDef = inventory.GetEquipment(i).equipmentDef;
                if (equipmentDef && Util.CheckRoll(equipmentDef.dropOnDeathChance * 100f, baseLuck + i, report.attackerMaster))
                {
                    var ray = report.victimBody.inputBank
                        ? report.victimBody.inputBank.GetAimRay()
                        : new Ray(report.victimBody.corePosition, report.victimBody.transform.rotation * Vector3.forward);

                    ray.origin += Vector3.up * 1.5f;
                    ray.direction = (ray.direction * 2f) + (Vector3.up * 20f);

                    PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(equipmentDef.equipmentIndex), ray.origin, ray.direction);
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
        internal static void TransferAffixes(Inventory source, CharacterBody targetBody)
        {
            if (!source)
                return;

            var target = targetBody ? targetBody.inventory : null;
            if (!target)
                return;

            // first empty slot
            uint targetIndex = 0;
            for (uint i = 0; i <= target.GetEquipmentSlotCount(); i++)
            {
                var indexForSlot = target.GetEquipment(i).equipmentIndex;
                if (indexForSlot == EquipmentIndex.None)
                {
                    targetIndex = i;
                    break;
                }
            }

            for (uint i = 0; i < source.GetEquipmentSlotCount(); i++)
            {
                var def = source.GetEquipment(i).equipmentDef;
                if (def && def.equipmentIndex != EquipmentIndex.None && def.passiveBuffDef && def.passiveBuffDef.isElite)
                {
                    if (targetBody.HasBuff(def.passiveBuffDef.buffIndex))
                        continue;

                    target.SetEquipmentIndexForSlot(def.equipmentIndex, targetIndex);
                    targetBody.AddBuff(def.passiveBuffDef);
                    targetIndex++;
                }
            }
        }

        internal static void GiveAffix(CharacterBody body, Inventory inventory, EquipmentDef equipment)
        {
            //Fill in first empty equipment slot
            for (uint i = 0; i <= inventory.GetEquipmentSlotCount(); i++)
            {
                var indexForSlot = inventory.GetEquipment(i).equipmentIndex;
                if (indexForSlot == EquipmentIndex.None || indexForSlot == equipment.equipmentIndex)
                {
                    inventory.SetEquipmentIndexForSlot(equipment.equipmentIndex, i);
                    break;
                }
            }
            body.SetBuffCount(equipment.passiveBuffDef.buffIndex, 1);
        }

        internal static void GiveItemBoosts(Inventory inventory, EliteDef def, int affixes)
        {
            inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt(def.healthBoostCoefficient * 10f / (affixes + 2)));
            inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((def.damageBoostCoefficient - 1f) * 10f / (affixes + 1)));
        }

        internal static void GiveDeathReward(DeathRewards deathReward, uint xp, uint gold, int affixes)
        {
            if (deathReward)
            {
                if (xp != 0)
                    deathReward.expReward += Convert.ToUInt32(xp / affixes);
                if (gold != 0)
                    deathReward.goldReward += Convert.ToUInt32(gold / affixes);
            }
        }

        internal static bool IsValid(EliteDef ed, HashSet<BuffIndex> currentBuffs)
        {
            return ed && ed.IsAvailable() && ed.eliteEquipmentDef?.passiveBuffDef?.isElite == true &&
                   !_blacklistedElites.Contains(ed.eliteEquipmentDef.equipmentIndex) &&
                   !currentBuffs.Contains(ed.eliteEquipmentDef.passiveBuffDef.buffIndex);
        }
    }
}