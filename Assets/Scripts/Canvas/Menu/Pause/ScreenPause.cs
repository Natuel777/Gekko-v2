
using UnityEngine;

public class ScreenPause : Screens
{
    public void Initialize()
    {

    }

    public void BTN_Menu()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        IScreen screen = ScreenManager.Instance.PushAndGet("Canvas_Reafirm");
        if (screen is ScreenReafirm reafirm)
            reafirm.SetValues(ReafirmCanvas.GoToMenu);
    }

    public void BTN_Options()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        // La pausa se va mientras entra Opciones (sin ocultarse: sigue en la pila). Vuelve a entrar en Activate.
        var intro = GetComponent<OptionsIntroAnimation>();
        if (intro) intro.PlayOut(null);
        ScreenManager.Instance.Push("Canvas_Options");
    }

    public override void BTN_Back()
    {
        base.BTN_Back();
        if(GameManager.Instance)
        {
            GameManager.Instance.CanPause = false;
            GameManager.Instance.Pj.ActivateInputs();
            Cursor.lockState = CursorLockMode.Locked;
            Time.timeScale = 1f;
            EventManager.Trigger("UnPauseEvent");
        }
    }

    public override void Activate()
    {
        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true); // si estaba oculta, OnEnable de la animación ya hace la entrada
        base.Activate();

        // Ya estaba visible pero afuera de pantalla (volviendo de Opciones, o pausando de nuevo mientras salía): entrar.
        var intro = GetComponent<OptionsIntroAnimation>();
        if (wasActive && intro && intro.IsOut) intro.PlayIn();
    }

    public override void Free()
    {
        // Continuar/Esc: animación de salida y recién después se oculta (la pantalla se reutiliza, no se destruye).
        var intro = GetComponent<OptionsIntroAnimation>();
        if (intro && gameObject.activeInHierarchy)
        {
            Deactivate(); // botones no interactuables mientras se va
            intro.PlayOut(() => gameObject.SetActive(false));
        }
        else gameObject.SetActive(false);
    }
}