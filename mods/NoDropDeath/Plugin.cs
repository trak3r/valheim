using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace TeflonTed.NoDropDeath
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.nodropdeath";
        public const string PluginName = "Teflon Ted's No Drop Death";
        public const string PluginVersion = "1.0.0";

        internal static bool InCreateTombStone;

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Let vanilla create the empty tombstone and death pin; only suppress inventory
    /// transfer / delete while CreateTombStone is running.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
    internal static class Player_CreateTombStone_Patch
    {
        private static void Prefix()
        {
            Plugin.InCreateTombStone = true;
        }

        private static void Finalizer()
        {
            Plugin.InCreateTombStone = false;
        }
    }

    /// <summary>
    /// Vanilla moves unequipped items into the new grave. Skip so the tombstone
    /// stays empty and the player keeps the bag.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
    internal static class Inventory_MoveInventoryToGrave_Patch
    {
        private static bool Prefix()
        {
            return !Plugin.InCreateTombStone;
        }
    }

    /// <summary>
    /// World modifiers can delete unequipped items during CreateTombStone. Skip so
    /// "keep inventory" still holds under those keys.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveUnequipped))]
    internal static class Inventory_RemoveUnequipped_Patch
    {
        private static bool Prefix()
        {
            return !Plugin.InCreateTombStone;
        }
    }
}
