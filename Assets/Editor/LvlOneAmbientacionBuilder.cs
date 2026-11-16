using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ambienta LvlOne: post-proceso propio del nivel (VP_LvlOne) y un rig de partículas ambientales que sigue a la cámara
// (polen, luciérnagas y hojas cayendo), para que haya ambiente en todo el nivel y no solo en las cajas de AmbientPS.
//
// Se puede volver a correr: el rig "Ambientacion LvlOne" se borra y se rearma, y el perfil se actualiza en el lugar
// (si se ajustó algo a mano en VP_LvlOne, volver a correr el menú lo pisa con estos valores).
// No guarda la escena: queda marcada como modificada para revisarla y guardarla a mano.
public static class LvlOneAmbientacionBuilder
{
    const string RootName = "Ambientacion LvlOne";
    const string Folder = "Assets/Art/LvlOne/Ambientacion";
    const string ProfilePath = Folder + "/VP_LvlOne.asset";

    [MenuItem("Tools/Gekko/LvlOne/Ambientar (post-proceso + partículas)")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if(scene.name != "LvlOne") return "[LvlOne] Abrí la escena LvlOne primero.";

        EnsureFolder(Folder);

        var log = new System.Text.StringBuilder("[LvlOne] Ambientación lista:");
        PostProceso(log);
        Camara(log);
        Particulas(log);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        log.Append(" Guardá la escena para conservar los cambios.");
        return log.ToString();
    }

