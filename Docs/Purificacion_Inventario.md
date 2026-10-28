# Inventario para la purificación por partes — GEKKO (Fase 0)

> Generado leyendo los archivos del repo (solo lectura). No se escribió ni modificó nada del sistema.
> Escenas analizadas: **`Assets/Scenes/BossLvlOne.unity`** (escena de prueba, ver la sección de abajo) y
> **`Assets/Scenes/LvlOne.unity`** (el nivel completo, secciones 0.1–0.6). Fecha: 2026-10-28.
>
> **Cómo se obtuvo:** se parseó el YAML de la escena, cruzando cada `guid` con los `.meta` de `Assets/` y de
> `Library/PackageCache` (paquetes). Los contenidos de los prefabs instanciados y de los prototipos de `ScatterField`
> se resolvieron abriendo cada `.prefab` (incluye prefabs anidados y overrides de materiales). Las posiciones son de
> mundo, calculadas componiendo los Transforms. Los números son confiables para planificar, pero **conviene
> chequear en el Editor** las posiciones/radios de las regiones con los gizmos antes de fijarlos.

---

## Resumen: lo que cambia respecto de la guía

Estas son las cosas encontradas que **no encajan** con lo que asume `PURIFICACION_ZONA_GUIA.md`. Todo lo demás del
plan se puede seguir tal cual.

| # | La guía asume | Lo que hay en el proyecto | Impacto / propuesta |
|---|---|---|---|
| 1 | `MaterialPropertyBlock` para dissolve y crossfade | **GPU Resident Drawer activo** en `PC_RPAsset` (`m_GPUResidentDrawerMode: 1`). El código existente evita MPB a propósito y usa instancias `r.materials` (`PurificationBarrier`, `PurificationCore`, `PurificationChallenge` lo explican en comentarios). | `CorruptedPlant`/`PurifyMaterialSwap` tienen que usar **instancias de material** (y liberarlas en `OnDestroy`), igual que el código actual. La máscara global (`Shader.SetGlobalVectorArray`) no tiene ese problema. |
| 2 | Piso = Unity Terrain o mallas con Shader Graph | **No hay ningún Unity Terrain** en LvlOne. El piso es **SplineTerrain** (54 objetos) + mallas Polybrush, dibujado con **`GekkoPathBlend.shader`** (HLSL escrito a mano, 718 líneas, no Shader Graph). | Las 3 opciones de Terrain de la Fase 2 no aplican. Lo más directo es agregar la máscara al `.shader` por `#include` (es código de texto, se puede editar con seguridad, pero es un archivo existente: **hay que aprobarlo**). |
| 3 | Árboles/pasto como prefabs o GameObjects a los que se les pone un script | **Árboles, arbustos, hongos y flores se dibujan con `ScatterField`** (`Graphics.DrawMeshInstanced`, **sin GameObjects**). ~3.900 instancias en 5 campos. | Imposible poner `PurifyReactive` por instancia. Todo lo de vegetación dispersa tiene que ser **por shader**. Incluye las variantes corruptas (`CorruptedAbeto`, `C_Lavandas`…), que también son instancias de Scatter. |
| 4 | Pasto: detalles de Terrain o mallas | **`GrassField` propio** (`Assets/_GrassSandbox`), shader **`GekkoGrass.shader`** (HLSL a mano). ~77.000 briznas en 2 campos. Ya usa un **array global** (`_GrassInteractors` vía `Shader.SetGlobalVectorArray`), el mismo patrón que propone la guía. | Encaja perfecto: máscara + "pasto aplastado" en `GrassDisplace()` del shader. También es archivo existente → aprobar. |
| 5 | Toda la zona arranca corrupta | **Casi todo el nivel ya está arteado sano.** La corrupción visible hoy está concentrada en la arena del desafío (`combatZone`/`CorruptionChallenge`): planta jefe, núcleos, escudo, vegetación `C_*`, decals de corrupción, partículas `CorruptedLine_PS`, enredaderas `Line` entre núcleos. | Decisión de diseño: ¿se corrompe visualmente **todo** el nivel con la máscara (cambio grande de look) o solo algunas regiones? Ver preguntas. |
| 6 | Arándanos reducen los toques de lengua | El **`Blueberry`** común cura (20 %) o suma combo de velocidad (`BlueberryComboTracker`); **no toca la purificación**. Existe aparte **`PurificationBerry`** (contador propio en `CollectiblesRegister`) que solo usa `PurificationGate` para el jefe de la cueva; **no hay ninguno en LvlOne**. | `PurifyHeart.ReducirToques(int)` tendría que leer `PurificationBerry` (o lo que se decida). Hoy no hay nada enganchado en LvlOne. |
| 7 | Hay que crear el "corazón" de cada región | Ya existe **`PurificationCore`** (núcleo golpeable con la lengua, evento `Broken`) y **`PurificationChallenge`** (desafío por zona, eventos `CoreBroken`, `ShieldDropped`, `Completed`). | Propuesta: no crear `PurifyHeart` desde cero; usar un **`PurifyTrigger`** que escuche esos eventos existentes. Sin tocar los scripts actuales. |
| 8 | Guardado | `SaveManager` existe (JSON en `persistentDataPath/Hola.Hola`) pero **`SaveData` está vacío** y el `SaveManager` solo está en `Menu.unity`. Checkpoints solo en memoria. | Hay que definir qué se guarda (ver preguntas). |
| 9 | Código en `Assets/_Gekko/Purification/`, namespace `Gekko.Purification` | El código de juego (`Assets/Scripts/`) **no usa namespaces**; los sistemas nuevos aislados sí (`Gekko.GrassSandbox`, `Gekko.PaintTools`, `Gekko.Tools`). Ya existe `Assets/Scripts/Objects/Purification/` con el sistema de desafíos. | Propuesta: carpeta nueva **`Assets/_Gekko/Purification/`** con namespace **`Gekko.Purification`** (sigue el patrón de los sandboxes y no se mezcla con lo existente). Confirmar. |

---

## Escena de prueba: `BossLvlOne.unity` (decidido el 2026-10-28)

El sistema se prueba primero en **BossLvlOne**. Esa escena es mucho más chica que LvlOne y no tiene varios de los
sistemas complicados, lo que la hace buena para probar. El inventario de LvlOne (resto del documento) queda como
referencia para cuando se lleve el sistema al nivel completo.

### Qué tiene y qué no (comparado con LvlOne)

| | BossLvlOne | Consecuencia |
|---|---|---|
| GameObjects / prefabs | 738 / 31 | Escena chica, rápida de probar |
| `ScatterField` / `GrassField` | **No hay** | No hace falta tocar vegetación instanciada ni `GekkoGrass.shader` para la prueba |
| `LittleBarricade` | **No hay** | El evento autorizado para `LittleBarricade` no se necesita acá (queda para LvlOne) |
| Piso | 10 `SplineTerrain` + 7 `platParkour1`, con **`M_GroundPath 2`** (violáceo, `GekkoPathBlend.shader`, 38 renderers) y `Platform_Dirt` (URP Lit, 19) | El piso es la pieza principal: la máscara va en `GekkoPathBlend.shader` |
| Vegetación | 1 `tree.prefab` (hojas `M_Bushes 1`, `S_Leaf`), hojas `Plantas.mat` en los núcleos | Poca: sirve para probar el follaje con una sola pieza |
| Agua | 2 cubos con `M_Water` (`Stylized_Water`) | Opcional |
| Enredaderas corruptas | `MuerdagoAgua/Colina/Parkour` (6 `LineController`, uno por núcleo) y 2 `FinalEnredaderas` (`enredadera.mat`) con barreras | Ya desaparecen con el sistema actual |
| Luz | 1 Directional Light **Realtime**, sin lightmaps ni probes | Los cambios de luz se ven |
| Niebla | **Apagada** (`m_Fog: 0`) | La Fase 3 tiene que prenderla |
| Volume | **No hay ninguno** | La Fase 3 tiene que agregar el Volume "Corrupto" (paso manual) |
| Checkpoints | 1 | — |

