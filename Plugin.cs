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
        internal const string ModVersion = "1.1.7";
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
            int num2;
            if (!includeEnemies && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs))
            {
                WearNTear component = me.GetComponent<WearNTear>();
                if (component != null)
                {
                    num2 = component.GetHealthPercentage() == 1.0 ? 1 : 0;
                    goto label_4;
                }
            }

            num2 = 0;
            label_4:
            if (num2 != 0)
                return null;
            bool areWeEnemy = (includePlayers || includeTamed) && !includeEnemies;
            if (!areWeEnemy)
            {
                foreach (Character target in allCharacters)
                {
                    if ((includePlayers || target is not Player) && (includeEnemies || target is not Player) && (includeTamed || !target.IsTamed()))
                    {
                        ImFRIENDLYDAMMITPlugin.ImFRIENDLYDAMMITLogger.LogDebug($"Checking {target.m_name} from {Utils.GetPrefabName(me.gameObject.name)}");
                        if (!AttackTarget(target) || !includeEnemies)
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
                                float num3 = Vector3.Distance(target.transform.position, me.position);
                                if (num3 < (double)num1 || closestCreature == null)
                                {
                                    closestCreature = target;
                                    num1 = num3;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                return BaseAI.FindClosestCreature(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision, passiveAggresive, includePlayers, includeTamed, includeEnemies, onlyTargets);
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
            ImFRIENDLYDAMMITPlugin.ImFRIENDLYDAMMITLogger.LogDebug($"Shooting projectile from {Utils.GetPrefabName(__instance.gameObject.name)} ({__instance.m_name})");
            if (__instance.m_name == "$piece_charredballista")
            {
                return true;
            }

            return TurretUpdateTargetPatch.AttackTarget(___m_target);
        }
    }
}