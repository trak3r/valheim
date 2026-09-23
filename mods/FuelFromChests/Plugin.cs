using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TeflonTed.Common;
using UnityEngine;

namespace TeflonTed.FuelFromChests
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.fuelfromchests";
        public const string PluginName = "Teflon Ted's Fuel From Chests";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Kilns / smelters / blast furnaces pull matching items from chests within ~2.5m.
    /// Wood → kiln ore queue; coal → smelter fuel; ores → smelter/furnace queues.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class Smelter_UpdateSmelter_Patch
    {
        private static readonly Dictionary<int, float> LastRun = new Dictionary<int, float>();

        private static void Postfix(Smelter __instance, ZNetView ___m_nview)
        {
            if (__instance == null || ___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner())
            {
                return;
            }

            int id = __instance.GetInstanceID();
            if (LastRun.TryGetValue(id, out float last) && Time.time - last < 0.5f)
            {
                return;
            }

            LastRun[id] = Time.time;

            var chests = NearbyContainers.Find(__instance.transform.position, Radii.FuelAdjacency);
            if (chests.Count == 0)
            {
                return;
            }

            // Fuel first (coal for smelter/furnace).
            if (__instance.m_fuelItem?.m_itemData?.m_shared != null && SmelterFuel.FuelRoom(__instance) > 0)
            {
                string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
                if (NearbyContainers.TakeItem(chests, fuelName, 1) > 0)
                {
                    SmelterFuel.AddOneFuel(__instance);
                    return;
                }
            }

            // Then conversion inputs (wood for kiln, metal for smelter).
            if (SmelterFuel.OreRoom(__instance) <= 0 || __instance.m_conversion == null)
            {
                return;
            }

            foreach (var conversion in __instance.m_conversion)
            {
                var from = conversion?.m_from;
                if (from?.m_itemData?.m_shared == null)
                {
                    continue;
                }

                string sharedName = from.m_itemData.m_shared.m_name;
                string prefabName = from.gameObject.name;
                if (NearbyContainers.TakeItem(chests, sharedName, 1) <= 0)
                {
                    continue;
                }

                SmelterFuel.AddOneOre(__instance, prefabName);
                return;
            }
        }
    }
}