### Cómo está armado el desafío hoy

- **Planta jefe** en el centro, (-1, 1, 0), con escudo (`Shield`, `PurificationBarrier`).
- **`ChallengeMananger`**: `PurificationChallenge` + `BossPlantPhases` + `BossCinematicSignals`. Al purificar la
  planta: cae `_pathBarrier` (`barricade` de `FinalEnredaderas`, en (16, 6, 36)), se disuelven los
  `_corruptedVisuals`, corre el timeline de cierre y los créditos.
- **3 núcleos** (`PurificationCore`, 1 toque cada uno), cada uno con su enredadera que va hacia la planta:

| Núcleo | Posición | Enredaderas que borra | Extra |
|---|---|---|---|
| `NewNucleo_lp` | (13, 8, -24) — sur | `Line (2)`, `Line (3)` (MuerdagoParkour) | — |
| `NewNucleo_lp (1)` | (-7, 6, 34) — norte | `Line`, `Line (1)` (MuerdagoColina) | **`PurifiedMaterialSwap`**: cambia `M_GroundPath 2` (violáceo) → `M_GroundPath 1` (verde) en `platParkour1 (3…9)` y `Spline Terrain 1 (1)` |
| `NewNucleo_lp (2)` | (-28, 5, -9) — oeste | `Line (4)`, `Line (5)` (MuerdagoAgua) | — |

**Ya existe una "purificación por partes" básica:** el núcleo norte pinta de verde su sector cambiando el material
de golpe. El sistema nuevo hace lo mismo pero con la onda que avanza, y para cada sector. Cuando la región norte
funcione, ese `PurifiedMaterialSwap` hay que **sacarlo de la escena** (paso manual), porque si no pinta todo de
verde antes de que llegue la onda.

### Regiones propuestas para BossLvlOne

Una región por núcleo, más el centro que se purifica con la planta. Las posiciones salen de los objetos de cada
sector; los radios son estimados (las mallas del SplineTerrain no se pueden medir sin abrir el Editor), así que
hay que ajustarlos con los gizmos.

| Región | Qué cubre | Nodo (centro → radio aprox.) | Disparador |
|---|---|---|---|
| `sector_norte` | núcleo (1), `platParkour1 (3…9)`, `Spline Terrain 1 (1)`, ganchos (1)(2)(3), plataformas flotantes (3)(4) | (-12, 2, 28) → 16 | `NewNucleo_lp (1).Broken` |
| `sector_sur` | núcleo (0), subibaja, `GrapplePoint`, hongos (8)(9), plataformas flotantes (1)(2), gancho (0) | (2, 4, -26) → 16 | `NewNucleo_lp.Broken` |
| `sector_oeste` | núcleo (2), plataforma flotante (5), gancho (4), hongos (6)(7), `tree`, `FinalEnredaderas (1)` | (-29, 3, 2) → 14 | `NewNucleo_lp (2).Broken` |
| `centro` | planta, `Spline Terrain 1`, `Spline Terrain 2`, `Spline Terrain 1 (4)` | (0, 0, 0) → 14 | `PurificationChallenge.Completed` (planta purificada) |

Queda afuera el sector este, donde arranca el jugador (28, 2, 10), con `Spline Terrain 1 (7)`.

Todo se hace **escuchando eventos que ya existen**. No hay que modificar ningún script para BossLvlOne.

### Guardado (respuesta a "no entendí")

La pregunta era qué pasa con las regiones purificadas en dos casos:
- **Morir y reaparecer en un checkpoint:** la escena no se recarga, así que los núcleos rotos y las regiones
  purificadas quedan como estaban. Esto funciona solo, sin hacer nada.
- **Cerrar el juego y volver a abrirlo:** hoy el juego no guarda el progreso de ningún nivel (`SaveData` está
  vacío), así que el desafío entero arranca de cero. Las regiones harían lo mismo.

**Propuesta:** que las regiones no se guarden en disco, igual que el resto del nivel. Si algún día se guarda el
progreso del nivel, se agregan ahí.

---

## 0.1 Proyecto

| Item | Valor |
|---|---|
| Unity | **6000.0.62f1** (`ProjectSettings/ProjectVersion.txt`) |
| URP | **17.0.4** (`com.unity.render-pipelines.universal`) — Shader Graph 17.0.4 |
| RP Asset activo (calidad PC) | `Assets/Settings/PC_RPAsset.asset` → renderer `PC_Renderer` |
| GPU Resident Drawer | **Activo** (`m_GPUResidentDrawerMode: 1`), SRP Batcher activo |
| Renderer features (`PC_Renderer`) | `DecalRendererFeature`, `ScreenSpaceAmbientOcclusion`, `FullScreenPassRendererFeature` (viento del boost, lo prende `WindEffectController`), `PlayerHidden` / `PlayerVisible` (render objects para la silueta del Gekko) |

**Paquetes relevantes** (`Packages/manifest.json`):

| Paquete | ¿Instalado? | Versión / nota |
|---|---|---|
| Input System | Sí | 1.14.2 |
| Cinemachine | Sí | **3.1.7** (API `Unity.Cinemachine`, `CinemachineImpulseSource` ya en uso) |
| DOTween | **No** | Solo aparece mencionado en comentarios. → corrutinas + `AnimationCurve` |
| VFX Graph | Sí | 17.0.4 (no se encontró uso en LvlOne; las partículas son `ParticleSystem`) |
| ProBuilder | Sí | 6.0.9 |
| Polybrush | Sí | 1.2.1 — 51 `PolybrushMesh` en LvlOne + ~370 objetos "(Polybrush Clone)" |
| Terrain Tools | Sí | 5.3.2 (pero **no hay Terrain** en la escena) |
| Splines | Sí (dependencia) | 2.0.0 — base del `SplineTerrain` |
| Animation Rigging, Timeline, AI Navigation, FBX, Alembic | Sí | — |
| Vegetación/viento de terceros | **No** | El viento es propio: Shader Graphs `S_Leaf`/`S_LeafPine` y `GekkoGrass.shader` |
| `com.unity.pipeline` | Sí | 0.9.0-exp.1 (CLI `unity`) |

Herramientas propias relevantes: `Assets/_PaintToolsSandbox` (caminos + `ScatterField`), `Assets/_GrassSandbox`
(`GrassField`), `Assets/Spline Terrain` (asmdef propio), `Assets/_CloudsSandbox`, `Assets/_GekkoTools`.

---

## 0.2 Escenas

### Build Settings (`ProjectSettings/EditorBuildSettings.asset`)

| # | Escena | Activa |
|---|---|---|
| 0 | `Scenes/MenuPrueba/Menu.unity` | sí |
| 1 | `Scenes/SceneLoader.unity` | sí |
| 2 | `Scenes/LvlParcialBlocking 2.unity` | no |
| 3 | **`Scenes/LvlOne.unity`** | sí |
| 4 | `Scenes/intentocin.unity` | no |
| 5 | `Scenes/LvlParcialBlocking.unity` | no (el archivo no existe) |
| 6–8 | `Playground`, `CinPlayground`, `ia` | no (no existen en `Assets/`) |

