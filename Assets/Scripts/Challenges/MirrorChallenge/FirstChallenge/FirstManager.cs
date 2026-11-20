using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class FirstManager : MonoBehaviour
{
    [Header("Turrets to Track")]
    [SerializeField] private List<ChallengeTurret> _turretsToTrack = new List<ChallengeTurret>();

    private bool _challengeCompleted;



    [Header("Challenge Completion")]
    [SerializeField] private List<GameObject> _vinesToDisable = new List<GameObject>();

    [Header("Climb Unlock")]
    [SerializeField] private List<GameObject> _climbSurfaces = new List<GameObject>();

    [SerializeField] private int _unlockedLayer;

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
        FinishChallenge();
    }

    private void FinishChallenge()
    {

        // Desactivar las enredaderas.
        foreach (GameObject vine in _vinesToDisable)
        {
            if (vine != null)
                vine.SetActive(false);
        }

        foreach (GameObject surface in _climbSurfaces)
        {
            if (surface != null)
                surface.layer = _unlockedLayer;
        }

        Debug.Log("¡CHALLENGE COMPLETADO! Las enredaderas desaparecieron y la escalada está desbloqueada.");
    }
}
