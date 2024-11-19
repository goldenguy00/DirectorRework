using System;
using System.Collections.Generic;
using DirectorRework.Config;
using DirectorRework.Cruelty;
using EntityStates;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DirectorRework.Hooks
{
    /// <summary>
    /// Fixed it.
    /// </summary>
    public class CrueltyManager
    {
        private HashSet<EquipmentIndex> BlacklistedElites { get; set; } = [];

        public static CrueltyManager Instance { get; private set; }
        public bool HooksEnabled { get; set; }

        public static void Init() => Instance ??= new CrueltyManager();

        private CrueltyManager()
        {
            RoR2Application.onLoad += OnLoad;

            PluginConfig.enableCruelty.SettingChanged += OnSettingChanged;
            OnSettingChanged(null, null);
        }

        public void OnSettingChanged(object sender, EventArgs args)
        {
            if (PluginConfig.enableCruelty.Value)
                SetHooks();
            else
                UnsetHooks();
        }

        public void SetHooks()
        {
            if (!HooksEnabled)
            {
                GlobalEventManager.onCharacterDeathGlobal += this.GlobalEventManager_onCharacterDeathGlobal;

                On.RoR2.CombatDirector.Awake += CombatCruelty.CombatDirector_Awake;
                On.RoR2.ScriptedCombatEncounter.Awake += ScriptedCruelty.ScriptedCombatEncounter_Awake;

                IL.EntityStates.VoidInfestor.Infest.FixedUpdate += Infest_FixedUpdate;
                IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
                HooksEnabled = true;
            }
        }

        public void UnsetHooks()
        {
            if (HooksEnabled)
            {
                GlobalEventManager.onCharacterDeathGlobal -= this.GlobalEventManager_onCharacterDeathGlobal;

                On.RoR2.CombatDirector.Awake -= CombatCruelty.CombatDirector_Awake;
                On.RoR2.ScriptedCombatEncounter.Awake -= ScriptedCruelty.ScriptedCombatEncounter_Awake;

                IL.EntityStates.VoidInfestor.Infest.FixedUpdate -= Infest_FixedUpdate;
                IL.RoR2.GlobalEventManager.OnCharacterDeath -= GlobalEventManager_OnCharacterDeath;
                HooksEnabled = false;
            }
        }

        private void OnLoad()
        {
            var blightIndex = EquipmentCatalog.FindEquipmentIndex("AffixBlightedMoffein");
            if (blightIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(blightIndex);
                if (ed && ed.passiveBuffDef && ed.passiveBuffDef.eliteDef)
                    BlacklistedElites.Add(blightIndex);
            }

            var perfectedIndex = EquipmentCatalog.FindEquipmentIndex("EliteLunarEquipment");
            if (perfectedIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(perfectedIndex);
                if (ed && ed.passiveBuffDef && ed.passiveBuffDef.eliteDef)
                    BlacklistedElites.Add(perfectedIndex);
            }
        }

        private void GlobalEventManager_onCharacterDeathGlobal(DamageReport report)
        {
            if (NetworkServer.active && report.victimIsElite && report.victimBody)
            {
                var inventory = report.victimMaster ? report.victimMaster.inventory : null;
                if (inventory && inventory.GetEquipmentSlotCount() > 1)
                {
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
                            ray.direction = ray.direction * 2f + Vector3.up * 20f;

                            PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(equipmentDef.equipmentIndex), ray.origin, ray.direction);
                        }
                    }
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
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.inventory))
                )) &&
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
            var target = targetBody ? targetBody.inventory : null;
            if (source && target)
            {
                // first empty slot
                uint targetIndex = 0;
                for (uint i = 0; i <= target.GetEquipmentSlotCount(); i++)
                    if (target.GetEquipment(i).Equals(EquipmentState.empty))
                    {
                        targetIndex = i;
                        break;
                    }

                for (uint i = 0; i < source.GetEquipmentSlotCount(); i++)
                {
                    var def = source.GetEquipment(i).equipmentDef;
                    if (def && def.equipmentIndex != EquipmentIndex.None && def.passiveBuffDef && def.passiveBuffDef.isElite)
                    {
                        target.SetEquipmentIndexForSlot(def.equipmentIndex, targetIndex);
                        targetBody.AddBuff(def.passiveBuffDef);
                        targetIndex++;
                    }
                }
            }
        }

        internal static void GiveAffix(Inventory inventory, EquipmentIndex equipmentIdx)
        {
            //Fill in first empty equipment slot
            for (uint i = 0; i <= inventory.GetEquipmentSlotCount(); i++)
                if (inventory.GetEquipment(i).Equals(EquipmentState.empty))
                {
                    inventory.SetEquipmentIndexForSlot(equipmentIdx, i);
                    break;
                }
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

        internal static bool IsValid(EliteDef ed, List<BuffIndex> currentBuffs)
        {
            return ed && ed.IsAvailable() && ed.eliteEquipmentDef &&
                   ed.eliteEquipmentDef.passiveBuffDef &&
                   ed.eliteEquipmentDef.passiveBuffDef.isElite &&
                   !Instance.BlacklistedElites.Contains(ed.eliteEquipmentDef.equipmentIndex) &&
                   !currentBuffs.Contains(ed.eliteEquipmentDef.passiveBuffDef.buffIndex);
        }
    }
}