    // =====================================================================================================
    #region Post-proceso
    static void PostProceso(System.Text.StringBuilder log)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if(profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        // Tonemapping ACES: contraste "de cine" y altas luces que no se queman con el bloom.
        var tone = Get<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        // ACES oscurece y desatura: se compensa con exposición y saturación. Filtro apenas cálido (selva soleada).
        var ca = Get<ColorAdjustments>(profile);
        ca.postExposure.Override(0.25f);
        ca.contrast.Override(14f);
        ca.saturation.Override(10f);
        ca.colorFilter.Override(new Color(1f, 0.98f, 0.94f));
        ca.hueShift.overrideState = false;

        var wb = Get<WhiteBalance>(profile);
        wb.temperature.Override(6f);
        wb.tint.Override(3f);

        // Sombras un poco frías (turquesa, combina con la niebla azul) y luces cálidas: separa planos sin cambiar
        // los colores de los assets.
        var smh = Get<ShadowsMidtonesHighlights>(profile);
        smh.shadows.Override(new Vector4(0.9f, 1f, 1.1f, -0.03f));
        smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        smh.highlights.Override(new Vector4(1.06f, 1.02f, 0.94f, 0.02f));

        // Bloom suave: hace brillar luciérnagas, núcleos y partículas HDR sin lavar toda la imagen.
        var bloom = Get<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.75f);
        bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(1f, 0.95f, 0.85f));
        bloom.highQualityFiltering.Override(true);

        var vig = Get<Vignette>(profile);
        vig.color.Override(new Color(0.04f, 0.1f, 0.12f));
        vig.center.Override(new Vector2(0.5f, 0.5f));
        vig.intensity.Override(0.26f);
        vig.smoothness.Override(0.42f);
        vig.rounded.Override(false);

        // Toque mínimo de aberración en los bordes: le da textura de lente sin que se note como efecto.
        var chroma = Get<ChromaticAberration>(profile);
        chroma.intensity.Override(0.06f);

        EditorUtility.SetDirty(profile);

        // Volume global de la escena (prioridad baja: el Volume corrupto de AtmosferaZonas, prioridad 5, se mezcla
        // encima al entrar en una zona corrupta).
        Volume vol = null;
        foreach(var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if(v.name == "Global Volume") { vol = v; break; }

        if(vol == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Global Volume LvlOne");
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
        }

        Undo.RecordObject(vol, "Perfil LvlOne");
        vol.sharedProfile = profile;
        vol.weight = 1f;
        vol.enabled = true;
        vol.gameObject.SetActive(true);

        log.Append(" post-proceso VP_LvlOne en 'Global Volume';");
    }

    // Devuelve el override del perfil o lo crea como sub-asset (si no se agrega al asset se pierde al recargar).
    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if(!profile.TryGet(out T comp))
        {
            comp = profile.Add<T>();
            comp.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(comp, profile);
        }

        comp.active = true;
        return comp;
    }
    #endregion

    // =====================================================================================================
    #region Cámara
    // SMAA saca el serrucho de los bordes (la cámara estaba sin antialiasing) y asegura que renderice post-proceso.
    static void Camara(System.Text.StringBuilder log)
    {
        var cam = Camera.main;
        if(cam == null) { log.Append(" (no encontré Main Camera)"); return; }

        var data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Camara LvlOne");
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        EditorUtility.SetDirty(data);
        log.Append(" cámara con SMAA;");
    }
    #endregion

    // =====================================================================================================
    #region Partículas
    static void Particulas(System.Text.StringBuilder log)
    {
        var old = GameObject.Find(RootName);
        if(old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Ambientacion LvlOne");

        var rig = Child(root, "ParticulasAmbiente (sigue a la cámara)");
        rig.AddComponent<AmbientParticlesFollow>();
        if(Camera.main != null) rig.transform.position = Camera.main.transform.position;

        // Polen / motas de luz: muchas, chiquitas y suaves, flotando con ruido.
        var polen = Sistema(rig, "Polen", MatAditivo("M_LvlOne_Polen", new Color(1.3f, 1.25f, 1.05f, 1f)),
            new Vector3(44f, 18f, 44f), Vector3.zero, 14f, 0.025f, 0.07f, 6f, 11f, 200, 0.012f);
        Color2(polen, new Color(1f, 0.96f, 0.8f, 0.3f), new Color(0.85f, 1f, 0.85f, 0.2f));
        Ruido(polen, 0.3f, 0.2f);
        Parpadeo(polen, false);

        // Luciérnagas: muy pocas y chiquitas, apenas sobre el umbral del bloom (un brillito, no una bola de luz).
        // Parpadeo suave: nunca se apagan del todo entre pulsos.
        var luc = Sistema(rig, "Luciernagas", MatAditivo("M_LvlOne_Luciernagas", new Color(1.15f, 1.4f, 0.6f, 1f)),
            new Vector3(36f, 6f, 36f), new Vector3(0f, -3f, 0f), 2.5f, 0.05f, 0.09f, 6f, 10f, 40, 0.015f);
        Color2(luc, new Color(0.9f, 1f, 0.45f, 0.85f), new Color(1f, 0.85f, 0.35f, 0.75f));
        Ruido(luc, 0.7f, 0.25f);
        Parpadeo(luc, true);

        // Hojas: de a una cada tanto, cayendo lento y girando; colores apagados para que no compitan con el escenario.
        var hojas = Sistema(rig, "Hojas", MatHojas(), new Vector3(46f, 2f, 46f), new Vector3(0f, 14f, 0f),
            1.5f, 0.15f, 0.26f, 12f, 16f, 40, 0.035f);
        Color2(hojas, new Color(0.38f, 0.52f, 0.24f, 1f), new Color(0.62f, 0.58f, 0.28f, 1f));
        var main = hojas.main;
        main.gravityModifier = 0.035f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rot = hojas.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        rot.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
        rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        Ruido(hojas, 0.6f, 0.15f);
        var hr = hojas.GetComponent<ParticleSystemRenderer>();
        hr.alignment = ParticleSystemRenderSpace.Local;
        Parpadeo(hojas, false);

        foreach(var ps in rig.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);

        log.Append(" partículas (polen, luciérnagas, hojas) siguiendo a la cámara;");
    }

    static ParticleSystem Sistema(GameObject parent, string name, Material mat, Vector3 caja, Vector3 offset,
        float rate, float sMin, float sMax, float vidaMin, float vidaMax, int max, float maxPantalla)
    {
        var go = Child(parent, name);
        go.transform.localPosition = offset;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(sMin, sMax);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = caja;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.minParticleSize = 0f;
        // Tope de tamaño en pantalla (fracción del alto): una partícula que pasa pegada a la cámara no se vuelve una
        // mancha gigante. Lo que molesta es el área que ocupan en pantalla, no la cantidad.
        r.maxParticleSize = maxPantalla;
        return ps;
    }

    static void Color2(ParticleSystem ps, Color a, Color b)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
    }

    static void Ruido(ParticleSystem ps, float fuerza, float frecuencia)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = fuerza;
        noise.frequency = frecuencia;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    // Aparece y se va suave; con parpadeo, el alfa sube y baja varias veces (luciérnagas).
    static void Parpadeo(ParticleSystem ps, bool parpadea)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var colores = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        g.SetKeys(colores, parpadea
            ? new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.18f), new GradientAlphaKey(0.4f, 0.4f),
                new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.45f, 0.8f), new GradientAlphaKey(0f, 1f)
            }
            : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
    #endregion

    // =====================================================================================================
    #region Materiales
    // Copia del material aditivo de las esporas (URP Particles/Unlit, ya probado en el proyecto) con otro color.
    static Material MatAditivo(string nombre, Color color)
    {
        string p = Folder + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m == null)
        {
            AssetDatabase.CopyAsset("Assets/_Gekko/Purification/Materials/M_EsporasCorruptas.mat", p);
            m = AssetDatabase.LoadAssetAtPath<Material>(p);
        }

        m.SetColor("_BaseColor", color);
        FadeCercaDeCamara(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Hoja recortada por alfa (T_Particleleaves, gris para teñirla con el color de la partícula), de las dos caras.
    static Material MatHojas()
    {
        string p = Folder + "/M_LvlOne_Hojas.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, p);
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Effects/Particles/Materials&Textures/T_Particleleaves.png");
        m.SetTexture("_BaseMap", tex);
        if(m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.45f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ZWrite", 1f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        FadeCercaDeCamara(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Las partículas que pasan pegadas a la cámara se desvanecen en vez de tapar la pantalla.
    static void FadeCercaDeCamara(Material m)
    {
        const float cerca = 1f, lejos = 4f;
        m.SetFloat("_CameraFadingEnabled", 1f);
        m.SetFloat("_CameraNearFadeDistance", cerca);
        m.SetFloat("_CameraFarFadeDistance", lejos);
        m.SetVector("_CameraFadeParams", new Vector4(cerca, 1f / (lejos - cerca), 0f, 0f));
        m.EnableKeyword("_FADING_ON");
    }
    #endregion

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
