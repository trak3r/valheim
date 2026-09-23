using System;
using System.Collections;
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
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Ocean fog is the Misty environment entry on the Ocean biome.
    /// Remove it at startup so Misty never rolls while sailing.
    /// Plains Misty (and everything else) is left alone.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Awake))]
    internal static class EnvMan_Awake_Patch
    {
        private static void Postfix(EnvMan __instance)
        {
            if (__instance?.m_biomes == null)
            {
                return;
            }

            int removedTotal = 0;
            foreach (BiomeEnvSetup biome in __instance.m_biomes)
            {
                if (biome == null || biome.m_biome != Heightmap.Biome.Ocean)
                {
                    continue;
                }

                removedTotal += RemoveMistyEntries(biome.m_environments);
            }

            if (removedTotal > 0)
            {
                BepInEx.Logging.Logger.CreateLogSource(Plugin.PluginName)
                    .LogInfo($"Removed {removedTotal} Misty weather entr{(removedTotal == 1 ? "y" : "ies")} from Ocean");
            }
        }

        private static int RemoveMistyEntries(IList environments)
        {
            if (environments == null || environments.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int i = environments.Count - 1; i >= 0; i--)
            {
                object entry = environments[i];
                if (entry == null)
                {
                    continue;
                }

                if (!IsMisty(entry))
                {
                    continue;
                }

                environments.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private static bool IsMisty(object entry)
        {
            string envName = ReadString(entry, "m_environment");
            if (!string.IsNullOrEmpty(envName) &&
                envName.IndexOf("Misty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            object envSetup = ReadField(entry, "m_env");
            if (envSetup != null)
            {
                string name = ReadString(envSetup, "m_name");
                if (!string.IsNullOrEmpty(name) &&
                    name.IndexOf("Misty", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadString(object obj, string fieldName)
        {
            object value = ReadField(obj, fieldName);
            return value as string;
        }

        private static object ReadField(object obj, string fieldName)
        {
            if (obj == null)
            {
                return null;
            }

            var field = AccessTools.Field(obj.GetType(), fieldName);
            return field?.GetValue(obj);
        }
    }
}
