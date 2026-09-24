using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.Common
{
    /// <summary>
    /// Helpers for Smelter-based stations (kiln / smelter / blast furnace / windmill /
    /// spinning wheel). Kilns take wood as queued "ore"; smelters take coal as fuel and
    /// metal as ore; windmills take barley; spinning wheels take flax.
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
        /// World points where players dump fuel/ore. Blast furnaces put ore and coal on
        /// opposite sides — search every intake, not only the first switch. Also include
        /// the piece origin so compact stations (spinning wheel) still see adjacent chests
        /// when the switch transform sits oddly. Tall pieces like the windmill still prefer
        /// the ground-level intake switch over the elevated origin alone.
        /// </summary>
        public static List<Vector3> IntakePositions(Smelter smelter)
        {
            var points = new List<Vector3>(4);
            if (smelter == null)
            {
                return points;
            }

            void Add(Transform t)
            {
                if (t == null)
                {
                    return;
                }

                Vector3 p = t.position;
                for (int i = 0; i < points.Count; i++)
                {
                    if ((points[i] - p).sqrMagnitude < 0.01f)
                    {
                        return;
                    }
                }

                points.Add(p);
            }

            Add(smelter.m_addOreSwitch != null ? smelter.m_addOreSwitch.transform : null);
            Add(smelter.m_addWoodSwitch != null ? smelter.m_addWoodSwitch.transform : null);

            if (smelter.m_windmill != null)
            {
                Add(smelter.m_windmill.transform);
            }

            // Always include piece origin as a fallback search center.
            Add(smelter.transform);

            return points;
        }

        /// <summary>Primary intake (first switch / fallback). Prefer <see cref="IntakePositions"/>.</summary>
        public static Vector3 IntakePosition(Smelter smelter)
        {
            var points = IntakePositions(smelter);
            return points.Count > 0 ? points[0] : Vector3.zero;
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

            return PrefabName(item.m_dropPrefab.name);
        }

        /// <summary>
        /// Instance names are like "Flax(Clone)"; IsItemAllowed wants "Flax".
        /// Same rules as vanilla ItemDrop.GetPrefabName.
        /// </summary>
        public static string PrefabName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            int cut = name.IndexOfAny(new[] { '(', ' ' });
            return cut >= 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>
        /// Prefer fuel when the station uses it; otherwise accept conversion ("ore") inputs
        /// such as wood for a charcoal kiln, barley for a windmill, or flax for a spinning wheel.
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
