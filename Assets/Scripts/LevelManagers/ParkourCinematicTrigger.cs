using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayableDirector))]
public class ParkourCinematicTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private bool _playOnce = true;
    // Objetos (ej. split cams) que solo deben estar prendidos mientras corre la cinemática.
    [SerializeField] private GameObject[] _cinematicOnlyObjects;

    [Header("Skip")]
    [Tooltip("Mantener Interactuar (E) este tiempo salta la cinemática.")]
    [Min(0.1f)]
    [SerializeField] private float _skipHoldSeconds = 3f;
    [SerializeField] private SkipCinematicPrompt _skipPrompt;

    private bool _played;
    private CinematicSkipHandler _skipHandler;

    private void Awake()
    {
        if(!_director) _director = GetComponent<PlayableDirector>();
        SetCinematicObjectsActive(false);
        _skipHandler = new CinematicSkipHandler(_skipHoldSeconds, _skipPrompt);
    }

    // Saltear llama Stop() sobre _director, que ya tiene suscripto OnCinematicStopped: la limpieza
    // (soltar input, apagar los cinematic-only objects) la hace ese mismo callback, no hace falta repetirla.
    private void Update()
    {
        if(_played) _skipHandler.ArtificialUpdate(_director);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(_played && _playOnce) return;
        if(!other.TryGetComponent(out Player player)) return;

        _played = true;
        // SetFrozenForCinematic (no solo Inputs(false)) corta también la gravedad/salto en curso: si Gekko
        // entra a la cinemática en el aire, sin esto seguía cayendo/saltando con la cámara ya cortada.
        player.SetFrozenForCinematic(true);
        EventManager.Trigger("OnCinematicStart");
        _director.stopped += OnCinematicStopped;
        _director.time = 0;
        _director.Play();
    }

    private void OnCinematicStopped(PlayableDirector director)
    {
        director.stopped -= OnCinematicStopped;
        SetCinematicObjectsActive(false);
        GameManager.Instance.Pj?.SetFrozenForCinematic(false);
        EventManager.Trigger("OnCinematicEnd");
    }

    private void SetCinematicObjectsActive(bool active)
    {
        if(_cinematicOnlyObjects == null) return;
        foreach (var go in _cinematicOnlyObjects)
            if(go != null) go.SetActive(active);
    }
}
