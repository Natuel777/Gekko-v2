using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class FirstManager : MonoBehaviour
{
    [Header("Turrets to Track")]
    [SerializeField] private List<ChallengeTurret> _turretsToTrack = new List<ChallengeTurret>();

    private bool _challengeCompleted;

    private void Start()
    {
        foreach (ChallengeTurret turret in _turretsToTrack)
        {
            turret.Purified += OnTurretPurified;
        }

        CheckChallengeCompletion();
    }

    private void OnDestroy()
    {
        foreach (ChallengeTurret turret in _turretsToTrack)
        {
            turret.Purified -= OnTurretPurified;
        }
    }

    private void OnTurretPurified(ChallengeTurret turret)
    {
        Debug.Log($"¡La torreta {turret.name} fue purificada!");
        CheckChallengeCompletion();
    }

    private void CheckChallengeCompletion()
    {
        if (_challengeCompleted) return;

        foreach (ChallengeTurret turret in _turretsToTrack)
        {
            if (turret == null)
            {
                Debug.LogWarning("Hay una referencia vacía en la lista de torretas!");
                return;
            }

            if (!turret.IsPurified)
                return;
        }

        _challengeCompleted = true;
        Debug.Log("¡CHALLENGE COMPLETADO! Todas las torretas fueron purificadas.");
    }
}
