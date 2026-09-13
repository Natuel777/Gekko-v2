using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Gekko.ProceduralPrueba.Editor
{
    /// <summary>
    /// Ventana única para afinar la marcha procedural de CUALQUIER gecko de la escena
    /// (Gecko, gekko_paton, o el que sigue), sin tener que ir a buscar los 4 GameObjects
    /// "Home_*" y los "ik_*_hint" a mano.
    ///
    /// Por qué existe: Gecko y gekko_paton NO son proporcionales entre sí (huesos de
    /// distinto largo, patas delanteras/traseras con la relación invertida), así que cada
    /// modelo necesita SUS PROPIOS valores de zancada/hint/etc. Ir e inspector por
    /// inspector para eso es tedioso y fácil de errar. Acá se edita todo junto, agrupado
    /// por lo que realmente se toca en conjunto (delanteras vs traseras, izquierda vs
    /// derecha), con una explicación de qué hace cada cosa al lado.
    ///
    /// No inventa mecanismos nuevos: solo expone con SerializedObject/SerializedProperty
    /// los campos que ya existen en GeckoMover / GeckoAnimation / GeckoLeg / GeckoRigSetup /
    /// GeckoIdlePose, así el Undo (Ctrl+Z) y el resaltado de "override de prefab" de Unity
    /// funcionan igual que si los tocaras en el Inspector normal.
    /// </summary>
    public class GeckoTuningWindow : EditorWindow
    {
        [MenuItem("Tools/Gekko/Afinar marcha del Gecko...")]
        private static void Open()
        {
            var win = GetWindow<GeckoTuningWindow>("Gecko - Marcha");
            win.minSize = new Vector2(420f, 480f);
        }

        // ------------------------------------------------------------------ estado
        private GameObject _target;

        private GeckoMover _mover;
        private GeckoAnimation _anim;
        private GeckoRigSetup _rigSetup;
        private GeckoIdlePose _idlePose; // opcional: no todos los gecko lo tienen

        private GeckoLeg _legFL, _legFR, _legBL, _legBR;
        private Transform _hintFL, _hintFR, _hintBL, _hintBR;

        private SerializedObject _soMover, _soAnim, _soRigSetup, _soIdlePose;
        private SerializedObject _soFront, _soBack; // GeckoLeg x2, para editar delanteras/traseras juntas

        private GeckoTuningSnapshotAsset _snapshotAsset;
        private const System.Reflection.BindingFlags PrivateInstance =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        private Vector2 _scroll;
        private bool _foldVelocidad = true, _foldPatas = true, _foldPostura = true, _foldIdle = true, _foldDiag = true;

        // ------------------------------------------------------------------ ciclo de vida
        private void OnEnable()
        {
            EnsureSnapshotAsset();
            if (_target != null) ResolveTarget(_target);
        }

        private void OnGUI()
        {
            DrawPicker();

            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "Elegí (o arrastrá) el GameObject raíz de un gecko: el que tiene GeckoAnimation.",
                    MessageType.Info);
                return;
            }

            if (_legFL == null || _legFR == null || _legBL == null || _legBR == null)
            {
                EditorGUILayout.HelpBox(
                    "No encontré las 4 patas (Legs/Home_FL, Home_FR, Home_BL, Home_BR) bajo este " +
                    "objeto. ¿Es un gecko armado con el mismo esqueleto que los demás?",
                    MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawDiagnostico();
            DrawVelocidad();
            DrawPatas();
            DrawPostura();
            DrawIdlePose();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ selección de personaje
        private void DrawPicker()
        {
            EditorGUILayout.LabelField("Personaje", EditorStyles.boldLabel);

            var found = Object.FindObjectsByType<GeckoAnimation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(g => g.gameObject)
                .OrderBy(g => g.name)
                .ToList();

            if (found.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var go in found)
                    {
                        bool isCurrent = go == _target;
                        var prev = GUI.backgroundColor;
                        if (isCurrent) GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                        if (GUILayout.Button(go.name, GUILayout.Height(24f))) ResolveTarget(go);
                        GUI.backgroundColor = prev;
                    }
                }
            }

            var picked = (GameObject)EditorGUILayout.ObjectField(
                "(o arrastrá cualquiera)", _target, typeof(GameObject), true);
            if (picked != _target) ResolveTarget(picked);

            EditorGUILayout.Space(6);
            DrawSaveLoad();
            EditorGUILayout.Space(6);
        }

        // ------------------------------------------------------------------ guardar / cargar
        // Para poder probar valores EN Play Mode sin perderlos: Unity descarta cualquier
        // cambio de campo hecho durante Play apenas frenás (vuelve a lo que tenía guardada
        // la escena). Guardar escribe una foto de TODOS los valores de este gecko en un
        // asset del proyecto (sobrevive a salir de Play, y a cerrar Unity); Cargar la trae
        // de vuelta y la reaplica a los componentes de la escena.
        private void DrawSaveLoad()
        {
            if (_target == null) return;

            GeckoTuningSnapshot existing = _snapshotAsset != null ? _snapshotAsset.Find(_target.name) : null;

            using (new EditorGUILayout.HorizontalScope())
            {
                var prevColor = GUI.backgroundColor;

                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("💾 Guardar foto de estos valores", GUILayout.Height(26f)))
                    SaveSnapshot();

                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                using (new EditorGUI.DisabledScope(existing == null))
                {
                    if (GUILayout.Button("📥 Cargar última foto", GUILayout.Height(26f)))
                        LoadSnapshot();
                }

                GUI.backgroundColor = prevColor;
            }

            if (existing != null)
                EditorGUILayout.HelpBox("Última foto de '" + _target.name + "': " + existing.savedAt, MessageType.None);
            else
                EditorGUILayout.HelpBox(
                    "Todavía no hay una foto guardada de '" + _target.name + "'. " +
                    "Flujo típico: dale Play, ajustá los valores, tocá Guardar, salí de Play, tocá Cargar.",
                    MessageType.None);
        }

        private void EnsureSnapshotAsset()
        {
            if (_snapshotAsset != null) return;

            _snapshotAsset = AssetDatabase.LoadAssetAtPath<GeckoTuningSnapshotAsset>(GeckoTuningSnapshotAsset.AssetPath);
            if (_snapshotAsset != null) return;

            _snapshotAsset = ScriptableObject.CreateInstance<GeckoTuningSnapshotAsset>();
            AssetDatabase.CreateAsset(_snapshotAsset, GeckoTuningSnapshotAsset.AssetPath);
            AssetDatabase.SaveAssets();
        }

        private void SaveSnapshot()
        {
            EnsureSnapshotAsset();
            var snap = _snapshotAsset.FindOrCreate(_target.name);

            snap.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (_mover != null)
            {
                snap.speed = GetFloat(_mover, "_speed");
                snap.rotationSpeed = GetFloat(_mover, "_rotationSpeed");
            }
            if (_anim != null)
            {
                snap.gaitSpeedScale = GetFloat(_anim, "_gaitSpeedScale");
                snap.gaitApparentSpeed = GetFloat(_anim, "_gaitApparentSpeed");
                snap.velocitySmoothing = GetFloat(_anim, "_velocitySmoothing");
                snap.alternationBias = GetFloat(_anim, "_alternationBias");
            }
            if (_rigSetup != null)
            {
                snap.frontLegStretch = GetFloat(_rigSetup, "_frontLegStretch");
                snap.backLegStretch = GetFloat(_rigSetup, "_backLegStretch");
            }

            SaveLeg(_legFL, snap.front);
            SaveLeg(_legBL, snap.back);

            if (_hintFR != null) snap.hintFront = _hintFR.localPosition;
            if (_hintBR != null) snap.hintBack = _hintBR.localPosition;

            snap.hasIdlePose = _idlePose != null;
            if (_idlePose != null)
            {
                snap.leftKneeAngle = GetFloat(_idlePose, "_leftKneeAngle");
                snap.rightKneeAngle = GetFloat(_idlePose, "_rightKneeAngle");
                snap.leftThighAngle = GetFloat(_idlePose, "_leftThighAngle");
                snap.rightThighAngle = GetFloat(_idlePose, "_rightThighAngle");
                snap.leftAnkleAngle = GetFloat(_idlePose, "_leftAnkleAngle");
                snap.rightAnkleAngle = GetFloat(_idlePose, "_rightAnkleAngle");
                snap.poseWeight = GetFloat(_idlePose, "_poseWeight");
                snap.walkPoseWeight = GetFloat(_idlePose, "_walkPoseWeight");
                snap.alsoDriveIkWeight = GetBool(_idlePose, "_alsoDriveIkWeight");
                snap.lockAnkleWhileWalking = GetBool(_idlePose, "_lockAnkleWhileWalking");
            }

            EditorUtility.SetDirty(_snapshotAsset);
            AssetDatabase.SaveAssets(); // vuelca a disco YA, no espera a salir de Play

            Debug.Log("[GeckoTuningWindow] Foto guardada de '" + _target.name + "' (" + snap.savedAt + ").");
        }

        private static void SaveLeg(GeckoLeg leg, GeckoLegSnapshot s)
        {
            if (leg == null) return;
            s.stepDistance = GetFloat(leg, "_stepDistance");
            s.maxStepDistance = GetFloat(leg, "_maxStepDistance");
            s.overshoot = GetFloat(leg, "_overshoot");
            s.maxLead = GetFloat(leg, "_maxLead");
            s.stepStretch = GetFloat(leg, "_stepStretch");
            s.maxReach = GetFloat(leg, "_maxReach");
            s.reachUsage = GetFloat(leg, "_reachUsage");
            s.stepHeight = GetFloat(leg, "_stepHeight");
            s.stepDuration = GetFloat(leg, "_stepDuration");
            s.minStepDuration = GetFloat(leg, "_minStepDuration");
            s.referenceSpeed = GetFloat(leg, "_referenceSpeed");
            s.maxFootLag = GetFloat(leg, "_maxFootLag");
            s.lagSpeedRange = GetFloat(leg, "_lagSpeedRange");
        }

        private void LoadSnapshot()
        {
            EnsureSnapshotAsset();
            var snap = _snapshotAsset.Find(_target.name);
            if (snap == null)
            {
                Debug.LogWarning("[GeckoTuningWindow] No hay foto guardada de '" + _target.name + "'.");
                return;
            }

            if (_mover != null)
            {
                Undo.RecordObject(_mover, "Cargar foto de marcha");
                SetFloat(_mover, "_speed", snap.speed);
                SetFloat(_mover, "_rotationSpeed", snap.rotationSpeed);
                EditorUtility.SetDirty(_mover);
            }
            if (_anim != null)
            {
                Undo.RecordObject(_anim, "Cargar foto de marcha");
                SetFloat(_anim, "_gaitSpeedScale", snap.gaitSpeedScale);
                SetFloat(_anim, "_gaitApparentSpeed", snap.gaitApparentSpeed);
                SetFloat(_anim, "_velocitySmoothing", snap.velocitySmoothing);
                SetFloat(_anim, "_alternationBias", snap.alternationBias);
                EditorUtility.SetDirty(_anim);
            }
            if (_rigSetup != null)
            {
                Undo.RecordObject(_rigSetup, "Cargar foto de marcha");
                SetFloat(_rigSetup, "_frontLegStretch", snap.frontLegStretch);
                SetFloat(_rigSetup, "_backLegStretch", snap.backLegStretch);
                EditorUtility.SetDirty(_rigSetup);
            }

            LoadLeg(_legFL, snap.front);
            LoadLeg(_legFR, snap.front);
            LoadLeg(_legBL, snap.back);
            LoadLeg(_legBR, snap.back);

            if (_hintFL != null && _hintFR != null)
            {
                Undo.RecordObjects(new Object[] { _hintFL, _hintFR }, "Cargar foto de marcha");
                _hintFR.localPosition = snap.hintFront;
                _hintFL.localPosition = new Vector3(-snap.hintFront.x, snap.hintFront.y, snap.hintFront.z);
                EditorUtility.SetDirty(_hintFL);
                EditorUtility.SetDirty(_hintFR);
            }
            if (_hintBL != null && _hintBR != null)
            {
                Undo.RecordObjects(new Object[] { _hintBL, _hintBR }, "Cargar foto de marcha");
                _hintBR.localPosition = snap.hintBack;
                _hintBL.localPosition = new Vector3(-snap.hintBack.x, snap.hintBack.y, snap.hintBack.z);
                EditorUtility.SetDirty(_hintBL);
                EditorUtility.SetDirty(_hintBR);
            }

            if (_idlePose != null && snap.hasIdlePose)
            {
                Undo.RecordObject(_idlePose, "Cargar foto de marcha");
                SetFloat(_idlePose, "_leftKneeAngle", snap.leftKneeAngle);
                SetFloat(_idlePose, "_rightKneeAngle", snap.rightKneeAngle);
                SetFloat(_idlePose, "_leftThighAngle", snap.leftThighAngle);
                SetFloat(_idlePose, "_rightThighAngle", snap.rightThighAngle);
                SetFloat(_idlePose, "_leftAnkleAngle", snap.leftAnkleAngle);
                SetFloat(_idlePose, "_rightAnkleAngle", snap.rightAnkleAngle);
                SetFloat(_idlePose, "_poseWeight", snap.poseWeight);
                SetFloat(_idlePose, "_walkPoseWeight", snap.walkPoseWeight);
                SetBool(_idlePose, "_alsoDriveIkWeight", snap.alsoDriveIkWeight);
                SetBool(_idlePose, "_lockAnkleWhileWalking", snap.lockAnkleWhileWalking);
                EditorUtility.SetDirty(_idlePose);
            }

            // Si ya salimos de Play y estamos en modo edición, esto es lo que hace que la
            // carga quede realmente guardada en la escena (si no, es "dirty" nomás en RAM).
            if (!Application.isPlaying)
            {
                var scene = _target.scene;
                if (scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log("[GeckoTuningWindow] Foto de '" + _target.name + "' cargada (guardada " + snap.savedAt + ").");
        }

        private static void LoadLeg(GeckoLeg leg, GeckoLegSnapshot s)
        {
            if (leg == null) return;
            Undo.RecordObject(leg, "Cargar foto de marcha");
            SetFloat(leg, "_stepDistance", s.stepDistance);
            SetFloat(leg, "_maxStepDistance", s.maxStepDistance);
            SetFloat(leg, "_overshoot", s.overshoot);
            SetFloat(leg, "_maxLead", s.maxLead);
            SetFloat(leg, "_stepStretch", s.stepStretch);
            SetFloat(leg, "_maxReach", s.maxReach);
            SetFloat(leg, "_reachUsage", s.reachUsage);
            SetFloat(leg, "_stepHeight", s.stepHeight);
            SetFloat(leg, "_stepDuration", s.stepDuration);
            SetFloat(leg, "_minStepDuration", s.minStepDuration);
            SetFloat(leg, "_referenceSpeed", s.referenceSpeed);
            SetFloat(leg, "_maxFootLag", s.maxFootLag);
            SetFloat(leg, "_lagSpeedRange", s.lagSpeedRange);
            EditorUtility.SetDirty(leg);
        }

        // -------- helpers chicos de reflection (los campos son todos privados) --------
        private static float GetFloat(object target, string field) =>
            (float)target.GetType().GetField(field, PrivateInstance).GetValue(target);

        private static bool GetBool(object target, string field) =>
            (bool)target.GetType().GetField(field, PrivateInstance).GetValue(target);

        private static void SetFloat(object target, string field, float value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private static void SetBool(object target, string field, bool value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private void ResolveTarget(GameObject go)
        {
            _target = go;
            _mover = null;
            _anim = null;
            _rigSetup = null;
            _idlePose = null;
            _legFL = _legFR = _legBL = _legBR = null;
            _hintFL = _hintFR = _hintBL = _hintBR = null;
            _soMover = _soAnim = _soRigSetup = _soIdlePose = _soFront = _soBack = null;

            if (go == null) return;

            _mover = go.GetComponent<GeckoMover>();
            _anim = go.GetComponent<GeckoAnimation>();
            _rigSetup = go.GetComponent<GeckoRigSetup>();
            _idlePose = go.GetComponent<GeckoIdlePose>(); // puede ser null, es opcional

            Transform legs = go.transform.Find("Legs");
            if (legs != null)
            {
                _legFL = GetLeg(legs, "Home_FL");
                _legFR = GetLeg(legs, "Home_FR");
                _legBL = GetLeg(legs, "Home_BL");
                _legBR = GetLeg(legs, "Home_BR");
            }

            Transform rig = go.transform.Find("Rig 1");
            if (rig != null)
            {
                _hintFL = FindHint(rig, "ik_FL");
                _hintFR = FindHint(rig, "ik_FR");
                _hintBL = FindHint(rig, "ik_BL");
                _hintBR = FindHint(rig, "ik_BR");
            }

            if (_mover != null) _soMover = new SerializedObject(_mover);
            if (_anim != null) _soAnim = new SerializedObject(_anim);
            if (_rigSetup != null) _soRigSetup = new SerializedObject(_rigSetup);
            if (_idlePose != null) _soIdlePose = new SerializedObject(_idlePose);

            if (_legFL != null && _legFR != null)
                _soFront = new SerializedObject(new Object[] { _legFL, _legFR });
            if (_legBL != null && _legBR != null)
                _soBack = new SerializedObject(new Object[] { _legBL, _legBR });
        }

        private static GeckoLeg GetLeg(Transform legs, string name)
        {
            Transform t = legs.Find(name);
            return t != null ? t.GetComponent<GeckoLeg>() : null;
        }

        private static Transform FindHint(Transform rig, string ikName)
        {
            Transform ik = rig.Find(ikName);
            return ik != null ? ik.Find(ikName + "_hint") : null;
        }

        // ------------------------------------------------------------------ diagnóstico en vivo
        private void DrawDiagnostico()
        {
            _foldDiag = EditorGUILayout.Foldout(_foldDiag, "Diagnóstico en vivo (solo durante Play)", true);
            if (!_foldDiag) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "Dale Play para ver acá, por pata: si el pie está pisando o dando un paso, " +
                        "y qué tan cerca está del freno de seguridad (Max Reach / el largo real de " +
                        "la pata). Si una pata está SIEMPRE pegada al freno, por eso arrastra.",
                        MessageType.None);
                    return;
                }

                var rb = _target.GetComponent<Rigidbody>();
                float bodySpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
                EditorGUILayout.LabelField("Velocidad real del cuerpo", bodySpeed.ToString("F2") + " m/s");

                DrawLegDiagnostic("FL", _legFL);
                DrawLegDiagnostic("FR", _legFR);
                DrawLegDiagnostic("BL", _legBL);
                DrawLegDiagnostic("BR", _legBR);
            }

            Repaint(); // refrescar seguido mientras está en Play
        }

        private static void DrawLegDiagnostic(string label, GeckoLeg leg)
        {
            if (leg == null) return;
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(GeckoLeg);

            var ikRoot = t.GetField("_ikRoot", f).GetValue(leg) as Transform;
            float chainReach = (float)t.GetField("_chainReach", f).GetValue(leg);
            float reachUsage = (float)t.GetField("_reachUsage", f).GetValue(leg);
            Vector3 curPos = (Vector3)t.GetField("_currentPos", f).GetValue(leg);
            float effDist = (float)t.GetField("_effStepDistance", f).GetValue(leg);

            string cap = "-";
            float pct = 0f;
            if (ikRoot != null && chainReach > 0f)
            {
                Vector3 fromRoot = curPos - ikRoot.position;
                Vector3 up = leg.transform.root.up;
                float flat = (fromRoot - Vector3.Project(fromRoot, up)).magnitude;
                float limit = chainReach * reachUsage;
                pct = limit > 0f ? flat / limit : 0f;
                cap = flat.ToString("F3") + " / " + limit.ToString("F3") + "  (" + (pct * 100f).ToString("F0") + "%)";
            }

            var color = pct > 0.92f ? Color.red : (pct > 0.75f ? new Color(0.9f, 0.7f, 0.1f) : Color.green);
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(label + (leg.IsStepping ? "  (paso en curso)" : "  (apoyada)"),
                "alcance usado: " + cap + "   zancada: " + effDist.ToString("F3"));
            GUI.contentColor = prev;
        }

        // ------------------------------------------------------------------ velocidad / cadencia
        private void DrawVelocidad()
        {
            _foldVelocidad = EditorGUILayout.Foldout(_foldVelocidad, "Velocidad y cadencia", true);
            if (!_foldVelocidad) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (_soMover != null)
                {
                    _soMover.Update();
                    Field(_soMover, "_speed", "Speed (velocidad real)",
                        "La velocidad de MOVIMIENTO del personaje, en m/s. Es la de gameplay: no la " +
                        "cambies para arreglar las patas, para eso están los campos de más abajo.");
                    Field(_soMover, "_rotationSpeed", "Rotation Speed",
                        "Qué tan rápido gira el cuerpo hacia la dirección en la que caminás.");
                    _soMover.ApplyModifiedProperties();
                }

                if (_soAnim != null)
                {
                    _soAnim.Update();
                    Field(_soAnim, "_gaitSpeedScale", "Gait Speed Scale (cadencia)",
                        "Ritmo de las 4 patas, DESACOPLADO de la velocidad real. Más alto = pasos más " +
                        "cortos y apurados. Más bajo = pasos más largos y lentos, aunque el cuerpo se " +
                        "mueva igual de rápido.");
                    Field(_soAnim, "_gaitApparentSpeed", "Gait Apparent Speed",
                        "Si es MAYOR A 0, la marcha adaptativa de las patas (duración, zancada, " +
                        "adelanto del pie) usa ESTE valor en vez de la velocidad real. Ojo: solo anda " +
                        "bien si está CERCA de la velocidad real — si la diferencia es grande, el pie " +
                        "no llega a tiempo a donde el cuerpo ya está y arrastra. 0 = usar la velocidad " +
                        "real (recomendado como punto de partida).");
                    Field(_soAnim, "_velocitySmoothing", "Velocity Smoothing",
                        "Suaviza la medición de velocidad. Más alto = más estable pero las patas se " +
                        "enteran más tarde de que el cuerpo arrancó o frenó.");
                    Field(_soAnim, "_alternationBias", "Alternation Bias",
                        "Ayuda a que las patas alternen parejo (un par, después el otro) en vez de que " +
                        "uno acapare los pasos y el bicho renguee.");
                    _soAnim.ApplyModifiedProperties();
                }
            }
        }

        // ------------------------------------------------------------------ patas (delanteras / traseras)
        private void DrawPatas()
        {
            _foldPatas = EditorGUILayout.Foldout(_foldPatas, "Patas — delanteras y traseras", true);
            if (!_foldPatas) return;

            EditorGUILayout.HelpBox(
                "Delanteras (FL+FR) y traseras (BL+BR) casi siempre necesitan valores DISTINTOS: " +
                "no tienen el mismo largo de hueso ni el mismo alcance real. Se editan juntas " +
                "izquierda+derecha porque esas sí son siempre simétricas.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Delanteras (FL / FR)", EditorStyles.boldLabel);
                    DrawLegFields(_soFront);
                }
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Traseras (BL / BR)", EditorStyles.boldLabel);
                    DrawLegFields(_soBack);
                }
            }
        }

        private void DrawLegFields(SerializedObject so)
        {
            if (so == null) return;
            so.Update();

            Field(so, "_stepDistance", "Step Distance",
                "Umbral base (parado o a velocidad baja) para disparar un paso nuevo.");
            Field(so, "_maxStepDistance", "Max Step Distance",
                "Techo de ese umbral a alta velocidad. Si ves 'micro-pasos' cortos y apurados, subí esto.");
            Field(so, "_overshoot", "Overshoot",
                "Cuánto se adelanta el pie en la dirección en la que caminás (proporcional a la velocidad).");
            Field(so, "_maxLead", "Max Lead",
                "Techo de ese adelanto. Si Overshoot no alcanza (el pie no llega más adelante), subí esto.");
            Field(so, "_stepStretch", "Step Stretch",
                "Cuánto crece la zancada por cada m/s extra de velocidad del cuerpo.");
            EditorGUILayout.Space(4);

            Field(so, "_maxReach", "Max Reach",
                "Freno de seguridad: el pie no se aleja más que esto del punto de reposo (Home). Si se " +
                "estira más, DESLIZA en vez de seguir estirándose.");
            Field(so, "_reachUsage", "Reach Usage",
                "Qué fracción del largo REAL de la pata (muslo-rodilla-tobillo) se deja usar. Si la pata " +
                "arrastra porque pega contra este freno todo el tiempo (mirá el Diagnóstico de arriba), " +
                "subilo un poco — pero no lo lleves a 1, la rodilla necesita margen para doblar.");
            EditorGUILayout.Space(4);

            Field(so, "_stepHeight", "Step Height", "Altura del arco que hace el pie al dar el paso.");
            Field(so, "_stepDuration", "Step Duration", "Duración base del paso, en segundos.");
            Field(so, "_minStepDuration", "Min Step Duration",
                "Duración mínima del paso a alta velocidad: el techo de qué tan rápido puede pisar.");
            Field(so, "_referenceSpeed", "Reference Speed",
                "A qué velocidad de cuerpo está calibrada la marcha base (Step Duration/Distance de arriba).");
            EditorGUILayout.Space(4);

            Field(so, "_maxFootLag", "Max Foot Lag",
                "A propósito: a alta velocidad el pie se retrasa un poco detrás del cuerpo (da sensación " +
                "de ir rápido). Si se ve mal en vez de estilizado, bajalo.");
            Field(so, "_lagSpeedRange", "Lag Speed Range",
                "Cuántos m/s por encima de Reference Speed hacen falta para llegar al retraso máximo de arriba.");

            so.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ postura / IK (sprawl)
        private void DrawPostura()
        {
            _foldPostura = EditorGUILayout.Foldout(_foldPostura, "Postura — largo de pata y sprawl (IK)", true);
            if (!_foldPostura) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (_soRigSetup != null)
                {
                    _soRigSetup.Update();
                    Field(_soRigSetup, "_frontLegStretch", "Front Leg Stretch",
                        "Multiplicador que alarga los huesos de las patas DELANTERAS para que lleguen " +
                        "al piso con la rodilla doblada (el modelo viene en T-pose, con patas cortas).");
                    Field(_soRigSetup, "_backLegStretch", "Back Leg Stretch",
                        "Lo mismo para las patas TRASERAS.");
                    _soRigSetup.ApplyModifiedProperties();
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    "Hints: hacia dónde apunta el 'codo' de cada pata. Más LEJOS del cuerpo = pata más " +
                    "abierta (sprawl de lagarto). Más CERCA = pata más vertical (puede verse como araña " +
                    "parada). Household: si al abrirlos mucho las patas empiezan a 'volar' al dar el " +
                    "paso, cerralos un poco.",
                    MessageType.None);
                DrawHintPair("Delanteras", _hintFL, _hintFR);
                DrawHintPair("Traseras", _hintBL, _hintBR);
            }
        }

        /// <summary>
        /// Los hints de una pareja izq/derecha son simétricos en X (uno negativo, uno positivo) e
        /// iguales en Y/Z. Se edita un solo Vector3 (el del lado derecho) y se espeja al izquierdo.
        /// </summary>
        private void DrawHintPair(string label, Transform hintL, Transform hintR)
        {
            if (hintL == null || hintR == null) return;

            EditorGUI.BeginChangeCheck();
            Vector3 v = EditorGUILayout.Vector3Field(label + " (lado derecho; el izquierdo se espeja solo)", hintR.localPosition);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { hintL, hintR }, "Editar hint " + label);
                hintR.localPosition = v;
                hintL.localPosition = new Vector3(-v.x, v.y, v.z);
                EditorUtility.SetDirty(hintL);
                EditorUtility.SetDirty(hintR);
            }
        }

        // ------------------------------------------------------------------ pose idle
        private void DrawIdlePose()
        {
            _foldIdle = EditorGUILayout.Foldout(_foldIdle, "Pose Idle (parado)", true);
            if (!_foldIdle) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (_idlePose == null)
                {
                    EditorGUILayout.HelpBox(
                        "Este gecko no tiene GeckoIdlePose (por ejemplo, 'Gecko' funciona 100% a IK, " +
                        "parado y caminando, sin este componente). No hay nada que tocar acá.",
                        MessageType.None);
                    return;
                }

                _soIdlePose.Update();
                Field(_soIdlePose, "_leftKneeAngle", "Left Knee Angle",
                    "Giro principal de la pose PARADO, lado izquierdo. Con -90 la pata baja desde la T-pose.");
                Field(_soIdlePose, "_rightKneeAngle", "Right Knee Angle", "Lo mismo, lado derecho (+90 típico).");
                Field(_soIdlePose, "_leftThighAngle", "Left Thigh Angle", "Cuánto se abre el muslo (0 = T-pose).");
                Field(_soIdlePose, "_rightThighAngle", "Right Thigh Angle", "Ídem, lado derecho.");
                Field(_soIdlePose, "_leftAnkleAngle", "Left Ankle Angle", "Orientación del pie parado.");
                Field(_soIdlePose, "_rightAnkleAngle", "Right Ankle Angle", "Ídem, lado derecho.");
                EditorGUILayout.Space(4);

                Field(_soIdlePose, "_poseWeight", "Pose Weight",
                    "1 = esta pose manda estando quieto (recomendado dejarlo en 1).");
                Field(_soIdlePose, "_walkPoseWeight", "Walk Pose Weight",
                    "¡OJO ACÁ! Cuánto de esta pose se mezcla TAMBIÉN mientras camina. En 0, mientras " +
                    "camina el IK maneja la pata solo — así se mueve 'Gecko'. Ponerlo arriba de 0 hace " +
                    "que la pata compita entre esta pose fija y el IK: es lo que da el look de araña / " +
                    "patas a 90° al caminar. Dejalo en 0 salvo que sepas bien por qué lo subís.");
                Field(_soIdlePose, "_alsoDriveIkWeight", "Also Drive Ik Weight",
                    "Baja el peso del IK a la par de esta pose para que no compitan.");
                Field(_soIdlePose, "_lockAnkleWhileWalking", "Lock Ankle While Walking",
                    "Fuerza el tobillo a un ángulo fijo mientras camina, para que no quede torcido por el IK.");

                _soIdlePose.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------------------ helper
        private static void Field(SerializedObject so, string propName, string label, string help)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                EditorGUILayout.HelpBox("(campo no encontrado: " + propName + ")", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            if (!string.IsNullOrEmpty(help))
                EditorGUILayout.HelpBox(help, MessageType.None);
            EditorGUILayout.Space(3);
        }
    }
}
