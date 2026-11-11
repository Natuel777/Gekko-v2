using System.Collections.Generic;
using System.Linq;
using Gekko.GrassSandbox;
using Gekko.PaintTools;
using Gekko.Purification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ambienta la escena Aldea con estética de atardecer mágico (skybox, luz, niebla, post, partículas, faroles,
// pasto y vegetación) usando los assets que ya tiene el proyecto, y arma las 5 zonas de purificación que usa
// AldeaIntroSequence (una por nivel superado).
//
// Todo lo nuevo queda bajo el objeto "Ambientacion Aldea": volver a correr el menú lo borra y lo rearma igual
// (el pasto y la vegetación salen de una semilla fija). Los props que ya estaban en la escena (casas flor, tronco,
// lámpara, correo, bebedero, piedra) se reubican y se les asignan materiales con purificación.
public static class AldeaAmbientacionBuilder
{
    const string RootName = "Ambientacion Aldea";
    const string Folder = "Assets/Art/Aldea";
    const string MatFolder = Folder + "/Materials";
    const int TempLayer = 31;

    // Centro de la plaza (donde mira el plano general).
    static readonly Vector2 Plaza = new Vector2(-68f, 6f);

    class ZonaDef
    {
        public string id, nombre;
        public Color color;
        public (Vector2 xz, float r)[] nodos;
        public Vector3 camDir; // hacia dónde mira la cámara del plano de la zona (horizontal)
        public int casas, faroles, correos, bebederos, piedras;
    }

    // Zonas siguiendo las mesetas de la isla (alturas aprox.): sur y≈-4, plaza y≈0..3, isla del árbol y≈2,
    // orilla este y≈4..10, meseta oeste y≈17 y meseta norte y≈13..16.
    // casas = casas nuevas (copias de casaFlor); el resto son cantidades de props que se ubican solos en la zona.
    static readonly ZonaDef[] Zonas =
    {
        new ZonaDef { id = "aldea_1_entrada", nombre = "Entrada sur", color = new Color(0.95f, 0.55f, 0.3f),
            nodos = new[] { (new Vector2(-78f, -17f), 10f), (new Vector2(-62f, -17f), 10f) }, camDir = new Vector3(0f, 0f, -1f),
            casas = 1, faroles = 2, correos = 1 },
        new ZonaDef { id = "aldea_2_plaza", nombre = "Casas de la plaza", color = new Color(0.9f, 0.4f, 0.8f),
            nodos = new[] { (new Vector2(-74f, 3f), 11f) }, camDir = new Vector3(-1f, 0f, 0f),
            faroles = 2, bebederos = 1 },
        new ZonaDef { id = "aldea_3_arbol", nombre = "Isla del árbol", color = new Color(0.35f, 0.85f, 0.95f),
            nodos = new[] { (new Vector2(-58f, 11f), 7f), (new Vector2(-55f, -4f), 7f) }, camDir = new Vector3(0.3f, 0f, 1f),
            faroles = 2, piedras = 1 },
        new ZonaDef { id = "aldea_4_orilla_este", nombre = "Orilla este", color = new Color(0.5f, 0.9f, 0.45f),
            nodos = new[] { (new Vector2(-46f, 28f), 12f) }, camDir = new Vector3(1f, 0f, 0.3f),
            casas = 1, faroles = 2, correos = 1 },
        new ZonaDef { id = "aldea_5_mesetas", nombre = "Mesetas y cascada", color = new Color(0.4f, 0.55f, 1f),
            nodos = new[] { (new Vector2(-92f, 16f), 13f), (new Vector2(-77f, 51f), 11f) }, camDir = new Vector3(-0.6f, 0f, 0.8f),
            casas = 2, faroles = 3, bebederos = 1 },
    };

    // Orden en que se purifican (índices de Zonas): al terminar el nivel 1 se purifica la plaza.
    static readonly int[] OrdenPurificacion = { 1, 0, 2, 3, 4 };

    static System.Random _rng;
    static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    [MenuItem("Tools/Gekko/Aldea/Ambientar escena (atardecer + 5 zonas)")]
    public static void BuildMenu() => Debug.Log(Build());

    [MenuItem("Tools/Gekko/Aldea/Resetear progreso de purificación")]
    static void ResetMenu()
    {
        AldeaIntroSequence.ResetearProgreso();
        Debug.Log("[Aldea] Progreso reseteado: la próxima vez arranca con las 5 zonas corruptas.");
    }

    public static string Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if(scene.name != "Aldea") return "[Aldea] Abrí la escena Aldea primero.";
        _rng = new System.Random(1234);
        var log = new System.Text.StringBuilder();

        Directory(Folder); Directory(MatFolder);

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if(old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager_Move(root, scene);

        // ---------- blockout: se apaga (el piso queda con collider, sin dibujarse, para que Gekko no caiga)
        var blockout = Find(scene, "Blockout");
        if(blockout != null)
            foreach(Transform c in blockout.transform)
            {
                if(c.name == "Blockout_Piso") { c.gameObject.SetActive(true); var mr = c.GetComponent<MeshRenderer>(); if(mr) mr.enabled = false; }
                else c.gameObject.SetActive(false);
            }

        // ---------- suelo para muestrear (colliders temporales de las mallas de la isla)
        var temps = new List<GameObject>();
        foreach(var g in scene.GetRootGameObjects())
        {
            if(!g.activeInHierarchy) continue;
            if(!(g.name.StartsWith("Initial Platform") || g.name.StartsWith("Cascada"))) continue;
            var mf = g.GetComponent<MeshFilter>();
            if(mf == null || mf.sharedMesh == null) continue;
            var t = new GameObject((g.name.StartsWith("Cascada") ? "TMPRIO_" : "TMPSUELO_") + g.name) { hideFlags = HideFlags.HideAndDontSave, layer = TempLayer };
            t.transform.SetPositionAndRotation(g.transform.position, g.transform.rotation);
            t.transform.localScale = g.transform.lossyScale;
            t.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            temps.Add(t);
        }
        Physics.SyncTransforms();

        try
        {
            // ---------- materiales de los props
            var mats = CrearMaterialesProps();
            Dictionary<string, Material> porNombre = MapaMateriales(mats);

            // ---------- purificación
            var purif = Child(root, "Purificacion");
            Child(purif, "PurifyManager").AddComponent<PurifyManager>();
            // Nodos de cada zona: los definidos arriba + nodos de relleno para que no quede piso de la isla fuera de
            // toda zona (lo que no está en ninguna región se ve sano desde el principio). Cada pedazo sin cubrir se
            // suma a la zona más cercana. Tope: 32 nodos por escena (PurifyManager.MaxEsferas).
            var nodosZona = Zonas.Select(z => z.nodos.Select(n => (p: Suelo(n.xz, out _), r: n.r)).ToList()).ToList();
            const float radioRelleno = 8f;
            bool Cubierto(Vector3 p) => nodosZona.Any(l => l.Any(n => (n.p - p).sqrMagnitude <= (n.r - 1f) * (n.r - 1f)));
            var sinCubrir = new List<Vector3>();
            for(float x = -106f; x <= -30f; x += 2.5f)
                for(float z = -30f; z <= 64f; z += 2.5f)
                    if(Piso(new Vector2(x, z), out var hh, 0.6f) && !Cubierto(hh.point)) sinCubrir.Add(hh.point);
            int totalNodos = nodosZona.Sum(l => l.Count);
            int agregados = 0;
            while(totalNodos < PurifyManager.MaxEsferas - 1)
            {
                sinCubrir.RemoveAll(Cubierto);
                if(sinCubrir.Count == 0) break;
                // el punto sin cubrir más cercano a alguna zona: así los nodos crecen desde las zonas hacia afuera
                float mejor = float.MaxValue; int zi = 0; Vector3 punto = default;
                foreach(var p in sinCubrir)
                    for(int i = 0; i < nodosZona.Count; i++)
                        foreach(var n in nodosZona[i])
                        {
                            float d = Vector3.Distance(n.p, p) - n.r;
                            if(d < mejor) { mejor = d; zi = i; punto = p; }
                        }
                // centrar el nodo nuevo un poco hacia adentro del pedazo sin cubrir
                var cerca = sinCubrir.Where(p => (p - punto).sqrMagnitude < radioRelleno * radioRelleno).ToList();
                Vector3 centro = cerca.Aggregate(Vector3.zero, (a, b) => a + b) / cerca.Count;
                nodosZona[zi].Add((centro, radioRelleno));
                totalNodos++; agregados++;
            }
            sinCubrir.RemoveAll(Cubierto);
            log.Append(" Nodos: " + totalNodos + " (" + agregados + " de relleno), piso sin zona: " + sinCubrir.Count + " puntos.");

            var regiones = new List<PurifyRegion>();
            for(int zi = 0; zi < Zonas.Length; zi++)
            {
                var z = Zonas[zi];
                var lista = nodosZona[zi];
                var go = Child(purif, z.id);
                go.transform.position = lista[0].p;
                var region = go.AddComponent<PurifyRegion>();
                var so = new SerializedObject(region);
                so.FindProperty("_regionId").stringValue = z.id;
                so.FindProperty("_nombreVisible").stringValue = z.nombre;
                so.FindProperty("_duracion").floatValue = 4.5f;
                so.FindProperty("_colorGizmo").colorValue = z.color;
                var nodos = so.FindProperty("_nodos");
                nodos.arraySize = lista.Count;
                for(int i = 0; i < lista.Count; i++)
                {
                    var n = Child(go, "Nodo" + (i + 1));
                    n.transform.position = lista[i].p;
                    var e = nodos.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("centro").objectReferenceValue = n.transform;
                    e.FindPropertyRelative("radioMaximo").floatValue = lista[i].r;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                regiones.Add(region);
            }
            PurifyRegion RegionDe(Vector3 p) => PurifyRegion.Buscar(p, regiones);

            // ---------- props
            var props = Child(root, "Props");
            var exclusiones = new List<(Vector2 c, float r)>();
            var lucesPorZona = regiones.ToDictionary(r => r, r => new List<Light>());

            void Registrar(Light l, Vector3 pos)
            {
                var reg = RegionDe(pos);
                if(reg != null) lucesPorZona[reg].Add(l);
            }

            // lo que ya estaba en la escena (casas flor, tronco, árbol) se respeta en su lugar
            var casas = scene.GetRootGameObjects().Where(g => g.name.StartsWith("casaFlor")).ToList();
            var tronco = Find(scene, "tronco");
            if(tronco != null) { AsignarMateriales(tronco, porNombre); var b = Bounds(tronco); exclusiones.Add((new Vector2(b.center.x, b.center.z), 3.5f)); }
            var arbol = Find(scene, "TreeMenu");
            if(arbol != null)
            {
                var b = Bounds(arbol);
                exclusiones.Add((new Vector2(b.center.x, b.center.z), 3.2f));
                var l = Luz(props, "Brillo del arbol", b.center + Vector3.up * 1f, new Color(0.35f, 0.9f, 1f), 5f, 16f);
                Registrar(l, b.center);
            }
            foreach(var casa in casas) { var b = Bounds(casa); exclusiones.Add((new Vector2(b.center.x, b.center.z), 3f)); }

            // Punto libre dentro de la zona: piso plano, lejos del río, del borde y de otros props.
            bool PuntoLibre(ZonaDef z, float sep, out Vector3 punto)
            {
                for(int intento = 0; intento < 400; intento++)
                {
                    var nodo = z.nodos[_rng.Next(z.nodos.Length)];
                    float ang = R(0f, Mathf.PI * 2f), d = R(0.15f, 0.7f) * nodo.r;
                    var xz = nodo.xz + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
                    if(!Piso(xz, out var h, 0.9f) || CercaDelRio(xz, 2f) || Excluido(xz, exclusiones.ToArray(), sep)) continue;
                    if(DistanciaBorde(xz, h.point.y, 3f) <= 3f) continue;
                    punto = h.point;
                    return true;
                }
                punto = default;
                return false;
            }

            var usados = new Dictionary<string, int>();
            GameObject Colocar(string nombre, ZonaDef z, float sep, float exclusion)
            {
                var original = nombre == "casaFlor" ? casas.FirstOrDefault() : Find(scene, nombre);
                if(original == null) { log.Append(" (falta '" + nombre + "')"); return null; }
                if(!PuntoLibre(z, sep, out var p)) { log.Append(" (sin lugar para " + nombre + " en " + z.nombre + ")"); return null; }
                usados.TryGetValue(nombre, out int n);
                usados[nombre] = n + 1;
                // la primera vez se usa el objeto original (lámpara, correo...), después copias. Las casas siempre son copias.
                bool usarOriginal = n == 0 && nombre != "casaFlor";
                var go = usarOriginal ? original : Object.Instantiate(original, props.transform);
                if(!usarOriginal) go.name = nombre + " (aldea " + (n + 1) + ")";
                go.transform.position = p;
                Vector3 mira = Suelo(Plaza, out _) - p; mira.y = 0f;
                go.transform.rotation = nombre == "casaFlor" && mira.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(mira) : Quaternion.Euler(0f, R(0f, 360f), 0f);
                AsignarMateriales(go, porNombre);
                exclusiones.Add((new Vector2(p.x, p.z), exclusion));
                return go;
            }

            foreach(var z in Zonas)
            {
                for(int i = 0; i < z.casas; i++) { var c = Colocar("casaFlor", z, 3.5f, 3f); if(c != null) casas.Add(c); }
                for(int i = 0; i < z.faroles; i++)
                {
                    var go = Colocar("lampara", z, 2.5f, 0.7f);
                    if(go == null) continue;
                    var foco = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "foco_lp");
                    Vector3 p = foco != null ? Bounds(foco.gameObject).center : go.transform.position + Vector3.up * 1.2f;
                    Registrar(Luz(props, "Luz " + go.name, p + Vector3.up * 0.15f, new Color(1f, 0.78f, 0.45f), 3f, 7f), p);
                }
                for(int i = 0; i < z.correos; i++) Colocar("correo", z, 1.5f, 0.7f);
                for(int i = 0; i < z.bebederos; i++) Colocar("bebedero", z, 1.5f, 0.9f);
                for(int i = 0; i < z.piedras; i++) Colocar("piedra", z, 2f, 1.4f);
            }

            foreach(var casa in casas)
            {
                AsignarMateriales(casa, porNombre);
                Vector3 c = Bounds(casa).center;
                // luz cálida en la ventana
                var ventana = casa.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "plano_lp");
                Vector3 lp = ventana != null ? Bounds(ventana.gameObject).center : c;
                var luz = Luz(props, "Luz ventana " + casa.name, lp + (lp - c).normalized * 0.6f, new Color(1f, 0.68f, 0.38f), 2.2f, 5f);
                Registrar(luz, c);
            }

            // ---------- partículas
            var part = Child(root, "Particulas");
            var matLuc = MaterialLuciernagas();
            var matEsp = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Gekko/Purification/Materials/M_EsporasCorruptas.mat");
            for(int i = 0; i < Zonas.Length; i++)
            {
                var z = Zonas[i];
                var zgo = Child(part, z.nombre);
                zgo.transform.position = regiones[i].transform.position;
                var sanas = new List<Object>();
                var corruptas = new List<Object>();
                // un par de sistemas por nodo, a la altura de su meseta
                foreach(var n in z.nodos)
                {
                    var ng = Child(zgo, "Nodo");
                    ng.transform.position = Suelo(n.xz, out _) + Vector3.up * 2.2f;
                    float lado = n.r * 1.5f;
                    sanas.Add(Particulas(ng, "Luciernagas", matLuc, new Vector3(lado, 3.5f, lado), 6f + n.r, new Color(1f, 0.95f, 0.6f), 0.07f, 0.14f, 0.15f));
                    corruptas.Add(Particulas(ng, "Esporas corruptas", matEsp, new Vector3(lado, 4f, lado), 9f + n.r, Color.white, 0.06f, 0.12f, 0.35f));
                }
                var sw = zgo.AddComponent<PurifyParticleSwitch>();
                var so = new SerializedObject(sw);
                so.FindProperty("_regionOverride").objectReferenceValue = regiones[i];
                Set(so.FindProperty("_sanas"), sanas.ToArray());
                Set(so.FindProperty("_corruptas"), corruptas.ToArray());
                so.ApplyModifiedPropertiesWithoutUndo();

                // luces de la zona
                var ls = zgo.AddComponent<PurifyLightSwitch>();
                var so2 = new SerializedObject(ls);
                so2.FindProperty("_regionOverride").objectReferenceValue = regiones[i];
                Set(so2.FindProperty("_sanas"), lucesPorZona[regiones[i]].Cast<Object>().ToArray());
                so2.ApplyModifiedPropertiesWithoutUndo();
            }
            // polvo dorado del atardecer en toda la isla (siempre prendido)
            var polvo = Child(part, "Polvo atardecer");
            polvo.transform.position = new Vector3(-66f, 6f, 16f);
            Particulas(polvo, "Polvo", matLuc, new Vector3(64f, 10f, 86f), 40f, new Color(1f, 0.7f, 0.45f, 0.45f), 0.04f, 0.08f, 0.1f);

            // ---------- pasto y vegetación
            var exclArr = exclusiones.ToArray();
            int briznas = Pasto(root, exclArr);
            int veg = Vegetacion(root, exclArr, log);

            // ---------- cielo, luz, niebla, post
            Atmosfera(scene, root, log);

            // ---------- intro: planos de cámara y zonas
            Intro(scene, regiones, log);

            // cobertura: qué parte del pasto cae dentro de alguna zona
            log.Insert(0, "[Aldea] Listo. Pasto: " + briznas + " briznas. Vegetación: " + veg + " props. ");
        }
        finally
        {
            foreach(var t in temps) Object.DestroyImmediate(t);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // =====================================================================================================
    #region Suelo
    static Vector3 Suelo(Vector2 xz, out RaycastHit hit)
    {
        if(Physics.Raycast(new Vector3(xz.x, 120f, xz.y), Vector3.down, out hit, 300f, 1 << TempLayer, QueryTriggerInteraction.Ignore))
            return hit.point;
        return new Vector3(xz.x, 0f, xz.y);
    }

    // true si es piso (no pared, no río).
    static bool Piso(Vector2 xz, out RaycastHit hit, float minNormal = 0.8f)
    {
        if(!Physics.Raycast(new Vector3(xz.x, 120f, xz.y), Vector3.down, out hit, 300f, 1 << TempLayer, QueryTriggerInteraction.Ignore)) return false;
        if(hit.collider.name.StartsWith("TMPRIO_")) return false;
        return hit.normal.y >= minNormal;
    }

    static bool CercaDelRio(Vector2 xz, float d)
    {
        for(int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            var p = xz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if(Physics.Raycast(new Vector3(p.x, 120f, p.y), Vector3.down, out var h, 300f, 1 << TempLayer) && h.collider.name.StartsWith("TMPRIO_")) return true;
        }
        return false;
    }

    // Distancia al borde del piso (precipicio o pared), muestreando 8 direcciones.
    static float DistanciaBorde(Vector2 xz, float y, float max)
    {
        for(float d = 1f; d <= max; d += 1f)
            for(int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var p = xz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if(!Physics.Raycast(new Vector3(p.x, 120f, p.y), Vector3.down, out var h, 300f, 1 << TempLayer)) return d;
                if(Mathf.Abs(h.point.y - y) > 1.2f) return d;
            }
        return max + 1f;
    }

    static bool Excluido(Vector2 xz, (Vector2 c, float r)[] excl, float extra = 0f)
    {
        foreach(var e in excl) if((xz - e.c).sqrMagnitude < (e.r + extra) * (e.r + extra)) return true;
        return false;
    }
    #endregion

    // =====================================================================================================
    #region Pasto
    static int Pasto(GameObject root, (Vector2 c, float r)[] excl)
    {
        string dataPath = Folder + "/Aldea_Grass.asset";
        var data = AssetDatabase.LoadAssetAtPath<GrassData>(dataPath);
        if(data == null) { data = ScriptableObject.CreateInstance<GrassData>(); AssetDatabase.CreateAsset(data, dataPath); }
        data.Blades.Clear();

        const float paso = 0.3f;
        for(float x = -104f; x <= -30f; x += paso)
            for(float z = -30f; z <= 64f; z += paso)
            {
                var xz = new Vector2(x + R(-0.13f, 0.13f), z + R(-0.13f, 0.13f));
                if(!Piso(xz, out var h)) continue;
                if(Excluido(xz, excl)) continue;
                if(CercaDelRio(xz, 0.9f)) continue;
                data.Blades.Add(new GrassBladeData
                {
                    Position = h.point,
                    Normal = h.normal,
                    Height = R(0.3f, 0.55f),
                    Width = R(0.08f, 0.12f),
                    Yaw = R(0f, 360f),
                    Variation = R(0f, 1f),
                });
            }
        EditorUtility.SetDirty(data);

        string matPath = Folder + "/M_Grass_Aldea.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(mat == null)
        {
            AssetDatabase.CopyAsset("Assets/_GrassSandbox/Materials/M_Grass 1.mat", matPath);
            mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
        mat.SetColor("_TopColor", new Color(0.4f, 0.7f, 0.28f));
        mat.SetColor("_BottomColor", new Color(0.08f, 0.22f, 0.14f));
        mat.SetColor("_VariationColor", new Color(0.28f, 0.62f, 0.46f));
        mat.SetColor("_ShadowTint", new Color(0.5f, 0.38f, 0.62f));
        mat.SetFloat("_Translucency", 0.2f);
        EditorUtility.SetDirty(mat);

        var go = Child(root, "Pasto");
        go.SetActive(false);
        var field = go.AddComponent<GrassField>();
        var so = new SerializedObject(field);
        so.FindProperty("_data").objectReferenceValue = data;
        so.FindProperty("_grassMaterial").objectReferenceValue = mat;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true);
        return data.Count;
    }
    #endregion

    // =====================================================================================================
    #region Vegetación
    static int Vegetacion(GameObject root, (Vector2 c, float r)[] excl, System.Text.StringBuilder log)
    {
        var veg = Child(root, "Vegetacion");
        _obstaculos.Clear();
        var ocupados = new List<(Vector2 c, float r)>();
        int total = 0;

        bool Libre(Vector2 xz, float r)
        {
            foreach(var o in ocupados) if((xz - o.c).sqrMagnitude < (r + o.r) * (r + o.r)) return false;
            return true;
        }

        // Candidatos de piso con su distancia al borde (se calcula una vez).
        var candidatos = new List<(Vector2 xz, RaycastHit h, float borde)>();
        for(float x = -104f; x <= -30f; x += 1.3f)
            for(float z = -30f; z <= 64f; z += 1.3f)
            {
                var xz = new Vector2(x + R(-0.5f, 0.5f), z + R(-0.5f, 0.5f));
                if(!Piso(xz, out var h, 0.7f)) continue;
                if(CercaDelRio(xz, 1.4f)) continue;
                candidatos.Add((xz, h, DistanciaBorde(xz, h.point.y, 7f)));
            }
        // orden al azar estable
        candidatos = candidatos.OrderBy(_ => _rng.Next()).ToList();

        GameObject[] P(params string[] nombres) => nombres.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prop/" + n + ".prefab")).Where(g => g != null).ToArray();

        int Capa(string nombre, GameObject[] protos, float bordeMin, float bordeMax, float radio, float exclExtra, int max, float sMin, float sMax, bool alinear, float yOffset)
        {
            if(protos.Length == 0) { log.Append(" (sin prefabs para " + nombre + ")"); return 0; }
            string path = Folder + "/Aldea_" + nombre + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<ScatterData>(path);
            if(data == null) { data = ScriptableObject.CreateInstance<ScatterData>(); AssetDatabase.CreateAsset(data, path); }
            data.Clear();
            data.Prototypes.Clear();
            data.Prototypes.AddRange(protos);
            int n = 0;
            foreach(var c in candidatos)
            {
                if(n >= max) break;
                if(c.borde < bordeMin || c.borde > bordeMax) continue;
                var xz = c.xz + new Vector2(R(-0.4f, 0.4f), R(-0.4f, 0.4f));
                if(Excluido(xz, excl, exclExtra) || !Libre(xz, radio)) continue;
                if(Mathf.Abs(xz.x - Plaza.x) < 4f && Mathf.Abs(xz.y - Plaza.y) < 4f) continue;
                ocupados.Add((xz, radio));
                float s = R(sMin, sMax);
                if(nombre == "Arboles") _obstaculos.Add((c.h.point, 2.6f * s, 9f * s));
                else if(nombre == "Arbustos") _obstaculos.Add((c.h.point, 1.6f * s * 2f, 3f * s));
                Quaternion yaw = Quaternion.Euler(0f, R(0f, 360f), 0f);
                data.Instances.Add(new ScatterInstance
                {
                    PrototypeIndex = _rng.Next(protos.Length),
                    Position = c.h.point + Vector3.up * yOffset,
                    Rotation = alinear ? Quaternion.FromToRotation(Vector3.up, c.h.normal) * yaw : yaw,
                    Scale = Vector3.one * s,
                });
                n++;
            }
            EditorUtility.SetDirty(data);
            var go = Child(veg, nombre);
            go.SetActive(false);
            var field = go.AddComponent<ScatterField>();
            var so = new SerializedObject(field);
            so.FindProperty("_data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            var falta = field.MissingInstancingMaterials.ToList();
            if(falta.Count > 0) log.Append(" (" + nombre + ": materiales sin GPU Instancing: " + string.Join(", ", falta) + ")");
            return n;
        }

        // Árboles en el anillo del borde (como el bosque que rodea la aldea en la referencia).
        total += Capa("Arboles", P("Abeto", "Pino", "Tronco_1"), 1.5f, 3.5f, 4f, 2.5f, 40, 0.85f, 1.2f, false, -0.1f);
        total += Capa("Arbustos", P("Bush_1", "Bush_2", "Bush_3", "Bush_4"), 0.5f, 2.5f, 1.6f, 1f, 90, 0.4f, 0.65f, false, -0.15f);
        total += Capa("Piedras", P("R_Short_lp", "R_Tall_lp", "R_Small_lp"), 0.5f, 4f, 1.2f, 0.8f, 25, 0.5f, 0.95f, false, -0.1f);
        total += Capa("Hongos", P("Hongo 1", "Hongo 3", "Hongo 4", "Hongo 8", "Hongo 10", "Hongo7"), 1f, 8f, 0.55f, 0.4f, 160, 0.55f, 0.95f, true, 0f);
        total += Capa("Flores", P("Corruption/C_DecoAzul", "Corruption/C_DecoAmarilla", "Corruption/C_Lavandas", "Corruption/C_Margaritas", "Corruption/C_Trebol 4H"),
            2f, 99f, 0.7f, 0.4f, 320, 0.6f, 0.95f, true, 0f);
        return total;
    }
    #endregion

    // =====================================================================================================
    #region Materiales
    static Dictionary<string, Material> CrearMaterialesProps()
    {
        var sh = Shader.Find("Gekko/Purification/SG_PurifyLit");
        Material M(string n, Color c, Color emis, float smooth = 0.25f)
        {
            string p = MatFolder + "/M_Aldea_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if(m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetColor("_EmissionColor", emis);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            m.globalIlluminationFlags = emis.maxColorComponent > 0f ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }
        var k = Color.black;
        return new Dictionary<string, Material>
        {
            ["Petalo"] = M("Petalo", new Color(0.82f, 0.5f, 0.86f), k, 0.35f),
            ["PetaloClaro"] = M("PetaloClaro", new Color(0.97f, 0.84f, 0.95f), k, 0.35f),
            ["Tallo"] = M("Tallo", new Color(0.33f, 0.6f, 0.32f), k),
            ["Madera"] = M("Madera", new Color(0.56f, 0.36f, 0.22f), k, 0.15f),
            ["MaderaOscura"] = M("MaderaOscura", new Color(0.34f, 0.21f, 0.14f), k, 0.15f),
            ["Cuerpo"] = M("Cuerpo", new Color(0.95f, 0.86f, 0.72f), k),
            ["Vidrio"] = M("Vidrio", new Color(1f, 0.85f, 0.5f), new Color(1.6f, 1.05f, 0.45f), 0.6f),
            ["Foco"] = M("Foco", new Color(1f, 0.88f, 0.5f), new Color(1.3f, 0.9f, 0.3f), 0.5f),
            ["Hoja"] = M("Hoja", new Color(0.36f, 0.68f, 0.4f), k, 0.3f),
            ["HongoRojo"] = M("HongoRojo", new Color(0.86f, 0.26f, 0.24f), k, 0.35f),
            ["HongoCrema"] = M("HongoCrema", new Color(0.93f, 0.72f, 0.5f), k, 0.3f),
            ["Piedra"] = M("Piedra", new Color(0.5f, 0.6f, 0.56f), k, 0.1f),
            ["PetaloAmarillo"] = M("PetaloAmarillo", new Color(1f, 0.82f, 0.32f), new Color(0.25f, 0.18f, 0.02f), 0.35f),
            ["Buzon"] = M("Buzon", new Color(0.78f, 0.32f, 0.26f), k, 0.2f),
        };
    }

    static Dictionary<string, Material> MapaMateriales(Dictionary<string, Material> m) => new Dictionary<string, Material>
    {
        // casa flor
        ["cilindro_lp"] = m["Cuerpo"], ["door_lp"] = m["MaderaOscura"], ["petaloDelantero_lp"] = m["PetaloClaro"],
        ["petalosCostados_lp"] = m["Petalo"], ["petaloTrasero_lp"] = m["Petalo"], ["tallo_lp"] = m["Tallo"],
        ["plano_lp"] = m["Vidrio"], ["tablaVentanaAbajo_lp"] = m["Madera"], ["tablaVentanaArriba_lp"] = m["Madera"], ["Ventana_lp"] = m["Madera"],
        // farol
        ["foco_lp"] = m["Foco"], ["focoAbajo_lp"] = m["Foco"], ["petalos_lp"] = m["Petalo"], ["talloLampara_lp"] = m["Tallo"],
        // correo
        ["casitaCorreo_lp"] = m["Buzon"], ["HongoCorreo_lp"] = m["HongoRojo"], ["troncoCorreo_lp"] = m["Madera"],
        // bebedero
        ["tronco_lp"] = m["Madera"],
        // tronco-casa
        ["hongo1_lp"] = m["HongoCrema"], ["hongo2_lp"] = m["HongoCrema"], ["hongo3_lp"] = m["HongoCrema"],
        ["troncoAdentro_lp"] = m["MaderaOscura"], ["troncoAfuera_lp"] = m["Madera"],
        // piedra
        ["PiedraChiquita1_lp"] = m["Piedra"], ["PiedraChiquita2_lp"] = m["Piedra"], ["PiedraMayor_lp"] = m["Piedra"],
        // prefijos (hoja1_lp.., petalo1_lp..)
        ["hoja"] = m["Hoja"], ["petalo"] = m["PetaloAmarillo"],
    };

    static void AsignarMateriales(GameObject go, Dictionary<string, Material> mapa)
    {
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m;
            if(!mapa.TryGetValue(r.name, out m))
            {
                if(r.name.StartsWith("hoja")) m = mapa["hoja"];
                else if(r.name.StartsWith("petalo") && r.name != "petalos_lp") m = mapa["petalo"];
            }
            if(m == null) continue;
            var arr = new Material[r.sharedMaterials.Length];
            for(int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }
    }

    static Material MaterialLuciernagas()
    {
        string p = MatFolder + "/M_Aldea_Luciernagas.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m == null)
        {
            AssetDatabase.CopyAsset("Assets/_Gekko/Purification/Materials/M_EsporasCorruptas.mat", p);
            m = AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        m.SetColor("_BaseColor", new Color(2.6f, 2.1f, 0.8f, 1f));
        if(m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(1.6f, 1.25f, 0.4f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }
    #endregion

    // =====================================================================================================
    #region Partículas y luces
    static ParticleSystem Particulas(GameObject parent, string name, Material mat, Vector3 caja, float rate, Color color, float sMin, float sMax, float vel)
    {
        var go = Child(parent, name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, vel);
        main.startSize = new ParticleSystem.MinMaxCurve(sMin, sMax);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.gravityModifier = -0.005f;
        var em = ps.emission;
        em.rateOverTime = rate;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = caja;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play(true);
        return ps;
    }

    static Light Luz(GameObject parent, string name, Vector3 pos, Color c, float intensidad, float rango)
    {
        var go = Child(parent, name);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensidad;
        l.range = rango;
        l.shadows = LightShadows.None;
        go.AddComponent<UniversalAdditionalLightData>();
        return l;
    }
    #endregion

    // =====================================================================================================
    #region Atmósfera
    static void Atmosfera(UnityEngine.SceneManagement.Scene scene, GameObject root, System.Text.StringBuilder log)
    {
        // skybox de atardecer (copia del skybox del proyecto, mismo shader)
        string skyPath = Folder + "/M_Skybox_AldeaAtardecer.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(sky == null) { AssetDatabase.CopyAsset("Assets/Art/Materials/Skybox/M_Skybox2.mat", skyPath); sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath); }
        sky.SetColor("_SkyColor", new Color(0.36f, 0.33f, 0.62f));
        sky.SetColor("_HorizonColor", new Color(1f, 0.6f, 0.42f));
        sky.SetColor("_GroundColor", new Color(0.32f, 0.17f, 0.36f));
        sky.SetFloat("_SkyBlendPower", 1.6f);
        sky.SetFloat("_GroundBlendPower", 0.7f);
        sky.SetColor("_CloudColor", new Color(1f, 0.74f, 0.66f, 0.55f));
        sky.SetColor("_ShadowColor", new Color(0.62f, 0.4f, 0.62f, 0f));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.68f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.7f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.27f, 0.36f);
        RenderSettings.ambientIntensity = 1f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.86f, 0.58f, 0.58f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 230f;

        var sunGo = Find(scene, "Directional Light");
        Light sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
        if(sun != null)
        {
            sun.color = new Color(1f, 0.64f, 0.4f);
            sun.intensity = 1.9f;
            sun.transform.rotation = Quaternion.Euler(32f, 35f, 0f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }

        // post: copia del perfil de la escena, más cálido y con más bloom (para faroles y luciérnagas)
        var volGo = Find(scene, "Global Volume");
        var vol = volGo != null ? volGo.GetComponent<Volume>() : null;
        if(vol != null && vol.sharedProfile != null)
        {
            string vp = Folder + "/VP_AldeaAtardecer.asset";
            var src = vol.sharedProfile;
            if(AssetDatabase.GetAssetPath(src) != vp)
            {
                if(AssetDatabase.LoadAssetAtPath<VolumeProfile>(vp) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), vp);
                vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(vp);
            }
            var prof = vol.sharedProfile;
            if(prof.TryGet(out Bloom bloom))
            {
                bloom.active = true;
                bloom.intensity.Override(1.1f);
                bloom.threshold.Override(0.95f);
                bloom.scatter.Override(0.7f);
                bloom.tint.Override(new Color(1f, 0.85f, 0.75f));
            }
            if(prof.TryGet(out ColorAdjustments ca))
            {
                ca.active = true;
                ca.colorFilter.Override(new Color(1f, 0.93f, 0.86f));
                ca.saturation.Override(12f);
                ca.contrast.Override(8f);
            }
            if(prof.TryGet(out Vignette vig))
            {
                vig.active = true;
                vig.intensity.Override(0.3f);
                vig.color.Override(new Color(0.18f, 0.08f, 0.22f));
            }
            EditorUtility.SetDirty(prof);
        }
        else log.Append(" (no encontré Global Volume)");

        // volume corrupto (lo maneja PurifyAtmosphere según cuántas zonas están purificadas)
        var atm = Child(root, "Atmosfera");
        var vc = Child(atm, "Volume Corrupto");
        var volC = vc.AddComponent<Volume>();
        volC.isGlobal = true;
        volC.priority = 10f;
        volC.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Gekko/Purification/Settings/VP_Corrupto.asset");
        var pa = atm.AddComponent<PurifyAtmosphere>();
        var so = new SerializedObject(pa);
        so.FindProperty("_volumeCorrupto").objectReferenceValue = volC;
        so.FindProperty("_luz").objectReferenceValue = sun;
        so.FindProperty("_nieblaColorCorrupta").colorValue = new Color(0.32f, 0.22f, 0.38f);
        so.FindProperty("_nieblaFinCorrupta").floatValue = 120f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    // =====================================================================================================
    #region Intro
    static void Intro(UnityEngine.SceneManagement.Scene scene, List<PurifyRegion> regiones, System.Text.StringBuilder log)
    {
        var seqGo = Find(scene, "AldeaIntro_Sequence");
        if(seqGo == null) { log.Append(" (no encontré AldeaIntro_Sequence)"); return; }
        var seq = seqGo.GetComponent<AldeaIntroSequence>();

        Vector3 plaza = Suelo(Plaza, out _);
        var wide = seqGo.transform.Find("WideShotPoint");
        if(wide == null) wide = Child(seqGo, "WideShotPoint").transform;
        wide.position = plaza + new Vector3(-4f, 30f, -52f);
        wide.rotation = Quaternion.LookRotation(plaza + new Vector3(0f, -4f, 6f) - wide.position);
        var close = seqGo.transform.Find("CloseShotPoint");
        if(close != null) Object.DestroyImmediate(close.gameObject);

        foreach(var viejo in seqGo.transform.Cast<Transform>().Where(t => t.name.StartsWith("Plano ")).ToList())
            Object.DestroyImmediate(viejo.gameObject);

        var so = new SerializedObject(seq);
        var zonas = so.FindProperty("_zonas");
        zonas.arraySize = Zonas.Length;
        for(int k = 0; k < Zonas.Length; k++)
        {
            int i = OrdenPurificacion[k];
            var z = Zonas[i];
            string n = "Plano " + (i + 1) + " - " + z.nombre;
            var old = seqGo.transform.Find(n);
            if(old != null) Object.DestroyImmediate(old.gameObject);
            var plano = Child(seqGo, n).transform;

            // se encuadra el nodo principal (el primero): las zonas con dos mesetas tienen los nodos separados
            Vector3 c = Suelo(z.nodos[0].xz, out _);
            float rad = z.nodos[0].r;
            Vector3 target = c + Vector3.up * 1f;
            plano.position = BuscarPlano(target, rad, z.camDir.normalized);
            plano.rotation = Quaternion.LookRotation(target - plano.position);

            var e = zonas.GetArrayElementAtIndex(k);
            e.FindPropertyRelative("nombre").stringValue = z.nombre;
            e.FindPropertyRelative("region").objectReferenceValue = regiones[i];
            e.FindPropertyRelative("plano").objectReferenceValue = plano;
        }
        so.FindProperty("_wideShotPoint").objectReferenceValue = wide;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    // Busca una posición de cámara que vea la zona sin paredes ni árboles en el medio, lo más parecida posible a la
    // dirección preferida.
    static Vector3 BuscarPlano(Vector3 target, float rad, Vector3 preferida)
    {
        Vector3 mejor = target - preferida * rad * 1.3f + Vector3.up * (rad * 0.8f + 3f);
        float mejorScore = float.MaxValue;
        foreach(float dist in new[] { 1.5f, 1.9f, 2.4f })
            foreach(float alto in new[] { 0.9f, 1.2f, 1.5f })
                for(int a = 0; a < 24; a++)
                {
                    float ang = a * 15f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 pos = target - dir * (rad * dist) + Vector3.up * (rad * alto + 3f);
                    // que haya aire debajo (no adentro de una pared) y nada en el medio
                    if(Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var h, 200f, 1 << TempLayer) && h.point.y > pos.y - 3f) continue;
                    Vector3 hacia = target - pos;
                    if(Physics.Raycast(pos, hacia.normalized, hacia.magnitude - 1.5f, 1 << TempLayer)) continue;
                    if(Tapado(pos, target)) continue;
                    float score = Vector3.Angle(dir, preferida) + dist * 8f + Mathf.Abs(alto - 1.2f) * 20f;
                    if(score < mejorScore) { mejorScore = score; mejor = pos; }
                }
        return mejor;
    }

    static readonly List<(Vector3 p, float r, float alto)> _obstaculos = new List<(Vector3, float, float)>();

    static bool Tapado(Vector3 desde, Vector3 hasta)
    {
        for(int i = 1; i < 24; i++)
        {
            Vector3 s = Vector3.Lerp(desde, hasta, i / 24f);
            foreach(var o in _obstaculos)
            {
                if(s.y < o.p.y || s.y > o.p.y + o.alto) continue;
                float dx = s.x - o.p.x, dz = s.z - o.p.z;
                if(dx * dx + dz * dz < o.r * o.r) return true;
            }
        }
        return false;
    }
    #endregion

    // =====================================================================================================
    #region Utilidades
    static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach(var g in scene.GetRootGameObjects())
        {
            if(g.name == name) return g;
            var t = g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if(t != null) return t.gameObject;
        }
        return null;
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if(go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if(rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach(var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Set(SerializedProperty arr, Object[] values)
    {
        arr.arraySize = values.Length;
        for(int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Directory(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Directory(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    #endregion
}
