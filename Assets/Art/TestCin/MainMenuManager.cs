using System.Collections;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineCamera vcamIntro;
    public CinemachineCamera vcamMainMenu;
    public CinemachineCamera vcamCredits;

    [Header("UI")]
    public GameObject titleGroup;        // título + press any key
    public GameObject mainMenuButtons;   // Play, Credits, Exit
    public GameObject ButtonsPressAnyKey;
    public GameObject BackCredits;
    public GameObject creditsText;

    private MenuState currentState = MenuState.Intro;
    public float creditsDelay = 2f;
    private void Start()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Menu, true);
    }

    void Update()
    {
        if (currentState == MenuState.Intro && Input.anyKeyDown)
            GoToMainMenu();
    }

    void GoToMainMenu()
    {
        currentState = MenuState.MainMenu;

        // Cámaras
        vcamIntro.Priority = 0;
        vcamMainMenu.Priority = 10;

        // UI
        titleGroup.SetActive(false);
        ButtonsPressAnyKey.SetActive(false);
        mainMenuButtons.SetActive(true);
        creditsText.SetActive(false);
    }

    public void OnCreditsPressed()
    {
        currentState = MenuState.Credits;

        vcamMainMenu.Priority = 0;
        vcamCredits.Priority = 10;

        mainMenuButtons.SetActive(false);
        BackCredits.SetActive(true);
        StartCoroutine(ShowCreditsAfterDelay());
    }

    IEnumerator ShowCreditsAfterDelay()
    {
        creditsText.SetActive(false);

        yield return new WaitForSeconds(creditsDelay);

        if (currentState == MenuState.Credits)
            creditsText.SetActive(true);
    }

    public void OnBackFromCredits()
    {
        currentState = MenuState.MainMenu;

        vcamCredits.Priority = 0;
        vcamMainMenu.Priority = 10;
        mainMenuButtons.SetActive(true);
        BackCredits.SetActive(false);
        creditsText.SetActive(false);
    }
    public void OnExitPressed()
    {
        SaveManager.Instance.SaveGame();
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
public enum MenuState { Intro, MainMenu, Credits }
