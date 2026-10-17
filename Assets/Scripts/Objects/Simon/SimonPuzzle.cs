using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

public class SimonPuzzle : MonoBehaviour, IActivable
{
    [SerializeField] private SimonPiece[] _pieces;
    [SerializeField] private SimonRounds[] _rounds;
    [SerializeField] private float _waitTime;
    private float _timeCount;
    private int _currentRound;
    private bool _showing;


    private void Start()
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            _pieces[i].SetNumber(i);
        }
    }
    public void Activated()
    {
        if(_showing) return;

        _showing = true;
        StartCoroutine(ShowRound());
    }

    private IEnumerator ShowRound()
    {
        List<int> list = _rounds[_currentRound].Order.ToList();

        while(list.Count >= 0)
        {
            _timeCount -= Time.deltaTime;
            if(_timeCount<= 0)
            {


                _timeCount = _waitTime;
            }
        }

        _showing = false;
        _timeCount = 0;
        yield return null;
    }
}
