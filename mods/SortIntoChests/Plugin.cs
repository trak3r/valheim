using System.Collections.Generic;
using BepInEx;
using TeflonTed.Common;
using UnityEngine;

namespace TeflonTed.SortIntoChests
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.sortintochests";
        public const string PluginName = "Teflon Ted's Sort Into Chests";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.BackQuote))
            {
                return;
            }

            if (ShouldIgnoreInput())
            {
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            int moved = SortIntoNearbyChests(player);
            if (moved > 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, $"Sorted {moved} item(s) into chests");
            }
        }

        private static bool ShouldIgnoreInput()
        {
            if (Console.IsVisible() || TextInput.IsVisible() || Minimap.IsOpen())
            {
                return true;
            }

            if (Chat.instance != null && Chat.instance.HasFocus())
            {
                return true;
            }

            if (Menu.IsVisible())
            {
                return true;
            }

            return false;
        }

        private static int SortIntoNearbyChests(Player player)
        {
            var inv = player.GetInventory();
            if (inv == null)
            {
                return 0;
            }

            var chests = NearbyContainers.Find(player.transform.position, Radii.Workstation);
            if (chests.Count == 0)
            {
                return 0;
            }

            var items = new List<ItemDrop.ItemData>(inv.GetAllItems());
            int deposited = 0;

            foreach (var item in items)
            {
                if (item == null || item.m_shared == null || item.m_stack <= 0)
                {
                    continue;
                }

                if (item.m_equipped)
                {
                    continue;
                }

                // Hotbar is inventory row y == 0.
                if (item.m_gridPos.y == 0)
                {
                    continue;
                }

                foreach (var chest in chests)
                {
                    if (item.m_stack <= 0)
                    {
                        break;
                    }

                    deposited += NearbyContainers.DepositMatching(chest, inv, item);
                }
            }

            return deposited;
        }
    }
}
