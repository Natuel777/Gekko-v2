using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gekko.ProceduralPrueba.Editor
{
    /// <summary>
    /// Foto de TODOS los valores que edita GeckoTuningWindow, para un gecko puntual.
    ///
    /// Para qué existe: los cambios hechos en Play Mode se PIERDEN al frenar el Play (Unity
    /// vuelve cada campo a lo que tenía guardada la escena). Esto permite guardar los
    /// valores mientras estás probando en Play, y volver a aplicarlos ya en modo edición
    /// para que ahora sí queden guardados en la escena.
    /// </summary>
    [Serializable]
    public class GeckoLegSnapshot
    {
        public float stepDistance;
        public float maxStepDistance;
        public float overshoot;
        public float maxLead;
        public float stepStretch;
        public float maxReach;
        public float reachUsage;
        public float stepHeight;
        public float stepDuration;
        public float minStepDuration;
        public float referenceSpeed;
        public float maxFootLag;
        public float lagSpeedRange;
    }

    [Serializable]
    public class GeckoTuningSnapshot
    {
        [Tooltip("Nombre del GameObject al que corresponde esta foto. Guardar/Cargar busca por este nombre.")]
        public string geckoName;
        [Tooltip("Se completa solo con la fecha/hora del último Guardar, para saber si es reciente.")]
        public string savedAt;

        // GeckoMover
        public float speed;
        public float rotationSpeed;

        // GeckoAnimation
        public float gaitSpeedScale;
        public float gaitApparentSpeed;
        public float velocitySmoothing;
        public float alternationBias;

        // GeckoRigSetup
        public float frontLegStretch;
        public float backLegStretch;

        // GeckoLeg, delanteras (FL+FR) y traseras (BL+BR)
        public GeckoLegSnapshot front = new GeckoLegSnapshot();
        public GeckoLegSnapshot back = new GeckoLegSnapshot();

        // Hints (lado derecho; el izquierdo siempre se espeja en X)
        public Vector3 hintFront;
        public Vector3 hintBack;

        // GeckoIdlePose (opcional: no todos los gecko lo tienen)
        public bool hasIdlePose;
        public float leftKneeAngle, rightKneeAngle;
        public float leftThighAngle, rightThighAngle;
        public float leftAnkleAngle, rightAnkleAngle;
        public float poseWeight, walkPoseWeight;
        public bool alsoDriveIkWeight, lockAnkleWhileWalking;
    }

    /// <summary>
    /// Un solo asset en el proyecto con una foto guardada por cada gecko (buscadas por
    /// nombre de GameObject). Vive en disco, así que sobrevive a entrar/salir de Play y a
    /// cerrar Unity — a diferencia de los campos de la propia ventana o de la escena.
    /// </summary>
    public class GeckoTuningSnapshotAsset : ScriptableObject
    {
        public const string AssetPath = "Assets/Art/ProceduralPrueba/Editor/GeckoTuningSnapshots.asset";

        public List<GeckoTuningSnapshot> snapshots = new List<GeckoTuningSnapshot>();

        public GeckoTuningSnapshot Find(string geckoName)
        {
            return snapshots.Find(s => s.geckoName == geckoName);
        }

        public GeckoTuningSnapshot FindOrCreate(string geckoName)
        {
            var found = Find(geckoName);
            if (found != null) return found;

            var created = new GeckoTuningSnapshot { geckoName = geckoName };
            snapshots.Add(created);
            return created;
        }
    }
}
