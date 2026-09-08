# Auditoría de rendimiento — sistema de caminos

Medido en el proyecto real con el MCP, no estimado. Escena `PruebasCaminos.unity`,
Unity 6000.0.62f1, URP 17.

## Veredicto

**El enfoque está bien planteado. No hay que cambiarlo.** Pintar sobre una máscara
proyectada es la opción correcta para este proyecto y es más barata que las alternativas
(vertex colors necesita subdividir el piso; decals de URP agregan un pase y overdraw).

Pero la **implementación** tenía tres problemas concretos. Dos ya están corregidos. El
tercero necesita una decisión tuya.

---

## Lo que se midió

| Punto | Resultado | Estado |
|---|---|---|
| **Draw calls** | El sistema agrega **cero**. Es un shader sobre geometría que ya se dibujaba. | Correcto |
| **Materiales** | 1 por zona de camino. | Correcto |
| **Overdraw** | **Ninguno**. Opaco, una sola capa, sin transparencias apiladas. | Correcto |
| **Batching / instancing** | El shader **no era compatible con SRP Batcher** (code 15). | **Corregido** |
| **Meshes** | El sistema no genera geometría. Funciona igual con 4 vértices que con 121. | Correcto |
| **GameObjects** | 1 por zona. | Correcto |
| **CPU en runtime** | **Cero**. La máscara solo cambia al pintar. | Correcto |
| **GPU** | 3 samples + ~30 ALU de ruido por píxel de piso. | Aceptable, mejorable |
| **Resolución del mapa** | 17 texels/unidad con 1024² sobre 60×60. De sobra. | Correcto |
| **Actualizar solo cuando hace falta** | `Apply()` ahora solo reempuja si algo cambió. | **Corregido** |
| **Memoria de texturas** | **4–8 MB por zona.** | **Sin resolver** |
| **Persistencia** | Se pierde trabajo pintado en un reimport. | **Sin resolver** |

---

## 1 · SRP Batcher — CORREGIDO

Unity reportaba, textual:

```
GetSRPBatcherCompatibilityCode = 15
"UnityPerMaterial CBuffer inconsistent size inside a SubShader (DepthNormals)"
```

**Causa**: los tres pases de sombra y profundidad se heredaban con
`UsePass "Universal Render Pipeline/Lit/..."`. Cada pase heredado trae el CBUFFER
`UnityPerMaterial` de URP Lit, que tiene otro tamaño que el de este shader, y el SRP
Batcher exige que **todos** los pases de un SubShader declaren exactamente el mismo
layout. Con un solo pase distinto, el shader entero queda fuera del batcher.

**Arreglo**: se escribieron los tres pases a mano, compartiendo el CBUFFER del
`HLSLINCLUDE`. Verificado: **code 0, compatible**, 0 mensajes de compilación, sin cambio
visual.

De paso: `Gekko/Grass` y `Gekko/Stylized Cloud` ya daban code 0, están bien.

> Nota medida: `UnityStats.drawCalls` **no** sirve para ver esto. Con 50 tiles dio 302 en
> los tres casos (con MPB, sin MPB, y con URP Lit). El SRP Batcher no reduce el número de
> draw calls: reduce el trabajo de CPU de armar los constant buffers. El dato autoritativo
> es el compat code.

## 2 · MaterialPropertyBlock

`PathCanvas` empuja la máscara por `MaterialPropertyBlock`, y un renderer con MPB queda
fuera del SRP Batcher. Con pocos renderers de piso da igual; con muchos empieza a pesar.

**Alternativa, si hiciera falta**: hornear `_PathCanvasMin` / `_PathCanvasSize` / `_PathMask`
en el **material** en vez del MPB, y usar un material por zona. Los materiales son
baratos y así los renderers vuelven al batcher.

No lo cambié porque ahora mismo el costo es despreciable y el MPB permite compartir un
material entre zonas. Queda anotado por si el nivel crece.

## 3 · Memoria — SIN RESOLVER

Medido:

