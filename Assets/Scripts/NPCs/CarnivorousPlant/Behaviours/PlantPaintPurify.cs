using System.Collections;
using System.Collections.Generic;
using Gekko.Purification;
using UnityEngine;

// Purificación pintada de la planta carnívora (DataSO > Paint On Hit): cada golpe que cuenta pinta la planta de abajo hacia
// arriba con el material purificado, con el mismo shader que las torretas (Paint Transition). Con 3 golpes sube 1/3 por golpe.
// Lo maneja CarnivorousPlant (OnHit en los golpes que no purifican, Complete al aplicar el material purificado), siguiendo el
// patrón de clases planas del proyecto; las corrutinas corren en la planta.
//
// Por golpe: tramo de pintado con la franja dorada y chispas que la acompañan, destello de brillo en toda la planta, chispas
// donde pegó la lengua y el piso alrededor (la PurifyRegion que contiene a la planta) se limpia un tramo.
// Golpe final: cámara lenta corta, termina de pintarse, destello final y queda Purified Material en todos los renderers (como
// antes). El piso termina de limpiarse.
//
// La planta se mueve mucho (muerde, se dobla): para que la pintura no "nade" sobre el cuerpo, la altura se mide sobre la pose
// de reposo, guardada al arrancar en TEXCOORD3 de una copia de cada malla (el shader la usa con _UseRestPosition). Por eso el
// FBX de la planta necesita Read/Write.
public class PlantPaintPurify
{
    private const float HitFlashSeconds = 0.3f;
    private const float FinalPulseSeconds = 0.6f;

    private readonly CarnivorousPlant _plant;
    private readonly CarnivorousPlantDataSO _data;
    private readonly SkinnedMeshRenderer[] _allRenderers;
    private readonly List<SkinnedMeshRenderer> _targets = new();
    private readonly List<Mesh> _meshCopies = new();
    private readonly Material _from;

    private Material _paint;
    private bool _restReady;
    private float _bottom, _top;
    private Vector3 _center;
    private float _height; // altura actual del frente (mundo, pose de reposo)
    private Coroutine _paintRoutine, _flashRoutine;
    private ParticleSystem _edgeSparks;
    private PurifyRegion _region;
    private bool _regionSearched, _completed;

    public PlantPaintPurify(CarnivorousPlant plant, SkinnedMeshRenderer[] renderers, CarnivorousPlantDataSO data)
    {
        _plant = plant;
        _data = data;
        _allRenderers = renderers ?? new SkinnedMeshRenderer[0];

        // El material corrupto es el más usado (la lengua tiene otro): se pintan solo las piezas que lo usan.
        _from = MostUsedMaterial(_allRenderers);
        foreach(SkinnedMeshRenderer r in _allRenderers)
            if(r != null && r.sharedMaterial == _from) _targets.Add(r);

        BakeRestPose();
    }

    // Golpe que cuenta pero no purifica (hits < needed).
    public void OnHit(int hits, int needed, Vector3? point)
    {
        if(_completed || !EnsurePaint()) return;

        float fraction = Mathf.Clamp01((float)hits / Mathf.Max(1, needed));

        StartPaint(fraction, _data.paintStepSeconds, false);
        Flash(_data.paintHitFlashIntensity, HitFlashSeconds);
        SpawnImpact(point);
        AdvanceGround(fraction);
    }

    // Golpe final (o DebugPurify): reemplaza el cambio directo a Purified Material.
    public void Complete(Vector3? point)
    {
        if(_completed) return;
        _completed = true;

        if(!EnsurePaint())
        {
            ApplyPurifiedToAll();
            return;
        }

        SpawnImpact(point);
        AdvanceGround(1f);
        _plant.StartCoroutine(SlowMo());
        StartPaint(1f, _data.paintStepSeconds, true);
    }

    public void Dispose()
    {
        if(_paint != null) Object.Destroy(_paint);
        foreach(Mesh m in _meshCopies)
            if(m != null) Object.Destroy(m);
    }

    // ------------------------------------------------------------------ Pintado

