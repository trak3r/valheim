using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.ThinnerMist
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.thinnermist";
        public const string PluginName = "Teflon Ted's Thinner Mist";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }
    }

    /// <summary>
    /// Mistlands ParticleMist only: cut emission (~Foglands daytime) and push the clear
    /// radius out a bit. No day/night veil, weather multipliers, config, or demister hooks.
    /// Inspired by Azumatt Foglands, stripped to this one job.
    /// </summary>
    [HarmonyPatch(typeof(ParticleMist), "Update")]
    internal static class ParticleMist_Update_Patch
    {
        // Foglands daytime emission floor — about 40% of vanilla particle density.
        private const float EmissionScale = 0.4f;

        // Foglands daytime clear-space bump (vanilla minDistance × 1.5 at full day).
        private const float MinDistanceScale = 1.35f;

        // Soft gray fog particles (Foglands defaults).
        private const float FogR = 0.45f;
        private const float FogG = 0.45f;
        private const float FogB = 0.5f;
        private const float FogAlpha = 0.25f;
        private const float ParticleSize = 5f;

        private static bool _stored;
        private static bool _psInitialized;
        private static int _localEmission;
        private static int _localEmissionPerUnit;
        private static float _emissionMax;
        private static float _emissionPerUnit;
        private static float _distantEmissionMax;
        private static float _distantThickness;
        private static float _minDistance;

        private static void Prefix(ParticleMist __instance)
        {
            if (__instance == null || __instance.m_biome != Heightmap.Biome.Mistlands)
            {
                return;
            }

            if (!_stored)
            {
                _localEmission = __instance.m_localEmission;
                _localEmissionPerUnit = __instance.m_localEmissionPerUnit;
                _emissionMax = __instance.m_emissionMax;
                _emissionPerUnit = __instance.m_emissionPerUnit;
                _distantEmissionMax = __instance.m_distantEmissionMax;
                _distantThickness = __instance.m_distantThickness;
                _minDistance = __instance.m_minDistance;
                _stored = true;
            }

            __instance.m_localEmission = Mathf.RoundToInt(_localEmission * EmissionScale);
            __instance.m_localEmissionPerUnit = Mathf.RoundToInt(_localEmissionPerUnit * EmissionScale);
            __instance.m_emissionMax = _emissionMax * EmissionScale;
            __instance.m_emissionPerUnit = _emissionPerUnit * EmissionScale;
            __instance.m_distantEmissionMax = _distantEmissionMax * EmissionScale;
            __instance.m_distantThickness = _distantThickness * EmissionScale;
            __instance.m_minDistance = _minDistance * MinDistanceScale;

            if (__instance.m_ps == null)
            {
                return;
            }

            if (!_psInitialized)
            {
                var mainInit = __instance.m_ps.main;
                mainInit.maxParticles = 2000;
                mainInit.startLifetime = 4f;
                mainInit.simulationSpeed = 0.75f;
                _psInitialized = true;
            }

            var main = __instance.m_ps.main;
            main.startColor = new Color(FogR, FogG, FogB, FogAlpha);
            main.startSize = ParticleSize;
        }
    }
}
