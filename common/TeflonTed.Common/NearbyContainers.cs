using System.Collections.Generic;
using UnityEngine;

namespace TeflonTed.Common
{
    public static class NearbyContainers
    {
        /// <summary>
        /// Find player-accessible containers within <paramref name="radius"/> of <paramref name="center"/>.
        /// </summary>
        public static List<Container> Find(Vector3 center, float radius)
        {
            var result = new List<Container>();
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return result;
            }

            long playerId = player.GetPlayerID();
            var hits = Physics.OverlapSphere(center, Mathf.Max(radius, 0f), LayerMask.GetMask("piece"));
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var container = hit.GetComponentInParent<Container>();
                if (container == null)
                {
                    continue;
                }

                var nview = container.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid())
                {
                    continue;
                }

                if (container.GetInventory() == null)
                {
                    continue;
                }

                if (!container.CheckAccess(playerId))
                {
                    continue;
                }

                if (container.m_checkGuardStone &&
                    !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false, wardCheck: false))
                {
                    continue;
                }

                if (!result.Contains(container))
                {
                    result.Add(container);
                }
            }

            return result;
        }

        public static int CountItem(IEnumerable<Container> containers, string sharedName, int quality = -1)
        {
            int total = 0;
            foreach (var container in containers)
            {
                var inv = container.GetInventory();
                if (inv != null)
                {
                    total += inv.CountItems(sharedName, quality, matchWorldLevel: true);
                }
            }

            return total;
        }

        /// <summary>
        /// Remove up to <paramref name="amount"/> of <paramref name="sharedName"/> from containers.
        /// Returns how many were actually removed.
        /// </summary>
        public static int TakeItem(IEnumerable<Container> containers, string sharedName, int amount, int quality = -1)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int taken = 0;
            int remaining = amount;
            foreach (var container in containers)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var inv = container.GetInventory();
                if (inv == null)
                {
                    continue;
                }

                int have = inv.CountItems(sharedName, quality, matchWorldLevel: true);
                if (have <= 0)
                {
                    continue;
                }

                int remove = Mathf.Min(have, remaining);
                inv.RemoveItem(sharedName, remove, quality, worldLevelBased: true);
                container.Save();
                taken += remove;
                remaining -= remove;
            }

            return taken;
        }

        /// <summary>
        /// Deposit into a chest that already contains this item type.
        /// Returns amount moved from the player stack into the chest.
        /// </summary>
        public static int DepositMatching(Container container, Inventory playerInv, ItemDrop.ItemData item)
        {
            if (container == null || playerInv == null || item?.m_shared == null || item.m_stack <= 0)
            {
                return 0;
            }

            var inv = container.GetInventory();
            if (inv == null)
            {
                return 0;
            }

            if (!inv.HaveItem(item.m_shared.m_name, matchWorldLevel: true))
            {
                return 0;
            }

            int deposited = 0;
            while (item.m_stack > 0)
            {
                var one = item.Clone();
                one.m_stack = 1;
                if (!inv.CanAddItem(one))
                {
                    break;
                }

                if (!inv.AddItem(one))
                {
                    break;
                }

                item.m_stack--;
                deposited++;
            }

            if (deposited > 0)
            {
                if (item.m_stack <= 0)
                {
                    playerInv.RemoveItem(item);
                }

                container.Save();
            }

            return deposited;
        }
    }
}
