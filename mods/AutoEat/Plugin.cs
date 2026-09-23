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
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// When a food buff expires naturally this food-tick, re-eat the same food from inventory if available.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class Player_UpdateFood_Patch
    {
        private const float FoodTickInterval = 1f;

        private static readonly List<string> Expiring = new List<string>();
        private static int Depth;

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
                    Expiring.Add(food.m_item.m_shared.m_name);
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
                foreach (string sharedName in Expiring)
                {
                    var item = inventory.GetItem(sharedName, -1, isPrefabName: false);
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
    }
}
