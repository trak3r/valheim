using System.Collections.Generic;
using UnityEngine;

namespace TeflonTed.Common
{
    public static class NearbyContainers
    {
        /// <summary>
        /// Find player-accessible containers within <paramref name="radius"/> of <paramref name="center"/>.
        /// Includes Obliterator (Incinerator) inventories, which are easy to miss via physics alone.
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
            float radiusSq = radius * radius;

            void TryAdd(Container container)
            {
                if (container == null || result.Contains(container))
                {
                    return;
                }

                if ((container.transform.position - center).sqrMagnitude > radiusSq &&
                    (container.transform.root.position - center).sqrMagnitude > radiusSq)
                {
                    return;
                }

                var nview = container.GetComponent<ZNetView>() ??
                            container.GetComponentInParent<ZNetView>();
                if (nview == null || !nview.IsValid())
                {
                    return;
                }

                if (container.GetInventory() == null)
                {
                    return;
                }

                if (!container.CheckAccess(playerId))
                {
                    return;
                }

                if (container.m_checkGuardStone &&
                    !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false, wardCheck: false))
                {
                    return;
                }

                result.Add(container);
            }

            // Normal chests / pieces via physics.
            var hits = Physics.OverlapSphere(center, Mathf.Max(radius, 0f), LayerMask.GetMask("piece"));
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var container = hit.GetComponentInParent<Container>() ??
                                hit.GetComponentInChildren<Container>();
                TryAdd(container);
            }

            // Obliterator: Incinerator holds a Container reference that OverlapSphere often misses.
            foreach (var incinerator in Object.FindObjectsOfType<Incinerator>())
            {
                if (incinerator == null)
                {
                    continue;
                }

                TryAdd(incinerator.m_container);
                TryAdd(incinerator.GetComponent<Container>());
                TryAdd(incinerator.GetComponentInChildren<Container>());
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
