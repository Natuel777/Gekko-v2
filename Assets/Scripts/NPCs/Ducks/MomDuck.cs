using System.Collections;
using UnityEngine;

public class MomDuck : MonoBehaviour
{
    [SerializeField] private BabyDuck[] _ducks;
    private Animator _anim;
    [SerializeField] private float _timePerDuck;
    void Start()
    {
        _anim = GetComponent<Animator>();
        _anim.SetBool("DuckisWalk", false);
        LevelOneManager.Instance.OnBridgeConstructed += StartChildrenMove;
    }

    private void StartChildrenMove()
    {
        StartCoroutine(ChildrenMove());
    }
    private IEnumerator ChildrenMove()
    {
        int currentDuck = 0;
        while (currentDuck < _ducks.Length)
        {
            StartCoroutine(_ducks[currentDuck].StartMoving());
            currentDuck++;
            yield return new WaitForSeconds(_timePerDuck);
        }
    }
    public void DuckPositioned()
    {
        bool finish = true;
        for (int i = 0; i < _ducks.Length; i++)
        {
            if (!_ducks[i].OnPosition) finish = false;
        }
        if (finish)
        {
            GetComponent<AudioSource>().Play();
            GetComponentInChildren<ParticleSystem>().Play();
        }
    }
    private void OnDisable()
    {
        LevelOneManager.Instance.OnBridgeConstructed -= StartChildrenMove;
    }
}
