using UnityEngine;
using UnityEngine.UI;

public class HeartIcon : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _fullSprite;
    [SerializeField] private Sprite _emptySprite;

    public void SetFull(bool full)
    {
        if (!_image) return;
        _image.sprite = full ? _fullSprite : _emptySprite;
    }
}