Otras escenas en `Assets/` (fuera del build): `BossLvlOne`, `LvlTwo`, `LevelTemplate`, `SplashArtScene`, `Pruebas/*`,
`Spline Terrain/*`, sandboxes (`PruebasClouds`, `PruebasCaminos`, `PruebasTriplanar`), `Gekko-Pruebas`, `Proyectil`.

### ¿Cuál es la zona bosque?

**`LvlOne.unity`** (27 MB, 9.144 GameObjects, 1.079 instancias de prefab). Es el único nivel jugable en el build, es
el único que usa `ScatterField`/`GrassField` (pinos, abetos, troncos, arbustos, hongos, flores) y su música es
`SoundNames.LvlOne`. `BossLvlOne` repite el desafío de la planta con `BossPlantPhases` ("boss de la aldea") y
`LvlTwo` no tiene vegetación dispersa. **Asumo que la zona bosque es LvlOne — confirmar.**

### Jerarquía de LvlOne (raíces e hijos principales)

Coordenadas = posición de mundo del pivote. `<n>` = cantidad de descendientes.

```
@Terrain                         <1380>   ← arte del nivel, ya separado por zonas
├─ Initial Zone                  <77>     SplineTerrains, agua inicial, ganchos, frutilla
├─ ParkourZone                   <57>     SplineTerrains, plataformas, 17 arándanos, timeline Zona2
├─ CascadeZone                   <130>    cascada, plataforma del castor, 33 arándanos
├─ waterSlideZone                <126>    tobogán de agua, 11 plataformas parkour, 5 ganchos
├─ combatZone                    <151>    lago (71), plataformas de combate, 11 arándanos
├─ CorruptionLEaves              <24>     24 DecalProjectors de corrupción (combate)
├─ Bordes                        <244>    hojas grandes (HojaGrande/HojaBasica) en los bordes
├─ Props Poly                    <272>    rocas (174), Abeto (36), Tronco_1 (31), Pino (23) colocados con Polybrush
├─ Decals                        <153>    decals de flores azules/rojas, rocas, tréboles
├─ EnredaderasDelca              <32>     decals de enredaderas en paredes
├─ Effects                       <95>     LeavesPS (69), ButterfliesVFX (10), AmbientPS (13)
├─ Caminos, Paredes              —        PathCanvas (máscaras de camino pintadas)
├─ Arboles                       —        ScatterField  (Abeto/Pino/Tronco + variantes corruptas)
├─ Arbustos                      —        ScatterField  (Bush_1..4 + CorruptedBush)
├─ Hongos                        —        ScatterField  (Hongo 1..10)
├─ Vegetacion                    —        ScatterField  (flores/plantas sanas)
├─ VegeacionCorrupta             —        ScatterField  (C_* corruptas, solo en la arena)
└─ GrassField                    —        GrassField    (pasto)
/// Game                         <8755>
├─ pj                            [-3,0,-8]       Player, DamageFeedback, CinemachineImpulseSource…
├─ CorruptionChallenge           [143,38,-120]   <8420>  ← el desafío de purificación
│  ├─ CarniborousPlantBOSS       [139,37,-119]   CarnivorousPlant
│  ├─ ChallengeCore (0/1/2)      [154,41,-95] [132,39,-98] [135,38,-145]   PurificationCore
│  ├─ ChallengeMananger          [147,38,-117]   PurificationChallenge
│  ├─ Shield                     [139,38,-119]   PurificationBarrier (escudo de la planta)
│  ├─ HeavyBeetle (0..5)         alrededor de [143,38,-112]   HeavyBeetle (IPurifiable)
│  ├─ MuerdagoColina, MuerdagoParkour             enredaderas corruptas
│  └─ ClearShot Camera
├─ Barricade Parkour             [-4,7,-74]      prefab, LittleBarricade + enredaderas (M_Enredaderas)
├─ BarricadeWaterParkour         [92,45,-148]    prefab, LittleBarricade + 12 CorruptedLine_PS
├─ SecondZoneMuerdago            [-11,7,-77]     2 LineController (enredadera corrupta)
├─ PathParkourMuerdago           [89,27,-113]    2 LineController
├─ ParkourCaminoEspecial         [89,27,-113]    2 LineController
├─ FinalEnredaderas (x2)         [156,37,-82] [117,37,-122]
├─ NPC                           5 Duck, MomDuck [8,19,-109]
├─ beaverpuzzle (OFF)            castor
├─ platforms                     21 plataformas especiales (hongos rebote, subibaja…)
├─ LevelOneManagert              LevelOneManager
└─ CameraStateManager, Canvas, cámaras Cinemachine
Checkpoint                       5 CheckPoint: [-2,0,-2] [-15,3,-58] [-17,17,-92] [82,17,-81] [107,38,-122]
Directional Light, Main Camera (CinemachineBrain + ImpulseSource), Global Volume [prefab],
GameManager (+LevelCheckpointRegistrar), SceneManager (ScreenManager), RespawnCanvas (RespawnController),
DamageFeedbackCanvas, fader (ScreenFader), AudioManager, BulletFactory, DeathZone x2, Levelstats, EventSystem
```

### Extensión de cada zona (percentil 5–95 de los objetos con malla)

| Grupo | Centro (x, y, z) | X | Y | Z |
|---|---|---|---|---|
| Initial Zone | (-3, 2, -23) | -29 … 23 | -3 … 7 | -42 … -3 |
| ParkourZone | (-13, 9, -69) | -33 … 7 | 2 … 15 | -90 … -49 |
| CascadeZone | (11, 9, -88) | -21 … 42 | -8 … 26 | -129 … -47 |
| waterSlideZone | (80, 22, -106) | 48 … 111 | 4 … 39 | -143 … -69 |
| combatZone | (144, 39, -112) | 118 … 170 | 34 … 43 | -158 … -66 |
| CorruptionChallenge | (143, 39, -109) | 133 … 152 | 38 … 40 | -126 … -93 |
| VegeacionCorrupta (scatter) | — | 114 … 171 | 37 … 39 | -165 … -74 |

---

## 0.3 Terreno, vegetación e iluminación

### Piso
- **No es Unity Terrain.** Hay tres `TerrainData` en `Assets/Terrain/`, pero ningún componente `Terrain` en LvlOne.
- El piso son **`SplineTerrain`** (54 en escena, `Assets/Spline Terrain/`, genera mallas + colliders horneados en
  `Spline Terrain/Baked/`) y mallas **Polybrush**.
- Los renderers visibles del piso usan `GekkoPathBlend.shader` (`M_GroundPath 1/2`, `M_WallPath 3`): base + hasta 4
  texturas pintadas por **máscara splat** (`PathCanvas`, texturas en `_PaintToolsSandbox/Data/LvlOne_*Mask.png`) +
  máscara de tinte. Otros con URP Lit (`Platform_Dirt`, `Platform_DarkDirt`, `New Material`).
- Los `M_Floor/M_Wall/M_Edge` de `Spline Terrain/Materials` aparecen en los `SplineTerrain` con un shader built-in;
  parecen ser los materiales por defecto del generador. **Verificar en el Editor si se ven** o si quedan tapados.

### Pasto
- **`GrassField`** propio (2 campos en LvlOne): `LvlOne_GrassField_Grass` (~52.700 briznas, `M_Grass 2`) y
  `LvlOne_GrassField_Grass 2` (~24.100 briznas, `M_Grass 3`). Chunks de 8 u, 2 segmentos, sin sombras.
