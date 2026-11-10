using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Arma la escena vitrina del rio (Assets/_RiverSandbox/RiverShowcase.unity): una estacion por
// funcionalidad, separadas en X. Se navega en Play con 1..9 (ver RiverShowcaseCamera).
// Se puede volver a correr: pisa la escena. Reusa materiales/prefabs de RiverSandboxBuilder.
//   Menu: Tools > Gekko > Build River Showcase
public static class RiverShowcaseBuilder
{
    const string ScenePath = "Assets/_RiverSandbox/RiverShowcase.unity";
    const string WaterOnlyScenePath = "Assets/_RiverSandbox/RiverShowcase_SoloAgua.unity";
    const string TexturesFolder = "Assets/_RiverSandbox/Textures";
    const float Spacing = 90f;

    static Material s_water, s_bed, s_arrow, s_splash, s_grass, s_stone, s_sand;
    static GameObject s_lily, s_crate, s_log, s_sinker, s_heavy;

    // Modo "solo agua": ningun rio genera cauce (orillas/margen); solo la superficie del agua sobre
    // un piso plano, como quedaria metido en el terreno de un nivel.
    static bool s_waterOnly;

    [MenuItem("Tools/Gekko/Build River Showcase")]
    public static void Build()
    {
        Build(ScenePath, false);
    }

    [MenuItem("Tools/Gekko/Build River Showcase (Solo agua)")]
    public static void BuildWaterOnly()
    {
        Build(WaterOnlyScenePath, true);
    }

