using System;
using UnityEngine;

public class LevelTwoManager : LevelManager
{
    public event Action OnBeaverMission;
    // Música propia de LvlTwo: SoundNames.LvlTwo -> SoundSO LvlTwo.asset (en la lista "sounds" del prefab AudioManager), independiente
    // de la de LvlOne: cambiarle el clip a uno no toca al otro. SoundNames.LvlTwo está en la lista de GameManager.Pause, así que la
    // música sigue sonando con el juego en pausa, igual que en LvlOne.
    public override void StartSong()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlTwo, true);
    }
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
