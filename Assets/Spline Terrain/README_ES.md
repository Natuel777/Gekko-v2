# Spline Terrain Tool — Guía de uso (Español)

Herramienta de editor de Unity que genera terrenos sólidos a partir de splines. Dibujás un spline
(por ejemplo un círculo) y la herramienta arma un mesh completo con piso, paredes y bordes, que se
actualiza en tiempo real mientras lo editás. Soporta tres modos, materiales tileables por
superficie, bevel curvo, suavizado, perfiles reutilizables, una **grilla densa de piso pintable**
(Polybrush), **piezas piso/pared separables**, **colliders separables** y bake a assets (mesh +
collider optimizado + un asset de datos).

> Versión en inglés: ver `README_EN.md`. Hay una guía visual en HTML (EN/ES) en `index.html`.

---

## Requisitos
- **Unity 6** (desarrollado en 6000.0.62f1)
- **URP** (Universal Render Pipeline)
- Paquete **com.unity.splines** (ya lo usa la herramienta)
- **Polybrush** (opcional) — solo si querés pintar por vértices la grilla densa del piso.

Todo vive en `Assets/Spline Terrain/`. El código de runtime está en `Runtime/`, el de editor en
`Editor/` (cada uno con su Assembly Definition) y la documentación en `Documentation/`.

---

## Inicio rápido
1. Abrí la ventana: **Tools ▸ Spline Terrain ▸ Manager**.
2. Andá a la pestaña **Create**, elegí un **Mode** y una **Start shape** (Circle / Square / Line),
   poné el **Size / Radius**, opcionalmente un **Name**, y tocá **Create new terrain**.
3. Se crea un GameObject con un `SplineContainer`, el componente `SplineTerrain`, un hijo
   `TopTransform` y los materiales por defecto. El mesh aparece al instante.
4. Seleccioná el terreno y **editá los knots del spline** en la escena — el mesh se regenera en vivo.
5. Usá **W / E / R** sobre el terreno para mover / rotar / escalar el piso (ver *Top transform*).

---

## Los tres modos

### Internal (meseta)
Rellena el interior del spline (cerrado): un piso superior + paredes inclinadas que bajan al
contorno base. El piso superior es una copia del contorno base transformada por el **Top transform**
(offset, rotación y escala). También funciona con splines abiertas (la forma se cierra sola).

### Road (camino)
Un camino elevado completo que sigue el spline: piso elevado con un **ancho** configurable, paredes
laterales hasta y=0, y tapas en las puntas si el spline es abierto (un anillo si es cerrado). El Top
transform controla **altura (Y)**, **offset lateral (XZ)** y **ancho (escala)**.

### External (isla con agujero)
El interior del spline queda como hueco; la geometría se genera hacia afuera como un anillo entre el
spline (borde interno) y un borde externo = el spline empujado hacia afuera por **Outward distance**
y deformado con **ruido**. Genera paredes interna y externa; la pared del rim del agujero es opcional.

---

## Edición

### Spline
Editá los knots con las herramientas normales de Spline de Unity. El terreno se regenera en vivo.

### Top transform (el handle del piso)
Cada terreno tiene un hijo `TopTransform`. Al seleccionar el terreno aparece un handle que sigue la
**herramienta activa de Unity**:
- **W (Mover)** → offset del piso (Y = altura, XZ = offset lateral).
- **E (Rotar)** → inclina el piso; las paredes hacen una curva para acompañar la inclinación.
- **R (Escalar)** → escala XZ uniforme del piso (en modo Road es el ancho).

También podés editar todo numéricamente en el **Inspector** o en la pestaña **Edit** de la ventana —
ambos quedan sincronizados con el gizmo.

---

## Referencia de parámetros

**General**
- *Resolution (segments)* — puntos muestreados a lo largo del spline para el mesh visual. Más = más suave y más pesado. (3–400)
- *Collider = visual shape* — si está activo, el collider bakeado es idéntico al visual. Apagalo para un collider optimizado que mantiene la **misma forma (perímetro, piso y bevel)** pero recorta lo redundante.
- *Collider extra simplify (perimeter)* — opcional, apagado por defecto. El collider optimizado ya colapsa las **filas verticales redundantes** de pared/bevel a una sola fila cuando no hay curvatura (sin pérdida). El cap del piso ya está al mínimo, así que subí esto solo para decimar el perímetro (menos triángulos de piso y columnas de pared) a costa de la fidelidad de la forma. 0 = perímetro completo. (0–1)
- *Collider split* — cómo se separa el collider en piezas `MeshCollider` distintas. **No afecta el mesh visual.** Ver *Colliders separables* más abajo.
- *Outline smoothing* — suavizado Laplaciano del contorno (0 = ninguno). Ayuda con splines muy pronunciadas. Muchas iteraciones encogen la forma.
- *Smoothing iterations* — (0–50).

