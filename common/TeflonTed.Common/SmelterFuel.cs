using HarmonyLib;
using UnityEngine;

namespace TeflonTed.Common
{
    /// <summary>
    /// Helpers for Smelter-based stations (kiln / smelter / blast furnace / windmill).
    /// Kilns take wood as queued "ore"; smelters take coal as fuel and metal as ore;
    /// windmills take barley (etc.) as ore.
    /// </summary>
    public static class SmelterFuel
    {
        private static readonly AccessTools.FieldRef<Smelter, int> MaxFuel =
            AccessTools.FieldRefAccess<Smelter, int>("m_maxFuel");

        private static readonly AccessTools.FieldRef<Smelter, int> MaxOre =
            AccessTools.FieldRefAccess<Smelter, int>("m_maxOre");

        private static readonly System.Reflection.MethodInfo GetFuelMethod =
            AccessTools.Method(typeof(Smelter), "GetFuel");

        private static readonly System.Reflection.MethodInfo GetQueueSizeMethod =
            AccessTools.Method(typeof(Smelter), "GetQueueSize");

        private static readonly System.Reflection.MethodInfo IsItemAllowedMethod =
            AccessTools.Method(typeof(Smelter), "IsItemAllowed", new[] { typeof(string) });

        /// <summary>
        /// World point where players dump fuel/ore — better than Smelter.transform for tall
        /// pieces like the windmill, whose component origin can sit far above ground chests.
        /// </summary>
        public static Vector3 IntakePosition(Smelter smelter)
        {
            if (smelter == null)
            {
                return Vector3.zero;
            }

            if (smelter.m_addOreSwitch != null)
            {
                return smelter.m_addOreSwitch.transform.position;
            }

            if (smelter.m_addWoodSwitch != null)
            {
                return smelter.m_addWoodSwitch.transform.position;
            }

            if (smelter.m_windmill != null)
            {
                return smelter.m_windmill.transform.position;
            }

            return smelter.transform.position;
        }

        public static int FuelRoom(Smelter smelter)
        {
            if (smelter == null || smelter.m_nview == null || !smelter.m_nview.IsValid())
            {
                return 0;
            }

            if (smelter.m_fuelItem == null)
            {
                return 0;
            }

            float current = GetFuelMethod != null
                ? (float)GetFuelMethod.Invoke(smelter, null)
                : smelter.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);

            return Mathf.Max(0, MaxFuel(smelter) - Mathf.CeilToInt(current));
        }

        public static int OreRoom(Smelter smelter)
        {
            if (smelter == null || smelter.m_nview == null || !smelter.m_nview.IsValid())
            {
                return 0;
            }

            int queued = GetQueueSizeMethod != null
                ? (int)GetQueueSizeMethod.Invoke(smelter, null)
                : 0;

            return Mathf.Max(0, MaxOre(smelter) - queued);
        }

        public static void AddOneFuel(Smelter smelter)
        {
            if (smelter?.m_nview == null || !smelter.m_nview.IsValid())
            {
                return;
            }

            smelter.m_nview.InvokeRPC("RPC_AddFuel");
        }

        public static void AddOneOre(Smelter smelter, string prefabName, bool cheated = false)
        {
            if (smelter?.m_nview == null || !smelter.m_nview.IsValid() || string.IsNullOrEmpty(prefabName))
            {
                return;
            }

            smelter.m_nview.InvokeRPC("RPC_AddOre", prefabName, cheated);
        }

        public static bool IsFuelItem(Smelter smelter, string sharedName)
        {
            return smelter?.m_fuelItem != null &&
                   smelter.m_fuelItem.m_itemData != null &&
                   smelter.m_fuelItem.m_itemData.m_shared != null &&
                   smelter.m_fuelItem.m_itemData.m_shared.m_name == sharedName;
        }

        public static bool IsFuelItem(Smelter smelter, ItemDrop.ItemData item)
        {
            return item?.m_shared != null && IsFuelItem(smelter, item.m_shared.m_name);
        }

        public static bool IsOreItem(Smelter smelter, ItemDrop.ItemData item)
        {
            string prefab = PrefabName(item);
            if (smelter == null || string.IsNullOrEmpty(prefab) || IsItemAllowedMethod == null)
            {
                return false;
            }

            return (bool)IsItemAllowedMethod.Invoke(smelter, new object[] { prefab });
        }

        public static string PrefabName(ItemDrop.ItemData item)
        {
            if (item?.m_dropPrefab == null)
            {
                return null;
            }

            // Instance names are like "Barley(Clone)"; IsItemAllowed wants "Barley".
            string name = item.m_dropPrefab.name;
            int cut = name.IndexOfAny(new[] { '(', ' ' });
            return cut >= 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>
        /// Prefer fuel when the station uses it; otherwise accept conversion ("ore") inputs
        /// such as wood for a charcoal kiln or barley for a windmill.
        /// </summary>
        public static bool TryAcceptItem(Smelter smelter, ItemDrop.ItemData item)
        {
            if (smelter == null || item?.m_shared == null)
            {
                return false;
            }

            if (IsFuelItem(smelter, item) && FuelRoom(smelter) > 0)
            {
                AddOneFuel(smelter);
                return true;
            }

            if (IsOreItem(smelter, item) && OreRoom(smelter) > 0)
            {
                string prefab = PrefabName(item);
                if (string.IsNullOrEmpty(prefab))
                {
                    return false;
                }

                AddOneOre(smelter, prefab, item.m_cheated);
                return true;
            }

            return false;
        }
    }
}