    private bool EnsurePaint()
    {
        if(_paint != null) return true;
        if(!_restReady || _data.paintShader == null || _data.purifiedMaterial == null || _from == null) return false;

        _paint = PaintTransitionMaterial.Create(_data.paintShader, _from, _data.purifiedMaterial);

        // Medidas en proporción a la planta (en la torreta, de 1,6 m: irregularidad 0,14 y borde 0,1).
        float size = Mathf.Max(0.1f, _top - _bottom);
        PaintTransitionMaterial.SetLook(_paint, _data.paintEdgeColor, _data.paintEdgeIntensity, size * 0.06f, size * 0.085f,
                                        _data.paintFinalPulseIntensity);
        _paint.SetFloat(PaintTransitionMaterial.UseRestPositionId, 1f);

        _height = Bottom;
        _paint.SetFloat(PaintTransitionMaterial.PaintHeightId, _height);

        foreach(SkinnedMeshRenderer r in _targets)
            if(r != null) r.sharedMaterial = _paint;

        return true;
    }

    // Margen: el ruido adelanta/atrasa el frente y la franja tiene ancho; sin esto quedarían manchas al empezar o al terminar.
    private float Margin => (_top - _bottom) * (0.085f + 0.06f);
    private float Bottom => _bottom - Margin;
    private float Top => _top + Margin;

    private void StartPaint(float fraction, float seconds, bool final)
    {
        if(_paintRoutine != null) _plant.StopCoroutine(_paintRoutine);
        _paintRoutine = _plant.StartCoroutine(PaintRoutine(Mathf.LerpUnclamped(Bottom, Top, fraction), seconds, final));
    }

