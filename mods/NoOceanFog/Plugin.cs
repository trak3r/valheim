using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace TeflonTed.NoOceanFog
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.nooceanfog";
        public const string PluginName = "Teflon Ted's No Ocean Fog";
        public const string PluginVersion = "1.1.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Dense whiteout fog is the Misty weather (Ocean, Plains, rarely elsewhere) — not
    /// Mistlands mist. ZoneSystem.AppendBiomeSetup can re-add entries after Awake, so we
    /// purge on awake + append, filter selection, and kick Clear if Misty is already active.
    /// </summary>
    internal static class MistyWeather
    {
        internal static bool IsMistyName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf("Misty", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsMisty(EnvEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (IsMistyName(entry.m_environment))
            {
                return true;
            }

            return entry.m_env != null && IsMistyName(entry.m_env.m_name);
        }

        internal static int PurgeAll(EnvMan envMan)
        {
            if (envMan?.m_biomes == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (BiomeEnvSetup biome in envMan.m_biomes)
            {
                if (biome?.m_environments == null)
                {
                    continue;
                }

                removed += biome.m_environments.RemoveAll(IsMisty);
            }

            return removed;
        }

        internal static void StripList(List<EnvEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            entries.RemoveAll(IsMisty);
        }

        internal static void LogRemoved(int removed, string where)
        {
            if (removed <= 0)
            {
                return;
            }

            BepInEx.Logging.Logger.CreateLogSource(Plugin.PluginName)
                .LogInfo($"Removed {removed} Misty entr{(removed == 1 ? "y" : "ies")} ({where})");
        }
    }

    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Awake))]
    internal static class EnvMan_Awake_Patch
    {
        private static void Postfix(EnvMan __instance)
        {
            MistyWeather.LogRemoved(MistyWeather.PurgeAll(__instance), "Awake");
        }
    }

    /// <summary>
    /// ZoneSystem merges locationlist biome setups after Awake via AddRange.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.AppendBiomeSetup))]
    internal static class EnvMan_AppendBiomeSetup_Patch
    {
        private static void Postfix(EnvMan __instance, BiomeEnvSetup biomeEnv)
        {
            if (biomeEnv != null)
            {
                MistyWeather.StripList(biomeEnv.m_environments);
            }

            MistyWeather.LogRemoved(MistyWeather.PurgeAll(__instance), "AppendBiomeSetup");
        }
    }

    /// <summary>
    /// Never offer Misty for any biome (also drops AltBiome.m_addEnvironments injections).
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetAvailableEnvironments))]
    internal static class EnvMan_GetAvailableEnvironments_Patch
    {
        private static void Postfix(List<EnvEntry> __result)
        {
            MistyWeather.StripList(__result);
        }
    }

    /// <summary>
    /// If Misty is already active, kick to Clear instead of waiting out the weather period.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), "UpdateEnvironment")]
    internal static class EnvMan_UpdateEnvironment_Patch
    {
        private static void Postfix(EnvMan __instance)
        {
            if (__instance == null)
            {
                return;
            }

            EnvSetup current = __instance.GetCurrentEnvironment();
            if (current == null || !MistyWeather.IsMistyName(current.m_name))
            {
                return;
            }

            __instance.QueueEnvironment("Clear");
        }
    }
}
