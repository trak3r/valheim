using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TeflonTed.Common;
using UnityEngine;

namespace TeflonTed.SortFromGround
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.sortfromground";
        public const string PluginName = "Teflon Ted's Sort From Ground";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// World item drops suck into nearby chests that already contain the same item —
    /// e.g. kiln coal spit → coal chest within ~4 m. Seeded chests only; no empty dumps.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), "SlowUpdate")]
    internal static class ItemDrop_SlowUpdate_Patch
    {
        // Let the drop settle (and give Fuel From Ground a beat) before vacuuming.
        private const double SettleSeconds = 1.0;

        private static readonly Dictionary<int, float> LastTry = new Dictionary<int, float>();

        private static void Postfix(ItemDrop __instance, ZNetView ___m_nview)
        {
            if (__instance == null || ___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner())
            {
                return;
            }

            if (Player.m_localPlayer == null)
            {
                return;
            }

            if (__instance.m_itemData?.m_shared == null || __instance.m_itemData.m_stack <= 0)
            {
                return;
            }

            if (__instance.IsPiece())
            {
                return;
            }

            if (__instance.GetTimeSinceSpawned() < SettleSeconds)
            {
                return;
            }

            int id = __instance.GetInstanceID();
            if (LastTry.TryGetValue(id, out float last) && Time.time - last < 0.5f)
            {
                return;
            }

            LastTry[id] = Time.time;

            var chests = NearbyContainers.Find(__instance.transform.position, Radii.FuelAdjacency);
            if (chests.Count == 0)
            {
                return;
            }

            foreach (Container chest in chests)
            {
                if (__instance == null || __instance.m_itemData == null || __instance.m_itemData.m_stack <= 0)
                {
                    break;
                }

                NearbyContainers.DepositMatchingFromDrop(chest, __instance);
            }

            // Drop instance may have been destroyed — prune stale throttle keys opportunistically.
            if (LastTry.Count > 64)
            {
                LastTry.Clear();
            }
        }
    }
}