- Shader `GekkoGrass.shader` (HLSL): viento por ruido (`_WindDirection/Scale/Speed/Strength`), sway e interacción
  (`_GrassInteractors[]` global, lo sube `GrassInteractionManager`). Desplazamiento en `GrassDisplace(positionWS…)`.
- Las mallas no se guardan (se rearman en `OnEnable`), así que **cualquier efecto tiene que ir en el shader**.

### Árboles y vegetación dispersa (`ScatterField`, GPU instancing)

| Campo | Prototipos (instancias) | Materiales |
|---|---|---|
| **Arboles** (`LvlOne_ScatterField_Scatter`) | Abeto 300, Tronco_1 310, Pino 282 · **CorruptedAbeto 48, CorruptedPino 39, CorruptedTronco_1 56** | Tronco: `Trees.mat` (URP Lit). Hojas: `M_AnimatedLeaves` / `M_PineAnimLeaves` (sanas) · `M_CorruptedTree` / `M_PineAnimLeaves 1` (corruptas), todas con `S_Leaf`/`S_LeafPine` |
| **Arbustos** | Bush_1..4 (295) · **CorruptedBush 1..4 (78)** | `M_Bushes 1` / `M_CorruptedBush` (`S_Leaf`) |
| **Hongos** | Hongo 1..10 (419) | `Hongos.mat` (URP Lit) |
| **Vegetacion** | DecoAmarilla, DecoAzul, Lavandas, Margaritas, Plant2/3/5, Plants1, Treboles, panchito (1.171) | `Plants 1.mat`, `Prop/PlantsGreener.mat` (URP Lit, alpha clip) |
| **VegeacionCorrupta** | C_DecoAmarilla, C_DecoAzul, C_Lavandas, C_Margaritas, C_Plant2/3/5, C_Plants1, C_Treboles (585) | `Prop/Corruption/PlantsGreener.mat` (URP Lit, alpha clip) |

- Ubicación de las variantes corruptas: `CorruptedPino/Tronco` y `CorruptedBush` solo en x 113…185 (arena y
  alrededores). `CorruptedAbeto` va de x -30 a 182 con z < -95 (más repartido). `VegeacionCorrupta` solo en la arena.
- Hay además **árboles y rocas como GameObjects** (colocados con Polybrush, bajo `Props Poly`): Abeto 36,
  Tronco_1 31, Pino 23, rocas R_Small/Tall/Short 182, con los mismos materiales. Y 4 `tree.prefab` (plataformas-árbol).
- **Hojas:** `S_Leaf` y `S_LeafPine` son Shader Graph Lit con **alpha clip** (`_AlphaCutOf`) y **viento por vértice**
  (`_WindDirection/Speed/Strength`, `_WiggleOffset`, Simple Noise + Rotate sobre Position). No son SpeedTree.
- **Requisito de `ScatterField`:** los materiales deben tener *GPU Instancing* activado (si no, no se dibujan). Un
  material nuevo para vegetación tiene que respetarlo.

### Luces
- **Una sola Directional Light**, modo **Realtime** (`m_Lightmapping: 4`), intensidad 1, color (1.00, 0.96, 0.84),
  sombras soft.
- **Sin lightmaps**: `m_LightingDataAsset` apunta al default (no hay Lighting Data Asset de la escena). Baked GI
  figura habilitado en la config pero no hay nada horneado. **No hay Light Probes ni Reflection Probes.**
- Consecuencia para la Fase 3: **cambiar la luz direccional sí se va a ver** (no hay sombras horneadas que lo
  contradigan).

### Post-proceso y niebla
- **Un solo Volume**, global: prefab `Assets/Prefabs/Global Volume.prefab` (prioridad 1, peso 1), perfil
  **`Assets/Scenes/intentocin/Global Volume Profile 1.asset`** — ojo: el perfil está en la carpeta de otra escena y
  lo comparte. Overrides activos: Tonemapping (ACES), Vignette (azulada, 0.345), Bloom, Color Adjustments
  (postExposure 0.5, otro valor en -15). FilmGrain y DepthOfField presentes pero inactivos.
- **No hay Volumes locales.**
- Niebla (Lighting): activada, **Exponential Squared**, densidad **0.01**, color (0.35, 0.50, 0.74).
- Skybox: material con `S_SkyBox.shadergraph`.

### Partículas ambientales
- `Effects/LeavesPS`: 69 instancias de `Leaves.prefab` (hojas cayendo, `M_ParticleLeave`).
- `Effects/AmbientPS`: 13 `ambientalParticles.prefab`.
- `Effects/ButterfliesVFX`: 10 (mariposas).
- Corrupción: 27 `CorruptedLine_PS` (material `Corption.mat`), 12 de ellas en `BarricadeWaterParkour`; los núcleos
  tienen un array `CorruptionParticles` que apagan al romperse.
- Por objeto: `PS_Purified`, `Ps_Selected`, `PS_Angry` en enemigos/núcleos (feedback, no ambiente).
- Nota: `M_ParticleLeave` y `Corption.mat` usan un shader de partículas built-in (fileID 211).

---

## 0.4 Materiales y shaders de LvlOne

Leyenda de "usado por": cantidad de renderers/instancias. "Scatter" = dibujado por `ScatterField`.

