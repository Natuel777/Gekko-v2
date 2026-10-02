using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayableDirector))]
public class ParkourCinematicTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private bool _playOnce = true;

    private bool _played;

    private void Awake()
    {
        if(!_director) _director = GetComponent<PlayableDirector>();
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
        GameManager.Instance.Pj?.Inputs(true);
    }
}
