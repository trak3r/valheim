using System.Collections.Generic;
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
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            FarmGridSnap.DestroyGrid();
        }
    }

    /// <summary>
    /// Snaps cultivator plant placement to the eight neighbour cells around the nearest
    /// planted crop, spaced at 2× grow radius so plants fit optimally. Draws a green
    /// grid while placing. No config — mimic of FarmGridRemake's core snap only.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class FarmGridSnap
    {
        private const float SpacingSlop = 0.01f;
        private const int GridSections = 2;
        private const float GridYOffset = 0.2f;
        private static readonly Color GridColor = new Color(0f, 1f, 0f, 0.35f);

        private static readonly Collider[] OverlapBuffer = new Collider[512];

        private static GameObject[] _lines;
        private static Material _lineMaterial;
        private static bool _snapValid;
        private static Vector3 _snapAnchor;
        private static Vector3 _snapRaw;
        private static Vector3 _snapPoint;
        private static Vector3 _gridDir = Vector3.forward;
        private static float _gridSize;
        private static Vector3 _visualOrigin;
        private static bool _visualOriginValid;

        private static void Postfix(Player __instance)
        {
            if (__instance == null || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (!IsCultivatorEquipped(__instance))
            {
                HideGrid();
                ResetSnap();
                return;
            }

            GameObject ghost = __instance.m_placementGhost;
            if (ghost == null || !ghost.activeInHierarchy)
            {
                HideGrid();
                ResetSnap();
                return;
            }

            Plant ghostPlant = ghost.GetComponent<Plant>() ?? ghost.GetComponentInChildren<Plant>(true);
            if (ghostPlant == null)
            {
                HideGrid();
                ResetSnap();
                return;
            }

            Vector3 raw = ghost.transform.position;
            float growth = Mathf.Max(0f, ghostPlant.m_growRadius);
            float gridSize = Mathf.Max(0.05f, growth * 2f + SpacingSlop);
            float searchRadius = Mathf.Max(2.5f, gridSize * 2.25f);

            List<PlantAnchor> anchors = FindNearbyPlants(raw, searchRadius, ghost);
            if (anchors.Count == 0)
            {
                HideGrid();
                ResetSnap();
                return;
            }

            anchors.Sort((a, b) => HorizontalSqr(a.Position, raw).CompareTo(HorizontalSqr(b.Position, raw)));

            // Stick to the previous anchor while the cursor stays near the last snap cell,
            // so planting one crop does not immediately re-anchor onto it.
            PlantAnchor anchor = anchors[0];
            float stickRadius = Mathf.Max(0.15f, (_snapValid && _gridSize > 0.001f ? _gridSize : gridSize) * 0.45f);
            bool cursorNearLast = _snapValid && HorizontalSqr(_snapRaw, raw) <= stickRadius * stickRadius;
            if (_snapValid && cursorNearLast)
            {
                for (int i = 0; i < anchors.Count; i++)
                {
                    if (HorizontalSqr(anchors[i].Position, _snapAnchor) <= 0.0025f)
                    {
                        anchor = anchors[i];
                        break;
                    }
                }
            }

            Vector3 gridDir = Flatten(ghost.transform.forward);
            if (gridDir.sqrMagnitude < 0.0001f)
            {
                gridDir = Flatten(ghost.transform.right);
            }

            if (gridDir.sqrMagnitude < 0.0001f)
            {
                gridDir = Vector3.forward;
            }
            else
            {
                gridDir.Normalize();
            }

            // Keep orientation stable unless the player rotated the ghost while stuck on a cell.
            bool rotated = _snapValid && cursorNearLast &&
                           Mathf.Abs(Mathf.DeltaAngle(
                               Quaternion.LookRotation(_gridDir).eulerAngles.y,
                               ghost.transform.eulerAngles.y)) > 0.1f;
            if (_snapValid && !rotated && cursorNearLast)
            {
                gridDir = _gridDir;
            }

            Vector3 perpendicular = new Vector3(gridDir.z, 0f, -gridDir.x);
            bool needResnap = !_snapValid ||
                              HorizontalSqr(_snapAnchor, anchor.Position) > 0.0025f ||
                              Mathf.Abs(_gridSize - gridSize) > 0.001f ||
                              (!cursorNearLast) ||
                              HasOverlap(_snapPoint, growth, ghost);

            if (needResnap)
            {
                Vector3 snap = FindClosestNeighbour(
                    anchor.Position, raw, gridDir, perpendicular, gridSize, growth, ghost);
                if (snap == Vector3.zero)
                {
                    HideGrid();
                    ResetSnap();
                    return;
                }

                _snapPoint = snap;
                _snapValid = true;
                _snapAnchor = anchor.Position;
                _snapRaw = raw;
                _gridSize = gridSize;
                _gridDir = gridDir;
                if (!_visualOriginValid || HorizontalSqr(_visualOrigin, snap) > gridSize * gridSize * 4f)
                {
                    _visualOrigin = snap;
                    _visualOriginValid = true;
                }
            }
            else
            {
                _snapRaw = raw;
                _gridDir = gridDir;
            }

            ghost.transform.position = _snapPoint;
            DrawGrid(_visualOriginValid ? _visualOrigin : _snapPoint, _gridDir, _gridSize);
        }

        private static Vector3 FindClosestNeighbour(
            Vector3 anchor,
            Vector3 ghostPosition,
            Vector3 gridDir,
            Vector3 perpendicular,
            float gridSize,
            float ghostGrowth,
            GameObject ghost)
        {
            Vector3 best = Vector3.zero;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0)
                    {
                        continue;
                    }

                    Vector3 candidate = SetGround(
                        anchor + gridDir * (x * gridSize) + perpendicular * (z * gridSize));
                    if (HasOverlap(candidate, ghostGrowth, ghost))
                    {
                        continue;
                    }

                    float dist = HorizontalSqr(candidate, ghostPosition);
                    if (!found || dist < bestDist)
                    {
                        found = true;
                        bestDist = dist;
                        best = candidate;
                    }
                }
            }

            return best;
        }

        private static List<PlantAnchor> FindNearbyPlants(Vector3 position, float radius, GameObject ghost)
        {
            var result = new List<PlantAnchor>();
            int hits = Physics.OverlapSphereNonAlloc(
                position, radius, OverlapBuffer, -1, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Collider hit = OverlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                if (ghost != null && hit.transform.IsChildOf(ghost.transform))
                {
                    continue;
                }

                Plant plant = hit.GetComponentInParent<Plant>() ??
                              hit.GetComponentInChildren<Plant>(true);
                if (plant == null)
                {
                    continue;
                }

                // Skip the placement ghost's own Plant.
                if (ghost != null &&
                    (plant.gameObject == ghost || plant.transform.IsChildOf(ghost.transform)))
                {
                    continue;
                }

                Vector3 pos = plant.transform.position;
                bool duplicate = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (HorizontalSqr(result[j].Position, pos) < 0.0004f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    result.Add(new PlantAnchor(pos, Mathf.Max(0f, plant.m_growRadius)));
                }
            }

            return result;
        }

        private static bool HasOverlap(Vector3 position, float growth, GameObject ghost)
        {
            float search = Mathf.Max(growth * 2f + SpacingSlop + 1f, 2f);
            foreach (PlantAnchor other in FindNearbyPlants(position, search, ghost))
            {
                float min = growth + other.Growth + SpacingSlop;
                if (HorizontalSqr(position, other.Position) < min * min * 0.999f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCultivatorEquipped(Player player)
        {
            ItemDrop.ItemData right = player.GetRightItem();
            return right?.m_shared != null && right.m_shared.m_name == "$item_cultivator";
        }

        private static Vector3 SetGround(Vector3 position)
        {
            if (ZoneSystem.instance != null)
            {
                position.y = ZoneSystem.instance.GetGroundHeight(position);
            }

            return position;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float HorizontalSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static void DrawGrid(Vector3 origin, Vector3 gridDir, float gridSize)
        {
            if (gridSize <= 0.001f)
            {
                return;
            }

            EnsureLines();
            if (_lines == null)
            {
                return;
            }

            Vector3 across = new Vector3(gridDir.z, 0f, -gridDir.x);
            int count = GridSections * 2 + 1;

            for (int i = -GridSections; i <= GridSections; i++)
            {
                LineRenderer line = _lines[i + GridSections].GetComponent<LineRenderer>();
                line.enabled = true;
                line.positionCount = count;
                line.startColor = GridColor;
                line.endColor = GridColor;
                Vector3 row = origin + gridDir * (i * gridSize);
                for (int j = -GridSections; j <= GridSections; j++)
                {
                    Vector3 p = SetGround(row + across * (j * gridSize));
                    p.y += GridYOffset;
                    line.SetPosition(j + GridSections, p);
                }
            }

            int offset = count;
            for (int i = -GridSections; i <= GridSections; i++)
            {
                LineRenderer line = _lines[offset + i + GridSections].GetComponent<LineRenderer>();
                line.enabled = true;
                line.positionCount = count;
                line.startColor = GridColor;
                line.endColor = GridColor;
                Vector3 col = origin + across * (i * gridSize);
                for (int j = -GridSections; j <= GridSections; j++)
                {
                    Vector3 p = SetGround(col + gridDir * (j * gridSize));
                    p.y += GridYOffset;
                    line.SetPosition(j + GridSections, p);
                }
            }
        }

        private static void HideGrid()
        {
            if (_lines == null)
            {
                return;
            }

            foreach (GameObject go in _lines)
            {
                if (go == null)
                {
                    continue;
                }

                LineRenderer line = go.GetComponent<LineRenderer>();
                if (line != null)
                {
                    line.enabled = false;
                }
            }
        }

        private static void ResetSnap()
        {
            _snapValid = false;
            _visualOriginValid = false;
        }

        private static void EnsureLines()
        {
            int needed = (GridSections * 2 + 1) * 2;
            if (_lines != null && _lines.Length == needed && _lines[0] != null)
            {
                return;
            }

            DestroyGrid();
            EnsureMaterial();
            _lines = new GameObject[needed];
            for (int i = 0; i < needed; i++)
            {
                GameObject go = new GameObject("TeflonTed_FarmGridLine");
                Object.DontDestroyOnLoad(go);
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = 0.015f;
                line.positionCount = GridSections * 2 + 1;
                line.startColor = GridColor;
                line.endColor = GridColor;
                if (_lineMaterial != null)
                {
                    line.material = _lineMaterial;
                }

                line.enabled = false;
                _lines[i] = go;
            }
        }

        private static void EnsureMaterial()
        {
            if (_lineMaterial != null)
            {
                return;
            }

            foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat != null && mat.name == "Default-Line")
                {
                    _lineMaterial = mat;
                    return;
                }
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _lineMaterial = new Material(shader);
            }
        }

        internal static void DestroyGrid()
        {
            if (_lines != null)
            {
                foreach (GameObject go in _lines)
                {
                    if (go != null)
                    {
                        Object.Destroy(go);
                    }
                }
            }

            _lines = null;
            ResetSnap();
        }

        private readonly struct PlantAnchor
        {
            internal readonly Vector3 Position;
            internal readonly float Growth;

            internal PlantAnchor(Vector3 position, float growth)
            {
                Position = position;
                Growth = growth;
            }
        }
    }
}
