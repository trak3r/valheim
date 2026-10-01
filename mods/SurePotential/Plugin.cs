using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.SurePotential
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.surepotential";
        public const string PluginName = "Teflon Ted's Sure Potential";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        /// <summary>
        /// Idols carry m_upgradeChance / m_breakChance on SharedData. Force surefire
        /// upgrades so Forge of Potential (and any other upgrader station) never gambles.
        /// </summary>
        internal static void ApplySurefire(ObjectDB db)
        {
            if (db?.m_items == null)
            {
                return;
            }

            int changed = 0;
            foreach (GameObject item in db.m_items)
            {
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData shared = drop?.m_itemData?.m_shared;
                if (shared == null)
                {
                    continue;
                }

                // Only touch upgrader resources (idols); leave normal gear alone.
                if (shared.m_upgradeChance <= 0f && shared.m_breakChance <= 0f)
                {
                    continue;
                }

                if (shared.m_upgradeChance >= 1f && shared.m_breakChance <= 0f)
                {
                    continue;
                }

                shared.m_upgradeChance = 1f;
                shared.m_breakChance = 0f;
                changed++;
            }

            if (changed > 0)
            {
                BepInEx.Logging.Logger.CreateLogSource(PluginName)
                    .LogInfo($"Forced 100% upgrade success on {changed} idol/upgrader item(s)");
            }
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            Plugin.ApplySurefire(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    internal static class ObjectDB_CopyOtherDB_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            Plugin.ApplySurefire(__instance);
        }
    }
}
