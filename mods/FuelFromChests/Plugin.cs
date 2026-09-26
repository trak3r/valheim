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
        public const string PluginVersion = "1.1.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    internal static class ChestSupply
    {
        private static readonly MethodInfo FindCookableItemMethod =
            AccessTools.Method(typeof(Smelter), "FindCookableItem", new[] { typeof(Inventory) });

        private static readonly MethodInfo SetAnimationMethod =
            AccessTools.Method(typeof(Smelter), "SetAnimation", new[] { typeof(bool) });

        internal static List<Container> Nearby(Smelter smelter)
        {
            if (smelter == null)
            {
                return new List<Container>();
            }

            return NearbyContainers.FindNearAny(SmelterFuel.IntakePositions(smelter), Radii.FuelAdjacency);
        }

        internal static bool InventoryHasFuel(Smelter smelter, Humanoid user)
        {
            var inv = user?.GetInventory();
            var shared = smelter?.m_fuelItem?.m_itemData?.m_shared?.m_name;
            return inv != null && shared != null && inv.HaveItem(shared, matchWorldLevel: true);
        }

        internal static bool InventoryHasOre(Smelter smelter, Humanoid user)
        {
            var inv = user?.GetInventory();
            if (smelter == null || inv == null || FindCookableItemMethod == null)
            {
                return false;
            }

            return FindCookableItemMethod.Invoke(smelter, new object[] { inv }) != null;
        }

        internal static bool ChestsHaveFuel(Smelter smelter, List<Container> chests)
        {
            var shared = smelter?.m_fuelItem?.m_itemData?.m_shared?.m_name;
            return shared != null && NearbyContainers.CountItem(chests, shared) > 0;
        }

        internal static bool ChestsHaveOre(Smelter smelter, List<Container> chests)
        {
            if (smelter?.m_conversion == null)
            {
                return false;
            }

            foreach (var conversion in smelter.m_conversion)
            {
                var from = conversion?.m_from;
                if (from?.m_itemData?.m_shared == null)
                {
                    continue;
                }

                string shared = from.m_itemData.m_shared.m_name;
                string prefab = SmelterFuel.PrefabName(from.gameObject.name);
                if (SmelterFuel.IsPremiumWood(prefab, shared))
                {
                    continue;
                }

                if (NearbyContainers.CountItem(chests, shared) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryTakeFuelFromChests(Smelter smelter, Humanoid user)
        {
            if (SmelterFuel.FuelRoom(smelter) <= 0)
            {
                return false;
            }

            string shared = smelter.m_fuelItem?.m_itemData?.m_shared?.m_name;
            if (shared == null)
            {
                return false;
            }

            var chests = Nearby(smelter);
            if (NearbyContainers.TakeItem(chests, shared, 1) <= 0)
            {
                return false;
            }

            SmelterFuel.AddOneFuel(smelter);
            user?.Message(MessageHud.MessageType.Center, "$msg_added " + shared);
            return true;
        }

        internal static bool TryTakeOreFromChests(Smelter smelter, Humanoid user)
        {
            if (SmelterFuel.OreRoom(smelter) <= 0 || smelter.m_conversion == null)
            {
                return false;
            }

            var chests = Nearby(smelter);
            foreach (var conversion in smelter.m_conversion)
            {
                var from = conversion?.m_from;
                if (from?.m_itemData?.m_shared == null)
                {
                    continue;
                }

                string sharedName = from.m_itemData.m_shared.m_name;
                string prefabName = SmelterFuel.PrefabName(from.gameObject.name);
                if (SmelterFuel.IsPremiumWood(prefabName, sharedName))
                {
                    continue;
                }

                if (NearbyContainers.TakeItem(chests, sharedName, 1) <= 0)
                {
                    continue;
                }

                SmelterFuel.AddOneOre(smelter, prefabName);
                user?.Message(MessageHud.MessageType.Center, "$msg_added " + sharedName);
                if (smelter.m_addOreAnimationDuration > 0f)
                {
                    SetAnimationMethod?.Invoke(smelter, new object[] { true });
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Auto-pull from nearby chests while the station runs.
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

            // Avoid chest scans when this station cannot accept anything.
            if (SmelterFuel.FuelRoom(__instance) <= 0 && SmelterFuel.OreRoom(__instance) <= 0)
            {
                return;
            }

            var chests = ChestSupply.Nearby(__instance);
            if (chests.Count == 0)
            {
                return;
            }

            if (__instance.m_fuelItem?.m_itemData?.m_shared != null && SmelterFuel.FuelRoom(__instance) > 0)
            {
                string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
                if (NearbyContainers.TakeItem(chests, fuelName, 1) > 0)
                {
                    SmelterFuel.AddOneFuel(__instance);
                    return;
                }
            }

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
                string prefabName = SmelterFuel.PrefabName(from.gameObject.name);
                if (SmelterFuel.IsPremiumWood(prefabName, sharedName))
                {
                    continue;
                }

                if (NearbyContainers.TakeItem(chests, sharedName, 1) <= 0)
                {
                    continue;
                }

                SmelterFuel.AddOneOre(__instance, prefabName);
                return;
            }
        }
    }

    /// <summary>
    /// Allow E-use when bags are empty but a nearby chest has the needed item.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.CanUseItems))]
    internal static class Smelter_CanUseItems_Patch
    {
        private static bool Prefix(Smelter __instance, Player player, Switch switchRef, ref bool __result)
        {
            if (__instance == null || player == null || switchRef == null)
            {
                return true;
            }

            var chests = ChestSupply.Nearby(__instance);

            if (__instance.m_addOreSwitch != null && switchRef == __instance.m_addOreSwitch)
            {
                if (SmelterFuel.OreRoom(__instance) <= 0)
                {
                    return true;
                }

                if (ChestSupply.InventoryHasOre(__instance, player))
                {
                    return true;
                }

                if (ChestSupply.ChestsHaveOre(__instance, chests))
                {
                    __result = true;
                    return false;
                }

                return true;
            }

            if (__instance.m_addWoodSwitch != null && switchRef == __instance.m_addWoodSwitch)
            {
                if (SmelterFuel.FuelRoom(__instance) <= 0)
                {
                    return true;
                }

                if (ChestSupply.InventoryHasFuel(__instance, player))
                {
                    return true;
                }

                if (ChestSupply.ChestsHaveFuel(__instance, chests))
                {
                    __result = true;
                    return false;
                }

                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Manual fuel (E): if inventory lacks fuel, take one from a nearby chest.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class Smelter_OnAddFuel_Patch
    {
        private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
        {
            if (__instance == null || user == null)
            {
                return true;
            }

            // Holding the wrong item — leave vanilla messaging alone.
            if (item?.m_shared != null &&
                __instance.m_fuelItem?.m_itemData?.m_shared != null &&
                item.m_shared.m_name != __instance.m_fuelItem.m_itemData.m_shared.m_name)
            {
                return true;
            }

            if (ChestSupply.InventoryHasFuel(__instance, user))
            {
                return true;
            }

            if (ChestSupply.TryTakeFuelFromChests(__instance, user))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Manual ore/wood (E): if inventory lacks a cookable item, take one from a nearby chest.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class Smelter_OnAddOre_Patch
    {
        private static bool Prefix(Smelter __instance, Humanoid user, ref bool __result)
        {
            if (__instance == null || user == null)
            {
                return true;
            }

            if (ChestSupply.InventoryHasOre(__instance, user))
            {
                return true;
            }

            if (ChestSupply.TryTakeOreFromChests(__instance, user))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}
