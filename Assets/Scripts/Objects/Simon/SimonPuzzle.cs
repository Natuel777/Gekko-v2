using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimonPuzzle : MonoBehaviour, IActivable
{
    [SerializeField] private SimonPiece[] _pieces;
    [SerializeField] private SimonRounds[] _rounds;
    [SerializeField] private float _waitTime;
    private float _count;
    private int _currentRound;
    private bool _showing;
    private bool _hearing;

    private void Start()
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            _pieces[i].SetNumber(i);
        }
    }
    private void Update()
    {
        
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
        List<Material> mats = new();
        foreach (SimonPiece i in _pieces)
        {
            mats.Add(i.GetComponentInChildren<Renderer>().material);
        }
        _count = _waitTime;
        bool finish = false;
        while(list.Count > 0)
        {
            mats[list.First()].color = Color.blue;
            _count -= Time.deltaTime;

            if(_count <= 0) finish = true;
            if (finish)
            {
                mats[list.First()].color = Color.white;
                _count = _waitTime;
                list.RemoveAt(0);
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
    
    }

}
