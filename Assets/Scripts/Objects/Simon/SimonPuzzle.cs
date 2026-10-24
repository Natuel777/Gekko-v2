using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimonPuzzle : MonoBehaviour, IActivable
{
    [SerializeField] private SimonPiece[] _pieces;
    [SerializeField] private SimonRounds[] _rounds;
    private Material[] _mats;
    [SerializeField] private float _waitTime;
    [SerializeField] private float _showTime;
    private float _count;
    private int _currentRound;
    private Queue<int> _currentRoundQueue;
    private bool _showing;
    private bool _hearing;
    private bool _finish;

    private void Start()
    {
        _currentRoundQueue = new();
        _mats = new Material[_pieces.Length];

        for (int i = 0; i < _pieces.Length; i++)
        {
            _pieces[i].SetNumber(i);
            _mats[i] = _pieces[i].GetComponentInChildren<Renderer>().material;
        }
    }
    public void Activated()
    {
        if(_showing || _hearing || _finish) return;

        _showing = true;
        StartCoroutine(ShowRound());
    }

    private IEnumerator ShowRound()
    {
        Queue<int> queue = new();
        foreach (var item in _rounds[_currentRound].Order)
        {
            queue.Enqueue(item);
            _currentRoundQueue.Enqueue(item);
        }
        _count = _waitTime;
        bool finish = false;
        while(queue.Count > 0)
        {
            _mats[queue.Peek()].color = Color.blue;
            _count -= Time.deltaTime;

            if(_count <= 0) finish = true;
            if (finish)
            {
                _mats[queue.Peek()].color = Color.white;
                _count = _waitTime;
                queue.Dequeue();
                finish = false;
            }
            yield return null;

        }
        _showing = false;
        _hearing = true;
        yield return null;
    }
    private void OnEnable()
    {
        foreach (var item in _pieces)
        {
            item.OnPressed += InputRecieved;
        }
    }
    private void OnDisable()
    {
        foreach (var item in _pieces)
        {
            item.OnPressed -= InputRecieved;
        }
    }
    private void InputRecieved(int id)
    {
        if (!_hearing) return;
        if(id != _currentRoundQueue.Dequeue())
        {
            _hearing = false;
            _mats[id].color = Color.red;
            return;
        }

        _mats[id].color = Color.green;

        if(_currentRoundQueue.Count <=0)
        {
            _hearing = false;
            _currentRound++;
            StartCoroutine(FinishRound());
            if (_rounds.Length >= _currentRound) _finish = true;
        }
    }
    private IEnumerator FinishRound()
    {
        ChangeAllPiecesColor(Color.green);

        yield return new WaitForSeconds(_showTime);

        ChangeAllPiecesColor(Color.white);
    }
    private void ChangeAllPiecesColor(Color color)
    {
        foreach (var m in _mats)
        {
            m.color = color;
        }
    }

}