**Floor (Internal / External)**
- *Offset (XZ) / Height (Y)* — espejo de la posición del Top transform.
- *Rotation (tilt)* — espejo de la rotación del Top transform.
- *Floor scale (XZ)* — tamaño del contorno superior respecto de la base.

**Walls / Smoothing**
- *Vertical subdivisions* — resolución de la pared; más = paredes más suaves y mejor curvatura. (1–64)
- *Curvature (bulge)* — abomba las paredes; >0 convexo hacia afuera, <0 cóncavo.
- *Smooth shading* — normales promediadas vs facetado.
- *Wall–floor bevel* — chaflán entre pared y piso (usa el material de **borde**).
  - *Bevel size*, *Bevel subdivisions* (1–32), *Bevel curvature* (−1…1, **relativa al tamaño del bevel**: +convexo / −cóncavo, ~0.55 ≈ cuarto redondeado).

**Visual mesh layout** — ver *Piso denso y meshes separados* más abajo.

**Road** — *Width* (ancho).

**Island (External)** — *Outward distance*, *Noise amplitude / frequency / seed*, *Inner wall (rim)*.

**UVs / Tiling** — *Floor UV scale*, *Wall UV scale*. El tiling lo dan las UVs del mesh (piso =
planar XZ, paredes = longitud de arco × altura), así que cambiarlos re-tilea sin tocar los materiales.

---

## Piso denso y meshes separados (listo para Polybrush)

Estas opciones están bajo **Visual mesh layout** en el inspector / ventana. Solo cambian el mesh
**visual** y mantienen todo parentado bajo el GameObject del terreno para mejor orden.

- **Dense floor grid (Polybrush)** — reemplaza la triangulación mínima del piso por una grilla densa
  para que tenga suficientes polígonos para pintar por vértices. *Floor UV scale* sigue controlando el tiling.
  - **Grid cell size** — tamaño aproximado en mundo de cada celda. Más chico = más denso (más polys).
    El mismo valor maneja todos los modos (topeado internamente para que un valor muy chico no explote el mesh).
  - **Grid style (solo Internal)** — cómo se densifica el piso sólido del modo *Internal*:
    - **Clipped Grid** — grilla regular de filas/columnas recortada al contorno. Quads parejos en el
      interior, pero las celdas del borde quedan partidas en polígonos chicos irregulares.
    - **Subdivided Contour** — triangula el contorno y subdivide cada triángulo de forma uniforme.
      El borde sigue exactamente el outline (sin slivers de recorte); el interior es a base de
      triángulos. Suele dar bordes más limpios. Los pisos de *Road* y *External* ya son tiras/anillos
      de quads, así que siempre subdividen sus quads sin importar esta opción.

- **Separate floor / walls meshes** — separa el mesh visual en GameObjects hijos bajo el terreno:
  **`STT_Visual_Floor`** (material de piso) y **`STT_Visual_Walls`** (materiales de pared + bevel).
  Permite pintar el piso denso sin tocar las paredes. Si está apagado, queda un único mesh combinado
  en el GameObject del terreno y las piezas separadas se borran.

---

## Colliders separables

**Collider split** elige cómo se parte el collider en piezas `MeshCollider` hijas (bajo el terreno).
Usa los grupos de submeshes — **piso**, **pared** y **bevel** — y **nunca cambia el mesh visual**:

- **All together** — un solo collider con piso + pared + bevel.
- **All separate** — tres colliders: piso, pared, bevel.
- **Floor+bevel together, wall separate** — dos colliders.
- **Wall+bevel together, floor separate** — dos colliders.

Las piezas sin geometría (por ejemplo el grupo bevel cuando no hay bevel) se omiten. Los colliders se
generan como GameObjects hijos llamados **`STT_Collider…`**.

- **Bake** genera las piezas de collider con assets de mesh guardados.
- **Rebuild colliders (preview)** (botón en la sección Bake) genera la misma jerarquía con meshes
  temporales para que veas/uses los colliders separados en el editor sin un bake completo.
  (Los colliders no se reconstruyen en cada edición del spline porque cocinar MeshColliders es costoso.)

---

## Materiales
El mesh se divide en **3 submeshes**, cada uno con su slot de material:
- **0 Floor (piso)** — UVs planares XZ.
- **1 Wall (pared)** — UVs de longitud de arco a lo largo del spline × altura.
- **2 Edge (borde)** — el bevel (y, cuando existen, bordes especiales).

Por defecto se asignan `M_Floor`, `M_Wall`, `M_Edge` (URP Lit) al crear. Asigná cualquier material
URP tileable a cada slot. Cuando el visual está separado en piezas, la pieza de piso usa el material
de piso y la pieza de paredes usa los materiales de pared (+ borde).

---

## Perfiles (presets reutilizables)
Un **Perfil** es un ScriptableObject que guarda un set completo de parámetros (y materiales
opcionales), sin la forma del spline ni los meshes. Sirve para aplicar un look ya definido a
cualquier terreno.

