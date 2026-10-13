using UnityEditor;
using UnityEngine;

// Corta automáticamente el spritesheet de la animación de carga en 60 sprites
// (grilla 10x6, 720x176 cada uno) apenas se importa/reimporta, sin tener que
// usar el Sprite Editor a mano. Solo actúa sobre ESE archivo puntual.
public class CargaSpriteSheetPostprocessor : AssetPostprocessor
{
    private const string TargetFileName = "cargando_spritesheet_10x6";
    private const int Columns = 10;
    private const int Rows = 6;
    private const int CellWidth = 720;
    private const int CellHeight = 176;

    private void OnPreprocessTexture()
    {
        if (!IsTarget(assetPath)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;

        importer.maxTextureSize = 8192; // si queda en 2048 (el default) Unity achica el sheet entero y se ve borroso
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        // El tamaño real del PNG recién se conoce acá (antes de que termine de importarse),
        // así que lo leemos de los bytes crudos del archivo para calcular la grilla.
        if (!TryGetPngSize(assetPath, out int texWidth, out int texHeight))
        {
            Debug.LogWarning($"[CargaSpriteSheetPostprocessor] No pude leer el tamaño de '{assetPath}', no se cortó.");
            return;
        }

#pragma warning disable 0618 // SpriteMetaData está obsoleto pero sigue siendo la forma simple de cortar por código.
        var metas = new SpriteMetaData[Columns * Rows];

        for (int i = 0; i < metas.Length; i++)
        {
            int col = i % Columns;
            int row = i / Columns; // 0 = fila de arriba (orden de lectura: izquierda a derecha, arriba a abajo)

            float x = col * CellWidth;
            // Unity mide sprites con el origen (0,0) abajo a la izquierda; como el orden pedido
            // es "de arriba hacia abajo", hay que invertir la fila al calcular el Y del rect.
            float y = texHeight - (row + 1) * CellHeight;

            metas[i] = new SpriteMetaData
            {
                name = $"cargando_{i:00}",
                rect = new Rect(x, y, CellWidth, CellHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }

        importer.spritesheet = metas;
#pragma warning restore 0618
    }

    private static bool IsTarget(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path) == TargetFileName;
    }

    // Lee el ancho/alto directo de la cabecera PNG (IHDR), sin depender de que la textura
    // ya esté importada (en OnPreprocessTexture todavía no lo está).
    private static bool TryGetPngSize(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;

        byte[] bytes = System.IO.File.ReadAllBytes(assetPath);
        if (bytes.Length < 24) return false;

        width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width > 0 && height > 0;
    }
}
