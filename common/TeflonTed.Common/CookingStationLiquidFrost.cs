using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.Common
{
    /// <summary>
    /// Liquid Frost fuel for CookingStation pieces (Frost Foundry). Deliberately
    /// excludes wood-burning ovens / cooking stations.
    /// </summary>
    public static class CookingStationLiquidFrost
    {
        public const string FrozenFuelSharedName = "$item_frozenfuel";

        private static readonly AccessTools.FieldRef<CookingStation, int> MaxFuel =
            AccessTools.FieldRefAccess<CookingStation, int>("m_maxFuel");

        private static readonly System.Reflection.MethodInfo GetFuelMethod =
            AccessTools.Method(typeof(CookingStation), "GetFuel");

        public static bool UsesLiquidFrostFuel(CookingStation station)
        {
            if (station == null || !station.m_useFuel)
            {
                return false;
            }

            string shared = station.m_fuelItem?.m_itemData?.m_shared?.m_name;
            return shared == FrozenFuelSharedName;
        }

        public static List<Vector3> IntakePositions(CookingStation station)
        {
            var points = new List<Vector3>(2);
            if (station == null)
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

            Add(station.m_addFuelSwitch != null ? station.m_addFuelSwitch.transform : null);
            Add(station.transform);
            return points;
        }

        public static int FuelRoom(CookingStation station)
        {
            if (station?.m_nview == null || !station.m_nview.IsValid() || !UsesLiquidFrostFuel(station))
            {
                return 0;
            }

            float current = GetFuelMethod != null
                ? (float)GetFuelMethod.Invoke(station, null)
                : station.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);

            return Mathf.Max(0, MaxFuel(station) - Mathf.CeilToInt(current));
        }

        public static void AddOneFuel(CookingStation station)
        {
            if (station?.m_nview == null || !station.m_nview.IsValid())
            {
                return;
            }

            station.m_nview.InvokeRPC("RPC_AddFuel");
        }
    }
}
