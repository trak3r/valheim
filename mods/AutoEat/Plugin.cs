using System.Collections.Generic;
using BepInEx;
using HarmonyLib;

namespace TeflonTed.AutoEat
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.autoeat";
        public const string PluginName = "Teflon Ted's Auto Eat";
        public const string PluginVersion = "1.0.1";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// When a food buff expires naturally this food-tick, re-eat the same food from inventory if available.
    /// Prefers the original stack (often on the hotbar) then any matching stack including hotbar row y==0.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class Player_UpdateFood_Patch
    {
        private const float FoodTickInterval = 1f;

        private static readonly List<PendingEat> Expiring = new List<PendingEat>();
        private static int Depth;

        private struct PendingEat
        {
            public string SharedName;
            public ItemDrop.ItemData OriginalStack;
        }

        private static void Prefix(Player __instance, float dt, bool forceUpdate)
        {
            // Ignore nested UpdateFood from EatFood while we auto-eat.
            if (Depth > 0)
            {
                return;
            }

            Expiring.Clear();

            if (__instance == null || __instance != Player.m_localPlayer || __instance.IsDead())
            {
                return;
            }

            // Vanilla only burns food when the 1s food timer elapses (or forceUpdate).
            float nextTimer = __instance.m_foodUpdateTimer + dt * Game.m_foodRate;
            bool willTick = forceUpdate || nextTimer >= FoodTickInterval;
            if (!willTick)
            {
                return;
            }

            var foods = __instance.GetFoods();
            if (foods == null)
            {
                return;
            }

            foreach (Player.Food food in foods)
            {
                if (food?.m_item?.m_shared == null)
                {
                    continue;
                }

                // This tick subtracts FoodTickInterval from m_time; <= 0 removes the buff.
                if (food.m_time <= FoodTickInterval)
                {
                    // EatFood stores a live reference to the inventory stack that was eaten
                    // (commonly the hotbar). Keep it so we can consume that stack first.
                    Expiring.Add(new PendingEat
                    {
                        SharedName = food.m_item.m_shared.m_name,
                        OriginalStack = food.m_item,
                    });
                }
            }
        }

        private static void Postfix(Player __instance)
        {
            if (Depth > 0 || Expiring.Count == 0)
            {
                return;
            }

            if (__instance == null || __instance != Player.m_localPlayer || __instance.IsDead())
            {
                Expiring.Clear();
                return;
            }

            var inventory = __instance.GetInventory();
            if (inventory == null)
            {
                Expiring.Clear();
                return;
            }

            Depth++;
            try
            {
                foreach (PendingEat pending in Expiring)
                {
                    var item = FindFoodStack(inventory, pending);
                    if (item == null)
                    {
                        continue;
                    }

                    if (!__instance.CanEat(item, false))
                    {
                        continue;
                    }

                    __instance.ConsumeItem(inventory, item, false);
                }
            }
            finally
            {
                Depth--;
                Expiring.Clear();
            }
        }

        /// <summary>
        /// Resolve a stack to eat: original buff stack if still in inventory, else any match
        /// (hotbar row y==0 preferred, then the rest of the bag).
        /// </summary>
        private static ItemDrop.ItemData FindFoodStack(Inventory inventory, PendingEat pending)
        {
            if (pending.OriginalStack != null
                && pending.OriginalStack.m_stack > 0
                && inventory.ContainsItem(pending.OriginalStack))
            {
                return pending.OriginalStack;
            }

            if (string.IsNullOrEmpty(pending.SharedName))
            {
                return null;
            }

            ItemDrop.ItemData bagMatch = null;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared == null || item.m_stack <= 0)
                {
                    continue;
                }

                if (item.m_shared.m_name != pending.SharedName)
                {
                    continue;
                }

                // Hotbar is inventory row y == 0 — prefer it (where players usually keep food).
                if (item.m_gridPos.y == 0)
                {
                    return item;
                }

                if (bagMatch == null)
                {
                    bagMatch = item;
                }
            }

            return bagMatch;
        }
    }
}
