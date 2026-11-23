using System;
using System.Collections;
using UnityEngine;

// Grupo de torretas que, purificadas todas, completan una zona (ej. las 4 del PrimerChallenge en LvlTwo, que abren el
// árbol de la Second Zone). No es un Challenge: no hay planta ni escudo, solo avisa con Completed. Lo escuchan
// ChallengeVinesDissolve y VinesRevealCinematic (campo "Turret Group") en vez de un desafío.
public class TurretGroupPurified : MonoBehaviour
{
    [Tooltip("Torretas que hay que purificar.")]
    [SerializeField] private ChallengeTurret[] _turrets = new ChallengeTurret[0];
    [Tooltip("Segundos desde que se purifica la última torreta hasta que avisa (para que se vea terminar su pintado antes de la cinemática).")]
    [Min(0f)]
    [SerializeField] private float _delay = 2.5f;

    private bool _counting;

    public bool IsCompleted { get; private set; }
    public System.Collections.Generic.IReadOnlyList<ChallengeTurret> Turrets => _turrets;
    public event Action Completed;

    private void OnEnable()
    {
        foreach(ChallengeTurret turret in _turrets)
            if(turret != null) turret.Purified += OnTurretPurified;
    }

    private void OnDisable()
    {
        foreach(ChallengeTurret turret in _turrets)
            if(turret != null) turret.Purified -= OnTurretPurified;
    }

    // Si ya estaban todas purificadas al arrancar, queda completo directo (sin evento: no hay nada que mostrar).
    private void Start()
    {
        if(_turrets.Length == 0) Debug.LogWarning($"[{name}] TurretGroupPurified sin torretas asignadas.", this);
        else if(AllPurified()) IsCompleted = true;
    }

    private void OnTurretPurified(ChallengeTurret turret)
    {
        if(IsCompleted || _counting || !AllPurified()) return;

        if(_delay <= 0f || !isActiveAndEnabled)
        {
            Complete();
            return;
        }

        StartCoroutine(CompleteAfterDelay());
    }

    private IEnumerator CompleteAfterDelay()
    {
        _counting = true;
        yield return new WaitForSeconds(_delay);
        _counting = false;
        Complete();
    }

    private void Complete()
    {
        if(IsCompleted) return;

        IsCompleted = true;
        Debug.Log($"[{name}] Todas las torretas purificadas.", this);
        Completed?.Invoke();
    }

    private bool AllPurified()
    {
        foreach(ChallengeTurret turret in _turrets)
            if(turret != null && !turret.IsPurified) return false;

        return true;
    }
}
