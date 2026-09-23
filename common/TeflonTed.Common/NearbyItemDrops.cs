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
                if (hit == null || hit.attachedRigidbody == null)
                {
                    continue;
                }

                var drop = hit.attachedRigidbody.GetComponent<ItemDrop>();
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

        public static string PrefabName(Component component)
        {
            string name = component.gameObject.name;
            int cut = name.IndexOfAny(new[] { '(', ' ' });
            return cut >= 0 ? name.Substring(0, cut) : name;
        }
    }
}
