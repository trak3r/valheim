using System.Collections.Generic;
using System.Reflection;
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
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// When a food buff's timer hits zero this tick, re-eat the same food from inventory if available.
    /// Manual food removal and death are ignored — only natural expiry.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class Player_UpdateFood_Patch
    {
        private static readonly List<string> Expiring = new List<string>();

        private static void Prefix(Player __instance, float dt)
        {
            Expiring.Clear();

            if (__instance == null || __instance != Player.m_localPlayer || __instance.IsDead())
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

                // This food will be removed by UpdateFood this frame.
                if (food.m_time <= dt)
                {
                    Expiring.Add(food.m_item.m_shared.m_name);
                }
            }
        }

        private static void Postfix(Player __instance)
        {
            if (Expiring.Count == 0 || __instance == null || __instance != Player.m_localPlayer || __instance.IsDead())
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

            foreach (string sharedName in Expiring)
            {
                var item = inventory.GetItem(sharedName, -1, isPrefabName: false);
                if (item == null)
                {
                    continue;
                }

                // Same path as manually eating from the inventory.
                __instance.ConsumeItem(inventory, item, false);
            }

            Expiring.Clear();
        }
    }
}
