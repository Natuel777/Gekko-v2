using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine;
using UnityEditor;

public class ScreenReafirm : Screens
{
    public static ScreenReafirm Instance;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;
    [Tooltip("Título del banner (opcional): cambia según la acción a confirmar.")]
    [SerializeField] private TextMeshProUGUI _title;
    [Tooltip("Botón 'Volver': arranca con el foco para no confirmar sin querer con Enter/joystick.")]
    [SerializeField] private Button _backButton;

    private void Start()
    {
        Instance = this;
    }

    public override void Activate()
    {
        base.Activate();
        if (_backButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(_backButton.gameObject);
    }

    public override void Free()
    {
        // Volver: animación de salida y recién después se destruye el cartel.
        var anim = GetComponent<OptionsIntroAnimation>();
        if (anim && gameObject.activeInHierarchy)
        {
            Deactivate(); // botones no interactuables mientras se va
            anim.PlayOut(() => base.Free());
        }
        else base.Free();
    }

    public void SetValues(ReafirmCanvas change)
    {
        // Con MenuButtonFX el onClick dispara el efecto (Click) y la acción real va en OnConfirmed
        var fx = _button.GetComponent<MenuButtonFX>();
        UnityEngine.Events.UnityEvent action = fx ? fx.OnConfirmed : _button.onClick;
        action.RemoveAllListeners();
        if (change == ReafirmCanvas.GoToMenu)
        {
            action.AddListener(BTN_Menu);
            SetTexts("reafirm_menu_title", "reafirm_menu_msg");
        }
        else if (change == ReafirmCanvas.RestartLevel)
        {
            action.AddListener(BTN_RestartLvl);
            SetTexts("reafirm_restart_title", "reafirm_restart_msg");
        }
        else if (change == ReafirmCanvas.ExitGame)
        {
            action.AddListener(BTN_ExitGame);
            SetTexts("reafirm_exit_title", "reafirm_exit_msg");
        }
        else if (change == ReafirmCanvas.DeleteData)
        {
            action.AddListener(BTN_DeleteData);
            SetTexts("reafirm_delete_title", "reafirm_delete_msg");
        }
    }

    private void BTN_DeleteData()
    {
        SaveManager.DeleteProgress();
        // Vuelve a Opciones (el cartel se cierra con su animación).
        if (ScreenManager.Instance) ScreenManager.Instance.Pop();
    }

    private void SetTexts(string titleKey, string messageKey)
    {
        if (_title) _title.text = Localizer.Get(titleKey);
        if (_text) _text.text = Localizer.Get(messageKey);
    }

    private void BTN_Menu()
    {
        if(GameManager.Instance)
        GameManager.Instance.CanPause = false;
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
