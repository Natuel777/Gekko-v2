using UnityEngine;

public class FinishLevelTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        LevelStats.Instance?.StopTimer();

        var screenFinish = Instantiate(Resources.Load<ScreenFinishLevel>("Canvas_Finish"));
        ScreenManager.Instance.Push(screenFinish);
        Cursor.lockState = CursorLockMode.Confined;
    }
}
