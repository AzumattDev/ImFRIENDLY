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
        internal const string ModVersion = "1.0.3";
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
    static class TurretUpdateTargetPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> source = new(instructions);
            for (int index = 0; index < source.Count; ++index)
            {
                CodeInstruction codeInstruction = source[index];
                if (codeInstruction.opcode != OpCodes.Call) continue;
                MethodInfo operand = codeInstruction.operand as MethodInfo;
                if (operand == null || operand.Name != "FindClosestCreature") continue;
                source[index] = new CodeInstruction(OpCodes.Call, ImFRIENDLYDAMMIT);
                break;
            }
            return source.AsEnumerable();
        }

        internal static bool DontAttack(Character target)
        {
            if (target.IsTamed()) return true;
            if (target.GetComponentsInChildren<Growup>().Any())
                return true;
            if (!target.IsPlayer() && target != Player.m_localPlayer) return true;
            if (target.IsPVPEnabled())
            {
                return true;
            }

            return false;
        }
        
        public static Character ImFRIENDLYDAMMIT( // This is basically a copy of the method we need to fix up for the turrets, we are just replacing the call to it with this method
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
            foreach (Character target in allCharacters.Where(target => !DontAttack(target)).Where(target => (includePlayers || target is not Player) && (includeTamed || !target.IsTamed())))
            {
                if (onlyTargets is { Count: > 0 })
                {
                    bool flag = onlyTargets.Any(onlyTarget => target.m_name == onlyTarget.m_name);
                    if (!flag)
                        continue;
                }

                if (target.IsDead()) continue;
                BaseAI baseAi = target.GetBaseAI();
                if ((baseAi != null && baseAi.IsSleeping()) || !BaseAI.CanSenseTarget(me, eyePoint, hearRange,
                        viewRange, viewAngle, alerted, mistVision, target)) continue;
                float num2 = Vector3.Distance(target.transform.position, me.position);
                if (!(num2 < (double)num1) && closestCreature! != null) continue;
                closestCreature = target;
                num1 = num2;
            }
            return closestCreature;
        }
    }
}