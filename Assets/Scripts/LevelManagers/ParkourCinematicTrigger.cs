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

    private bool _played;

    private void Awake()
    {
        if(!_director) _director = GetComponent<PlayableDirector>();
        SetCinematicObjectsActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(_played && _playOnce) return;
        if(!other.TryGetComponent(out Player player)) return;

        _played = true;
        player.Inputs(false);
        _director.stopped += OnCinematicStopped;
        _director.time = 0;
        _director.Play();
    }

    private void OnCinematicStopped(PlayableDirector director)
    {
        director.stopped -= OnCinematicStopped;
        SetCinematicObjectsActive(false);
        GameManager.Instance.Pj?.Inputs(true);
    }

    private void SetCinematicObjectsActive(bool active)
    {
        if(_cinematicOnlyObjects == null) return;
        foreach (var go in _cinematicOnlyObjects)
            if(go != null) go.SetActive(active);
    }
}