En la sección **Profile** (inspector o ventana):
- **Export new** — guarda los parámetros actuales como un perfil nuevo.
- Campo **Profile** — asignás un perfil existente.
- **Apply profile** — copia los parámetros del perfil a este terreno.
- **Save into profile** — sobrescribe el perfil asignado con los parámetros actuales.

Flujo típico: ajustás un terreno → *Export new* → en otro terreno arrastrás el perfil y
*Apply profile* — sin volver a configurar nada. (Los perfiles incluyen los nuevos ajustes de grilla
de piso y de separación de colliders.)

---

## Bake
El bake congela el terreno en assets para que no necesite regenerarse en runtime. Todo queda
organizado **bajo el GameObject del terreno (el padre)**.

En la sección **Bake**:
- **Include collider** — si genera y asigna piezas de collider.
- **Mesh folder** / **Data folder** — dónde se guardan los assets (puede ser la misma o distinta).
- **Bake** — crea los assets de mesh, los asigna, y escribe un `<Nombre>_Data.asset`
  (un `SplineTerrainData` que guarda todos los parámetros + un snapshot de los knots del spline):
  - *Visual combinado:* `<Nombre>_Visual.asset` en el MeshFilter del terreno.
  - *Visual separado:* `<Nombre>_Floor.asset` y `<Nombre>_Walls.asset` en las piezas hijas `STT_Visual_*`.
  - *Colliders:* un `<Nombre>_Collider….asset` por grupo, en las piezas hijas `STT_Collider…`.
- **Load from SO** — restaura parámetros y forma del spline desde un asset de datos bakeado para
  seguir editando.

> Nota: el collider optimizado mantiene la misma forma y bevel que el visual; solo colapsa las filas
> verticales rectas redundantes (sin pérdida) y siempre usa la triangulación mínima del piso (la
> grilla densa de pintura es solo para el piso visual). **Collider = visual shape** viene activo.

---

## Tips
- Si una spline muy pronunciada rompe el borde, subí un poco **Outline smoothing** (mantené pocas iteraciones, 2–5, para no encoger la forma).
- La cantidad de vértices crece con *Resolución × (subdivisiones de pared + subdivisiones de bevel)* y, en el piso, con **1 / (Grid cell size)²** — arrancá con una celda grande y bajala hasta tener densidad suficiente para pintar.
- Para Polybrush: activá **Dense floor grid**, elegí un **Grid cell size** y prendé **Separate floor / walls meshes** para pintar el hijo `STT_Visual_Floor` sin tocar las paredes.
- Internal/External funcionan con splines abiertas (se cierran solas). Road puede ser abierto (con tapas) o cerrado (un anillo).

---

## Para desarrolladores (arquitectura)
```
Runtime/
  SplineTerrain.cs            Componente principal [ExecuteAlways]: regen en vivo, sync del gizmo,
                              manejo de piezas visual/collider, builders de bake
  SplineTerrainSettings.cs    Todos los parámetros serializables (+ enums: SplineTerrainMode,
                              ColliderSplitMode, FloorGridStyle; BuildTopMatrix, Validate)
  SplineTerrainData.cs        ScriptableObject del bake (settings + snapshot de knots + refs a meshes)
  SplineTerrainProfile.cs     Perfil de parámetros reutilizable
  Generation/                 ISplineTerrainGenerator, MeshBuildResult (+ ToMeshSubset),
                              GeneratorUtils, generadores Internal/Road/External
  Util/                       SplineSampler, PolygonOffset, EarClippingTriangulator,
                              GridCapTriangulator, TerrainNoise
Editor/
  SplineTerrainInspector.cs   Inspector custom + handles de escena (W/E/R)
  SplineTerrainWindow.cs      Ventana con pestañas (Edit / Create / Scene)
  SplineTerrainGUI.cs         GUI compartida de parámetros/bake/perfil (la usan inspector y ventana)
  SplineTerrainBaker.cs       Bake (visual separado + colliders separados) + load-from-SO
  SplineTerrainProfileIO.cs   Exportar / guardar perfiles
  SplineShapeFactory.cs       Splines iniciales círculo / cuadrado / línea
Documentation/                READMEs (EN/ES) + guía visual HTML (index.html)
```
Los generadores devuelven un `MeshBuildResult` con 3 submeshes (piso / pared / borde). Paredes y
bevels los arma `GeneratorUtils.AddWall` / `BuildBevel`. El cap del piso lo arma `AddFlatCap` (ear
clipping simple), `AddGridCap` (grilla recortada, vía `GridCapTriangulator` — Sutherland–Hodgman por
celda) o `AddSubdividedCap` (ear clip + subdivisión uniforme de triángulos); Road/External densifican
con `AddFloorQuadGrid`. `MeshBuildResult.ToMeshSubset` extrae grupos de submeshes (con vértices
compactados) para las piezas visuales separadas y los colliders separados. Las piezas hijas se llaman
`STT_Visual_*` / `STT_Collider*` y siempre quedan parentadas bajo el GameObject del terreno.
