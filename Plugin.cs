using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImFRIENDLY
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ImFRIENDLYDAMMITPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ImFRIENDLYDAMMIT";
        internal const string ModVersion = "1.0.4";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ImFRIENDLYDAMMITLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
        }
    }


    [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTarget))]
    public static class TurretUpdateTargetPatch
    {
        private static bool _useNewPatch;

        public static void Prepare()
        {
            if (IsVersionNewerOrEqual(0, 213, 3))
                _useNewPatch = true;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var targetMethod = _useNewPatch
                ? typeof(TurretUpdateTargetPatch).GetMethod("ImFRIENDLYDAMMITPTB")
                : typeof(TurretUpdateTargetPatch).GetMethod("ImFRIENDLYDAMMIT");

            return instructions.Select(inst =>
                inst.opcode == OpCodes.Call &&
                inst.operand is MethodInfo { Name: "FindClosestCreature" }
                    ? new CodeInstruction(OpCodes.Call, targetMethod)
                    : inst);
        }

        public static Character ImFRIENDLYDAMMITPTB(
            Transform me,
            Vector3 eyePoint,
            float hearRange,
            float viewRange,
            float viewAngle,
            bool alerted,
            bool mistVision,
            bool includePlayers = true,
            bool includeTamed = true,
            List<Character> onlyTargets = null)
        {
            List<Character> allCharacters = Character.GetAllCharacters();
            Character closestCreature = null;
            float num1 = 99999f;
            foreach (Character target in allCharacters)
            {
                if ((includePlayers || !(target is Player)) && (includeTamed || !target.IsTamed()))
                {
                    if (!AttackTarget(target))
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
                        if ((!(baseAi != null) || !baseAi.IsSleeping()) &&
                            BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision,
                                target))
                        {
                            float num2 = Vector3.Distance(target.transform.position, me.position);
                            if (num2 < (double)num1 ||
                                (Object)closestCreature == null)
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

        public static Character ImFRIENDLYDAMMIT(
            Turret __instance,
            Transform me,
            Vector3 eyePoint,
            float hearRange,
            float viewRange,
            float viewAngle,
            bool alerted,
            bool mistVision)
        {
            List<Character> allCharacters = Character.GetAllCharacters();
            Character unfriendlyCreature = null;
            float num1 = 99999f;
            foreach (Character character in allCharacters)
            {
                if (!AttackTarget(character)) continue;
                BaseAI baseAi = character.GetBaseAI();
                if ((baseAi != null && baseAi.IsSleeping()) || !BaseAI.CanSenseTarget(me, eyePoint, hearRange,
                        viewRange, viewAngle, alerted, mistVision, character)) continue;
                float num2 = Vector3.Distance(character.transform.position, me.position);
                if (!(num2 < (double)num1) && unfriendlyCreature != null) continue;
                unfriendlyCreature = character;
                num1 = num2;
            }

            return unfriendlyCreature;
        }

        internal static bool AttackTarget(Character target)
        {
            return target.m_nview.IsValid() && !target.IsDead() && !target.IsTamed() &&
                   !target.GetComponents<Growup>().Any() && target.IsPVPEnabled() &&
                   (!target.IsPlayer() || target != Player.m_localPlayer);
        }


        public static bool IsVersionNewerOrEqual(int major, int minor, int patch)
        {
            return major > Version.m_major || major == Version.m_major && minor > Version.m_minor ||
                   major == Version.m_major && minor == Version.m_minor && patch >= Version.m_patch;
        }
    }
}