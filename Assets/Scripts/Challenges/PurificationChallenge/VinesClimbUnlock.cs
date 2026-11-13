using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Superficie escalable tapada por enredaderas (ej. el tronco del árbol de la Second Zone en LvlTwo). Mientras las
// enredaderas (ChallengeVinesDissolve) están enteras no se puede trepar: la superficie pasa a una layer sólida que no está en
// GameManager.ClimbLayer ni en GroundLayer. Cuando terminan de disolverse vuelve a su layer original y aparecen, una por una
// y en orden, las patitas (decals) que marcan que ya se puede escalar.
public class VinesClimbUnlock : MonoBehaviour
{
    [Tooltip("Enredaderas que tapan la superficie. Al terminar de disolverse se habilita la escalada.")]
    [SerializeField] private ChallengeVinesDissolve _vines;

    [Header("Escalada")]
    [Tooltip("Objetos con el collider escalable (ej. Tronco_2). Mientras haya enredaderas se los pasa a la layer bloqueada.")]
    [SerializeField] private GameObject[] _surfaces;
    [Tooltip("Layer mientras las enredaderas tapan la superficie: sólida pero fuera de ClimbLayer y GroundLayer (14 = Obstacle).")]
    [Range(0, 31)]
    [SerializeField] private int _lockedLayer = 14;

    [Header("Patitas")]
    [Tooltip("En el orden en que aparecen (de abajo hacia arriba). De cada una se toman todos los DecalProjector de sus hijos (Pata y PataNormal).")]
    [SerializeField] private Transform[] _footprints;
    [Tooltip("Segundos de espera desde que terminan de disolverse las enredaderas hasta la primera patita.")]
    [Min(0f)]
    [SerializeField] private float _delay = 0.2f;
    [Tooltip("Segundos entre una patita y la siguiente.")]
    [Min(0f)]
    [SerializeField] private float _interval = 0.25f;
    [Tooltip("Segundos que tarda cada patita en aparecer.")]
    [Min(0.01f)]
    [SerializeField] private float _fadeDuration = 0.35f;
    [Tooltip("Tamaño con el que arranca cada patita: crece hasta su tamaño mientras aparece.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float _startScale = 0.8f;

    private int[] _originalLayers;
    private DecalProjector[][] _decals;
    private Vector3[][] _decalSizes;
    private bool _initialized, _unlocked;

    // Avisan el avance de las patitas (la cámara de VinesRevealCinematic las sigue): cada una al empezar a aparecer, y al
    // terminar de aparecer la última.
    public event Action<Transform> FootprintRevealed;
    public event Action FootprintsRevealed;
    public bool AllFootprintsRevealed { get; private set; }

    private void Awake() => EnsureInit();

    private void OnEnable()
    {
        EnsureInit();

        // Sin enredaderas no hay nada que espere: mejor dejar la superficie escalable que trabada para siempre.
        if(_vines == null)
        {
            Debug.LogWarning($"[{name}] VinesClimbUnlock sin ChallengeVinesDissolve asignado: la superficie queda escalable desde el arranque.", this);
            Unlock();
            return;
        }

        _vines.Dissolved += Unlock;

        if(_vines.IsDissolved) Unlock();
    }

    private void OnDisable()
    {
        if(_vines != null) _vines.Dissolved -= Unlock;
    }

    private void EnsureInit()
    {
        if(_initialized) return;
        _initialized = true;

        // La superficie arranca bloqueada.
        _originalLayers = new int[_surfaces != null ? _surfaces.Length : 0];
        for(int i = 0; i < _originalLayers.Length; i++)
        {
            if(_surfaces[i] == null) continue;

            _originalLayers[i] = _surfaces[i].layer;
            _surfaces[i].layer = _lockedLayer;
        }

        // Las patitas arrancan apagadas.
        int count = _footprints != null ? _footprints.Length : 0;
        _decals = new DecalProjector[count][];
        _decalSizes = new Vector3[count][];

        for(int i = 0; i < count; i++)
        {
            _decals[i] = _footprints[i] != null ? _footprints[i].GetComponentsInChildren<DecalProjector>(true) : new DecalProjector[0];
            _decalSizes[i] = new Vector3[_decals[i].Length];

            for(int j = 0; j < _decals[i].Length; j++)
            {
                _decalSizes[i][j] = _decals[i][j].size;
                _decals[i][j].fadeFactor = 0f;
                _decals[i][j].enabled = false;
            }
        }
    }

    private void Unlock()
    {
        if(_unlocked) return;
        _unlocked = true;

        for(int i = 0; i < _originalLayers.Length; i++)
            if(_surfaces[i] != null) _surfaces[i].layer = _originalLayers[i];

        // Desactivado no puede correr la corrutina: las patitas aparecen de una.
        if(!gameObject.activeInHierarchy)
        {
            for(int i = 0; i < _decals.Length; i++) SetFootprint(i, 1f);
            FinishReveal();
            return;
        }

        StartCoroutine(RevealFootprints());
    }

    private IEnumerator RevealFootprints()
    {
        if(_delay > 0f) yield return new WaitForSeconds(_delay);

        for(int i = 0; i < _decals.Length; i++)
        {
            StartCoroutine(FadeIn(i));
            if(_footprints[i] != null) FootprintRevealed?.Invoke(_footprints[i]);

            // Después de la última solo se espera a que termine de aparecer.
            float wait = i < _decals.Length - 1 ? _interval : _fadeDuration;
            if(wait > 0f) yield return new WaitForSeconds(wait);
        }

        FinishReveal();
    }

    private void FinishReveal()
    {
        if(AllFootprintsRevealed) return;
        AllFootprintsRevealed = true;
        FootprintsRevealed?.Invoke();
    }

    private IEnumerator FadeIn(int index)
    {
        for(float t = 0f; t < 1f; t += Time.deltaTime / _fadeDuration)
        {
            SetFootprint(index, t);
            yield return null;
        }

        SetFootprint(index, 1f);
    }

    // Fundido de 0 (apagada) a 1 (entera): la patita se enciende y "se asienta" creciendo hasta su tamaño.
    private void SetFootprint(int index, float t)
    {
        float fade = Mathf.SmoothStep(0f, 1f, t);
        float scale = Mathf.Lerp(_startScale, 1f, 1f - (1f - t) * (1f - t) * (1f - t));

        for(int j = 0; j < _decals[index].Length; j++)
        {
            DecalProjector decal = _decals[index][j];
            if(decal == null) continue;

            Vector3 size = _decalSizes[index][j];
            decal.enabled = true;
            decal.fadeFactor = fade;
            decal.size = new Vector3(size.x * scale, size.y * scale, size.z);
        }
    }
}
