using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gekko.Purification;

// Al purificarse la torreta, sus renderers (y los de sus hijos) que usan "From" (CorruptTurret) pasan a "To" (Turret)
// con un efecto de pintado de abajo hacia arriba:
//  1. Se crea una instancia del shader "Paint Transition" con las texturas de los dos materiales y se pone en los
//     slots que usan "From".
//  2. El frente de pintado sube desde el pie hasta la punta (altura en mundo, así todas las piezas se pintan como una),
//     con una franja brillante del color de la onda de purificación y chispas que lo acompañan.
//  3. Al llegar arriba hay un pulso de brillo y los slots quedan con "To" (el material Lit normal); la instancia se destruye.
// Solo se reemplazan los slots que coinciden con "From": otros materiales quedan como están. No toca los assets.
// La zona corrupta alrededor la purifica un PurifyTrigger con esta torreta asignada.
[RequireComponent(typeof(ChallengeTurret))]
public class TurretPurifiedMaterialSwap : MonoBehaviour
{
    private static readonly int PaintHeightId = PaintTransitionMaterial.PaintHeightId;
    private static readonly int PulseId = PaintTransitionMaterial.PulseId;

    [Header("Materiales")]
    [Tooltip("Material corrupto que se reemplaza.")]
    [SerializeField] private Material _from;
    [Tooltip("Material sano que queda al terminar el pintado.")]
    [SerializeField] private Material _to;
    [Tooltip("Shader de la transición (Gekko/Purification/Paint Transition). Vacío = cambio instantáneo, sin efecto.")]
    [SerializeField] private Shader _paintShader;

    [Header("Pintado")]
    [Tooltip("Segundos que tarda el frente en subir del pie a la punta.")]
    [Min(0.01f)]
    [SerializeField] private float _duracion = 2f;
    [Tooltip("Avance del frente en el tiempo (0..1 en los dos ejes).")]
    [SerializeField] private AnimationCurve _curva = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Irregularidad del frente, en metros (cuánto se adelantan o atrasan las manchas de pintura).")]
    [Min(0f)]
    [SerializeField] private float _irregularidad = 0.14f;

    [Header("Borde")]
    [Tooltip("Color de la franja que sube. Por defecto, el mismo que el frente de la onda del suelo (SG_PurifyLit).")]
    [ColorUsage(false, true)]
    [SerializeField] private Color _colorBorde = new(1f, 0.8f, 0.3f, 1f);
    [Min(0f)]
    [SerializeField] private float _intensidadBorde = 2f;
    [Tooltip("Ancho de la franja brillante, en metros.")]
    [Min(0.001f)]
    [SerializeField] private float _anchoBorde = 0.1f;

    [Header("Partículas (opcional)")]
    [Tooltip("Chispas que suben con el frente. Se mueven a la altura del borde mientras dura el pintado.")]
    [SerializeField] private ParticleSystem _particulasBorde;

    [Header("Pulso final")]
    [Tooltip("Segundos del destello al terminar el pintado (0 = sin pulso).")]
    [Min(0f)]
    [SerializeField] private float _duracionPulso = 0.6f;
    [Min(0f)]
    [SerializeField] private float _intensidadPulso = 1.5f;
    [Tooltip("Partícula que se dispara al arrancar el destello (PS_Purified). Se ubica en el centro de la torreta.")]
    [SerializeField] private ParticleSystem _particulasPurificado;

    private ChallengeTurret _turret;
    private Material _transicion;
    private Coroutine _rutina;
    private bool _aplicado;

    private void Awake()
    {
        _turret = GetComponent<ChallengeTurret>();
    }

    private void OnEnable()
    {
        _turret.Purified += OnTurretPurified;

        // Se purificó mientras estaba desactivada (o se cortó el pintado al desactivarse): queda sana directo.
        if(_turret.IsPurified && !_aplicado) TerminarSinEfecto();
    }

    private void OnDisable()
    {
        _turret.Purified -= OnTurretPurified;

        if(_rutina != null)
        {
            StopCoroutine(_rutina);
            _rutina = null;
        }
    }

    // Red de seguridad: si la torreta quedó purificada pero el pintado no arrancó (se perdió el evento, se cortó la
    // rutina, etc.), se arranca acá. Si ya está aplicado no hace nada.
    private void LateUpdate()
    {
        if(_aplicado || _rutina != null || !_turret.IsPurified || _from == null || _to == null) return;

        Debug.LogWarning($"[{name}] La torreta estaba purificada pero sin pintar: se pinta ahora.", this);
        OnTurretPurified(_turret);
    }

    private void OnDestroy()
    {
        if(_transicion != null) Destroy(_transicion);
    }

    private void OnTurretPurified(ChallengeTurret turret)
    {
        if(_aplicado || _from == null || _to == null) return;

        if(_rutina != null) return;

        if(_paintShader == null || !isActiveAndEnabled)
        {
            TerminarSinEfecto();
            return;
        }

        _rutina = StartCoroutine(RutinaPintado());
    }

