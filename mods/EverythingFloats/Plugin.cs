using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.EverythingFloats
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.everythingfloats";
        public const string PluginName = "Teflon Ted's Everything Floats";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        internal static void ApplyFloating(ObjectDB db)
        {
            if (db?.m_items == null)
            {
                return;
            }

            int added = 0;
            foreach (GameObject item in db.m_items)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.GetComponent<ItemDrop>() == null)
                {
                    continue;
                }

                var body = item.GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                if (item.GetComponentInChildren<Collider>() == null)
                {
                    continue;
                }

                var floating = item.GetComponent<Floating>();
                if (floating == null)
                {
                    floating = item.AddComponent<Floating>();
                    floating.m_waterLevelOffset = 0.7f;
                    added++;
                }

                floating.enabled = true;
            }

            if (added > 0)
            {
                BepInEx.Logging.Logger.CreateLogSource(PluginName)
                    .LogInfo($"Enabled floating on {added} item prefab(s)");
            }
        }
    }

    /// <summary>
    /// Give every item prefab a Floating component so drops stay on the water surface.
    /// </summary>
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            Plugin.ApplyFloating(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    internal static class ObjectDB_CopyOtherDB_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            Plugin.ApplyFloating(__instance);
        }
    }
}
