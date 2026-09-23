using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.AutoRepair
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.autorepair";
        public const string PluginName = "Teflon Ted's Auto Repair";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// When a crafting station UI is open, repair every damaged inventory item that
    /// station can repair — same eligibility as the vanilla repair hammer button.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateRepair")]
    internal static class InventoryGui_UpdateRepair_Patch
    {
        private static readonly MethodInfo CanRepairMethod =
            AccessTools.Method(typeof(InventoryGui), "CanRepair", new[] { typeof(ItemDrop.ItemData) });

        private static readonly List<ItemDrop.ItemData> WornItems = new List<ItemDrop.ItemData>();

        private static void Prefix(InventoryGui __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null || __instance == null || CanRepairMethod == null)
            {
                return;
            }

            var station = player.GetCurrentCraftingStation();
            if (station == null || !station.m_canRepair)
            {
                return;
            }

            // Match vanilla RepairOneItem: require a usable station (e.g. forge needs fire).
            if (!station.CheckUsable(player, false))
            {
                return;
            }

            var inv = player.GetInventory();
            if (inv == null)
            {
                return;
            }

            WornItems.Clear();
            inv.GetWornItems(WornItems);

            int repaired = 0;
            foreach (ItemDrop.ItemData item in WornItems)
            {
                if (item == null)
                {
                    continue;
                }

                bool canRepair = (bool)CanRepairMethod.Invoke(__instance, new object[] { item });
                if (!canRepair)
                {
                    continue;
                }

                item.m_durability = item.GetMaxDurability();
                repaired++;
            }

            if (repaired > 0 && station.m_repairItemDoneEffects != null)
            {
                station.m_repairItemDoneEffects.Create(
                    station.transform.position,
                    Quaternion.identity,
                    null,
                    1f,
                    -1);
            }
        }
    }
}
