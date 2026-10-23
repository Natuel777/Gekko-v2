using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider))]
public class FinishLevelTrigger : MonoBehaviour
{
    [Header("Cinemática de final (opcional)")]
    [Tooltip("Timeline que corre cuando Gekko llega al final. Vacío = Canvas_Finish aparece apenas Gekko toca el trigger.")]
    [SerializeField] private PlayableDirector _director;
    [Tooltip("Objetos (ej. la cámara de la cinemática) que solo se prenden al llegar al final. Quedan prendidos detrás de Canvas_Finish para que el último plano siga en pantalla.")]
    [SerializeField] private GameObject[] _cinematicOnlyObjects;

    [Header("Skip")]
    [SerializeField] private Key _skipKey = Key.Space;
    [Tooltip("Segundos de cinemática antes de poder saltearla, para que un salto justo al entrar no la corte.")]
    [SerializeField, Min(0f)] private float _skipDelay = 0.5f;

    private bool _finished;
    private bool _playing;

    private void Awake()
    {
        // Con Hold o Loop el director nunca termina y no dispararía 'stopped'.
        if(_director != null) _director.extrapolationMode = DirectorWrapMode.None;
        SetCinematicObjectsActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(_finished) return;
        if(!other.TryGetComponent(out Player player)) return;

        _finished = true;
        LevelStats.Instance?.StopTimer();
        player.Inputs(false);

        if(_director == null || _director.playableAsset == null)
        {
            FinishLevel();
            return;
        }

        if(player.PjViewer != null) player.PjViewer.Celebrate();
        SetCinematicObjectsActive(true);
        _director.stopped += OnCinematicStopped;
        _director.time = 0;
        _director.Play();
        _playing = true;
    }

    private void Update()
    {
        if(!_playing || _director.time < _skipDelay) return;

        if(Keyboard.current != null && Keyboard.current[_skipKey].wasPressedThisFrame)
            Skip();
    }

    // Salta al último frame (deja todo como lo dejaría la cinemática) y la corta: Stop() dispara 'stopped'.
    private void Skip()
    {
        _director.time = _director.duration;
        _director.Evaluate();
        _director.Stop();
    }

    private void OnCinematicStopped(PlayableDirector director)
    {
        director.stopped -= OnCinematicStopped;
        _playing = false;
        FinishLevel();
    }

    // GameManager escucha este evento y abre Canvas_Finish.
    private void FinishLevel() => EventManager.Trigger("OnLevelFinished");

    private void SetCinematicObjectsActive(bool active)
    {
        if(_cinematicOnlyObjects == null) return;
        foreach (var go in _cinematicOnlyObjects)
            if(go != null) go.SetActive(active);
    }
}
