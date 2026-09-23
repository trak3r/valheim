using UnityEngine;

namespace TeflonTed.Common
{
    public static class SmelterFuel
    {
        /// <summary>
        /// How many more fuel units this smelter/kiln/furnace can accept.
        /// </summary>
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

            float current = smelter.m_nview.GetZDO().GetFloat("fuel", 0f);
            return Mathf.Max(0, smelter.m_maxFuel - Mathf.CeilToInt(current));
        }

        public static void AddOneFuel(Smelter smelter)
        {
            if (smelter?.m_nview == null || !smelter.m_nview.IsValid())
            {
                return;
            }

            smelter.m_nview.InvokeRPC("AddFuel");
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
    }
}