| Categoría | Material | Shader | Usado por |
|---|---|---|---|
| **Piso / terreno** | `M_GroundPath 1` | `GekkoPathBlend.shader` (HLSL propio) | 38 directos + 27 por prefab: normal plat ×19, plat2, platParkour1, big circle, Initial Platform… |
| | `M_GroundPath 2` | `GekkoPathBlend.shader` | 26: platLake ×18, CombatPlatform, LakeForm… |
| | `M_WallPath 3` | `GekkoPathBlend.shader` | 10: paredes de plat2, waterSlide, Cascade, BeaverPlatform |
| | `Platform_Dirt` | URP Lit | 46: normal plat, platLake, big circle, fliying plat |
| | `Platform_DarkDirt` | URP Lit | 2: TopBeaverPlatform |
| | `TestCin/New Material` | URP Lit | 9: Ground ×5, pushingWater ×3, BehindCascade |
| | `M_Floor/M_Wall/M_Edge` | built-in (default del SplineTerrain) | 22 c/u en SplineTerrains (verificar si se ven) |
| | Decals: `Blue_flowers`, `Red_flowers`, `Rocks`, `Clovers` | URP `Decal.shadergraph` | ~150 DecalProjectors (`Decals`) |
| **Caminos** | (mismo `GekkoPathBlend`, capa pintada por `PathCanvas` `Caminos`/`Paredes`) | — | — |
| **Árboles: tronco** | `Trees.mat` | URP Lit | 90 GameObjects + ~1.035 Scatter (Abeto, Tronco_1, Pino y sus Corrupted) |
| | `M_Wood` | `S_Xray.shadergraph` | Abeto_Tree ×1 |
| **Árboles: follaje** | `M_AnimatedLeaves` | `S_Leaf` (alpha clip + viento) | 68 GO + 610 Scatter |
| | `M_PineAnimLeaves` | `S_LeafPine` (alpha clip + viento) | 23 GO + 282 Scatter |
| | `M_CorruptedTree` | `S_Leaf` | 104 Scatter (CorruptedAbeto/Tronco) |
| | `M_PineAnimLeaves 1` | `S_LeafPine` | 39 Scatter (CorruptedPino) |
| | `M_Bushes 1` | `S_Leaf` | 295 Scatter + 4 `tree.prefab` |
| | `MPlantasBorde` + `Tallo` | URP Lit (alpha clip) | 243 hojas grandes de `Bordes` |
| **Pasto** | `M_Grass 2`, `M_Grass 3` | `GekkoGrass.shader` (HLSL propio) | 2 GrassField (~77k briznas) |
| | `Plants 1`, `Prop/PlantsGreener` | URP Lit (alpha clip) | 1.171 Scatter (flores/plantas sanas) |
| **Rocas y estructuras** | `M_Rocks` | URP Lit | 182 (R_Small/Tall/Short) |
| | `Hongos.mat` | URP Lit | 419 Scatter |
| | `hongo.mat` | URP Lit | 13 hongos-plataforma (rebote) |
| | embebidos en FBX | importador (URP Lit) | `Cascada.fbx` ×4, `Cascada Final`, `Final2`, `LPlatform`, `Plat5.2` |
| **Vegetación corrupta** | `M_CorruptedBush` | `S_Leaf` | 78 Scatter |
| | `Prop/Corruption/PlantsGreener` | URP Lit (alpha clip) | 585 Scatter (`VegeacionCorrupta`) |
| | `M_Enredaderas` | `S_Enredaderas.shadergraph` (**tiene `_Dissolve`**) | barricadas, enredaderas corruptas |
| | `enredadera.mat` | URP Lit | FinalEnredaderas ×2 |
| | `D_CorruptionLeaves 1` | URP `Decal.shadergraph` | 24 DecalProjectors (`CorruptionLEaves`) |
| | `Corption.mat` | partículas built-in | 27 `CorruptedLine_PS` |
| | (LineRenderer de `LineController`) | — | 6 líneas de enredadera corrupta |
| **Vegetación nueva** | **No hay** objetos que aparezcan al purificar en LvlOne. (Lo más cercano: `_purifiedVisuals` de `PurificationChallenge`, `_activeWhenBroken` de `PurificationCore`.) | — | — |
| **Agua** | `M_Water`, `M_Water 1` | `Stylized_Water.shadergraph` | Initial Water ×4 |
| | `Waterfall` | `Waterfall.shadergraph` | CascadeWater ×3 |
| **Interactivos** | `M_Ladybug` | URP Lit | 15 ganchos de lengua (`Gancho`) |
| | `M_Bellota` | URP Lit | GrapplePoint |
| | `M_TeeterTotter` | URP Lit | 5 subibajas |
| | `Bridge`, `M_Stump`, `Outline` (`Outline.shadergraph`) | URP Lit / SG | puente del castor, tablones |
| | `BlueBerry`, `Raspberry` | `S_Collectable.shadergraph` | 122 arándanos, 6 frutillas |
| **Corruptos con colisión** | `Nucleo.mat` | `S_Enredaderas` (`_Dissolve`) | 3 `ChallengeCore` + barricadas |
| | `Plantas.mat` (`textus nucleo`) | URP Lit | `Barricade Parkour`, `BarricadeWaterParkour` |
| | `Shader Graphs_Shield` | `Escudo/Shield.shadergraph` (`_DissolveAmount`) | escudo de la planta |
| **Personajes y enemigos** | `M_Character`, `M_CharacterBody`, `M_tongueGekko` | URP Lit | Gekko |
| | `M_enemyScarab 1` + embebidos de `PF_Scarab.fbx` | URP Lit | 6 HeavyBeetle |
| | `M_enemycarnivorousPlant` | URP Lit | planta jefe (cambia a `data.purifiedMaterial` al purificarse) |
| | `M_NPCsDucks`, embebido de `PF_beaver.fbx` | URP Lit | patos, castor |
| **Otros** | `Transparente.mat` | URP Lit | 66 cubos invisibles (triggers/colisión) |
| | `Diamond.mat`, `Lit.mat` | URP Lit | frutillas de prueba, cubos sueltos |

**Shaders, por facilidad de modificación:**
- **HLSL propio (texto, editable):** `GekkoPathBlend.shader` (piso), `GekkoGrass.shader` (pasto). → `#include` directo de `PurifyMask.hlsl`.
- **Shader Graph propio:** `S_Leaf`, `S_LeafPine` (follaje), `S_Enredaderas` (enredaderas/núcleos), `Stylized_Water`, `Waterfall`, `S_Collectable`, `Shield`. → nodo Custom Function (paso manual).
- **URP Lit estándar:** troncos, rocas, hongos, flores, plataformas Dirt. → hay que pasarlos a un Shader Graph Lit (Pasos manuales B). Las flores/plantas usan alpha clip; troncos y rocas no.
- **Decal (URP):** flores/rocas pintadas y corrupción. → candidatos a un Decal Shader Graph con la máscara (los decals de corrupción podrían desvanecerse por región solo con la máscara).

---

## 0.5 Scripts

### Purificación con la lengua — cómo funciona hoy

1. **Disparo:** `TongueManager` (MonoBehaviour en el Player) extiende la lengua. Al llegar a la punta, si el
   objeto implementa `IDamageable`, llama `ApplyTipDamage(target)` → `target.Damage(1)`.
   Históricamente `Damage(1)` se llama **dos veces** por lengüetazo (punta y fin de retracción); los que implementan
   la interfaz marcadora **`IHitOncePerLick`** reciben uno solo.
2. **Conteo de toques**, cada uno con su lógica:
   - `PurificationCore` (`IHitOncePerLick`): `_hits++` contra `_hitsToBreak` (en LvlOne los 3 núcleos tienen **1**).
     Al romperse: apaga collider e indicador, disuelve sus renderers con `_Dissolve` (instancias de material),
     prende/apaga `_activeWhileIntact`/`_activeWhenBroken`, saca sus puntos de las `LineController` (enredaderas),
     apaga `CorruptionParticles`, sonido `SoundNames.Purify`, y dispara **`event Action<PurificationCore> Broken`**.
   - `CarnivorousPlant` (`IHitOncePerLick`): ignora toques si `IsShielded`. `_hits` contra `data.hitsToPurify`
     (`CarnivorousPlantDataSO`; hay assets con 1 y con 3). Cada toque le cuesta vida al Gekko (`OnPlayerDamaged`).
     Al purificarse: estado `PlantPurifiedState`, cambia a `data.purifiedMaterial`, **`event Action Purified`**.
   - `HeavyBeetle` (`IPurifiable`): un toque cuando está mareado → `SetPurified(true)`;
     **`event Action<IPurifiable> PurifiedChanged`**. Puede re-corromperse (lo maneja el desafío).
   - `LittleBarricade` (prefabs `Barricade Parkour` / `BarricadeWaterParkour`): `_life -= dmg`; al llegar a 0 apaga
     collider, se disuelve, muestra una cámara y saca puntos de línea. **No expone ningún evento.**
3. **Orquestación:** `PurificationChallenge` (uno por zona; en LvlOne `ChallengeMananger`): escucha `Broken` de sus
   núcleos → disuelve el escudo (`PurificationBarrier.DissolveStep`) un tramo por núcleo → `ShieldDown` → la planta es
   golpeable → al purificarla: purifica a todos sus escarabajos, baja `_pathBarrier`, disuelve `_corruptedVisuals`,
   prende `_purifiedVisuals`, corre el timeline de cierre. Eventos públicos: **`CoreBroken(remaining,total)`,
   `ShieldDropped`, `Completed`**. Se activa visualmente con `LevelOneManager.OnChallengeStart` (lo dispara
   `ChallengeTrigger` al entrar).
4. **Otros:** `CoreTriggeredBarrier` (barrera que cae al romper N núcleos, evento `Dropped`), `PurifiedMaterialSwap`
   (cambia material From→To al romper un núcleo; solo en BossLvlOne), `RecorruptScheduler`.

