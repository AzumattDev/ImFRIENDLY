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
        internal const string ModVersion = "1.0.2";
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
        static bool Prefix(Turret __instance, float dt)
        {
            ImFRIENDLYDAMMIT(__instance, dt);
            // By returning false, we skip other Prefix methods as well as the original UpdateTarget method.
            return false;
        }

        private static void ImFRIENDLYDAMMIT(Turret instance, float dt)
        {
            // This code is coppied from Turret.UpdateTarget, the only modification is where we get the closest creature
            // which uses our helper method instead of the original BaseAI.FindClosestCreature
            if (!instance.m_nview.IsValid())
            {
                return;
            }
            if (!instance.HasAmmo())
            {
                if (instance.m_haveTarget)
                {
                    instance.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetTarget", ZDOID.None);
                }
                return;
            }
            instance.m_updateTargetTimer -= dt;
            if (instance.m_updateTargetTimer <= 0f)
            {
                instance.m_updateTargetTimer = (Character.IsCharacterInRange(instance.transform.position, 40f) ? instance.m_updateTargetIntervalNear : instance.m_updateTargetIntervalFar);
                Character character = Helper.FindClosestCreature(instance.transform, instance.m_eye.transform.position, 0f, instance.m_viewDistance, instance.m_horizontalAngle, alerted: false, mistVision: false);
                if (character != instance.m_target)
                {
                    if ((bool)character)
                    {
                        instance.m_newTargetEffect.Create(instance.transform.position, instance.transform.rotation);
                    }
                    else
                    {
                        instance.m_lostTargetEffect.Create(instance.transform.position, instance.transform.rotation);
                    }
                    instance.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetTarget", character ? character.GetZDOID() : ZDOID.None);
                }
            }
            if (instance.m_haveTarget && (!instance.m_target || instance.m_target.IsDead()))
            {
                ZLog.Log("Target is gone");
                instance.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetTarget", ZDOID.None);
                instance.m_lostTargetEffect.Create(instance.transform.position, instance.transform.rotation);
            }
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
    static class TurretShootProjectilePatch
    {
        static bool Prefix(Turret __instance)
        {
            ImFRIENDLYDAMMIT(__instance);
            return false;
        }

        private static void ImFRIENDLYDAMMIT(Turret instance)
        {
            // This code is coppied from Turret.ShootProjectile, the only modification is the setup call for the
            // projectile to set the local player as the owner that way the projectile doesn't do friendly damage
            Transform transform = instance.m_eye.transform;
            instance.m_shootEffect.Create(transform.position, transform.rotation);
            instance.m_nview.GetZDO().Set("lastAttack", (float)ZNet.instance.GetTimeSeconds());
            instance.m_lastAmmo = instance.GetAmmoItem();
            int @int = instance.m_nview.GetZDO().GetInt("ammo");
            int num = Mathf.Min(1, (instance.m_maxAmmo == 0) ? instance.m_lastAmmo.m_shared.m_attack.m_projectiles : Mathf.Min(@int, instance.m_lastAmmo.m_shared.m_attack.m_projectiles));
            if (instance.m_maxAmmo > 0)
            {
                instance.m_nview.GetZDO().Set("ammo", @int - num);
            }
            ZLog.Log($"Turret '{instance.name}' is shooting {num} projectiles, ammo: {@int}/{instance.m_maxAmmo}");
            for (int i = 0; i < num; i++)
            {
                Vector3 forward = transform.forward;
                Vector3 axis = Vector3.Cross(forward, Vector3.up);
                float projectileAccuracy = instance.m_lastAmmo.m_shared.m_attack.m_projectileAccuracy;
                Quaternion quaternion = Quaternion.AngleAxis(Random.Range(0f - projectileAccuracy, projectileAccuracy), Vector3.up);
                forward = Quaternion.AngleAxis(Random.Range(0f - projectileAccuracy, projectileAccuracy), axis) * forward;
                forward = quaternion * forward;
                instance.m_lastProjectile = Object.Instantiate(instance.m_lastAmmo.m_shared.m_attack.m_attackProjectile, transform.position, transform.rotation);
                HitData hitData = new HitData();
                hitData.m_toolTier = instance.m_lastAmmo.m_shared.m_toolTier;
                hitData.m_pushForce = instance.m_lastAmmo.m_shared.m_attackForce;
                hitData.m_backstabBonus = instance.m_lastAmmo.m_shared.m_backstabBonus;
                hitData.m_staggerMultiplier = instance.m_lastAmmo.m_shared.m_attack.m_staggerMultiplier;
                hitData.m_damage.Add(instance.m_lastAmmo.GetDamage());
                hitData.m_statusEffect = (instance.m_lastAmmo.m_shared.m_attackStatusEffect ? instance.m_lastAmmo.m_shared.m_attackStatusEffect.name : "");
                hitData.m_blockable = instance.m_lastAmmo.m_shared.m_blockable;
                hitData.m_dodgeable = instance.m_lastAmmo.m_shared.m_dodgeable;
                hitData.m_skill = instance.m_lastAmmo.m_shared.m_skillType;
                if (instance.m_lastAmmo.m_shared.m_attackStatusEffect != null)
                {
                    hitData.m_statusEffect = instance.m_lastAmmo.m_shared.m_attackStatusEffect.name;
                }
                instance.m_lastProjectile.GetComponent<IProjectile>()?.Setup(Player.m_localPlayer.IsPVPEnabled() ? null : Player.m_localPlayer, forward * instance.m_lastAmmo.m_shared.m_attack.m_projectileVel, instance.m_hitNoise, hitData, null, instance.m_lastAmmo);
            }
            instance.m_warmup = -100f;
        }
    }
}
