using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImFRIENDLY
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ImFRIENDLYDAMMITPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ImFRIENDLYDAMMIT";
        internal const string ModVersion = "1.1.5";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource ImFRIENDLYDAMMITLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
        }
    }


    [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTarget))]
    public static class TurretUpdateTargetPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var targetMethod = typeof(TurretUpdateTargetPatch).GetMethod(nameof(TurretUpdateTargetPatch.ImFRIENDLYDAMMIT));

            return instructions.Select(inst =>
                inst.opcode == OpCodes.Call &&
                inst.operand is MethodInfo { Name: "FindClosestCreature" }
                    ? new CodeInstruction(OpCodes.Call, targetMethod)
                    : inst);
        }

        public static Character ImFRIENDLYDAMMIT(
            Transform me,
            Vector3 eyePoint,
            float hearRange,
            float viewRange,
            float viewAngle,
            bool alerted,
            bool mistVision,
            bool passiveAggresive,
            bool includePlayers = true,
            bool includeTamed = true,
            bool includeEnemies = true,
            List<Character> onlyTargets = null!)
        {
            List<Character> allCharacters = Character.GetAllCharacters();
            Character closestCreature = null!;
            float num1 = 99999f;
            foreach (Character target in allCharacters)
            {
                if ((includePlayers || target is not Player) && (includeEnemies || target is not Player) && (includeTamed || !target.IsTamed()))
                {
                    if (!AttackTarget(target) && (Utils.GetPrefabName(me.gameObject.name) != "piece_Charred_Balista"))
                        continue;
                    if (onlyTargets != null && onlyTargets.Count > 0)
                    {
                        bool flag = false;
                        foreach (Character onlyTarget in onlyTargets)
                        {
                            if (target.m_name == onlyTarget.m_name)
                            {
                                flag = true;
                                break;
                            }
                        }

                        if (!flag)
                            continue;
                    }

                    if (!target.IsDead())
                    {
                        BaseAI baseAi = target.GetBaseAI();
                        if ((!(baseAi != null) || !baseAi.IsSleeping()) && BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision, target, passiveAggresive, false)) // Setting passiveAggresive to true here because the base game does it in FindClosestCreature.
                        {
                            if (!includeEnemies && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs))
                            {
                                WearNTear component = me.GetComponent<WearNTear>();
                                if (component != null && (double)component.GetHealthPercentage() == 1.0)
                                    continue;
                            }

                            float num2 = Vector3.Distance(target.transform.position, me.position);
                            if (num2 < (double)num1 || closestCreature == null)
                            {
                                closestCreature = target;
                                num1 = num2;
                            }
                        }
                    }
                }
            }

            return closestCreature;
        }

        internal static bool AttackTarget(Character target)
        {
            if (!target.m_nview.IsValid()) return true;
            if (target.IsDead())
            {
                return false;
            }

            if (target.IsTamed())
            {
                return false;
            }

            if (target.GetComponents<Growup>().Any())
            {
                return false;
            }

            if (target.GetComponents<AnimalAI>().Any())
            {
                return false;
            }

            if (target.GetFaction() == Character.Faction.Players)
            {
                return false;
            }

            if (target.IsPVPEnabled())
            {
                return true;
            }

            if (!target.IsPlayer())
            {
                return true;
            }

            if (target.IsPlayer()) // If I don't do this, it will attack other players overall. Wish there was a better way to see if the player should be attacked.
            {
                return false;
            }

            if (target == Player.m_localPlayer)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
    static class TurretShootProjectilePatch
    {
        static bool Prefix(Turret __instance, ref Character ___m_target)
        {
            if (__instance.m_name == "$piece_charredballista")
            {
                return true;
            }

            return TurretUpdateTargetPatch.AttackTarget(___m_target);
        }
    }
}