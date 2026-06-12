using UnityEngine;

public class FinishLevelTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var screenFinish = Instantiate(Resources.Load<ScreenFinishLevel>("Canvas_Finish"));
        ScreenManager.Instance.Push(screenFinish);
        Cursor.lockState = CursorLockMode.Confined;
    }
}