    private IEnumerator PaintRoutine(float target, float seconds, bool final)
    {
        float from = _height;
        PlayEdgeSparks(true);

        for(float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, seconds));
            _height = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            _paint.SetFloat(PaintTransitionMaterial.PaintHeightId, _height);
            MoveEdgeSparks();
            yield return null;
        }

        PlayEdgeSparks(false);
        _paintRoutine = null;

        if(!final) yield break;

        // Destello final y recién ahí el material purificado de verdad.
        if(_flashRoutine != null) _plant.StopCoroutine(_flashRoutine);
        yield return FlashRoutine(_data.paintFinalPulseIntensity, FinalPulseSeconds);

        ApplyPurifiedToAll();
    }

    private void Flash(float intensity, float seconds)
    {
        if(intensity <= 0f) return;
        if(_flashRoutine != null) _plant.StopCoroutine(_flashRoutine);
        _flashRoutine = _plant.StartCoroutine(FlashRoutine(intensity, seconds));
    }

    // Sube y baja (seno). _PulseIntensity = 1 y el brillo va en el valor de _Pulse, así el destello por golpe y el final
    // pueden tener intensidades distintas.
    private IEnumerator FlashRoutine(float intensity, float seconds)
    {
        _paint.SetFloat(PaintTransitionMaterial.PulseIntensityId, intensity);

        for(float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, seconds));
            _paint.SetFloat(PaintTransitionMaterial.PulseId, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        _paint.SetFloat(PaintTransitionMaterial.PulseId, 0f);
        _flashRoutine = null;
    }

    // Igual que antes de este sistema (CarnivorousPlant.ApplyPurifiedMaterial): todos los renderers, lengua incluida.
    private void ApplyPurifiedToAll()
    {
        if(_data.purifiedMaterial == null) return;

        foreach(SkinnedMeshRenderer r in _allRenderers)
            if(r != null) r.sharedMaterial = _data.purifiedMaterial;

        if(_paint != null) Object.Destroy(_paint);
        _paint = null;
    }

    // ------------------------------------------------------------------ Pose de reposo

    // Copia de cada malla con la posición de cada vértice en la pose actual (al arrancar, antes de animar) guardada en
    // TEXCOORD3, en mundo. El orden de BakeMesh coincide con el de sharedMesh.
    private void BakeRestPose()
    {
        if(_targets.Count == 0) return;

        bool any = false;
        Bounds bounds = default;
        var baked = new Mesh();
        var rest = new List<Vector4>();

        foreach(SkinnedMeshRenderer r in _targets)
        {
            Mesh source = r.sharedMesh;
            if(source == null) continue;

            if(!source.isReadable)
            {
                Debug.LogWarning($"[{_plant.name}] La malla '{source.name}' no tiene Read/Write: la purificación pintada usa la posición animada (puede deslizarse un poco).", _plant);
                return;
            }

            r.BakeMesh(baked, false);
            Vector3[] vertices = baked.vertices;
            rest.Clear();

            foreach(Vector3 v in vertices)
            {
                Vector3 world = r.transform.TransformPoint(v);
                rest.Add(world);

                if(!any) { bounds = new Bounds(world, Vector3.zero); any = true; }
                else bounds.Encapsulate(world);
            }

            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (reposo)";
            copy.SetUVs(3, rest);
            r.sharedMesh = copy;
            _meshCopies.Add(copy);
        }

        Object.Destroy(baked);
        if(!any) return;

        _bottom = bounds.min.y;
        _top = bounds.max.y;
        _center = bounds.center;
        _restReady = true;
    }

    // ------------------------------------------------------------------ Feedback

    private void SpawnImpact(Vector3? point)
    {
        if(_data.paintImpactSparks == null) return;

        Vector3 at = point ?? new Vector3(_center.x, Mathf.Lerp(_bottom, _top, 0.6f), _center.z);
        Camera cam = Camera.main;
        Quaternion rotation = cam != null ? Quaternion.LookRotation(cam.transform.position - at) : Quaternion.identity;

        Object.Instantiate(_data.paintImpactSparks, at, rotation);
    }

    private void PlayEdgeSparks(bool play)
    {
        if(_data.paintEdgeSparks == null) return;

        if(_edgeSparks == null)
        {
            GameObject go = Object.Instantiate(_data.paintEdgeSparks, _plant.transform);
            _edgeSparks = go.GetComponent<ParticleSystem>();
            if(_edgeSparks == null) return;

            // El radio del aro de chispas, en proporción al ancho de la planta.
            ParticleSystem.ShapeModule shape = _edgeSparks.shape;
            shape.radius = Mathf.Max(0.2f, (_top - _bottom) * 0.25f);
        }

        if(play)
        {
            MoveEdgeSparks();
            _edgeSparks.Play(true);
        }
        else _edgeSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void MoveEdgeSparks()
    {
        if(_edgeSparks == null) return;
        _edgeSparks.transform.position = new Vector3(_center.x, Mathf.Clamp(_height, _bottom, _top), _center.z);
    }

    private void AdvanceGround(float fraction)
    {
        if(!_regionSearched)
        {
            _regionSearched = true;
            _region = PurifyRegion.Buscar(_plant.transform.position);
        }

        if(_region != null) _region.AvanzarHasta(fraction, _data.paintGroundStepSeconds);
    }

    // Cámara lenta corta del golpe final. Si mientras tanto alguien cambió la escala (pausa, otro hitstop), no se la pisa.
    private IEnumerator SlowMo()
    {
        float scale = _data.paintFinalSlowMoScale;
        if(scale >= 1f || _data.paintFinalSlowMoSeconds <= 0f || Time.timeScale <= 0f) yield break;

        float original = Time.timeScale;
        Time.timeScale = Mathf.Min(original, scale);
        float applied = Time.timeScale;

        yield return new WaitForSecondsRealtime(_data.paintFinalSlowMoSeconds);

        if(Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = original;
    }

    private static Material MostUsedMaterial(SkinnedMeshRenderer[] renderers)
    {
        var counts = new Dictionary<Material, int>();
        Material best = null;
        int bestCount = 0;

        foreach(SkinnedMeshRenderer r in renderers)
        {
            if(r == null || r.sharedMaterial == null) continue;

            counts.TryGetValue(r.sharedMaterial, out int c);
            counts[r.sharedMaterial] = ++c;
            if(c > bestCount) { bestCount = c; best = r.sharedMaterial; }
        }

        return best;
    }
}
