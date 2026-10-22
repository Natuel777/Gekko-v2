using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class PurificationChallengeHealthbar : MonoBehaviour
{
    [SerializeField] private Image[] _purifiedRunes;
    [SerializeField] private List<Image> _corruptedRunes = new List<Image>();
    [SerializeField] private GameObject _purifiedBG, _corruptedBG;
    [SerializeField] private float _fadeOutDelay = 2f;
    [SerializeField] private float _fadeOutDuration = 1f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

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

        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(_fadeOutDelay);

        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while(elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeOutDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }

    private bool StllRunesToBreak() => _corruptedRunes.Count != 0;
}
