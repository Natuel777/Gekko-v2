using System;
using UnityEngine;

public class LevelTwoManager : LevelManager
{
    public event Action OnBeaverMission;
    public override void StartSong()
    {
        //
    }
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
