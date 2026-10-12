using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int _index = -1;
    [SerializeField] private Transform _respawnPoint;

    [Header("Runa")]
    [SerializeField] private Renderer _runeRenderer;
    [SerializeField] private Color _runeEmissionColor = new Color(0.498f, 0.965f, 1f, 1f);
    [SerializeField] private float _runeIntensity = 2f;
    [SerializeField] private float _runeFadeDuration = 0.5f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private Material _runeMaterial;
    private bool _activated;

    public int Index => _index;

    // Punto fijo de aparición: el hijo _respawnPoint si está asignado, si no la posición del checkpoint.
    public Vector3 RespawnPosition => _respawnPoint != null ? _respawnPoint.position : transform.position;
    public Quaternion RespawnRotation => _respawnPoint != null ? _respawnPoint.rotation : transform.rotation;

    // Para que la UI muestre el aviso "Checkpoint" / ícono de guardado.
    public static event Action<Checkpoint> OnCheckpointActivated;

    private void Awake()
    {
        if(_runeRenderer == null) return;

        _runeMaterial = _runeRenderer.material;
        _runeMaterial.EnableKeyword("_EMISSION");
        _runeMaterial.SetColor(EmissionColorId, Color.black);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!other.TryGetComponent(out Player player)) return;

        GameManager.Instance.checkpointManager.SaveCheckpoint(
            RespawnPosition,
            RespawnRotation,
            player.health.CurrentHealth,
            _index);

        if(_activated) return;
        _activated = true;

        if(_runeMaterial != null) StartCoroutine(RuneFadeInRoutine());
        OnCheckpointActivated?.Invoke(this);
    }

    private IEnumerator RuneFadeInRoutine()
    {
        float elapsed = 0f;
        Color targetColor = _runeEmissionColor * _runeIntensity;

        while(elapsed < _runeFadeDuration)
        {
            elapsed += Time.deltaTime;
            _runeMaterial.SetColor(EmissionColorId, Color.Lerp(Color.black, targetColor, elapsed / _runeFadeDuration));
            yield return null;
        }

        _runeMaterial.SetColor(EmissionColorId, targetColor);
    }
}
