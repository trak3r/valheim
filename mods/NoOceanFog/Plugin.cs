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
        public const string PluginVersion = "1.0.1";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Ocean whiteout is the Misty weather entry. ZoneSystem.AppendBiomeSetup can re-add it
    /// after EnvMan.Awake, so we purge on awake + append and filter selection.
    /// </summary>
    internal static class OceanMisty
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

        internal static int PurgeFromOcean(EnvMan envMan)
        {
            if (envMan?.m_biomes == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (BiomeEnvSetup biome in envMan.m_biomes)
            {
                if (biome == null || biome.m_biome != Heightmap.Biome.Ocean || biome.m_environments == null)
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
    }

    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Awake))]
    internal static class EnvMan_Awake_Patch
    {
        private static void Postfix(EnvMan __instance)
        {
            int removed = OceanMisty.PurgeFromOcean(__instance);
            if (removed > 0)
            {
                BepInEx.Logging.Logger.CreateLogSource(Plugin.PluginName)
                    .LogInfo($"Removed {removed} Misty entr{(removed == 1 ? "y" : "ies")} from Ocean (Awake)");
            }
        }
    }

    /// <summary>
    /// ZoneSystem merges locationlist biome setups after Awake via AddRange — which
    /// puts Misty back onto Ocean. Strip again whenever a setup is appended.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.AppendBiomeSetup))]
    internal static class EnvMan_AppendBiomeSetup_Patch
    {
        private static void Postfix(EnvMan __instance, BiomeEnvSetup biomeEnv)
        {
            if (biomeEnv != null && biomeEnv.m_biome == Heightmap.Biome.Ocean)
            {
                OceanMisty.StripList(biomeEnv.m_environments);
            }

            int removed = OceanMisty.PurgeFromOcean(__instance);
            if (removed > 0)
            {
                BepInEx.Logging.Logger.CreateLogSource(Plugin.PluginName)
                    .LogInfo($"Removed {removed} Misty entr{(removed == 1 ? "y" : "ies")} from Ocean (AppendBiomeSetup)");
            }
        }
    }

    /// <summary>
    /// Final gate: never offer Misty while the player is in the Ocean biome
    /// (also drops Misty injected by AltBiome.m_addEnvironments).
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetAvailableEnvironments))]
    internal static class EnvMan_GetAvailableEnvironments_Patch
    {
        private static void Postfix(BiomeSector biome, List<EnvEntry> __result)
        {
            if (__result == null || biome.Biome != Heightmap.Biome.Ocean)
            {
                return;
            }

            OceanMisty.StripList(__result);
        }
    }

    /// <summary>
    /// If Misty is already active when entering Ocean (or was selected before a purge),
    /// kick to Clear immediately instead of waiting out the weather period.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), "UpdateEnvironment")]
    internal static class EnvMan_UpdateEnvironment_Patch
    {
        private static void Postfix(EnvMan __instance, BiomeSector biome)
        {
            if (__instance == null || biome.Biome != Heightmap.Biome.Ocean)
            {
                return;
            }

            EnvSetup current = __instance.GetCurrentEnvironment();
            if (current == null || !OceanMisty.IsMistyName(current.m_name))
            {
                return;
            }

            __instance.QueueEnvironment("Clear");
        }
    }
}
