using System;
using UnityEngine;

public class LevelTwoManager : LevelManager
{
    public event Action OnBeaverMission;
    // LvlTwo's own music: SoundNames.LvlTwo -> SoundSO LvlTwo.asset (in the AudioManager prefab's "sounds" list), independent
    // of LvlOne's: changing one's clip doesn't affect the other. SoundNames.LvlTwo is in GameManager.Pause's list, so the
    // music keeps playing while the game is paused, just like in LvlOne.
    public override void StartSong()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlTwo, true);
    }
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
