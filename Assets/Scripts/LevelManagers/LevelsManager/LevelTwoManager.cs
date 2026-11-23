using System;
using UnityEngine;

public class LevelTwoManager : LevelManager
{
    public event Action OnBeaverMission;
    // Misma música que LvlOne: es el mismo SoundSO (SoundNames.LvlOne -> LevelOneMusic.wav, en loop). Se usa ese nombre y no uno nuevo
    // a propósito: GameManager.Pause deja sin pausar a SoundNames.LvlOne (la música sigue sonando con el juego en pausa), y así LvlTwo
    // se comporta igual. Ojo: cambiar el clip de ese SoundSO cambia la música de los dos niveles. Si LvlTwo llega a tener música
    // propia: SoundNames + SoundSO nuevos, y sumar el nombre a la lista de GameManager.Pause.
    public override void StartSong()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlOne, true);
    }
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
