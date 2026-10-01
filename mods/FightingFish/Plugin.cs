using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.FightingFish
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.fightingfish";
        public const string PluginName = "Teflon Ted's Fighting Fish";
        public const string PluginVersion = "1.0.1";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// OnHooked always calls Escape() for the first fight. Skip that one so our
    /// center message does not cover vanilla "$msg_fishing_hooked".
    /// </summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.OnHooked))]
    internal static class Fish_OnHooked_Patch
    {
        private static void Prefix()
        {
            Fish_Escape_Patch.SuppressNextCue = true;
        }

        private static void Postfix()
        {
            // Escape may not run (invalid nview); clear so a later fight still cues.
            Fish_Escape_Patch.SuppressNextCue = false;
        }
    }

    /// <summary>
    /// Vanilla fish escape (fight) has no dedicated HUD/SFX — only subtle thrashing.
    /// When Escape() fires on a hooked fish owned by the local player, flash a center
    /// message and play a water splash so you know to ease off the reel.
    /// </summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.Escape))]
    internal static class Fish_Escape_Patch
    {
        internal static bool SuppressNextCue;

        // Water land splash first; blob plops as fallbacks if renamed.
        private static readonly string[] FightSfxPrefabs =
        {
            "sfx_land_water",
            "sfx_blob_jump",
            "sfx_blob_land",
        };

        private static void Postfix(Fish __instance)
        {
            if (SuppressNextCue)
            {
                SuppressNextCue = false;
                return;
            }

            if (__instance == null || !__instance.IsHooked())
            {
                return;
            }

            FishingFloat fishingFloat = __instance.m_fishingFloat;
            if (fishingFloat == null)
            {
                return;
            }

            Character owner = fishingFloat.GetOwner();
            if (owner == null || owner != Player.m_localPlayer)
            {
                return;
            }

            owner.Message(MessageHud.MessageType.Center, "Stop reeling!");
            PlayFightSfx(__instance.transform.position);
        }

        private static void PlayFightSfx(Vector3 position)
        {
            if (ZNetScene.instance == null)
            {
                return;
            }

            foreach (string name in FightSfxPrefabs)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab == null)
                {
                    continue;
                }

                Object.Instantiate(prefab, position, Quaternion.identity);
                return;
            }
        }
    }
}
