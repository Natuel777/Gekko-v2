using System.Collections;
using UnityEngine;

public class BabyDuck : MonoBehaviour
{
    [SerializeField] private Transform[] _wayp;
    [SerializeField] private float _speed;
    [SerializeField] private MomDuck _mom;
    private int _currentWayp;
    private Animator _anim;
    private bool _onPosition;
    public bool OnPosition => _onPosition;

    private void Start()
    {
        _anim = GetComponent<Animator>();
        _anim.SetBool("BabyDuckisWalk",false);
    }
    public IEnumerator StartMoving()
    {
        _anim.SetBool("BabyDuckisWalk", true);
        while (_currentWayp < _wayp.Length)
        {
            var dir = _wayp[_currentWayp].position - transform.position;

            transform.position += dir.normalized * _speed * Time.deltaTime;
            transform.forward = dir;

            if (dir.magnitude < 0.25f)
            {
                _currentWayp++;
            }
            yield return new WaitForEndOfFrame();
        }
        transform.rotation = _wayp[_currentWayp - 1].rotation;
        _onPosition = true;
        _anim.SetBool("BabyDuckisWalk", false);
        GetComponent<AudioSource>().Play();
        _mom.DuckPositioned();
    }
    
}
