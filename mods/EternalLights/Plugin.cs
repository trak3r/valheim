using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.EternalLights
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.eternallights";
        public const string PluginName = "Teflon Ted's Eternal Lights";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Keep fireplace / torch / brazier fuel topped up so lighting pieces never burn out.
    /// </summary>
    [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
    internal static class Fireplace_UpdateFireplace_Patch
    {
        private static void Postfix(Fireplace __instance, ZNetView ___m_nview)
        {
            if (__instance == null || ___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner())
            {
                return;
            }

            float max = __instance.m_maxFuel;
            if (max <= 0f)
            {
                return;
            }

            float current = ___m_nview.GetZDO().GetFloat("fuel", 0f);
            if (current < max)
            {
                ___m_nview.GetZDO().Set("fuel", max);
            }
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.IsBurning))]
    internal static class Fireplace_IsBurning_Patch
    {
        private static void Postfix(Fireplace __instance, ref bool __result)
        {
            if (__result || __instance == null)
            {
                return;
            }

            // Lighting pieces with a fuel capacity should always count as burning.
            if (__instance.m_maxFuel > 0f)
            {
                __result = true;
            }
        }
    }
}
