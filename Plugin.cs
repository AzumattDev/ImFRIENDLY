using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImFRIENDLY
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ImFRIENDLYDAMMITPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ImFRIENDLYDAMMIT";
        internal const string ModVersion = "1.0.1";
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
        static void Prefix(Turret __instance)
        {
            ImFRIENDLYDAMMIT(__instance);
        }

        static void Postfix(Turret __instance)
        {
            ImFRIENDLYDAMMIT(__instance);
        }

        private static void ImFRIENDLYDAMMIT(Turret __instance)
        {
            if (__instance.m_target == null) return;
            if (__instance.m_target.IsTamed())
                __instance.m_target = null;
            if (!__instance.m_target.IsPlayer() && __instance.m_target != Player.m_localPlayer) return;
            if (__instance.m_target.IsPVPEnabled())
            {
                return;
            }

            __instance.m_target = null;
        }
    }
}