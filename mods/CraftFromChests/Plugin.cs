using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TeflonTed.Common;
using UnityEngine;

namespace TeflonTed.CraftFromChests
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.craftfromchests";
        public const string PluginName = "Teflon Ted's Craft From Chests";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// While the game is checking or consuming craft/build requirements on the local
    /// player's inventory, also count (and take from) nearby chests.
    /// </summary>
    internal static class CraftScope
    {
        private static int _depth;
        private static Inventory _playerInventory;
        private static List<Container> _chests;
        private static bool _bypass;

        internal static bool Active => _depth > 0 && !_bypass;

        internal static void Enter(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            if (_depth == 0)
            {
                _playerInventory = player.GetInventory();
                _chests = NearbyContainers.Find(player.transform.position, Radii.Workstation);
            }

            _depth++;
        }

        internal static void Exit()
        {
            if (_depth <= 0)
            {
                return;
            }

            _depth--;
            if (_depth == 0)
            {
                _playerInventory = null;
                _chests = null;
            }
        }

        internal static void AddChestCount(Inventory inventory, string name, int quality, bool matchWorldLevel, ref int result)
        {
            if (!Active || inventory == null || inventory != _playerInventory || _chests == null)
            {
                return;
            }

            result += NearbyContainers.CountItem(_chests, name, quality);
        }

        internal static bool TryRemoveFromChests(Inventory inventory, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            if (!Active || inventory == null || inventory != _playerInventory || _chests == null || amount <= 0)
            {
                return false;
            }

            _bypass = true;
            try
            {
                int inBag = inventory.CountItems(name, itemQuality, worldLevelBased);
                int fromBag = Mathf.Min(inBag, amount);
                if (fromBag > 0)
                {
                    inventory.RemoveItem(name, fromBag, itemQuality, worldLevelBased);
                }

                int stillNeed = amount - fromBag;
                if (stillNeed > 0)
                {
                    NearbyContainers.TakeItem(_chests, name, stillNeed, itemQuality);
                }
            }
            finally
            {
                _bypass = false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    internal static class Player_HaveRequirementItems_Patch
    {
        private static void Prefix(Player __instance) => CraftScope.Enter(__instance);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(Player), "HaveRequirements", typeof(Piece), typeof(Player.RequirementMode))]
    internal static class Player_HaveRequirements_Piece_Patch
    {
        private static void Prefix(Player __instance) => CraftScope.Enter(__instance);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(Player), "HaveRequirements", typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
    internal static class Player_HaveRequirements_Recipe_Patch
    {
        private static void Prefix(Player __instance) => CraftScope.Enter(__instance);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(Player), "ConsumeResources")]
    internal static class Player_ConsumeResources_Patch
    {
        private static void Prefix(Player __instance) => CraftScope.Enter(__instance);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(Player), "GetFirstRequiredItem")]
    internal static class Player_GetFirstRequiredItem_Patch
    {
        private static void Prefix(Player __instance) => CraftScope.Enter(__instance);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(InventoryGui), "SetupRequirement")]
    internal static class InventoryGui_SetupRequirement_Patch
    {
        private static void Prefix(Player player) => CraftScope.Enter(player);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class InventoryGui_DoCrafting_Patch
    {
        private static void Prefix(Player player) => CraftScope.Enter(player);
        private static void Postfix() => CraftScope.Exit();
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
    internal static class Inventory_CountItems_Patch
    {
        private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
        {
            CraftScope.AddChestCount(__instance, name, quality, matchWorldLevel, ref __result);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
    internal static class Inventory_HaveItem_Patch
    {
        private static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
        {
            if (__result || !CraftScope.Active)
            {
                return;
            }

            int count = __instance.CountItems(name, -1, matchWorldLevel);
            // CountItems patch already adds chests when Active; if still 0, nothing nearby.
            if (count > 0)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), "RemoveItem", typeof(string), typeof(int), typeof(int), typeof(bool))]
    internal static class Inventory_RemoveItem_Patch
    {
        private static bool Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            return !CraftScope.TryRemoveFromChests(__instance, name, amount, itemQuality, worldLevelBased);
        }
    }
}
