using System.Collections.Generic;
using UnityEngine;

namespace TeflonTed.Common
{
    public static class NearbyItemDrops
    {
        /// <summary>
        /// Find ItemDrop pickups within <paramref name="radius"/> of <paramref name="center"/>.
        /// </summary>
        public static List<ItemDrop> Find(Vector3 center, float radius)
        {
            var result = new List<ItemDrop>();
            var hits = Physics.OverlapSphere(center, Mathf.Max(radius, 0f), LayerMask.GetMask("item"));
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                ItemDrop drop = null;
                if (hit.attachedRigidbody != null)
                {
                    drop = hit.attachedRigidbody.GetComponent<ItemDrop>();
                }

                if (drop == null)
                {
                    drop = hit.GetComponentInParent<ItemDrop>();
                }

                if (drop == null)
                {
                    continue;
                }

                var nview = drop.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid())
                {
                    continue;
                }

                if (drop.m_itemData == null || drop.m_itemData.m_shared == null)
                {
                    continue;
                }

                if (!result.Contains(drop))
                {
                    result.Add(drop);
                }
            }

            return result;
        }

        /// <summary>
        /// Union of drops within <paramref name="radius"/> of any center (e.g. ore + fuel intakes).
        /// </summary>
        public static List<ItemDrop> FindNearAny(IEnumerable<Vector3> centers, float radius)
        {
            var result = new List<ItemDrop>();
            if (centers == null)
            {
                return result;
            }

            foreach (Vector3 center in centers)
            {
                foreach (ItemDrop drop in Find(center, radius))
                {
                    if (!result.Contains(drop))
                    {
                        result.Add(drop);
                    }
                }
            }

            return result;
        }

        public static string PrefabName(Component component)
        {
            return SmelterFuel.PrefabName(component.gameObject.name);
        }
    }
}
