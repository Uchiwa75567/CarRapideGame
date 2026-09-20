#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CarRapide.Vehicle;
using CarRapide.World.Osm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarRapide.EditorTools
{
    public static class DakarOsmPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/DakarOsmPrototype.unity";
        private const string MaterialFolder = "Assets/Generated/DakarOsmPrototype/Materials";
        private const double South = 14.6650;
        private const double West = -17.4550;
        private const double North = 14.7000;
        private const double East = -17.4300;
        private const int MaxBuildings = 700;

        private static readonly Vector2 Medina = new Vector2(14.68049f, -17.45093f);
        private static readonly Vector2 Plateau = new Vector2(14.66732f, -17.43797f);
        private static readonly Vector2 Colobane = new Vector2(14.69512f, -17.44543f);
        private static readonly string[] OverpassEndpoints =
        {
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass-api.de/api/interpreter",
            "https://overpass.nchc.org.tw/api/interpreter"
        };

        [MenuItem("Car Rapide/Dakar OSM/Build or Refresh Prototype")]
        public static async void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "Dakar OSM Prototype",
                    "Télécharger les routes et bâtiments OpenStreetMap de Plateau / Médina / Colobane et créer une scène jouable ?",
                    "Construire", "Annuler")) return;

            try
            {
                EditorUtility.DisplayProgressBar("Dakar OSM", "Téléchargement OpenStreetMap...", 0.1f);
                OsmResponse data = await DownloadData();
                if (data?.elements == null || data.elements.Length == 0)
                    throw new InvalidOperationException("Aucune donnée OSM reçue.");

                EditorUtility.DisplayProgressBar("Dakar OSM", "Génération de la scène...", 0.35f);
                BuildScene(data);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Dakar OSM Prototype",
                    "Terminé. Ouvre Assets/Scenes/DakarOsmPrototype.unity puis appuie sur Play.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Erreur Dakar OSM", e.Message, "OK");
            }
        }

        [MenuItem("Car Rapide/Dakar OSM/Open Prototype Scene")]
        public static void OpenScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Dakar OSM Prototype",
                    "La scène n'existe pas encore. Lance d'abord Build or Refresh Prototype.", "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static async Task<OsmResponse> DownloadData()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(75) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CarRapideGame-DakarPrototype/1.1");

            // Découpe la zone en quatre tuiles pour réduire la charge de chaque requête Overpass.
            double midLat = (South + North) * 0.5;
            double midLon = (West + East) * 0.5;
            (double south, double west, double north, double east)[] tiles =
            {
                (South, West, midLat, midLon),
                (South, midLon, midLat, East),
                (midLat, West, North, midLon),
                (midLat, midLon, North, East)
            };

            Dictionary<string, OsmElement> unique = new Dictionary<string, OsmElement>();

            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                string bbox = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}",
                    tile.south, tile.west, tile.north, tile.east);

                EditorUtility.DisplayProgressBar(
                    "Dakar OSM",
                    $"Téléchargement des routes ({i + 1}/{tiles.Length})...",
                    0.05f + i * 0.06f);

                string roads = "[out:json][timeout:45];(" +
                               $"way[\"highway\"~\"^(primary|secondary|tertiary|residential|unclassified|service)$\"]({bbox});" +
                               $"node[\"amenity\"=\"marketplace\"]({bbox});" +
                               $"node[\"highway\"=\"bus_stop\"]({bbox});" +
                               ");out tags geom qt;";

                AddUnique(unique, await Download(client, roads));

                EditorUtility.DisplayProgressBar(
                    "Dakar OSM",
                    $"Téléchargement des bâtiments ({i + 1}/{tiles.Length})...",
                    0.30f + i * 0.06f);

                string buildings = "[out:json][timeout:45];" +
                                   $"way[\"building\"]({bbox});out tags geom qt 650;";

                AddUnique(unique, await Download(client, buildings));
            }

            return new OsmResponse { elements = unique.Values.ToArray() };
        }

        private static void AddUnique(Dictionary<string, OsmElement> destination, OsmResponse response)
        {
            if (response?.elements == null) return;

            foreach (OsmElement element in response.elements)
            {
                string key = $"{element.type}:{element.id}";
                destination[key] = element;
            }
        }

        private static async Task<OsmResponse> Download(HttpClient client, string query)
        {
            Exception last = null;

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                foreach (string endpoint in OverpassEndpoints)
                {
                    try
                    {
                        using var form = new FormUrlEncodedContent(
                            new[] { new KeyValuePair<string, string>("data", query) });

                        using HttpResponseMessage response = await client.PostAsync(endpoint, form);
                        string json = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                            throw new HttpRequestException(
                                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                        OsmResponse parsed = JsonUtility.FromJson<OsmResponse>(json);
                        if (parsed?.elements != null)
                            return parsed;

                        throw new InvalidOperationException("Réponse Overpass invalide.");
                    }
                    catch (Exception e)
                    {
                        last = e;
                        Debug.LogWarning(
                            $"Overpass indisponible (tentative {attempt}/2): {endpoint} ({e.Message})");
                    }
                }

                if (attempt < 2)
                    await Task.Delay(1500);
            }

            throw new InvalidOperationException(
                "Impossible de télécharger OpenStreetMap via Overpass après plusieurs tentatives. Vérifie ta connexion puis relance Car Rapide > Dakar OSM > Build or Refresh Prototype.",
                last);
        }

        private static void BuildScene(OsmResponse data)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolders();

            Material roadMain = MaterialAsset("Road_Main", new Color(0.16f, 0.16f, 0.17f));
            Material roadLocal = MaterialAsset("Road_Local", new Color(0.25f, 0.25f, 0.26f));
            Material building = MaterialAsset("Building", new Color(0.78f, 0.68f, 0.56f));
            Material ground = MaterialAsset("Ground", new Color(0.70f, 0.62f, 0.46f));
            Material marker = MaterialAsset("Marker", new Color(0.08f, 0.42f, 0.68f));

            GameObject root = new GameObject("Dakar OSM Prototype");
            Transform roadsRoot = Child("Roads (OSM)", root.transform);
            Transform buildingsRoot = Child("Buildings (OSM)", root.transform);
            Transform labelsRoot = Child("District Labels", root.transform);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(0f, -0.08f, 0f);
            floor.transform.localScale = new Vector3(450f, 1f, 450f);
            floor.GetComponent<Renderer>().sharedMaterial = ground;

            GameObject sun = new GameObject("Dakar Sun");
            sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.color = new Color(1f, 0.91f, 0.76f);
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.60f);

            List<OsmElement> roads = data.elements.Where(IsRoad)
                .Where(x => x.geometry != null && x.geometry.Length >= 2).ToList();
            foreach (OsmElement r in roads) CreateRoad(r, roadsRoot, roadMain, roadLocal);

            List<OsmElement> buildings = data.elements.Where(IsBuilding)
                .Where(x => x.geometry != null && x.geometry.Length >= 4).ToList();
            int step = Mathf.Max(1, Mathf.CeilToInt(buildings.Count / (float)MaxBuildings));
            int count = 0;
            for (int i = 0; i < buildings.Count && count < MaxBuildings; i += step)
                if (CreateBuilding(buildings[i], buildingsRoot, building)) count++;

            DistrictLabel("DAKAR-PLATEAU", Plateau, labelsRoot, marker);
            DistrictLabel("MÉDINA", Medina, labelsRoot, marker);
            DistrictLabel("COLOBANE", Colobane, labelsRoot, marker);

            Transform player = CreatePlayer(roads, root.transform);
            CreateCamera(player, root.transform);
            new GameObject("Prototype HUD + OSM Attribution").AddComponent<DakarPrototypeOverlay>().transform.SetParent(root.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = player.gameObject;
        }

        private static void CreateRoad(OsmElement road, Transform parent, Material main, Material local)
        {
            List<Vector3> p = road.geometry.Select(g => DakarGeoUtils.ToWorld(g.lat, g.lon, 0.02f)).ToList();
            p = RemoveClose(p, 0.75f);
            if (p.Count < 2) return;
            float width = RoadWidth(road.tags?.highway);

            Vector3[] v = new Vector3[p.Count * 2];
            int[] t = new int[(p.Count - 1) * 6];
            for (int i = 0; i < p.Count; i++)
            {
                Vector3 a = p[Mathf.Max(0, i - 1)], b = p[Mathf.Min(p.Count - 1, i + 1)];
                Vector3 right = Vector3.Cross(Vector3.up, (b - a).normalized) * width * 0.5f;
                v[i * 2] = p[i] - right;
                v[i * 2 + 1] = p[i] + right;
            }
            int k = 0;
            for (int i = 0; i < p.Count - 1; i++)
            {
                int n = i * 2;
                t[k++] = n; t[k++] = n + 2; t[k++] = n + 1;
                t[k++] = n + 1; t[k++] = n + 2; t[k++] = n + 3;
            }
            Mesh mesh = new Mesh { name = "OSM Road" };
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(road.tags?.name) ? $"Road {road.id}" : road.tags.name);
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = road.tags?.highway == "primary" || road.tags?.highway == "secondary" ? main : local;
        }

        private static bool CreateBuilding(OsmElement item, Transform parent, Material material)
        {
            Vector3[] pts = item.geometry.Select(g => DakarGeoUtils.ToWorld(g.lat, g.lon)).ToArray();
            Bounds b = new Bounds(pts[0], Vector3.zero);
            foreach (Vector3 p in pts) b.Encapsulate(p);
            if (b.size.x < 2f || b.size.z < 2f || b.size.x > 120f || b.size.z > 120f) return false;

            float h = 5.5f + (item.id % 7) * 1.6f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrWhiteSpace(item.tags?.name) ? $"Building {item.id}" : item.tags.name;
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(b.center.x, h * 0.5f, b.center.z);
            go.transform.localScale = new Vector3(b.size.x, h, b.size.z);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return true;
        }

        private static void DistrictLabel(string text, Vector2 latLon, Transform parent, Material material)
        {
            Vector3 pos = DakarGeoUtils.ToWorld(latLon.x, latLon.y);
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = text;
            pole.transform.SetParent(parent);
            pole.transform.position = pos + Vector3.up * 4f;
            pole.transform.localScale = new Vector3(0.35f, 8f, 0.35f);
            pole.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform CreatePlayer(IReadOnlyList<OsmElement> roads, Transform parent)
        {
            (Vector3 pos, Quaternion rot) = SpawnPose(roads, Medina.x, Medina.y);
            GameObject player = new GameObject("CarRapidePlayer");
            player.transform.SetParent(parent);
            player.transform.position = pos + Vector3.up;
            player.transform.rotation = rot;
            Rigidbody rb = player.AddComponent<Rigidbody>(); rb.mass = 2200f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider c = player.AddComponent<BoxCollider>(); c.center = new Vector3(0.167f, 0.141f, -0.017f); c.size = new Vector3(1.47f, 2.52f, 3.61f);
            player.AddComponent<VehicleController>();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/CarRapide/Car rapide.fbx");
            if (model != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Car rapide"; visual.transform.SetParent(player.transform);
                visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one;
            }
            return player.transform;
        }

        private static void CreateCamera(Transform target, Transform parent)
        {
            GameObject go = new GameObject("Main Camera"); go.tag = "MainCamera"; go.transform.SetParent(parent);
            go.transform.position = target.position + new Vector3(0f, 4f, -8f);
            Camera cam = go.AddComponent<Camera>(); cam.fieldOfView = 64f; cam.farClipPlane = 3500f;
            go.AddComponent<AudioListener>(); go.AddComponent<VehicleCameraFollow>().SetTarget(target);
        }

        private static (Vector3, Quaternion) SpawnPose(IReadOnlyList<OsmElement> roads, double lat, double lon)
        {
            Vector3 target = DakarGeoUtils.ToWorld(lat, lon), best = target, dir = Vector3.forward;
            float bestD = float.PositiveInfinity;
            foreach (OsmElement r in roads)
            {
                string type = r.tags?.highway;
                if (type != "primary" && type != "secondary" && type != "tertiary") continue;
                for (int i = 0; i < r.geometry.Length; i++)
                {
                    Vector3 p = DakarGeoUtils.ToWorld(r.geometry[i].lat, r.geometry[i].lon);
                    float d = (p - target).sqrMagnitude; if (d >= bestD) continue;
                    bestD = d; best = p;
                    int a = Mathf.Max(0, i - 1), b = Mathf.Min(r.geometry.Length - 1, i + 1);
                    Vector3 candidate = DakarGeoUtils.ToWorld(r.geometry[b].lat, r.geometry[b].lon) - DakarGeoUtils.ToWorld(r.geometry[a].lat, r.geometry[a].lon);
                    candidate.y = 0f; if (candidate.sqrMagnitude > 0.1f) dir = candidate.normalized;
                }
            }
            return (best, Quaternion.LookRotation(dir, Vector3.up));
        }

        private static float RoadWidth(string type) => type switch
        {
            "primary" => 12f, "secondary" => 9f, "tertiary" => 7f,
            "residential" => 5.5f, "service" => 4f, _ => 5f
        };

        private static bool IsRoad(OsmElement e) => e.type == "way" && !string.IsNullOrWhiteSpace(e.tags?.highway);
        private static bool IsBuilding(OsmElement e) => e.type == "way" && !string.IsNullOrWhiteSpace(e.tags?.building);

        private static List<Vector3> RemoveClose(List<Vector3> source, float min)
        {
            if (source.Count < 2) return source;
            List<Vector3> result = new List<Vector3> { source[0] };
            for (int i = 1; i < source.Count; i++) if (Vector3.Distance(source[i], result[result.Count - 1]) >= min) result.Add(source[i]);
            return result;
        }

        private static Transform Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name); go.transform.SetParent(parent); return go.transform;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Generated"); EnsureFolder("Assets/Generated/DakarOsmPrototype"); EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", System.IO.Path.GetFileName(path));
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color; if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m); return m;
        }
    }
}
#endif
