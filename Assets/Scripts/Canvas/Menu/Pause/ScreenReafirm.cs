using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class ScreenReafirm : Screens
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;
    private ReafirmCanvas _option;
    public ReafirmCanvas Option { set { _option = value; } }


    private void Start()
    {
        _button.onClick.RemoveAllListeners();
        if (_option == ReafirmCanvas.ExitGame) _button.onClick.AddListener();
        else if (_option == ReafirmCanvas.GoToMenu) _button.onClick.AddListener(BTN_Menu);
        else if (_option == ReafirmCanvas.RestartLevel) _button.onClick.AddListener();
    }
    public void BTN_Menu()
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


}
