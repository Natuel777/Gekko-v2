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

    public override void Activate()
    {
        base.Activate();

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
        base.Free();
    }

    public void BTN_Game() => ShowGame();
    public void BTN_Graphics() => ShowGraphics();
    public void BTN_Sound() => ShowSound();
    public void BTN_Controls() => ShowControls();

    public void BTN_Restart()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        IScreen screen = ScreenManager.Instance.PushAndGet("Canvas_Reafirm");
        if (screen is ScreenReafirm reafirm)
            reafirm.SetValues(ReafirmCanvas.RestartLevel);
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
