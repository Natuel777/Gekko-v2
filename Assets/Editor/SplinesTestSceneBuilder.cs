using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Herramienta de un solo uso para armar la escena de prueba de splines (ver el documento "Splines en
// Gekko - opciones de uso"). Corre vía:
//   Unity.exe -batchmode -quit -executeMethod SplinesTestSceneBuilder.Build
public static class SplinesTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Pruebas/SplinesTest.unity";
    private const string BlueberryPrefabPath = "Assets/Prefabs/Collectibles/Blueberry.prefab";

    [MenuItem("Tools/Gekko/Build Splines Test Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if(urpLit != null) ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(urpLit);

        BuildBlueberriesDemo();
        BuildVineMeshDemo();
        BuildSampledLineDemo();

        if(!AssetDatabase.IsValidFolder("Assets/Scenes/Pruebas"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Pruebas");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log(saved
            ? "[SplinesTestSceneBuilder] Listo: escena de prueba creada en " + ScenePath
            : "[SplinesTestSceneBuilder] No se pudo guardar la escena.");
    }

    private static void BuildBlueberriesDemo()
    {
        GameObject go = new GameObject("Demo_Arandanos", typeof(SplineContainer));
        go.transform.position = new Vector3(-8f, 0.5f, 0f);

        SplineContainer container = go.GetComponent<SplineContainer>();
        BuildCurve(container, new[]
        {
            new Vector3(0f, 0f, -6f),
            new Vector3(2f, 0.5f, -2f),
            new Vector3(-2f, 1f, 2f),
            new Vector3(1f, 0.5f, 6f),
        });

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlueberryPrefabPath);
        if(prefab == null)
        {
            Debug.LogWarning($"[SplinesTestSceneBuilder] No se encontro el prefab de arandano en '{BlueberryPrefabPath}'. Demo_Arandanos queda sin SplineInstantiate configurado.");
            return;
        }

        SplineInstantiate instantiate = go.AddComponent<SplineInstantiate>();
        instantiate.Container = container;
        instantiate.itemsToInstantiate = new[]
        {
            new SplineInstantiate.InstantiableItem { Prefab = prefab, Probability = 1f }
        };
        instantiate.InstantiateMethod = SplineInstantiate.Method.SpacingDistance;
        instantiate.MinSpacing = 2f;
        instantiate.MaxSpacing = 2f;
    }

    private static void BuildVineMeshDemo()
    {
        GameObject go = new GameObject("Demo_MallaEnredadera", typeof(SplineContainer));
        go.transform.position = new Vector3(0f, 0.5f, 0f);

        SplineContainer container = go.GetComponent<SplineContainer>();
        BuildCurve(container, new[]
        {
            new Vector3(0f, 0f, -6f),
            new Vector3(-2f, 2f, -2f),
            new Vector3(2f, 3f, 2f),
            new Vector3(0f, 1f, 6f),
        });

        SplineExtrude extrude = go.AddComponent<SplineExtrude>();
        extrude.Container = container;
        extrude.Radius = 0.15f;
        extrude.Sides = 8;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.3f, 0.6f, 0.25f) };
        AssetDatabase.CreateAsset(mat, "Assets/_SplinesSandbox/M_VineMeshDemo.mat");
        renderer.sharedMaterial = mat;

        extrude.Rebuild();
    }

    private static void BuildSampledLineDemo()
    {
        GameObject curveGO = new GameObject("Demo_LineaSampleada_Curva", typeof(SplineContainer));
        curveGO.transform.position = new Vector3(8f, 0.5f, 0f);

        SplineContainer container = curveGO.GetComponent<SplineContainer>();
        BuildCurve(container, new[]
        {
            new Vector3(0f, 0f, -6f),
            new Vector3(3f, 0f, -2f),
            new Vector3(-3f, 0f, 2f),
            new Vector3(0f, 0f, 6f),
        });

        GameObject lineGO = new GameObject("Demo_LineaSampleada", typeof(LineRenderer), typeof(SplineSampledLine));
        lineGO.transform.position = curveGO.transform.position;

        LineRenderer lr = lineGO.GetComponent<LineRenderer>();
        lr.widthMultiplier = 0.1f;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.cyan };

        SerializedObject so = new SerializedObject(lineGO.GetComponent<SplineSampledLine>());
        so.FindProperty("_spline").objectReferenceValue = container;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildCurve(SplineContainer container, Vector3[] localKnots)
    {
        Spline spline = container.Spline;
        foreach(Vector3 knot in localKnots)
            spline.Add(knot, TangentMode.AutoSmooth);
    }
}