**Puntos de enganche sin tocar código existente:** `PurificationCore.Broken`, `PurificationChallenge.Completed`
(y `CoreBroken`/`ShieldDropped`), `CarnivorousPlant.Purified`, `CoreTriggeredBarrier.Dropped`,
`PurificationBarrier.Dropped`. **`LittleBarricade` no tiene evento** → para que una barricada dispare una región
habría que agregarle un `event Action` (cambio mínimo, se muestra antes) o usar un trigger de zona detrás.

### Arándanos
- **`Blueberry`** (122 en LvlOne): `Grab()` → `BlueberryComboTracker.OnCollect()`. Si falta vida, cura 20 % y
  resetea combo; con vida llena suma combo y a los 5 activa boost de velocidad (`NotificationSO`), que se extiende
  2 s por arándano. Se cuenta en `CollectiblesRegister`. **No interviene en la purificación.**
- **`PurificationBerry`**: no cura ni acelera; contador propio `CollectiblesRegister.GetCollectibleCount(name)`.
  Lo lee `PurificationGate` una sola vez para fijar `CorruptedGeckoBoss.BlueberryHitReduction`. **No hay
  `PurificationBerry` ni `PurificationGate` en LvlOne.**
- `CollectiblesRegister` es **estático** (no se guarda, persiste mientras viva el dominio).

### Guardado y checkpoints
- `SaveManager` (singleton `DontDestroyOnLoad`, solo en `Menu.unity`): JSON con `JsonUtility` en
  `persistentDataPath/Hola.Hola`, guarda al pausar/salir, eventos `OnSave/OnLoad/OnDelete`. **`SaveData` está vacío**
  (solo un campo comentado). `PlayerPrefsKeys` guarda opciones (audio, video, idioma, controles).
- Checkpoints: `Checkpoint` (trigger) → `GameManager.Instance.checkpointManager.SaveCheckpoint(pos, rot, vida)`.
  `CheckpointManager` es en memoria. Al morir, `RespawnController` hace el iris y teletransporta **sin recargar la
  escena**; si no se pisó ningún checkpoint, **recarga la escena** (resetea coleccionables, y resetearía regiones).

### Cámara, screen shake y hitstop
- Cinemachine 3: `Main Camera` con `CinemachineBrain` + `CinemachineImpulseSource`; `CameraFollow` (en la cámara de
  tercera persona) genera el impulso. **Shake global por evento:** `EventManager.Trigger<float>("OnCameraShake", fuerza)`
  (lo escuchan `CameraFollow` y `CameraShakeOnEvent`; `CameraShakeSignalRelay` lo dispara desde timelines).
- **Hitstop:** `DamageFeedback.HitstopRoutine()` (`Time.timeScale = 0`, `WaitForSecondsRealtime`, restaura). Es
  privado y del daño al jugador; no hay hitstop genérico reutilizable.
- `CameraStateManager` (singleton), `CinematicSkipHandler` (mantener E para saltear timelines).

### Managers / singletons globales
`GameManager` (DDOL: `Pj`, `checkpointManager`, `factory`, capas), `ScreenManager`, `UIManager`, `CameraStateManager`,
`AudioManager` (`AudioManager.instance`, `SoundNames.Purify` existe), `SaveManager`, `RespawnController.Instance`,
`LevelOneManager.Instance` (eventos de misiones del nivel: castor, puente, patos, `OnChallengeStart`).
Bus global: `EventManager` (eventos por string, p. ej. `"OnCameraShake"`, `"OnPlayerDamaged"`, `"PauseEvent"`).

### Conteo de componentes en LvlOne (para estimar costo)

| Tipo | Cantidad |
|---|---|
| GameObjects / instancias de prefab | 9.144 / 1.079 |
| Instancias en ScatterField (sin GameObject) | ~3.900 (Arboles 1.035, Arbustos 373, Hongos 419, Vegetación 1.171, VegCorrupta 585) |
| Briznas de pasto (GrassField) | ~76.800 |
| DecalProjector | ~319 (incluye 24 de corrupción) |
| SplineTerrain / PolybrushMesh | 54 / 51 |
| Arándanos (Blueberry) / frutillas | 122 / 6 |
| ParticleSystem (directos) + prefabs de partículas | 55 + 69 Leaves + 13 ambient + 27 CorruptedLine |
| HeavyBeetle / CarnivorousPlant / PurificationCore | 6 / 1 / 3 |
| LittleBarricade / PurificationBarrier | 2 / 3 |
| LineController (enredaderas) | 12 |
| Checkpoints | 5 |

→ Reactivos por script (`PurifyReactive`) solo tienen sentido para decenas de objetos (decals de corrupción,
partículas, barreras, enredaderas). Árboles, flores, hongos y pasto: **solo shader**.

### Tabla de scripts propios

Se excluyen terceros (`Plugins/`, `TextMesh Pro`, `Samples/`, `TutorialInfo/`) y el paquete `Spline Terrain`
(asmdef propio, solo generación de terreno). "En LvlOne" = instancias del componente (incluye las de dentro de prefabs).

**Núcleo del juego**

| Script | Responsabilidad | En LvlOne | Depende de |
|---|---|---|---|
| `GameManager` | Singleton DDOL: player, checkpoints, factory, pausa, fin de nivel | 1 | CheckpointManager, ScreenManager, RespawnController |
| `EventManager` | Bus estático pub/sub por string | — | — |
| `StaticMethods`, `Enums`, `CinematicSkipHandler`, `LineController` | Utilidades; `LineController` dibuja líneas entre Transforms (enredaderas núcleo↔barrera) | LineController 12 | — |
| `Player` | Wrapper que maneja PlayerController, TongueManager, GekkoHealth, combo de arándanos… | 1 | GameManager, UIManager |
| `PlayerController`, `PlayerInputs`, `AimManager`, `InteractionManager`, `DebugController`, `InputRebindStore` | Movimiento/trepado, input, apuntado, interacción, debug, rebinds | 1 c/u | Player |
| `TongueManager` | Lengua: agarre, objetos, **daño a IDamageable** | 1 | IDamageable, IHitOncePerLick |
| `GekkoHealth`, `GekkoCollision`, `GekkoDetection`, `GekkoGrapple`, `GekkoSwinging`, `RopeSimulation`, `Spring`, `RotateGekkoWhileSwingin` | Vida, colisión, detección de objetivos, gancho/balanceo | (plain classes) | Player |
| `BlueberryComboTracker` | Curación/combo/boost por arándanos | (plain) | PlayerController, GekkoHealth |
| `DamageFeedback` | Hitstop, viñeta, knockback, shake, invulnerabilidad | 1 | EventManager |
| `PlayerViewer`, `HealthBar`, `BlueberryComboUI`, `WindEffectController` | Vista del player, HUD, efecto de viento (FullScreenPass) | 1 | — |
| `Player/Controller/Test/*` (Leg, LegsManager, Move, Spine, TestInputs) | Prototipos de animación procedural (no en uso en LvlOne) | 0 | — |

**Purificación y objetos**

