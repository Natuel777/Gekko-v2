using UnityEngine;
using UnityEngine.EventSystems;

public class ScreenOptions : Screens
{
    private GameObject _lastSelected;

    [Header("Paneles por categoria")]
    [SerializeField] private GameObject _gamePanel;
    [SerializeField] private GameObject _graphicsPanel;
    [SerializeField] private GameObject _soundPanel;
    [SerializeField] private GameObject _controlsPanel;

    [Header("Pestañas")]
    [SerializeField] private OptionsTab _gameTab;
    [SerializeField] private OptionsTab _graphicsTab;
    [SerializeField] private OptionsTab _soundTab;
    [SerializeField] private OptionsTab _controlsTab;

    [Header("Solo en nivel")]
    [Tooltip("Se ocultan si Opciones se abre desde el menú principal (ej. Reiniciar nivel: ahí no hay nivel que reiniciar).")]
    [SerializeField] private GameObject[] _levelOnly;

    [Header("Solo en menú principal")]
    [Tooltip("Se muestran solo si Opciones se abre desde el menú principal (ej. Borrar datos del jugador).")]
    [SerializeField] private GameObject[] _menuOnly;

    [Header("Navegación de la pestaña Idioma")]
    [Tooltip("Fila de idioma: hacia abajo va a Reiniciar nivel (en un nivel) o a Borrar datos (en el menú).")]
    [SerializeField] private UnityEngine.UI.Selectable _languageSelector;
    [SerializeField] private UnityEngine.UI.Selectable _restartButton;
    [SerializeField] private UnityEngine.UI.Selectable _deleteDataButton;
    [SerializeField] private TMPro.TMP_Text _deleteDataLabel;

    private void OnEnable() => SaveManager.OnProgressDeleted += OnProgressDeleted;
    private void OnDisable() => SaveManager.OnProgressDeleted -= OnProgressDeleted;

    public override void Activate()
    {
        base.Activate();

        // GameManager sobrevive entre escenas (DontDestroyOnLoad): desde el menú, RestartLvl cargaría el último nivel.
        bool inMainMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ScenesDictionary.SceneName[ScenesNames.Menu];
        if (_levelOnly != null)
            foreach (var go in _levelOnly) if (go) go.SetActive(!inMainMenu);
        if (_menuOnly != null)
            foreach (var go in _menuOnly) if (go) go.SetActive(inMainMenu);

        // La navegación explícita no salta objetos ocultos: apuntar al botón que esté visible.
        if (_languageSelector)
        {
            var nav = _languageSelector.navigation;
            nav.selectOnDown = inMainMenu ? _deleteDataButton : _restartButton;
            _languageSelector.navigation = nav;
        }
        if (_deleteDataButton)
        {
            var nav = _deleteDataButton.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnUp = _languageSelector;
            _deleteDataButton.navigation = nav;
        }

        AudioManager.instance.LoadGame();
        AudioManager.instance.SetMasterVolume(AudioManager.instance.masterValue);
        AudioManager.instance.SetMusicVolume(AudioManager.instance.musicValue);
        AudioManager.instance.SetSFXVolume(AudioManager.instance.sfxValue);

        ShowGame();

        // Sin un objeto seleccionado en el EventSystem las flechas/WASD no hacen nada hasta hacer click en algo.
        FocusTab(_gameTab);
    }

    private void Update()
    {
        var es = EventSystem.current;
        if (!es) return;

        if (es.currentSelectedGameObject != null)
        {
            _lastSelected = es.currentSelectedGameObject;
            return;
        }

        // Si el mouse deselecciono todo (click en vacio), la primera flecha/WASD devuelve el foco.
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
            Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
            Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
        {
            if (_lastSelected && _lastSelected.activeInHierarchy) es.SetSelectedGameObject(_lastSelected);
            else FocusTab(_gameTab);
        }
    }

    private void FocusTab(OptionsTab tab)
    {
        if (tab && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(tab.gameObject);
    }

    public override void Free()
    {
        AudioManager.instance.SaveGame();

        // Teclas cambiadas sin apretar Aplicar: se descartan al salir.
        InputRebindStore.DiscardPending();

        // Back: animación de salida (la entrada al revés) y recién después se destruye el canvas.
        var intro = GetComponent<OptionsIntroAnimation>();
        if (intro)
        {
            Deactivate(); // botones no interactuables mientras se va
            intro.PlayOut(() => base.Free());
        }
        else base.Free();
    }

    public void BTN_Game() => ShowGame();
    public void BTN_Graphics() => ShowGraphics();
    public void BTN_Sound() => ShowSound();
    public void BTN_Controls() => ShowControls();

    public void BTN_Restart()
    {
        // Por si se llega igual (ej. un atajo): desde el menú principal no hay nivel que reiniciar.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ScenesDictionary.SceneName[ScenesNames.Menu]) return;
        AudioManager.instance.Play(SoundNames.UiButton);
        IScreen screen = ScreenManager.Instance.PushAndGet("Canvas_Reafirm");
        if (screen is ScreenReafirm reafirm)
            reafirm.SetValues(ReafirmCanvas.RestartLevel);
    }

    // Borrar datos del jugador (solo progreso): pide confirmación con el cartel.
    public void BTN_DeleteData()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.UiButton);
        IScreen screen = ScreenManager.Instance.PushAndGet("Canvas_Reafirm");
        if (screen is ScreenReafirm reafirm)
            reafirm.SetValues(ReafirmCanvas.DeleteData);
    }

    private void OnProgressDeleted()
    {
        if (_deleteDataLabel) _deleteDataLabel.text = Localizer.Get("data_deleted");
    }

    private void ShowGame() => Select(_gamePanel, _gameTab);
    private void ShowGraphics() => Select(_graphicsPanel, _graphicsTab);
    private void ShowSound() => Select(_soundPanel, _soundTab);
    private void ShowControls() => Select(_controlsPanel, _controlsTab);

    private void Select(GameObject panel, OptionsTab tab)
    {
        if (_gamePanel) _gamePanel.SetActive(_gamePanel == panel);
        if (_graphicsPanel) _graphicsPanel.SetActive(_graphicsPanel == panel);
        if (_soundPanel) _soundPanel.SetActive(_soundPanel == panel);
        if (_controlsPanel) _controlsPanel.SetActive(_controlsPanel == panel);

        if (_gameTab) _gameTab.SetSelected(_gameTab == tab);
        if (_graphicsTab) _graphicsTab.SetSelected(_graphicsTab == tab);
        if (_soundTab) _soundTab.SetSelected(_soundTab == tab);
        if (_controlsTab) _controlsTab.SetSelected(_controlsTab == tab);
    }
}
