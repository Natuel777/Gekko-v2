using System;
using System.Collections;
using UnityEngine;

// Muérdago del jefe (estilo Plants vs Zombies): aparece en un punto de la arena, avisa por
// _warnDuration segundos y suelta un HeavyBeetle (prefab existente, sin modificar) en su posición.
// Visual placeholder: todavía no tiene arte/telegrafiado propio.
public class Mistletoe : MonoBehaviour
{
    [SerializeField] private float _warnDuration = 1.5f;

    private HeavyBeetle _beetlePrefab;
    private Action<Mistletoe> _onDone;
    private bool _initialized;

    public void Init(HeavyBeetle beetlePrefab, Action<Mistletoe> onDone)
    {
        _beetlePrefab = beetlePrefab;
        _onDone = onDone;
        _initialized = true;
    }

    private void Start()
    {
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSeconds(_warnDuration);

        if(_beetlePrefab != null)
            Instantiate(_beetlePrefab, transform.position, transform.rotation);

        if(_initialized) _onDone?.Invoke(this);

        GameManager.Instance.DestroyObject(gameObject);
    }
}
