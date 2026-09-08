using UnityEngine;

namespace Gekko.PaintTools
{
    /// <summary>
    /// Set de pinceles cargados por el usuario. Cada pincel es una textura en escala de
    /// grises: el blanco pinta, el negro no. Sirve cualquier alpha brush de Photoshop o
    /// Krita exportado como PNG.
    ///
    /// Va en un asset propio y no en el canvas para poder compartir el mismo set entre
    /// varias zonas del nivel.
    ///
    /// Las texturas tienen que tener "Read/Write Enabled" prendido en el importador: el
    /// pincel las lee por CPU. El inspector avisa y ofrece arreglarlo si falta.
    /// </summary>
    [CreateAssetMenu(fileName = "PathBrushSet", menuName = "Gekko/Paint Tools/Path Brush Set")]
    public class PathBrushSet : ScriptableObject
    {
        [SerializeField] private Texture2D[] _brushes = new Texture2D[0];

        public Texture2D[] Brushes => _brushes;

        public int Count => _brushes != null ? _brushes.Length : 0;

        public Texture2D Get(int index)
        {
            if (_brushes == null || index < 0 || index >= _brushes.Length)
            {
                return null;
            }
            return _brushes[index];
        }
    }
}