| Script | Responsabilidad | En LvlOne | Depende de |
|---|---|---|---|
| `PurificationChallenge` | Desafío por zona: núcleos → escudo → planta → cierre | 1 | PurificationCore, PurificationBarrier, CarnivorousPlant, HeavyBeetle, LevelOneManager |
| `PurificationCore` | Núcleo golpeable, disolve, evento `Broken` | 3 | IDamageable, LineController |
| `PurificationBarrier` | Barrera que se disuelve (de una o por fases), evento `Dropped` | 3 | — |
| `CoreTriggeredBarrier` | Barrera que cae al romper N núcleos | 0 | PurificationCore |
| `PurifiedMaterialSwap` | Swap de material al romper un núcleo | 0 (1 en BossLvlOne) | PurificationCore |
| `PurificationChallengeHealthbar` | HUD de runas del desafío | 1 | — |
| `ChallengeTrigger` | Trigger que arranca el desafío | 1 | LevelOneManager |
| `RecorruptScheduler` | Re-corrupción temporizada de escarabajos | (plain) | HeavyBeetle |
| `BossPlantPhases`, `BossCinematicSignals` | Fases/cinemáticas del boss de BossLvlOne | 0 | PurificationChallenge |
| `LittleBarricade` | Barricada corrupta rompible con la lengua (sin evento) | 2 (prefabs) | IDamageable, LineController |
| `Collectible`, `Blueberry`, `StrawBerry`, `PurificationBerry`, `CollectiblesRegister`, `CollectableView`, `BlueberryView`, `StrawBerryView` | Coleccionables y conteo | 122 / 6 / 0 | CollectiblesFactory, UIManager |
| `BaseObject`, `InteractableObject`, `GrabbableObject`, `CarriableObject`, `BringgableObject`, `Sphere`, `ShooteableObject`, `BugBullet` | Jerarquía de objetos interactuables / proyectiles | varios | Interfaces |
| `BouncingPlatform`, `SlipperyPlatform`, `PushPlatform`, `TeeterTotter*`, `FallingTeeterTotter` | Plataformas especiales | 13 / 3 / 5 | — |
| `BeaverBridge`, `BeaverBridge_Plank`, `OldBridge*` | Puente del castor | 1 / 3 | LevelOneManager |
| `SimonPuzzle`, `SimonPiece`, `PressurePlate` | Puzzle Simon | 0 | — |

**NPCs**

| Script | Responsabilidad | En LvlOne |
|---|---|---|
| `CarnivorousPlant` (+ `PlantSpitBehaviour`, `Plant*State`) | Planta jefe del desafío, FSM, purificable | 1 |
| `HeavyBeetle` (+ `ChargeMovement`, `DazedFlip`, `HeavyBeetleView/Collision`, `Beetle*State`) | Escarabajo embestidor, `IPurifiable` | 6 |
| `Bug` (+ behaviours y states) | Bichito volador | 0 |
| `CorruptedGeckoBoss` (+ states, `Mistletoe*`, `PurificationGate`, `CorruptedGeckoFriend*`, `FinishBeat`) | Jefe de la cueva | 0 |
| `PuzzleBeaver`, `BeaverTrigger` | Misión del castor | 1 |
| `MomDuck`, `BabyDuck`, `DuckPosition` | Misión de los patos | 1 / 5 / 5 |
| `LazyLadybug` | NPC mariquita (gancho) | 1 |
| `GroundCheck`, `StateMachine`, `IState` | Utilidades de NPC y FSM | — |

**Nivel, cámara, UI, audio**

| Script | Responsabilidad | En LvlOne |
|---|---|---|
| `LevelOneManager` | Eventos de misiones del nivel, música | 1 |
| `LevelStats`, `ParkourCinematicTrigger`, `DeathZone`, `FinishLevelTrigger` | Estadísticas, cinemáticas, límites del nivel | 1 / 3 / 2 / 1 |
| `Checkpoint`, `CheckpointManager`, `LevelCheckpointRegistrar`, `RespawnController` | Checkpoints y respawn (en memoria) | 5 / — / 1 / 1 |
| `CameraFollow`, `CameraRotation`, `CameraStateManager`, `CameraShakeOnEvent`, `CameraShakeSignalRelay` | Cámara 3ª persona, estados, shake | 1 c/u |
| `XRayFollow`, `PulseScaleRotate`, `TrackPosition`, `OscillationMath` | Efectos varios | 1 |
| `UIManager`, `NotificationManager`, `SkipCinematicPrompt`, `CreditsScreen`, `ScreenFader` | HUD, diálogos, notificaciones | 1 |
| `ScreenManager`, `Screens`, `ScreenGO`, `Screen*` (pausa, opciones, confirmación, fin), paneles de settings, `MenuButtonFX`, `UI*` | Menús | 1 |
| `AsyncLoader`, `LoadingScreen`, `LoadingTips`, `SceneLoadButton`, `ScenesDictionary`, `UISpriteSheetAnimator` | Carga de escenas | — |
| `Dialogue`, `DialogueNPC`, `DialogueInteractor` | Diálogos | varios |
| `AudioManager`, `Sounds` | Audio (singleton) | 1 |
| `SaveManager`, `SaveData`, `PlayerPrefsKeys` | Guardado | 0 (solo en Menu) |
| `Localizer`, `LocalizedText` | Idiomas | — |
| `ShooteableObjectFactory`, `CollectiblesFactory`, `Factory<T>`, `ObjectPool<T>` | Factories y pool | 1 |
| `*DataSO` (Flyweight) | Datos por tipo (planta, escarabajo, bug, desafío, notificaciones…) | assets |

**Herramientas y sistemas aislados**

| Script | Responsabilidad | En LvlOne |
|---|---|---|
| `ScatterField`, `ScatterData` (`Gekko.PaintTools`) | Props dispersos por GPU instancing, sin GameObjects | 5 |
| `PathCanvas`, `PathBrushSet` + editores | Caminos pintados por máscara para `GekkoPathBlend` | 3 |
| `GrassField`, `GrassData`, `GrassInteraction*`, `GrassInteractor` (`Gekko.GrassSandbox`) | Pasto en chunks + interacción por array global | 2 |
| `CloudField`, `CloudDrift*`, `CloudSeaScroll*` (`Gekko.CloudsSandbox`) | Nubes | 0 |
| `PrefabSet`, `GekkoToolsWindow`, `PrefabSetPreview`, `RecenterPlayerPivot` (`Gekko.Tools`) | Panel de herramientas del editor | — |
| `SplineSampledLine` | Demo de reemplazo de LineController por spline | 0 |
| `Art/ProceduralPrueba/Gecko*` | Animación procedural del gecko (prueba) | 0 |
| `Angelo Organiza DSP/*` (`Logica`, `SquashEffect`, proyectiles), `Bellota`, `TestCin/*` | Pruebas de arte/menú | GrapplePoint (Bellota) |
| `Editor/BossLvlOneCinematicBuilder`, `SplinesTestSceneBuilder`, `CargaSpriteSheetPostprocessor` | Herramientas de un solo uso | — |

---

## 0.6 Propuesta de regiones y preguntas

### Regiones propuestas

La escena ya separa el nivel en grupos por zona (`@Terrain/<Zona>`) y hay un checkpoint por zona, así que propongo
usar esa misma división. Centros y radios son **estimaciones** a partir de los bounding boxes; las zonas se tocan
en los bordes, así que los radios hay que ajustarlos con los gizmos para que no se pisen (la cascada en particular
necesita 2 nodos porque es alargada y se mete entre el parkour y la entrada).

