using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEditor;

public class ScreenReafirm : Screens
{
    public static ScreenReafirm Instance;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;

    private void Start()
    {
        Instance = this;
    }
    public void SetValues(ReafirmCanvas change)
    {
        _button.onClick.RemoveAllListeners();
        if (change == ReafirmCanvas.GoToMenu)
        {
            _button.onClick.AddListener(BTN_Menu);
            _text.text = "go back to the menu. You will lose all your progress";
        }
        else if (change == ReafirmCanvas.RestartLevel)
        {
            _button.onClick.AddListener(BTN_RestartLvl);
            _text.text = "Restart the level. You will lose all your progress ";
        }
        else if (change == ReafirmCanvas.ExitGame)
        {
            _button.onClick.AddListener(BTN_ExitGame);
            _text.text = "Exit the game";
        }
    }
    private void BTN_Menu()
    {
        if(GameManager.Instance)
        GameManager.Instance.IsPause = false;
        if (AudioManager.instance)
        {
            AudioManager.instance.ResetAudio();
            AudioManager.instance.Play(SoundNames.UiButton);
        }
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
    }
    private void BTN_RestartLvl()
    {
        if (GameManager.Instance)
            GameManager.Instance.RestartLvl();
    }
    private void BTN_ExitGame()
    {
        SaveManager.Instance.SaveGame();
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }


}
