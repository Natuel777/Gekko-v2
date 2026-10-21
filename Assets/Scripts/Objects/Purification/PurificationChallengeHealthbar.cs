using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PurificationChallengeHealthbar : MonoBehaviour
{
    [SerializeField] private Image[] _purifiedRunes;
    [SerializeField] private List<Image> _corruptedRunes = new List<Image>();
    [SerializeField] private GameObject _purifiedBG, _corruptedBG;

    private void Start()
    {
        for(int i = 0; i < _corruptedRunes.Count; i++)
            if(!_corruptedRunes[i].enabled) _corruptedRunes[i].enabled = true;

        for(int j = 0; j < _purifiedRunes.Length; j++)
            if(_purifiedRunes[j].enabled) _purifiedRunes[j].enabled = false;

        if(_purifiedBG.activeSelf) _purifiedBG.SetActive(false);
        
        gameObject.SetActive(false);
    }

    public void RemoveCorruptedRune()
    {
        if(!StllRunesToBreak()) return;

        int index = _corruptedRunes.Count -1;
        _corruptedRunes[index].enabled = false;
        if(index < _purifiedRunes.Length) _purifiedRunes[index].enabled = true;
        _corruptedRunes.RemoveAt(index);

        if(_corruptedRunes.Count != 0) return;

        SetPurifiedHealthBar();
    }

    private void SetPurifiedHealthBar()
    {
        if(StllRunesToBreak()) return;

        _corruptedBG.SetActive(false);
        _purifiedBG.SetActive(true);
    }

    private bool StllRunesToBreak() => _corruptedRunes.Count != 0;
}
