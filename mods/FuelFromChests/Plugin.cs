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
    /// Kilns, smelters, and blast furnaces pull wood/coal from chests within ~2.5m.
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

            int room = SmelterFuel.FuelRoom(__instance);
            if (room <= 0 || __instance.m_fuelItem?.m_itemData?.m_shared == null)
            {
                return;
            }

            string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            var chests = NearbyContainers.Find(__instance.transform.position, Radii.FuelAdjacency);
            if (chests.Count == 0)
            {
                return;
            }

            // One unit per tick keeps filling gentle and shared across machines.
            int taken = NearbyContainers.TakeItem(chests, fuelName, 1);
            if (taken > 0)
            {
                SmelterFuel.AddOneFuel(__instance);
            }
        }
    }
}
