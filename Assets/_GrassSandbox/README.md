# Sistema de pasto — carpeta sandbox

Aislado, no toca nada del proyecto. Cuando esté aprobado se reubica y se borra esta carpeta.

```
Assets/_GrassSandbox/
├─ Shaders/
│  └─ GekkoGrass.shader              URP: ForwardLit + ShadowCaster + DepthOnly
├─ Editor/
│  ├─ GrassFieldEditor.cs            el pincel de la vista de escena
│  └─ GrassSetupMenu.cs              material, campo e interactores
├─ Runtime/
│  ├─ GrassData.cs                   asset con las briznas pintadas
│  ├─ GrassField.cs                  arma las mallas de chunk
│  ├─ GrassInteractor.cs             marca "esto aplasta pasto"
│  ├─ GrassInteractionManager.cs     junta y manda al shader (clase plana)
│  └─ GrassInteractionDriver.cs      MonoBehaviour fino
└─ Data/                             assets de briznas, uno por campo
```

## Cómo usarlo

1. `Tools > Gekko > Grass > Crear material de pasto`
2. `Tools > Gekko > Grass > Crear campo de pasto en la escena`
3. En el `GrassField`, asigná el material.
4. **Activar modo pintura** → pintá en la escena.
   - click y arrastrar: pintar · shift+click: borrar · ctrl+rueda: radio
5. Seleccioná el gecko (y los NPCs) → `Tools > Gekko > Grass > Agregar interactor a la selección`.

El asset de datos se crea solo la primera vez que pintás, en `Data/`.

> **Requisito**: se pinta contra **colliders**. La superficie tiene que tener uno. El
> Spline Terrain los hornea, así que en tus niveles ya están. Es a propósito: los chunks
> de pasto no tienen collider, así que el pincel **nunca** puede plantar pasto encima del
> pasto ya pintado — que es el problema clásico de raycastear contra la geometría visible.

## Rendimiento: dónde se gastan las cosas

### Chunks, no una malla gigante

Una sola malla para todo el campo es un único objeto para Unity: o se dibuja entera o
nada. No hay culling posible y pagás el campo completo en cada frame.

Partido en chunks, el frustum culling normal descarta los que no se ven — en un
plataformero con cámara cerrada, eso es la enorme mayoría. El costo es **un draw call por
chunk visible**, así que `Tamaño de chunk` es un balance directo:

| Chunk chico | Chunk grande |
|---|---|
| mejor culling | menos draw calls |
| más draw calls | dibujás pasto que no se ve |

8 unidades es un punto de partida razonable. El inspector te muestra briznas, chunks,
vértices y triángulos para que decidas con números.

### Las mallas no se guardan

Se reconstruyen en `OnEnable` desde el asset de datos, y los GameObjects de chunk van con
`HideFlags.DontSave`. La escena no engorda y no hay assets de malla intermedios que
mantener.

### Los datos van en un asset, no en la escena

Un campo son decenas de miles de briznas. Meterlas en el `.unity` haría la escena
lentísima de abrir y volvería ilegible cualquier diff de git — con las ramas que tenés en
paralelo eso se paga caro. La escena solo guarda una referencia al asset.

### Geometría por brizna

`Segmentos` controla el detalle: cada segmento suma 2 vértices y 2 triángulos.

| Segmentos | Vértices | Triángulos | Qué da |
|---|---|---|---|
| 1 | 3 | 1 | la brizna solo se inclina |
| **2** | **5** | **3** | se curva — el default |
| 3 | 7 | 5 | curva suave, para primeros planos |

### Sombras apagadas por defecto

Las sombras de pasto son caras y aportan poco a esta distancia de cámara. Se prenden por
campo si las querés.

## La interacción

`GrassInteractor` en el gecko y los NPCs. Se registran solos en una lista estática — no
hay `FindObjectsOfType` por frame, son pocos objetos y cambian poco.

`GrassInteractionDriver` los manda al shader **una vez por frame, en `LateUpdate`**: para
entonces los personajes ya se movieron, así que el pasto no va un frame atrasado. Lo crea
solo el primer interactor que se habilita, no hay que acordarse de ponerlo.

Se mandan como propiedades **globales** (`Shader.SetGlobalVectorArray`), no por material.
Una sola escritura sirve para todos los materiales de pasto de la escena, y como no se
toca ningún material, el SRP Batcher los sigue agrupando.

**El tope es 8** y es fijo: el array del shader tiene tamaño fijo, y el vertex shader
recorre esa lista **por vértice** — cada interactor extra se paga en cada brizna en
pantalla. El loop está acotado por el contador real, no por el máximo, así que con 1
interactor pagás 1 iteración. Si hay más de 8 candidatos quedan los más cercanos a la
cámara, que son los únicos cuyo pasto se ve.

El aplastado empuja hacia afuera y hacia abajo, ponderado por `altura²`: la base queda
clavada al suelo y el movimiento se concentra en la punta. Al salir el interactor, la
brizna se endereza sola — no hay estado que guardar.

## El color

Cuatro capas que se suman, de la más grande a la más chica:

1. **Gradiente de la brizna** — `_BottomColor` → `_TopColor` según la altura. Es el que
   da la sensación de profundidad en el césped.
2. **Manchones en el mundo** — ruido de baja frecuencia sobre XZ mundial que mezcla hacia
   `_VariationColor`. Es lo que evita que un campo grande se lea como una alfombra de un
   solo verde, y es de dónde sale el aire turquesa de la referencia.
3. **Variación por brizna** — cada brizna tiene su tinte, guardado en el color de vértice.
4. **Oclusión en la base** — oscurece el pie sin geometría extra, y da sensación de
   densidad.

Encima va la luz: wrapped lambert usando **la normal del suelo**, no la de la brizna. Ese
es el truco del sombreado suave y "pintado" de la referencia — si cada brizna usara su
propia normal, el campo se vería facetado y ruidoso. Y una capa de translucidez que
enciende las puntas a contraluz.

## Ajustes rápidos

| Quiero… | Toco |
|---|---|
| Pasto más denso | `Densidad` del pincel, y repintar |
| Menos draw calls | `Tamaño de chunk` ↑ |
| Mejor culling | `Tamaño de chunk` ↓ |
| Olas más marcadas | `_WindStrength` ↑, `_WindScale` ↓ |
| Temblequeo más rápido | `_SwaySpeed` ↑ |
| Se aplasta más | `_PushStrength` y `_PushDown` ↑ |
| Radio de aplastado | `Radio` en el `GrassInteractor` |
| Más contraste de color | `_AmbientOcclusion` ↑, `_VariationStrength` ↑ |

## Pendiente / a decidir

- **Nada de esto se compiló todavía.** Hay que abrir Unity y ver la consola.
- El post de MinionsArt es de pago (403), así que esto está hecho sobre la técnica, no es
  una réplica de ese post.
- No hay LOD por distancia: todas las briznas de un chunk visible se dibujan enteras. Si
  hace falta, el paso siguiente es generar dos mallas por chunk (completa y reducida) y
  cambiar con `LODGroup`.
- No hay pase `DepthNormals`, así que si prendés SSAO el pasto no va a contribuir.
- El pincel no controla separación mínima entre briznas: repintar la misma zona acumula.
  Se borra con shift y se vuelve a pintar.