    private IEnumerator RutinaPintado()
    {
        _transicion = CrearMaterialTransicion();

        // Los slots con "From" pasan al material de transición; se junta el volumen de esas piezas.
        bool hayBounds = false;
        Bounds bounds = default;

        foreach(Renderer r in Renderers())
        {
            if(!Reemplazar(r, _from, _transicion)) continue;

            if(!hayBounds) bounds = r.bounds;
            else bounds.Encapsulate(r.bounds);
            hayBounds = true;
        }

        if(!hayBounds)
        {
            LogMaterialesActuales();
            Destroy(_transicion);
            _transicion = null;
            _rutina = null;
            TerminarSinEfecto();
            yield break;
        }

        // Margen: el ruido adelanta/atrasa el frente y la franja tiene ancho; sin esto quedarían manchas al final.
        float margen = _irregularidad + _anchoBorde;
        float abajo = bounds.min.y - margen;
        float arriba = bounds.max.y + margen;

        _transicion.SetFloat(PaintHeightId, abajo);

        if(_particulasBorde != null)
        {
            _particulasBorde.transform.position = new Vector3(bounds.center.x, abajo, bounds.center.z);
            _particulasBorde.Play(true);
        }

        for(float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / _duracion);
            float altura = Mathf.LerpUnclamped(abajo, arriba, _curva.Evaluate(t));

            _transicion.SetFloat(PaintHeightId, altura);

            if(_particulasBorde != null)
                _particulasBorde.transform.position = new Vector3(bounds.center.x, Mathf.Clamp(altura, bounds.min.y, bounds.max.y), bounds.center.z);

            yield return null;
        }

        if(_particulasBorde != null) _particulasBorde.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if(_particulasPurificado != null)
        {
            _particulasPurificado.transform.position = bounds.center;
            _particulasPurificado.Play(true);
        }

        // Pulso: sube y baja (seno), más fuerte en la silueta.
        for(float t = 0f; t < 1f && _duracionPulso > 0f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / _duracionPulso);
            _transicion.SetFloat(PulseId, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        foreach(Renderer r in Renderers()) Reemplazar(r, _transicion, _to);

        Destroy(_transicion);
        _transicion = null;
        _rutina = null;
        _aplicado = true;
    }

    // Deja la torreta con el material sano sin animación.
    private void TerminarSinEfecto()
    {
        if(_from == null || _to == null) return;

        foreach(Renderer r in Renderers())
        {
            Reemplazar(r, _from, _to);
            if(_transicion != null) Reemplazar(r, _transicion, _to);
        }

        if(_transicion != null) Destroy(_transicion);
        _transicion = null;
        if(_particulasBorde != null) _particulasBorde.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _aplicado = true;
    }

    // Diagnóstico: ningún renderer tenía "From". Muestra qué materiales tienen, para encontrar quién los cambió.
    private void LogMaterialesActuales()
    {
        var sb = new System.Text.StringBuilder();
        foreach(Renderer r in Renderers())
        {
            sb.Append("\n  ").Append(r.name).Append(": ");
            foreach(Material m in r.sharedMaterials) sb.Append(m != null ? m.name : "null").Append(", ");
        }
        Debug.LogWarning($"[{name}] Ningún renderer usa '{(_from != null ? _from.name : "null")}' al purificarse. Materiales:{sb}", this);
    }

    // Las partículas del borde no se tocan (tienen su propio material).
    private IEnumerable<Renderer> Renderers()
    {
        foreach(Renderer r in GetComponentsInChildren<Renderer>(true))
            if(!(r is ParticleSystemRenderer)) yield return r;
    }

    private static bool Reemplazar(Renderer r, Material de, Material a)
    {
        Material[] materials = r.sharedMaterials;
        bool changed = false;

        for(int i = 0; i < materials.Length; i++)
        {
            // También acepta una instancia del material ("CorruptTurret (Instance)"), por si algún script usó r.material.
            if(!EsMismoMaterial(materials[i], de)) continue;

            materials[i] = a;
            changed = true;
        }

        if(changed) r.sharedMaterials = materials;
        return changed;
    }

    private static bool EsMismoMaterial(Material actual, Material buscado)
    {
        if(actual == buscado) return true;
        if(actual == null || buscado == null) return false;
        return actual.name == buscado.name + " (Instance)";
    }

    // Instancia del shader de transición con las texturas de los dos Lit (ver PaintTransitionMaterial) y el look del borde.
    private Material CrearMaterialTransicion()
    {
        Material m = PaintTransitionMaterial.Create(_paintShader, _from, _to);
        PaintTransitionMaterial.SetLook(m, _colorBorde, _intensidadBorde, _anchoBorde, _irregularidad, _intensidadPulso);
        return m;
    }
}