```
grasstile.png   1024x1024  DXT1     1.33 MB
groundtile.png  1024x1024  DXT1     1.33 MB
máscara         1024x1024  RGBA32   4.00 MB   (8.00 MB si está readable)
```

**La máscara pesa 3 a 6 veces más que el arte real.** Y en disco, el `.asset` ocupa
**8 MB**. Con 10 zonas en un nivel son 40–80 MB de RAM solo en máscaras.

Dos causas:

1. **RGBA32 sin comprimir ni mips.** Es lo que crea el código hoy.
2. **`isReadable = true`** duplica la memoria (copia en CPU + copia en GPU). Hace falta
   para pintar, pero no para jugar.

## 4 · Persistencia — SIN RESOLVER, y es lo más grave

Comprobado con datos:

```
antes del reimport:  193.963 px pintados, 27.634 px con tinte
después:             137.225 px pintados,      0 px con tinte
```

**Se perdieron 56.738 píxeles de pintura y el tinte entero, sin un solo error en consola.**

`EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()` no persiste de forma confiable los
píxeles de un `Texture2D` creado por script y guardado como `.asset`. Cualquier reimport
—forzado o automático— revierte parte del trabajo.

---

## La propuesta: guardar la máscara como PNG

Un solo cambio resuelve los puntos 3 y 4 a la vez.

En vez de `AssetDatabase.CreateAsset(texture, ".asset")`, guardar con
`EncodeToPNG()` + `File.WriteAllBytes()` + `AssetDatabase.ImportAsset()`.

| | Hoy (.asset) | Con PNG |
|---|---|---|
| Disco | 8 MB | ~200–600 KB (PNG comprime muy bien una máscara) |
| RAM | 4–8 MB | ~1 MB con BC7, o 1.3 MB con DXT5 |
| Persistencia | **pierde datos** | robusta, es un archivo normal |
| Readable | siempre on | on solo mientras pintás |

**Sobre la calidad visual**, que era tu condición: la compresión BC introduce error en los
bordes, pero el shader ya rompe el borde de la máscara con ruido procedural antes de
recortarlo. El error de compresión queda por debajo de ese ruido — es imperceptible. Si
al probarlo no convence, se deja la máscara en formato sin comprimir y se gana igual la
persistencia y el tamaño en disco.

**Optimización adicional opcional** (mayor ahorro, mismo aspecto): separar los canales.
La cobertura necesita resolución; el tinte varía muy lento y no la necesita.

```
cobertura  R8      1024²  = 1.0 MB
tinte      RGB24    256²  = 0.19 MB
                    total = 1.2 MB   contra 4 MB de hoy
```

## Otras dos, menores

- **El ruido del borde cuesta ~30 ALU por píxel de piso** (4 hashes con `frac`/`dot`).
  Se puede reemplazar por 1 sample de una textura de ruido tileable de 128². Ahorro real
  en GPU, cero cambio visual. Vale la pena solo si el piso ocupa mucha pantalla.
- **RenderTexture**: no mejoraría el rendimiento del juego ni un poco — el shader samplea
  una textura igual en los dos casos. Sí haría el **pintado** más ágil (blit en GPU en vez
  de loops de píxeles en CPU). Hoy el pincel ya sube solo el rectángulo tocado, que es la
  optimización que importaba. No lo veo prioritario.

---

## Qué necesito que decidas

Pasar la máscara a PNG toca cómo se guardan los datos, así que no lo hice sin confirmar.

1. **PNG sin comprimir en runtime** — resuelve la pérdida de datos y el tamaño en disco,
   deja la RAM en 4 MB por zona. Cero riesgo visual.
2. **PNG + compresión BC7** — resuelve todo, baja la RAM a ~1 MB. Es lo que recomiendo.
3. **PNG + canales separados** — el máximo ahorro (1.2 MB), pero es el cambio más grande
   y toca el shader.

Lo de la pérdida de datos hay que arreglarlo sí o sí en cualquiera de los tres casos.
