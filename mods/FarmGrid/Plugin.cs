using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TeflonTed.FarmGrid
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.teflonted.valheim.farmgrid";
        public const string PluginName = "Teflon Ted's Farm Grid";
        public const string PluginVersion = "1.1.1";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            FarmGrid.DestroyFarmGrid();
        }
    }

    /// <summary>
    /// Cultivator planting snap modeled on Venture Farm Grid / Sarcen's Farm Grid:
    /// 1) Place a crop → grid activates and pivots freely around that root.
    /// 2) Place a second aligned crop → orientation locks to that row.
    /// 3) Further plants snap to the square field.
    /// No config.
    /// </summary>
    internal static class FarmGrid
    {
        // Vanilla HaveGrowSpace uses OverlapSphere(m_growRadius) against neighbor colliders.
        // Cell = 2×radius is the wiki minimum; a little extra covers collider extent / float slop
        // (flax was browning at ~2×radius with collider-offset positions).
        private const float ExtraSpacing = 0.05f;
        private const int GridSections = 2;
        private const float GridYOffset = 0.1f;
        private static readonly Color GridColor = new Color(0f, 1f, 0f, 0.35f);

        private static readonly string[] PlantLayers =
        {
            "Default", "Default_small", "item", "piece", "piece_nonsolid", "static_solid"
        };

        private static readonly Dictionary<string, float> VanillaDefaults = new Dictionary<string, float>
        {
            { "BlueberryBush", 0.5f },
            { "CloudberryBush", 0.5f },
            { "RaspberryBush", 0.5f },
            { "Pickable_Dandelion", 0.5f },
            { "Pickable_Fiddlehead", 0.5f },
            { "Pickable_Mushroom", 0.5f },
            { "Pickable_Mushroom_blue", 0.5f },
            { "Pickable_Mushroom_yellow", 0.5f },
            { "Pickable_SmokePuff", 0.5f },
            { "Pickable_Thistle", 0.5f }
        };

        private static readonly Dictionary<string, float> PlantSizes =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static int _plantMask;
        private static Vector3 _snapPoint = Vector3.zero;
        private static GameObject[] _farmGrid;
        private static bool _farmGridVisible;
        private static PlantObject _ghost;
        private static Vector3 _ghostPos;
        private static Material _lineMaterial;
        private static bool _freeDraw = true;

        private sealed class PlantObject
        {
            internal Vector3 Position;
            internal readonly float Growth;
            internal readonly int Id;

            internal PlantObject(Vector3 position, float growth, int id)
            {
                Position = position;
                Growth = growth;
                Id = id;
            }
        }

        internal static void SetupPlantCache()
        {
            if (ZNetScene.instance == null)
            {
                return;
            }

            PlantSizes.Clear();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                Plant plant = prefab.GetComponent<Plant>();
                if (plant == null || PlantSizes.ContainsKey(plant.name))
                {
                    continue;
                }

                PlantSizes[plant.name] = plant.m_growRadius;
                if (plant.m_grownPrefabs == null)
                {
                    continue;
                }

                foreach (GameObject grown in plant.m_grownPrefabs)
                {
                    if (grown != null && !PlantSizes.ContainsKey(grown.name))
                    {
                        PlantSizes[grown.name] = plant.m_growRadius;
                    }
                }
            }

            foreach (KeyValuePair<string, float> entry in VanillaDefaults)
            {
                if (!PlantSizes.ContainsKey(entry.Key))
                {
                    PlantSizes[entry.Key] = entry.Value;
                }
            }

            _plantMask = LayerMask.GetMask(PlantLayers);
            if (_plantMask == 0)
            {
                _plantMask = -1;
            }
        }

        private static float Spacing(float growth) => growth * 2f + ExtraSpacing;

        private static float SearchRadius(PlantObject plant) =>
            Spacing(plant.Growth) * (GridSections + 1);

        private static string PrefabName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int cut = name.IndexOfAny(new[] { '(', ' ' });
            return cut >= 0 ? name.Substring(0, cut) : name;
        }

        private static bool TryGetSize(string prefab, out float size)
        {
            string key = PrefabName(prefab);
            if (PlantSizes.TryGetValue(key, out size))
            {
                return true;
            }

            size = 0f;
            return false;
        }

        private static bool TryGetPlant(Collider collider, out PlantObject plant)
        {
            plant = null;
            if (collider == null)
            {
                return false;
            }

            // Prefer the Plant component: HaveGrowSpace checks from Plant.transform.position,
            // not child collider centers (which can sit off-axis and shrink snap spacing).
            Plant live = collider.GetComponentInParent<Plant>() ??
                         collider.GetComponentInChildren<Plant>(true);

            string prefab;
            Vector3 position;
            float size;
            int id;

            if (live != null)
            {
                prefab = PrefabName(live.gameObject.name);
                position = live.transform.position;
                id = live.GetInstanceID();
                if (!TryGetSize(prefab, out size))
                {
                    size = Mathf.Max(0f, live.m_growRadius);
                }
            }
            else
            {
                Transform root = collider.transform.root != null
                    ? collider.transform.root
                    : collider.transform;
                prefab = PrefabName(root.name);
                if (!TryGetSize(prefab, out size))
                {
                    return false;
                }

                position = root.position;
                id = root.GetInstanceID();
            }

            plant = new PlantObject(position, size, id);
            return true;
        }

        private static List<PlantObject> FindNearby(Vector3 position, float radius)
        {
            Collider[] buffer = Piece.s_pieceColliders;
            int count = Physics.OverlapSphereNonAlloc(position, radius, buffer, _plantMask);
            var ordered = buffer.Take(count)
                .OrderBy(c => (c.transform.position - (_ghost?.Position ?? position)).sqrMagnitude);

            var plants = new List<PlantObject>();
            var seen = new HashSet<int>();
            foreach (Collider hit in ordered)
            {
                if (!TryGetPlant(hit, out PlantObject plant))
                {
                    continue;
                }

                // Skip the placement ghost itself.
                if (_ghost != null &&
                    (plant.Position - _ghost.Position).sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                if (!seen.Add(plant.Id))
                {
                    continue;
                }

                plants.Add(plant);
                if (plants.Count >= 24)
                {
                    break;
                }
            }

            return plants;
        }

        private static bool Overlaps(Vector3 pos, List<PlantObject> others, float growth)
        {
            for (int i = 0; i < others.Count; i++)
            {
                if ((pos - others[i].Position).magnitude <= growth + others[i].Growth)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GroundY(Vector3 pos) =>
            ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(pos) : pos.y;

        /// <summary>
        /// Free pivot around a single root: direction from plant toward the ghost.
        /// </summary>
        private static void DrawFreeGrid(PlantObject root)
        {
            Vector3 gridDir = _ghost.Position - root.Position;
            gridDir.y = 0f;
            if (gridDir.sqrMagnitude < 0.0001f)
            {
                gridDir = Vector3.forward;
            }
            else
            {
                gridDir.Normalize();
            }

            _freeDraw = true;
            float cell = Spacing(Mathf.Max(_ghost.Growth, root.Growth));
            _snapPoint = root.Position + gridDir * cell;
            _snapPoint.y = GroundY(_snapPoint);

            Player.m_localPlayer.m_placementGhost.transform.position = _snapPoint;
            DrawLines(_snapPoint, gridDir, cell);
        }

        /// <summary>
        /// Locked orientation from two plants whose spacing matches the grow grid.
        /// </summary>
        private static void DrawFixedGrid(List<PlantObject> others)
        {
            if (!TryGridDir(others, out Vector3 gridDir, out PlantObject root) || root == null)
            {
                DrawFreeGrid(others[0]);
                return;
            }

            _freeDraw = false;
            float cell = Spacing(Mathf.Max(_ghost.Growth, root.Growth));
            List<Vector3> candidates = GridCandidates(_ghost.Position, root.Position, gridDir, cell);

            _snapPoint = candidates[0];
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!Overlaps(candidates[i], others, _ghost.Growth))
                {
                    _snapPoint = candidates[i];
                    break;
                }
            }

            _snapPoint.y = GroundY(_snapPoint);
            Player.m_localPlayer.m_placementGhost.transform.position = _snapPoint;
            DrawLines(_snapPoint, gridDir, cell);
        }

        private static List<Vector3> GridCandidates(
            Vector3 ghostPos, Vector3 rootPos, Vector3 gridDir, float cell)
        {
            var grid = new List<Vector3>();
            Vector3 across = new Vector3(gridDir.z, 0f, -gridDir.x);
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (x == 0 && z == 0)
                    {
                        continue;
                    }

                    grid.Add(rootPos + gridDir * (x * cell) + across * (z * cell));
                }
            }

            return grid.OrderBy(p => (p - ghostPos).sqrMagnitude).ToList();
        }

        private static bool TryGridDir(
            List<PlantObject> plants, out Vector3 gridDir, out PlantObject root)
        {
            root = plants[0];
            gridDir = Vector3.zero;

            int limit = Mathf.Min(2, plants.Count);
            for (int i = 0; i < limit; i++)
            {
                root = plants[i];
                for (int j = 1; j < plants.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    Vector3 delta = root.Position - plants[j].Position;
                    delta.y = 0f;
                    float required = Spacing(Mathf.Max(
                        Mathf.Max(root.Growth, plants[j].Growth),
                        _ghost.Growth));
                    float spacing = delta.magnitude;
                    float diff = spacing - required;
                    if (diff > -0.01f && diff < 0.01f * required)
                    {
                        gridDir = delta.normalized;
                        return true;
                    }
                }
            }

            root = null;
            return false;
        }

        internal static bool ApplySnap()
        {
            if (_ghost == null || Player.m_localPlayer == null)
            {
                HideFarmGrid();
                ResetCache();
                return false;
            }

            GameObject placementGhost = Player.m_localPlayer.m_placementGhost;
            if (placementGhost == null)
            {
                HideFarmGrid();
                ResetCache();
                return false;
            }

            // Skip tiny cursor jitter (Venture Farm Grid).
            float moved = (_ghostPos - placementGhost.transform.position).sqrMagnitude;
            if (((!_farmGridVisible || _freeDraw) && moved < 0.0001f) ||
                (_farmGridVisible && !_freeDraw && moved < 0.02f))
            {
                if (_snapPoint != Vector3.zero)
                {
                    placementGhost.transform.position = _snapPoint;
                }

                return true;
            }

            _snapPoint = Vector3.zero;
            _ghost.Position = placementGhost.transform.position;
            _ghostPos = _ghost.Position;

            List<PlantObject> others = FindNearby(_ghost.Position, SearchRadius(_ghost));
            if (others.Count < 1)
            {
                HideFarmGrid();
                return true;
            }

            if (others.Count == 1)
            {
                DrawFreeGrid(others[0]);
            }
            else
            {
                DrawFixedGrid(others);
            }

            return true;
        }

        internal static void SetGhostPlant()
        {
            Player player = Player.m_localPlayer;
            if (player?.m_placementGhost == null)
            {
                return;
            }

            Collider col = player.m_placementGhost.GetComponentInChildren<Collider>();
            if (TryGetPlant(col, out PlantObject plant))
            {
                // Ghost snap math must use the placement root, not a child collider.
                plant.Position = player.m_placementGhost.transform.position;
                _ghost = plant;
                _ghostPos = plant.Position;
            }
            else
            {
                // Placement ghosts sometimes lack a collider early — use Plant component.
                Plant live = player.m_placementGhost.GetComponent<Plant>() ??
                             player.m_placementGhost.GetComponentInChildren<Plant>(true);
                if (live != null)
                {
                    float size = Mathf.Max(0f, live.m_growRadius);
                    string prefab = PrefabName(live.gameObject.name);
                    if (TryGetSize(prefab, out float cached))
                    {
                        size = cached;
                    }

                    _ghost = new PlantObject(
                        player.m_placementGhost.transform.position,
                        size,
                        live.GetInstanceID());
                    _ghostPos = _ghost.Position;
                }
            }
        }

        internal static void ResetCache()
        {
            _ghost = null;
            _snapPoint = Vector3.zero;
            _freeDraw = true;
        }

        private static void DrawLines(Vector3 pos, Vector3 gridDir, float cell)
        {
            EnsureFarmGrid();
            if (_farmGrid == null || _farmGrid[0] == null)
            {
                return;
            }

            Vector3 across = new Vector3(gridDir.z, 0f, -gridDir.x);
            for (int i = -GridSections; i <= GridSections; i++)
            {
                DrawSegment(_farmGrid[i + GridSections], pos + gridDir * (i * cell), across, cell);
                DrawSegment(
                    _farmGrid[i + GridSections + (GridSections * 2 + 1)],
                    pos + across * (i * cell),
                    gridDir,
                    cell);
            }

            _farmGridVisible = true;
        }

        private static void DrawSegment(GameObject lineObj, Vector3 position, Vector3 along, float cell)
        {
            LineRenderer line = lineObj.GetComponent<LineRenderer>();
            line.widthMultiplier = 0.015f;
            line.enabled = true;
            for (int i = -GridSections; i <= GridSections; i++)
            {
                Vector3 p = position + along * (i * cell);
                p.y = GroundY(p) + GridYOffset;
                line.SetPosition(i + GridSections, p);
            }
        }

        private static void EnsureFarmGrid()
        {
            int needed = (GridSections * 2 + 1) * 2;
            if (_farmGrid != null && _farmGrid.Length == needed && _farmGrid[0] != null)
            {
                return;
            }

            DestroyFarmGrid();
            if (_lineMaterial == null)
            {
                _lineMaterial = Resources.FindObjectsOfTypeAll<Material>()
                    .FirstOrDefault(m => m != null && m.name == "Default-Line");
                if (_lineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        _lineMaterial = new Material(shader);
                    }
                }
            }

            _farmGrid = new GameObject[needed];
            for (int i = 0; i < needed; i++)
            {
                GameObject go = new GameObject("TeflonTed_FarmGridLine");
                LineRenderer line = go.AddComponent<LineRenderer>();
                if (_lineMaterial != null)
                {
                    line.sharedMaterial = _lineMaterial;
                }

                line.useWorldSpace = true;
                line.startColor = GridColor;
                line.endColor = GridColor;
                line.positionCount = GridSections * 2 + 1;
                line.enabled = false;
                _farmGrid[i] = go;
            }
        }

        private static void HideFarmGrid()
        {
            if (!_farmGridVisible || _farmGrid == null || _farmGrid[0] == null)
            {
                return;
            }

            for (int i = 0; i < _farmGrid.Length; i++)
            {
                if (_farmGrid[i] != null)
                {
                    _farmGrid[i].GetComponent<LineRenderer>().enabled = false;
                }
            }

            _farmGridVisible = false;
            _freeDraw = true;
        }

        internal static void DestroyFarmGrid()
        {
            if (_farmGrid == null)
            {
                return;
            }

            for (int i = 0; i < _farmGrid.Length; i++)
            {
                if (_farmGrid[i] != null)
                {
                    UnityEngine.Object.Destroy(_farmGrid[i]);
                }
            }

            _farmGrid = null;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPriority(Priority.Last)]
        private static class ZNetScene_Awake_Patch
        {
            private static void Postfix() => SetupPlantCache();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        private static class Player_SetupPlacementGhost_Patch
        {
            private static void Postfix()
            {
                ResetCache();
                SetGhostPlant();
            }
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class Player_UpdatePlacementGhost_Patch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == null || __instance != Player.m_localPlayer)
                {
                    return;
                }

                ItemDrop.ItemData right = __instance.GetRightItem();
                if (right?.m_shared == null || right.m_shared.m_name != "$item_cultivator")
                {
                    HideFarmGrid();
                    ResetCache();
                    return;
                }

                if (_ghost == null)
                {
                    SetGhostPlant();
                }

                if (_ghost == null)
                {
                    HideFarmGrid();
                    return;
                }

                ApplySnap();
            }
        }
    }
}
