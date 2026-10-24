using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

// Logica de "mantener E para saltear" compartida entre cinematicas (PurificationChallenge y
// ParkourCinematicTrigger la usan igual). Quien la posea le pasa el PlayableDirector a chequear
// cada frame; cuando se completa el hold, el handler para el director y avisa por onSkip.
public class CinematicSkipHandler
{
    private readonly float _holdSeconds;
    private readonly SkipCinematicPrompt _prompt;
    private float _elapsed;

    public CinematicSkipHandler(float holdSeconds, SkipCinematicPrompt prompt)
    {
        _holdSeconds = Mathf.Max(0.1f, holdSeconds);
        _prompt = prompt;
    }

    // director: la cinematica que se podria saltear ahora mismo, o null si ninguna esta activa.
    public void ArtificialUpdate(PlayableDirector director, Action onSkip = null)
    {
        Keyboard kb = Keyboard.current;
        bool held = director != null && director.state == PlayState.Playing && kb != null && kb.eKey.isPressed;

        if(!held)
        {
            if(_elapsed > 0f)
            {
                _elapsed = 0f;
                if(_prompt != null) _prompt.SetVisible(false);
            }
            return;
        }

        _elapsed += Time.deltaTime;

        if(_prompt != null)
        {
            _prompt.SetVisible(true);
            _prompt.SetProgress(_elapsed / _holdSeconds);
        }

        if(_elapsed < _holdSeconds) return;

        _elapsed = 0f;
        if(_prompt != null) _prompt.SetVisible(false);
        director.Stop();
        onSkip?.Invoke();
    }
}
