using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Arma la escena de prueba del rio (Assets/_RiverSandbox/RiverSandbox.unity) con sus materiales y
// prefabs flotantes. Se puede volver a correr: pisa la escena y los assets generados.
//   Menu: Tools > Gekko > Build River Sandbox
public static class RiverSandboxBuilder
{
    const string Root = "Assets/_RiverSandbox";
    const string ScenePath = Root + "/RiverSandbox.unity";
    internal const string MaterialsFolder = Root + "/Materials";
    const string TexturesFolder = Root + "/Textures";
    internal const string PrefabsFolder = Root + "/Prefabs";
    const string PropsFolder = "Assets/Prefabs/Prop";

    [MenuItem("Tools/Gekko/Build River Sandbox")]
    public static void Build()
    {
        EnsureFolder(MaterialsFolder);
        EnsureFolder(TexturesFolder);
        EnsureFolder(PrefabsFolder);

        Material water = CreateWaterMaterial();
        Material bed = CreateBedMaterial();
        Material green = CreateLitMaterial("M_River_LilyPad", new Color(0.36f, 0.66f, 0.28f));
        Material flower = CreateLitMaterial("M_River_Flower", new Color(1f, 0.93f, 0.95f));
        Material wood = CreateLitMaterial("M_River_Wood", new Color(0.55f, 0.36f, 0.2f));
        Material ball = CreateLitMaterial("M_River_Ball", new Color(1f, 0.45f, 0.2f));
        Material reed = CreateLitMaterial("M_River_Reed", new Color(0.45f, 0.62f, 0.25f));

        GameObject lilyPad = CreateLilyPadPrefab(green, flower);
        GameObject crate = CreateCratePrefab(wood);
        GameObject log = CreateLogPrefab(wood);
        GameObject[] floaters = { lilyPad, lilyPad, crate, log };

        // Se arma en modo aditivo para no descartar cambios sin guardar de la escena abierta;
        // al final se guarda y se cierra (abrirla despues desde el Project).
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        SetupLighting(scene);

        SplineRiver river = CreateRiver(water, bed);
        PlaceRocks(river);
        PlaceVegetation(river, reed);
        PlaceLilyPads(river, lilyPad);

        GameObject player = CreateBall(river, ball);
        SetupCamera(scene, player.transform, floaters);

        var spawner = new GameObject("Spawner (objetos flotantes)").AddComponent<RiverSandboxSpawner>();
        var spawnerSo = new SerializedObject(spawner);
        spawnerSo.FindProperty("_river").objectReferenceValue = river;
        SetArray(spawnerSo.FindProperty("_prefabs"), floaters);
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        if(previousActive.IsValid() && previousActive != scene)
        {
            SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log(saved
            ? "[RiverSandboxBuilder] Listo: escena creada en " + ScenePath
            : "[RiverSandboxBuilder] No se pudo guardar la escena.");
    }

    // ------------------------------------------------------------------ escena

    internal static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>();
            if(found != null)
                return found;
        }
        return null;
    }

    internal static void SetupLighting(Scene scene)
    {
        Light sun = FindInScene<Light>(scene);
        if(sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.64f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.26f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.86f, 0.95f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 140f;
    }

    internal static SplineRiver CreateRiver(Material water, Material bed)
    {
        var go = new GameObject("Rio (SplineRiver)", typeof(SplineContainer));
        SplineContainer container = go.GetComponent<SplineContainer>();
        Spline spline = container.Spline;
        spline.Clear();
        Vector3[] knots =
        {
            new Vector3(0f, 0f, -34f),
            new Vector3(9f, 0f, -21f),
            new Vector3(-3f, 0f, -8f),
            new Vector3(7f, 0f, 6f),
            new Vector3(-5f, 0f, 20f),
            new Vector3(2f, 0f, 34f)
        };
        foreach(Vector3 knot in knots)
            spline.Add(knot, TangentMode.AutoSmooth);

        SplineRiver river = go.AddComponent<SplineRiver>();
        var so = new SerializedObject(river);
        so.FindProperty("_container").objectReferenceValue = container;
        so.FindProperty("_waterMaterial").objectReferenceValue = water;
        so.FindProperty("_bedMaterial").objectReferenceValue = bed;
        so.FindProperty("_width").floatValue = 6f;
        SerializedProperty widths = so.FindProperty("_knotWidthMultipliers");
        float[] multipliers = { 0.9f, 1.1f, 0.6f, 1.7f, 1.2f, 1f };
        widths.arraySize = multipliers.Length;
        for(int i = 0; i < multipliers.Length; i++)
            widths.GetArrayElementAtIndex(i).floatValue = multipliers[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        river.Rebuild();
        return river;
    }

    internal static Vector3 BankPoint(SplineRiver river, float distance, int side, float extra)
    {
        Vector3 center = river.PointAt(distance, 0.5f);
        Vector3 edge = river.PointAt(distance, side > 0 ? 1f : 0f);
        Vector3 dir = edge - center;
        float halfWidth = dir.magnitude;
        dir = halfWidth > 1e-4f ? dir / halfWidth : Vector3.right * side;
        return center + dir * (halfWidth + extra);
    }

    internal static void PlaceRocks(SplineRiver river)
    {
        var parent = new GameObject("Rocas").transform;
        string[] rocks = { "R_Small_lp", "R_Short_lp", "R_Tall_lp" };
        var rng = new System.Random(7);

        // Rocas dentro del agua (generan anillos de espuma) y algunas en la orilla.
        (float distance, float lateral, float size, float sink)[] placements =
        {
            (12f, 0.3f, 1.6f, 0.35f),
            (26f, 0.72f, 2.2f, 0.5f),
            (40f, 0.5f, 1.2f, 0.3f),
            (52f, 0.2f, 2.6f, 0.6f),
            (55f, 0.85f, 1.4f, 0.35f),
            (68f, 0.6f, 1.8f, 0.45f),
            (84f, 0.35f, 1.3f, 0.3f)
        };

        foreach(var p in placements)
        {
            GameObject rock = InstantiateProp(rocks[rng.Next(rocks.Length)], parent);
            if(rock == null)
                continue;
            FitToSize(rock, p.size);
            Vector3 pos = river.PointAt(p.distance, p.lateral);
            rock.transform.position = pos + Vector3.down * p.sink;
            rock.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
        }

        for(int i = 0; i < 8; i++)
        {
            GameObject rock = InstantiateProp(rocks[rng.Next(rocks.Length)], parent);
            if(rock == null)
                continue;
            FitToSize(rock, 1f + (float)rng.NextDouble() * 1.6f);
            int side = i % 2 == 0 ? 1 : -1;
            rock.transform.position = BankPoint(river, 6f + i * 11f, side, 1.2f + (float)rng.NextDouble() * 2f) + Vector3.down * 0.25f;
            rock.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
        }
    }

    internal static void PlaceVegetation(SplineRiver river, Material reedMaterial)
    {
        var parent = new GameObject("Vegetacion").transform;
        var rng = new System.Random(11);
        string[] trees = { "Pine_Tree", "Roble_Tree", "Bush_1", "Bush_2" };

        for(int i = 0; i < 12; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            string name = trees[rng.Next(trees.Length)];
            GameObject tree = InstantiateProp(name, parent);
            if(tree == null)
                continue;
            bool isBush = name.StartsWith("Bush");
            FitToSize(tree, isBush ? 1.6f : 7f + (float)rng.NextDouble() * 3f);
            Vector3 pos = BankPoint(river, 3f + i * 7.5f, side, 4f + (float)rng.NextDouble() * 4f);
            tree.transform.position = SnapToGround(pos);
            tree.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
        }

        // Juncos en la orilla, como en la referencia.
        for(int c = 0; c < 9; c++)
        {
            int side = c % 2 == 0 ? 1 : -1;
            Vector3 clusterCenter = BankPoint(river, 8f + c * 9f, side, -0.6f);
            var cluster = new GameObject("Juncos").transform;
            cluster.SetParent(parent, false);
            cluster.position = clusterCenter;
            int count = 3 + rng.Next(3);
            for(int i = 0; i < count; i++)
            {
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(stem.GetComponent<Collider>());
                stem.name = "Junco";
                stem.transform.SetParent(cluster, false);
                float height = 0.8f + (float)rng.NextDouble() * 0.9f;
                stem.transform.localScale = new Vector3(0.12f, height * 0.5f, 0.12f);
                stem.transform.localPosition = new Vector3((float)rng.NextDouble() - 0.5f, height * 0.5f - 0.4f, (float)rng.NextDouble() - 0.5f);
                stem.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 10f - 5f, 0f, (float)rng.NextDouble() * 10f - 5f);
                stem.GetComponent<MeshRenderer>().sharedMaterial = reedMaterial;
            }
        }
    }

    internal static void PlaceLilyPads(SplineRiver river, GameObject lilyPad)
    {
        var parent = new GameObject("Nenufares iniciales").transform;
        var rng = new System.Random(3);
        for(int i = 0; i < 6; i++)
        {
            Vector3 pos = river.PointAt(44f + i * 3f, 0.2f + (float)rng.NextDouble() * 0.6f);
            var pad = (GameObject)PrefabUtility.InstantiatePrefab(lilyPad);
            pad.transform.SetParent(parent, true);
            pad.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.Euler(0f, rng.Next(360), 0f));
        }
    }

    internal static GameObject CreateBall(SplineRiver river, Material material)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Pelota de prueba (WASD / Espacio / R)";
        ball.transform.position = SnapToGround(BankPoint(river, 14f, -1, 2.5f)) + Vector3.up * 0.6f;
        ball.GetComponent<MeshRenderer>().sharedMaterial = material;

        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        RiverFloater floater = ball.AddComponent<RiverFloater>();
        var so = new SerializedObject(floater);
        so.FindProperty("_buoyancy").floatValue = 1.5f;
        so.FindProperty("_halfHeight").floatValue = 0.5f;
        so.FindProperty("_spin").floatValue = 0f;
        so.FindProperty("_currentFollow").floatValue = 0.8f;
        so.ApplyModifiedPropertiesWithoutUndo();

        ball.AddComponent<RiverSandboxBall>();
        return ball;
    }

    internal static void SetupCamera(Scene scene, Transform target, GameObject[] dropPrefabs)
    {
        Camera cam = FindInScene<Camera>(scene);
        if(cam == null)
            cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();

        cam.farClipPlane = 300f;
        cam.fieldOfView = 50f;
        var controller = cam.gameObject.AddComponent<RiverSandboxCamera>();
        var so = new SerializedObject(controller);
        so.FindProperty("_target").objectReferenceValue = target;
        SetArray(so.FindProperty("_dropPrefabs"), dropPrefabs);
        so.ApplyModifiedPropertiesWithoutUndo();

        Quaternion rotation = Quaternion.Euler(38f, 30f, 0f);
        cam.transform.SetPositionAndRotation(target.position - rotation * Vector3.forward * 14f, rotation);
    }

    // ------------------------------------------------------------------ prefabs

    internal static GameObject CreateLilyPadPrefab(Material green, Material flowerMaterial)
    {
        var root = new GameObject("RiverLilyPad");
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        pad.name = "Hoja";
        pad.transform.SetParent(root.transform, false);
        pad.transform.localScale = new Vector3(1.2f, 0.025f, 1.2f);
        pad.GetComponent<MeshRenderer>().sharedMaterial = green;

        GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(flower.GetComponent<Collider>());
        flower.name = "Flor";
        flower.transform.SetParent(root.transform, false);
        flower.transform.localPosition = new Vector3(0.15f, 0.08f, 0.1f);
        flower.transform.localScale = new Vector3(0.28f, 0.14f, 0.28f);
        flower.GetComponent<MeshRenderer>().sharedMaterial = flowerMaterial;

        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(1.2f, 0.08f, 1.2f);

        AddFloaterBody(root, 0.25f, 0.04f, 2.5f);
        return SavePrefab(root, "RiverLilyPad");
    }

    internal static GameObject CreateCratePrefab(Material wood)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "RiverCrate";
        crate.transform.localScale = Vector3.one * 0.7f;
        crate.GetComponent<MeshRenderer>().sharedMaterial = wood;
        AddFloaterBody(crate, 1.2f, 0.35f, 1.7f);
        return SavePrefab(crate, "RiverCrate");
    }

    internal static GameObject CreateLogPrefab(Material wood)
    {
        var root = new GameObject("RiverLog");
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        trunk.name = "Tronco";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        trunk.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = wood;

        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.direction = 0;
        capsule.radius = 0.2f;
        capsule.height = 2f;

        AddFloaterBody(root, 1.5f, 0.2f, 1.9f);
        return SavePrefab(root, "RiverLog");
    }

    internal static void AddFloaterBody(GameObject go, float mass, float halfHeight, float buoyancy)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        RiverFloater floater = go.AddComponent<RiverFloater>();
        var so = new SerializedObject(floater);
        so.FindProperty("_halfHeight").floatValue = halfHeight;
        so.FindProperty("_buoyancy").floatValue = buoyancy;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static GameObject SavePrefab(GameObject go, string name)
    {
        string path = PrefabsFolder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ materiales

    internal static Material CreateWaterMaterial()
    {
        Shader shader = Shader.Find("Gekko/River Stylized");
        Material material = LoadOrCreateMaterial("M_RiverWater", shader);
        material.shader = shader;
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static Material CreateBedMaterial()
    {
        Texture2D gradient = CreateBedGradient();
        Material material = LoadOrCreateMaterial("M_RiverBed", Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", gradient);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static Material CreateLitMaterial(string name, Color color)
    {
        Material material = LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.2f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static Material LoadOrCreateMaterial(string name, Shader shader)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    // Gradiente vertical del cauce: v 0 = fondo, 0.5 = linea de agua, 1 = orilla alta (pasto).
    internal static Texture2D CreateBedGradient()
    {
        string path = TexturesFolder + "/T_RiverBedGradient.png";
        (float v, Color c)[] stops =
        {
            (0.00f, new Color(0.80f, 0.72f, 0.55f)),
            (0.42f, new Color(0.96f, 0.89f, 0.72f)),
            (0.50f, new Color(0.84f, 0.74f, 0.56f)),
            (0.60f, new Color(0.95f, 0.87f, 0.68f)),
            (0.72f, new Color(0.44f, 0.62f, 0.30f)),
            (1.00f, new Color(0.36f, 0.55f, 0.27f))
        };

        const int height = 128;
        var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
        for(int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            Color color = stops[stops.Length - 1].c;
            for(int s = 0; s < stops.Length - 1; s++)
            {
                if(v <= stops[s + 1].v)
                {
                    float t = Mathf.InverseLerp(stops[s].v, stops[s + 1].v, v);
                    color = Color.Lerp(stops[s].c, stops[s + 1].c, Mathf.SmoothStep(0f, 1f, t));
                    break;
                }
            }
            for(int x = 0; x < 4; x++)
                texture.SetPixel(x, y, color);
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ utilidades

    internal static GameObject InstantiateProp(string name, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropsFolder + "/" + name + ".prefab");
        if(prefab == null)
        {
            Debug.LogWarning("[RiverSandboxBuilder] No se encontro el prop " + name);
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, true);
        instance.transform.position = Vector3.zero;
        return instance;
    }

    // Escala el objeto para que su lado mas grande mida "size" metros.
    internal static void FitToSize(GameObject go, float size)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if(renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach(Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if(largest > 1e-4f)
            go.transform.localScale *= size / largest;
    }

    // Altura de la orilla por defecto de SplineRiver (_bankHeight). Sin raycasts para no pegarle a
    // colliders de otras escenas abiertas.
    internal const float BankHeight = 0.6f;

    internal static Vector3 SnapToGround(Vector3 position)
    {
        return position + Vector3.up * BankHeight;
    }

    internal static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for(int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    internal static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
