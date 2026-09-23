using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TeflonTed.Common;
using UnityEngine;

namespace TeflonTed.FuelFromGround
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.fuelfromground";
        public const string PluginName = "Teflon Ted's Fuel From Ground";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Kilns/smelters/furnaces suck matching fuel or cookable item drops within ~2.5m.
    /// Wood next to a kiln is queued as ore; coal next to a smelter is added as fuel.
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

            if (SmelterFuel.FuelRoom(__instance) <= 0 && SmelterFuel.OreRoom(__instance) <= 0)
            {
                return;
            }

            var drops = NearbyItemDrops.Find(__instance.transform.position, Radii.FuelAdjacency);
            foreach (var drop in drops)
            {
                if (drop?.m_itemData == null)
                {
                    continue;
                }

                // Ensure prefab name is available for ore RPC (kiln wood, smelter ore, etc.).
                if (drop.m_itemData.m_dropPrefab == null)
                {
                    drop.m_itemData.m_dropPrefab = drop.gameObject;
                }

                if (!SmelterFuel.TryAcceptItem(__instance, drop.m_itemData))
                {
                    continue;
                }

                ConsumeOneFromDrop(drop);
                break; // one per tick
            }
        }

        private static void ConsumeOneFromDrop(ItemDrop drop)
        {
            var nview = drop.GetComponent<ZNetView>();
            if (drop.m_itemData.m_stack <= 1)
            {
                if (nview != null && nview.IsValid())
                {
                    nview.Destroy();
                }
                else if (ZNetScene.instance != null)
                {
                    ZNetScene.instance.Destroy(drop.gameObject);
                }
                else
                {
                    Object.Destroy(drop.gameObject);
                }

                return;
            }

            drop.m_itemData.m_stack--;
            AccessTools.Method(typeof(ItemDrop), "Save")?.Invoke(drop, null);
        }
    }
}
