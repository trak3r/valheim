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
    /// When a crafting station UI is open, repair every equipped item that station can repair.
    /// Uses the same eligibility rules as the vanilla repair button (via InventoryGui.CanRepair).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateRepair")]
    internal static class InventoryGui_UpdateRepair_Patch
    {
        private static readonly MethodInfo CanRepairMethod =
            AccessTools.Method(typeof(InventoryGui), "CanRepair", new[] { typeof(ItemDrop.ItemData) });

        private static void Prefix(InventoryGui __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null || __instance == null || CanRepairMethod == null)
            {
                return;
            }

            var station = player.GetCurrentCraftingStation();
            if (station == null)
            {
                return;
            }

            var inv = player.GetInventory();
            if (inv == null)
            {
                return;
            }

            int repaired = 0;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (item == null || !item.m_equipped || !item.m_shared.m_useDurability)
                {
                    continue;
                }

                if (item.m_durability >= item.GetMaxDurability())
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
