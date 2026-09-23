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
    /// Kilns/smelters/furnaces suck matching fuel ItemDrops within ~2.5m (assembly-line drops).
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
            if (room <= 0)
            {
                return;
            }

            var drops = NearbyItemDrops.Find(__instance.transform.position, Radii.FuelAdjacency);
            foreach (var drop in drops)
            {
                if (room <= 0)
                {
                    break;
                }

                if (!SmelterFuel.IsFuelItem(__instance, drop.m_itemData))
                {
                    continue;
                }

                // Consume one from the ground stack.
                var nview = drop.GetComponent<ZNetView>();
                if (drop.m_itemData.m_stack <= 1)
                {
                    if (nview != null && nview.GetZDO() != null && ZNetScene.instance != null)
                    {
                        ZNetScene.instance.Destroy(drop.gameObject);
                    }
                    else
                    {
                        Object.Destroy(drop.gameObject);
                    }
                }
                else
                {
                    drop.m_itemData.m_stack--;
                    AccessTools.Method(typeof(ItemDrop), "Save")?.Invoke(drop, null);
                }

                SmelterFuel.AddOneFuel(__instance);
                room--;
                break; // one per tick
            }
        }
    }
}