    static void Build(string scenePath, bool waterOnly)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[RiverShowcaseBuilder] Sali de Play antes de armar la escena.");
            return;
        }

        s_waterOnly = waterOnly;
        RiverSandboxBuilder.EnsureFolder(RiverSandboxBuilder.MaterialsFolder);
        RiverSandboxBuilder.EnsureFolder(RiverSandboxBuilder.PrefabsFolder);
        RiverSandboxBuilder.EnsureFolder(TexturesFolder);
        CreateAssets();

        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RiverSandboxBuilder.SetupLighting(scene);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 170f;

        var stations = new[]
        {
            BuildWidthStation(Origin(0)),
            BuildFloatStation(Origin(1)),
            BuildSplashStation(Origin(2)),
            BuildRapidsStation(Origin(3)),
            BuildLoopStation(Origin(4)),
            BuildReverseStation(Origin(5)),
            BuildNoBedStation(Origin(6)),
            BuildMaterialsStation(Origin(7)),
            BuildApiStation(Origin(8))
        };

        if(waterOnly)
        {
            // Piso plano bajo todas las estaciones: el agua queda "apoyada" a 1 m sobre el suelo.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Piso";
            ground.transform.position = new Vector3(Spacing * 4f, -1.5f, 0f);
            ground.transform.localScale = new Vector3(Spacing * 9f + 80f, 1f, 140f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = s_grass;
        }

        RiverSandboxBall ball = CreateBall(stations[0].BallSpawn.position);
        SetupCamera(scene, stations, ball);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        s_waterOnly = false;
        AssetDatabase.SaveAssets();
        if(previousActive.IsValid() && previousActive != scene)
        {
            SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log(saved
            ? "[RiverShowcaseBuilder] Listo: escena creada en " + scenePath
            : "[RiverShowcaseBuilder] No se pudo guardar la escena.");
    }

    static Vector3 Origin(int index) => new Vector3(index * Spacing, 0f, 0f);

    // ------------------------------------------------------------------ estaciones

    static RiverShowcaseStation BuildWidthStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -22), new Vector3(9, 0, -8), new Vector3(-3, 0, 5), new Vector3(7, 0, 18) };
        SplineRiver river = CreateRiver("Rio - ancho por knot", o, knots, new[] { 1.6f, 0.45f, 1.9f, 0.6f }, false, s_water,
            ("_width", 6f), ("_flowSpeed", 1.4f));

        foreach(Vector3 knot in knots)
            CreateProbe(river, o + knot, true);

        Spawner(river, new[] { s_lily, s_lily, s_crate }, 1.6f, 10);
        Decorate(river, 6, 1);

        return Station(o + new Vector3(2, 0, -2), "Ancho por knot y velocidad",
            "Cada knot tiene su multiplicador de ancho (lista <b>Knot Width Multipliers</b> en SplineRiver).\n\n" +
            "Con <b>Narrow Is Faster</b> activado, donde el rio se angosta el agua corre mas rapido: mira las " +
            "flechas y los m/s (rojo = rapido, celeste = lento).\n\n" +
            "La velocidad visual del shader y la fisica son la misma (<b>Flow Speed</b>).",
            BankSpawn(river, 6f), 15f, 58f, 48f);
    }

    static RiverShowcaseStation BuildFloatStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -26), new Vector3(3, 0, 0), new Vector3(0, 0, 26) };
        SplineRiver river = CreateRiver("Rio - flotacion", o, knots, new[] { 1f, 1.1f, 1f }, false, s_water,
            ("_width", 9f), ("_flowSpeed", 1.2f));

        Spawner(river, new[] { s_lily, s_crate, s_log, s_sinker, s_heavy }, 1.1f, 22);
        Decorate(river, 6, 2);

        return Station(o, "Flotacion y corriente",
            "Objetos con <b>RiverFloater</b>: flotan y los lleva la corriente.\n" +
            "  - Nenufar (verde), caja y tronco: Buoyancy > 1, flotan.\n" +
            "  - Piedra gris oscura: Buoyancy 0.55, se hunde despacio.\n\n" +
            "Bola gris SIN RiverFloater: es un Rigidbody comun. Se va al fondo y la corriente igual la " +
            "arrastra (<b>Push Rigidbodies</b>).\n\n" +
            "Al jugador NO lo arrastra salvo que actives <b>Affect Player</b>.",
            BankSpawn(river, 8f), 60f, 38f, 34f);
    }

    static RiverShowcaseStation BuildSplashStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -9), new Vector3(0, 0, 9) };
        SplineRiver river = CreateRiver("Rio - salpicaduras", o, knots, new[] { 1f, 1f }, false, s_water,
            ("_width", 14f), ("_flowSpeed", 0.15f), ("_depth", 2.2f));

        var fx = river.gameObject.AddComponent<RiverSplashFX>();
        Set(fx, ("_material", s_splash));

        var dropper = new GameObject("Dropper (suelta objetos)").AddComponent<RiverDropper>();
        dropper.transform.position = o + new Vector3(0f, 6f, 0f);
        Set(dropper, ("_prefabs", new Object[] { s_crate, s_log, s_heavy, s_lily }), ("_interval", 1.3f), ("_radius", 3.5f));

        Decorate(river, 4, 3);

        return Station(o, "Salpicaduras y estelas",
            "Todo Rigidbody que cruza la superficie salpica (onda + evento <b>Splashed</b>). La fuerza depende " +
            "de la velocidad vertical.\n\n" +
            "<b>RiverSplashFX</b> escucha el evento y tira gotitas: es el ejemplo para enganchar sonido o " +
            "particulas propias.\n\n" +
            "Mover la pelota por el agua (WASD) deja estela. Click izquierdo = onda, E = soltar un objeto.",
            BankSpawn(river, 0f), 35f, 32f, 24f);
    }

    static RiverShowcaseStation BuildRapidsStation(Vector3 o)
    {
        Vector3[] knots =
        {
            new Vector3(0, 8, -28), new Vector3(1.5f, 8, -17), new Vector3(-0.5f, 4.5f, -7),
            new Vector3(1, 1, 3), new Vector3(0, 0, 14), new Vector3(0, 0, 26)
        };
        SplineRiver river = CreateRiver("Rio - rapidos", o, knots, new[] { 1.6f, 1.2f, 0.6f, 0.7f, 1.4f, 1.6f }, false, s_water,
            ("_width", 6f), ("_flowSpeed", 3f));

        Spawner(river, new[] { s_lily, s_crate, s_log }, 1f, 18);
        CreateProbe(river, o + knots[2], true);
        CreateProbe(river, o + knots[4], true);

        return Station(o + new Vector3(0, 4f, 0), "Rapidos en pendiente",
            "La spline puede subir y bajar: el agua sigue la altura de los knots y el cauce acompana.\n\n" +
            "Con Flow Speed alto + tramos angostos se arman rapidos. Los objetos bajan y se frenan en la " +
            "laguna de abajo.",
            BankSpawn(river, 3f), 75f, 28f, 46f);
    }

    static RiverShowcaseStation BuildLoopStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(-14, 0, 0), new Vector3(0, 0, 11), new Vector3(14, 0, 0), new Vector3(0, 0, -11) };
        SplineRiver river = CreateRiver("Rio - circular", o, knots, new[] { 1f, 1.2f, 1f, 0.8f }, true, s_water,
            ("_width", 5f), ("_flowSpeed", 2.2f), ("_bankWidth", 2f), ("_margin", 2.5f));

        // Isla del centro (el cauce solo cubre el anillo). En "solo agua" no hace falta: hay piso.
        if(!s_waterOnly)
        {
            GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            island.name = "Isla central";
            island.transform.position = o + new Vector3(0f, 0.55f, 0f);
            island.transform.localScale = new Vector3(15f, 0.05f, 10f);
            island.GetComponent<MeshRenderer>().sharedMaterial = s_grass;
        }

        RiverSandboxSpawner spawner = Spawner(river, new[] { s_lily, s_crate, s_log }, 0.8f, 14);
        Set(spawner, ("_neverRecycle", true));

        return Station(o, "Rio circular (spline cerrada)",
            "Si la spline esta cerrada (<b>Closed</b>), el rio no tiene principio ni fin: los objetos dan " +
            "vueltas para siempre. Sirve para fosos, canales o un rio alrededor de una isla.",
            o + new Vector3(0f, 1.2f, 0f), 0f, 62f, 42f);
    }

    static RiverShowcaseStation BuildReverseStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -24), new Vector3(2, 0, 0), new Vector3(0, 0, 24) };
        Vector3 left = o + new Vector3(-9f, 0f, 0f);
        Vector3 right = o + new Vector3(9f, 0f, 0f);

        SplineRiver normal = CreateRiver("Rio - sentido normal", left, knots, new[] { 1f, 1f, 1f }, false, s_water,
            ("_width", 6f), ("_flowSpeed", 1.8f), ("_margin", 3f));
        SplineRiver reversed = CreateRiver("Rio - invertido", right, knots, new[] { 1f, 1f, 1f }, false, s_water,
            ("_width", 6f), ("_flowSpeed", 1.8f), ("_margin", 3f), ("_reverseFlow", true));

        for(int i = 0; i < 3; i++)
        {
            CreateProbe(normal, left + knots[i], false);
            CreateProbe(reversed, right + knots[i], false);
        }

        Spawner(normal, new[] { s_lily, s_crate }, 1.4f, 10);
        RiverSandboxSpawner back = Spawner(reversed, new[] { s_lily, s_crate }, 1.4f, 10);
        Set(back, ("_spawnAtEnd", true));

        Label("Normal", left + new Vector3(0f, 3f, -26f), 14f);
        Label("Reverse Flow", right + new Vector3(0f, 3f, -26f), 14f);

        return Station(o, "Corriente invertida",
            "Misma spline, dos rios. El de la derecha tiene <b>Reverse Flow</b>: el agua corre al reves del " +
            "sentido en que se dibujo la spline, sin tener que redibujarla.",
            BankSpawn(normal, 4f), 0f, 52f, 50f);
    }

    static RiverShowcaseStation BuildNoBedStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -22), new Vector3(0, 0, 22) };
        SplineRiver river = CreateRiver("Rio - sin cauce (terreno propio)", o, knots, new[] { 1f, 1f }, false, s_water,
            ("_width", 6f), ("_flowSpeed", 1.5f), ("_generateBed", false), ("_depth", 1.2f));

        // "Terreno propio": un canal hecho con cubos (podria ser ProBuilder, SplineTerrain, etc).
        var terrain = new GameObject("Terreno propio (cubos)").transform;
        Block(terrain, "Fondo", o + new Vector3(0f, -1.4f, 0f), new Vector3(6f, 0.4f, 46f), s_sand);
        Block(terrain, "Muro izq", o + new Vector3(-13f, -0.35f, 0f), new Vector3(20f, 2.1f, 46f), s_stone);
        Block(terrain, "Muro der", o + new Vector3(13f, -0.35f, 0f), new Vector3(20f, 2.1f, 46f), s_stone);
        Block(terrain, "Tapa inicio", o + new Vector3(0f, -0.35f, -22.5f), new Vector3(6f, 2.1f, 1f), s_stone);
        Block(terrain, "Tapa final", o + new Vector3(0f, -0.35f, 22.5f), new Vector3(6f, 2.1f, 1f), s_stone);

        Spawner(river, new[] { s_lily, s_crate, s_log }, 1.3f, 12);

        return Station(o, "Sin cauce: sobre tu propio terreno",
            "Con <b>Generate Bed</b> apagado, SplineRiver solo genera la superficie del agua. La pones dentro de " +
            "un canal de tu nivel (aca son cubos; puede ser ProBuilder o SplineTerrain).\n\n" +
            "La espuma de orilla, el color por profundidad y las causticas salen solos porque el shader lee " +
            "la profundidad de la escena: no hace falta pintar nada.",
            o + new Vector3(-6f, 1.4f, -8f), 25f, 42f, 34f);
    }

    static RiverShowcaseStation BuildMaterialsStation(Vector3 o)
    {
        (string name, Material material, float speed)[] variants =
        {
            ("Tropical", s_water, 1.2f),
            ("Pantano", CreateSwampMaterial(), 0.3f),
            ("Cristalina", CreateClearMaterial(), 1.6f),
            ("Sin refraccion", CreateNoRefractionMaterial(), 1.2f)
        };

        Vector3[] knots = { new Vector3(0, 0, -18), new Vector3(2, 0, 0), new Vector3(0, 0, 18) };
        var rng = new System.Random(5);
        for(int i = 0; i < variants.Length; i++)
        {
            Vector3 origin = o + new Vector3(-27f + i * 18f, 0f, 0f);
            SplineRiver river = CreateRiver("Rio - " + variants[i].name, origin, knots, new[] { 1f, 1f, 1f }, false,
                variants[i].material, ("_width", 7f), ("_flowSpeed", variants[i].speed), ("_margin", 2f));
            Label(variants[i].name, origin + new Vector3(0f, 3f, -20f), 12f);

            for(int r = 0; r < 2; r++)
            {
                GameObject rock = RiverSandboxBuilder.InstantiateProp(r == 0 ? "R_Small_lp" : "R_Short_lp", river.transform);
                if(rock == null)
                    continue;
                RiverSandboxBuilder.FitToSize(rock, 1.6f + r * 0.5f);
                rock.transform.position = river.PointAt(10f + r * 14f, r == 0 ? 0.3f : 0.7f) + Vector3.down * 0.4f;
                rock.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }

            for(int l = 0; l < 3; l++)
            {
                var pad = (GameObject)PrefabUtility.InstantiatePrefab(s_lily);
                pad.transform.SetParent(river.transform, true);
                pad.transform.position = river.PointAt(4f + l * 6f, 0.25f + l * 0.25f) + Vector3.up * 0.1f;
            }
        }

        return Station(o, "Variantes de material",
            "Mismo shader (<b>Gekko/River Stylized</b>), distintos valores en el material:\n" +
            "  - <b>Tropical</b>: M_RiverWater (el de las referencias).\n" +
            "  - <b>Pantano</b>: verde, opaco, lento, sin causticas.\n" +
            "  - <b>Cristalina</b>: casi transparente, causticas fuertes.\n" +
            "  - <b>Sin refraccion</b>: destildada Refraction. No usa Opaque Texture (mas barato / mobile); " +
            "la transparencia sale del alpha de los colores.",
            o + new Vector3(-33f, 1.4f, -10f), 0f, 48f, 62f);
    }

    static RiverShowcaseStation BuildApiStation(Vector3 o)
    {
        Vector3[] knots = { new Vector3(0, 0, -20), new Vector3(8, 0, -6), new Vector3(-6, 0, 6), new Vector3(4, 0, 20) };
        SplineRiver river = CreateRiver("Rio - campo de corriente", o, knots, new[] { 1.2f, 0.6f, 1.5f, 0.8f }, false, s_water,
            ("_width", 7f), ("_flowSpeed", 1.6f));

        var field = new GameObject("Campo de flechas (RiverProbe)").transform;
        for(float d = 1.5f; d < river.TotalLength - 1f; d += 2.5f)
        {
            foreach(float lateral in new[] { 0.15f, 0.5f, 0.85f })
                CreateProbe(river, river.PointAt(d, lateral), false).transform.SetParent(field, true);
        }

        return Station(o, "API: Sample, AddSplash, Splashed",
            "Cada flecha es un <b>RiverProbe</b> que llama a <b>river.Sample(posicion, out s)</b> y dibuja " +
            "s.FlowVelocity. Asi se ve el \"campo\" de corriente: mas rapido en lo angosto.\n\n" +
            "Desde codigo:\n" +
            "  river.Sample(pos, out var s)  -> s.Inside, s.SurfaceY, s.FlowVelocity\n" +
            "  river.AddSplash(pos, fuerza)  -> onda + evento\n" +
            "  river.Splashed += (pos, fuerza) => { sonido / particulas }\n" +
            "  river.PointAt(distancia, lateral01)  -> punto sobre el agua",
            BankSpawn(river, 4f), 0f, 72f, 46f);
    }

    // ------------------------------------------------------------------ piezas

    static SplineRiver CreateRiver(string name, Vector3 origin, Vector3[] knots, float[] widths, bool closed,
        Material water, params (string, object)[] props)
    {
        var go = new GameObject(name, typeof(SplineContainer));
        go.transform.position = origin;
        Spline spline = go.GetComponent<SplineContainer>().Spline;
        spline.Clear();
        foreach(Vector3 knot in knots)
            spline.Add(knot, TangentMode.AutoSmooth);
        spline.Closed = closed;

        SplineRiver river = go.AddComponent<SplineRiver>();
        var so = new SerializedObject(river);
        so.FindProperty("_container").objectReferenceValue = go.GetComponent<SplineContainer>();
        so.FindProperty("_waterMaterial").objectReferenceValue = water;
        so.FindProperty("_bedMaterial").objectReferenceValue = s_bed;
        SerializedProperty list = so.FindProperty("_knotWidthMultipliers");
        list.arraySize = widths.Length;
        for(int i = 0; i < widths.Length; i++)
            list.GetArrayElementAtIndex(i).floatValue = widths[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Set(river, props);
        if(s_waterOnly)
            Set(river, ("_generateBed", false));
        river.Rebuild();
        return river;
    }

    static RiverShowcaseStation Station(Vector3 focus, string title, string description, Vector3 ballSpawn,
        float yaw, float pitch, float distance)
    {
        var go = new GameObject("Estacion - " + title);
        go.transform.position = focus;
        var station = go.AddComponent<RiverShowcaseStation>();
        station.Title = title;
        station.Description = description;
        station.Yaw = yaw;
        station.Pitch = pitch;
        station.Distance = distance;

        var spawn = new GameObject("Spawn pelota").transform;
        spawn.SetParent(go.transform, false);
        spawn.position = ballSpawn;
        station.BallSpawn = spawn;

        return station;
    }

    static TextMeshPro Label(string text, Vector3 position, float fontSize)
    {
        var go = new GameObject("Cartel - " + text);
        go.transform.position = position;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(40f, 6f);
        go.AddComponent<RiverShowcaseBillboard>();
        return tmp;
    }

    static RiverProbe CreateProbe(SplineRiver river, Vector3 position, bool withLabel)
    {
        var go = new GameObject("Sonda de corriente");
        go.transform.position = position;
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = s_arrow;
        line.widthMultiplier = withLabel ? 0.16f : 0.11f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        var probe = go.AddComponent<RiverProbe>();
        TextMeshPro label = withLabel ? Label("", position + Vector3.up, 9f) : null;
        if(label != null)
            label.transform.SetParent(go.transform, true);
        Set(probe, ("_river", river), ("_label", label), ("_arrowScale", withLabel ? 0.9f : 0.6f));
        probe.Refresh();
        return probe;
    }

    static RiverSandboxSpawner Spawner(SplineRiver river, GameObject[] prefabs, float interval, int maxAlive)
    {
        var spawner = new GameObject("Spawner").AddComponent<RiverSandboxSpawner>();
        spawner.transform.SetParent(river.transform, false);
        Set(spawner, ("_river", river), ("_prefabs", (Object[])prefabs), ("_interval", interval), ("_maxAlive", maxAlive));
        return spawner;
    }

    static Vector3 BankSpawn(SplineRiver river, float distance)
    {
        return RiverSandboxBuilder.BankPoint(river, distance, -1, 3f) + Vector3.up * 1.4f;
    }

    static void Decorate(SplineRiver river, int count, int seed)
    {
        var rng = new System.Random(seed);
        string[] props = { "Pine_Tree", "Roble_Tree", "Bush_1", "Bush_2", "R_Short_lp" };
        float step = river.TotalLength / count;
        for(int i = 0; i < count; i++)
        {
            string name = props[rng.Next(props.Length)];
            GameObject prop = RiverSandboxBuilder.InstantiateProp(name, river.transform);
            if(prop == null)
                continue;
            bool small = name.StartsWith("Bush") || name.StartsWith("R_");
            RiverSandboxBuilder.FitToSize(prop, small ? 1.6f : 7f + (float)rng.NextDouble() * 2f);
            int side = i % 2 == 0 ? 1 : -1;
            prop.transform.position = RiverSandboxBuilder.BankPoint(river, step * (i + 0.5f), side, 5f + (float)rng.NextDouble() * 2.5f)
                + Vector3.up * (s_waterOnly ? -1f : RiverSandboxBuilder.BankHeight);
            prop.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
        }
    }

    static void Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = center;
        cube.transform.localScale = size;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static RiverSandboxBall CreateBall(Vector3 position)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Pelota de prueba (WASD / Espacio / R)";
        ball.transform.position = position;
        ball.GetComponent<MeshRenderer>().sharedMaterial =
            RiverSandboxBuilder.CreateLitMaterial("M_River_Ball", new Color(1f, 0.45f, 0.2f));

        var rb = ball.AddComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Set(ball.AddComponent<RiverFloater>(), ("_buoyancy", 1.5f), ("_halfHeight", 0.5f), ("_spin", 0f), ("_currentFollow", 0.8f));
        return ball.AddComponent<RiverSandboxBall>();
    }

    static void SetupCamera(Scene scene, RiverShowcaseStation[] stations, RiverSandboxBall ball)
    {
        Camera cam = RiverSandboxBuilder.FindInScene<Camera>(scene);
        cam.farClipPlane = 260f;
        cam.fieldOfView = 50f;
        var controller = cam.gameObject.AddComponent<RiverShowcaseCamera>();
        Set(controller, ("_stations", (Object[])stations), ("_ball", ball),
            ("_dropPrefabs", new Object[] { s_lily, s_crate, s_log, s_heavy }));

        RiverShowcaseStation first = stations[0];
        Quaternion rotation = Quaternion.Euler(first.Pitch, first.Yaw, 0f);
        cam.transform.SetPositionAndRotation(first.transform.position - rotation * Vector3.forward * first.Distance, rotation);
    }

    // ------------------------------------------------------------------ assets

    static void CreateAssets()
    {
        s_water = RiverSandboxBuilder.CreateWaterMaterial();
        s_bed = RiverSandboxBuilder.CreateBedMaterial();
        s_grass = RiverSandboxBuilder.CreateLitMaterial("M_River_Grass", new Color(0.36f, 0.55f, 0.27f));
        s_stone = RiverSandboxBuilder.CreateLitMaterial("M_River_Stone", new Color(0.56f, 0.56f, 0.6f));
        s_sand = RiverSandboxBuilder.CreateLitMaterial("M_River_Sand", new Color(0.9f, 0.82f, 0.64f));
        s_arrow = CreateParticleMaterial("M_River_Arrow", null);
        s_splash = CreateParticleMaterial("M_River_Splash", CreateDotTexture());

        Material green = RiverSandboxBuilder.CreateLitMaterial("M_River_LilyPad", new Color(0.36f, 0.66f, 0.28f));
        Material flower = RiverSandboxBuilder.CreateLitMaterial("M_River_Flower", new Color(1f, 0.93f, 0.95f));
        Material wood = RiverSandboxBuilder.CreateLitMaterial("M_River_Wood", new Color(0.55f, 0.36f, 0.2f));
        Material darkStone = RiverSandboxBuilder.CreateLitMaterial("M_River_DarkStone", new Color(0.3f, 0.31f, 0.34f));
        Material heavy = RiverSandboxBuilder.CreateLitMaterial("M_River_Heavy", new Color(0.62f, 0.62f, 0.66f));

        s_lily = RiverSandboxBuilder.CreateLilyPadPrefab(green, flower);
        s_crate = RiverSandboxBuilder.CreateCratePrefab(wood);
        s_log = RiverSandboxBuilder.CreateLogPrefab(wood);

        GameObject sinker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sinker.name = "RiverSinkingStone";
        sinker.transform.localScale = new Vector3(0.7f, 0.5f, 0.6f);
        sinker.GetComponent<MeshRenderer>().sharedMaterial = darkStone;
        RiverSandboxBuilder.AddFloaterBody(sinker, 2f, 0.25f, 0.55f);
        s_sinker = RiverSandboxBuilder.SavePrefab(sinker, "RiverSinkingStone");

        GameObject heavyBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        heavyBall.name = "RiverHeavyBall";
        heavyBall.transform.localScale = Vector3.one * 0.6f;
        heavyBall.GetComponent<MeshRenderer>().sharedMaterial = heavy;
        var rb = heavyBall.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        s_heavy = RiverSandboxBuilder.SavePrefab(heavyBall, "RiverHeavyBall");
    }

    static Material CreateWaterVariant(string name)
    {
        Material material = RiverSandboxBuilder.LoadOrCreateMaterial(name, Shader.Find("Gekko/River Stylized"));
        material.CopyPropertiesFromMaterial(s_water);
        material.renderQueue = -1;
        return material;
    }

    static Material CreateSwampMaterial()
    {
        Material m = CreateWaterVariant("M_RiverWater_Pantano");
        m.SetColor("_ShallowColor", new Color(0.55f, 0.6f, 0.3f, 0.5f));
        m.SetColor("_DeepColor", new Color(0.13f, 0.23f, 0.09f, 0.97f));
        m.SetFloat("_DepthMaxDistance", 1.2f);
        m.SetColor("_FoamColor", new Color(0.9f, 0.93f, 0.75f, 1f));
        m.SetColor("_ReflectionColor", new Color(0.7f, 0.8f, 0.6f, 1f));
        m.SetFloat("_SurfaceFoamStrength", 0.2f);
        m.SetFloat("_CausticsStrength", 0.1f);
        m.SetFloat("_SparkleStrength", 0.25f);
        m.SetFloat("_NormalStrength", 0.35f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CreateClearMaterial()
    {
        Material m = CreateWaterVariant("M_RiverWater_Cristalina");
        m.SetColor("_ShallowColor", new Color(0.6f, 1f, 0.95f, 0.03f));
        m.SetColor("_DeepColor", new Color(0.1f, 0.55f, 0.78f, 0.7f));
        m.SetFloat("_DepthMaxDistance", 3.5f);
        m.SetFloat("_ShallowTint", 0.2f);
        m.SetFloat("_CausticsStrength", 1.3f);
        m.SetFloat("_CausticsDepthFade", 3.5f);
        m.SetFloat("_SurfaceFoamStrength", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CreateNoRefractionMaterial()
    {
        Material m = CreateWaterVariant("M_RiverWater_SinRefraccion");
        m.SetFloat("_Refraction", 0f);
        m.DisableKeyword("_REFRACTION_ON");
        m.SetColor("_ShallowColor", new Color(0.36f, 0.93f, 0.86f, 0.35f));
        m.SetColor("_DeepColor", new Color(0.02f, 0.36f, 0.66f, 0.92f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // Particles/Unlit usa el color de vertice: sirve para flechas (LineRenderer) y gotitas.
    static Material CreateParticleMaterial(string name, Texture2D texture)
    {
        Material m = RiverSandboxBuilder.LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", Color.white);
        if(texture != null)
        {
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D CreateDotTexture()
    {
        string path = TexturesFolder + "/T_RiverDot.png";
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, d < 0.85f ? 1f : 0f));
            }
        }
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ utilidades

    static void Set(Object target, params (string name, object value)[] props)
    {
        var so = new SerializedObject(target);
        foreach((string name, object value) in props)
        {
            SerializedProperty p = so.FindProperty(name);
            if(p == null)
            {
                Debug.LogWarning("[RiverShowcaseBuilder] No existe la propiedad " + name + " en " + target.GetType().Name);
                continue;
            }

            switch(value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Object[] array: RiverSandboxBuilder.SetArray(p, array); break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