| Región (`regionId`) | Agrupa | Nodos (centro → radio aprox.) | Checkpoint | Disparador existente candidato |
|---|---|---|---|---|
| `entrada` | `Initial Zone` | (-3, 2, -23) → 22 | CP0 [-2,0,-2] | ninguno (¿arranca sana?) |
| `parkour` | `ParkourZone`, `SecondZoneMuerdago`, `Barricade Parkour` | (-13, 8, -68) → 20 | CP1 [-15,3,-58] | `LittleBarricade` de `Barricade Parkour` (no tiene evento) |
| `cascada` | `CascadeZone`, `beaverpuzzle`, patos | (2, 10, -100) → 22 · (30, 12, -78) → 18 | CP2 [-17,17,-92] | misión del castor / patos (`LevelOneManager.OnBridgeConstructed`, `OnMissionDuckFinish`) |
| `tobogan` | `waterSlideZone`, `BarricadeWaterParkour`, `PathParkourMuerdago`, `ParkourCaminoEspecial` | (80, 22, -106) → 30 | CP3 [82,17,-81] | `LittleBarricade` de `BarricadeWaterParkour` (no tiene evento) |
| `arena` | `combatZone`, `CorruptionChallenge`, `CorruptionLEaves`, `VegeacionCorrupta` | (144, 39, -112) → 35 · (138, 38, -150) → 15 | CP4 [107,38,-122] | **`PurificationChallenge.Completed`** (ya existe) |

Total: ~7 nodos, muy por debajo del límite de 32.

### Tratamiento por tipo de objeto (Fase 2)

| Tipo | Cómo se dibuja | Tratamiento propuesto |
|---|---|---|
| Piso (`GekkoPathBlend`) | HLSL propio | Máscara por píxel vía `#include "PurifyMask.hlsl"` en el `.shader` (aprobar). Look corrupto aplicado sobre el color final de la mezcla; caminos con menos desaturación usando la máscara de camino que ya tiene el shader. |
| Piso URP Lit (`Platform_Dirt`, etc.) | URP Lit | Pasar a Shader Graph Lit + `SG_PurifyLook` (paso manual), o cambiarles el material por `M_GroundPath`. |
| Troncos (`Trees.mat`) | URP Lit, GO + Scatter | Shader Graph Lit simple (Base Map + Normal + Smoothness) + `SG_PurifyLook`, **con GPU Instancing activado** (ScatterField lo exige). Opcional musgo por altura. |
| Follaje (`S_Leaf`, `S_LeafPine`) | Shader Graph | Agregar `SG_PurifyMask` + `SG_PurifyLook` **solo en el color**; no tocar la rama de Vertex Position (viento) ni el alpha clip. |
| Variantes corruptas en Scatter (`Corrupted*`, `C_*`) | Scatter | No se pueden apagar por script. Opción: en su material, **alpha clip por máscara** (se "queman" donde llega la onda, con borde emisivo). Así los prototipos sanos que ya están pintados alrededor quedan, y los corruptos desaparecen solo en su región. |
| Pasto (`GekkoGrass`) | HLSL propio | Máscara en el fragment (color) + en `GrassDisplace` escalar la altura por `lerp(0.4, 1, Mask)` para que "se levante". |
| Flores/plantas (`Plants 1`, `PlantsGreener`) | URP Lit + alpha clip, Scatter | Shader Graph Lit con alpha clip + `SG_PurifyLook` (+ GPU Instancing). Opcional: escala de vértices por máscara para que "broten" (son >200 por región → por shader, no por script). |
| Hongos, rocas | URP Lit | Solo look leve (referencia visual estable). |
| Decals de corrupción (`D_CorruptionLeaves 1`) | Decal | Decal Shader Graph con la máscara: se desvanecen donde está purificado. Alternativa: `PurifyReactive` (son 24). |
| Decals de flores | Decal | Look leve o sin cambio. |
| Enredaderas `M_Enredaderas` / núcleos | `S_Enredaderas` (ya tiene `_Dissolve`) | `CorruptedPlant` reutilizando `_Dissolve` con instancias de material (no MPB). |
| Barricadas (`LittleBarricade`) | prefab | Ya tienen su propia purificación con la lengua. Ver pregunta 5. |
| `CorruptedLine_PS`, `LeavesPS`, `AmbientPS`, mariposas | ParticleSystem | `PurifyParticleSwitch`: apagar corruptas / prender luciérnagas-mariposas por región. |
| Agua (`Stylized_Water`, `Waterfall`) | Shader Graph | Máscara en el color (oscura/violácea → clara). |
| Gekko, enemigos, coleccionables, UI | — | Fuera del sistema. |

### Preguntas antes de la Fase 1

1. **Escena:** ¿la zona bosque es `LvlOne`? ¿`BossLvlOne` también tiene que usar el sistema?
2. **Punto de partida visual:** hoy casi todo LvlOne está arteado sano y la corrupción está en la arena. ¿Querés que
   **todas** las regiones arranquen corruptas (con la máscara se oscurece/desatura todo el nivel), o solo algunas
   (p. ej. `tobogan` y `arena`) y las demás arrancan sanas?
3. **Disparadores:** ¿qué purifica cada región? Propuesta: `arena` = `PurificationChallenge.Completed`;
   `parkour`/`tobogan` = romper su barricada; `cascada` = terminar la misión del castor o de los patos; `entrada` =
   sana desde el inicio. ¿Se agregan núcleos nuevos por región o se usan esos eventos?
4. **¿Orden fijo o libre?** El nivel es lineal (entrada → parkour → cascada → tobogán → arena); ¿se exige el orden?
5. **Barricadas:** para que una `LittleBarricade` dispare una región hace falta agregarle un `event Action` (cambio
   mínimo en un script existente). ¿Lo autorizás, o preferís un trigger de zona detrás de la barricada?
6. **Arena/planta:** ¿la planta purifica la última región con la misma onda, o tiene un efecto distinto (más grande,
   con cinemática)? Hoy ya tiene su timeline de cierre.
7. **Arándanos:** el `Blueberry` común no afecta la purificación. ¿Los toques se reducen con `PurificationBerry`
   (habría que colocarlos en LvlOne), con el `Blueberry` común, o se deja para después?
8. **Enemigos:** al purificar una región, ¿los `HeavyBeetle` que queden se purifican/huyen, o nada?
9. **Guardado:** ¿las regiones tienen que persistir entre sesiones (agregar la lista a `SaveData`/`SaveManager`, que
   hoy solo vive en `Menu.unity`) o alcanza con la sesión? Y si el jugador muere **sin** checkpoint, la escena se
   recarga: ¿se pierden las regiones purificadas (como pasa con los coleccionables)?
10. **Shaders existentes:** ¿autorizás agregar un `#include` + unas líneas a `GekkoPathBlend.shader` y
    `GekkoGrass.shader`? Son HLSL de texto (no Shader Graph), el cambio es acotado y se muestra antes de hacerlo.
11. **URP Lit → Shader Graph:** troncos, rocas, hongos, flores y plataformas Dirt usan URP Lit. ¿Se pasan a un
    Shader Graph Lit propio (paso manual, mismos texturas) o algunos se dejan sin purificación?
12. **Variantes corruptas en Scatter:** ¿te sirve que los prototipos `Corrupted*`/`C_*` se "quemen" por shader donde
    llega la onda (y queden los sanos que ya están pintados), o preferís otra cosa (p. ej. que los sanos aparezcan)?
13. **Volume:** el perfil global está en `Assets/Scenes/intentocin/` y lo comparte `intentocin`. Para el Volume
    "Corrupto" propongo un perfil nuevo propio de la purificación. ¿Ok?
14. **Ubicación/namespace:** ¿`Assets/_Gekko/Purification/` con namespace `Gekko.Purification` (como los otros
    sistemas aislados), o preferís dentro de `Assets/Scripts/Objects/Purification/` sin namespace (como el código
    de juego)?
