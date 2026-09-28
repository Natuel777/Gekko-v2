# Paint Tools — caminos y dispersión de props

Dos herramientas de pincel, aisladas en esta carpeta. No tocan nada del proyecto.

```
Assets/_PaintToolsSandbox/
├─ Shaders/
│  └─ GekkoPathBlend.shader     piso = base + camino mezclados por máscara
├─ Editor/
│  ├─ SceneBrush.cs             pincel compartido (raycast, trazo, cursor)
│  ├─ PathCanvasEditor.cs       pintor de caminos
│  ├─ ScatterFieldEditor.cs     dispersor de props
│  └─ PaintToolsMenu.cs         atajos
├─ Runtime/
│  ├─ PathCanvas.cs             zona pintable + máscara
│  ├─ PathBrushSet.cs           tus pinceles cargados
│  ├─ ScatterData.cs            instancias como datos
│  └─ ScatterField.cs           dibujo con GPU instancing
└─ Data/                        máscaras y datos de dispersión
```

---

# 1 · Caminos

## Cómo usarlo

1. `Tools > Gekko > Paint Tools > Crear material de piso con camino` → asignale **tu**
   Textura base y **tu** Textura 1. Si querés más de una textura pintable en la misma
   zona, tildá `Textura 2 activa` (y 3/4) y asignale su textura — ver más abajo.
2. Poné ese material en los renderers del piso.
3. `Tools > Gekko > Paint Tools > Crear zona de camino` → ubicalo sobre la zona, ajustá
   el tamaño, y arrastrá los renderers del piso a `Target Renderers`.
4. **Crear máscaras** (elegí resolución) — crea las DOS máscaras de la zona (ver abajo).
5. **Activar modo pintura**, elegí la pestaña **Pintar textura** o **Pintar tinte**, y
   pintá.

> **El PathCanvas se crea en (0,0,0).** Si tu piso no está en el origen, movelo (el
> Transform, no un campo del inspector) hasta que el gizmo amarillo (`Size`) quede
> centrado sobre el piso — si no, el pincel raycastea bien contra el piso pero pinta
> en una zona del mundo que no es la que ves, y desde afuera parece que "no pasa nada"
> al pintar. Encuadrá el PathCanvas junto con el piso en la Scene View para confirmar
> que se solapan antes de pintar.

## Varias texturas pintables en la MISMA zona (piedra, tierra, arena...)

El material soporta hasta **4 texturas pintables** además de la base de fondo (5 en
total). No es un tinte — son texturas de verdad, cada una con su propio tiling y color:

- **Textura 1** siempre está disponible (es la mínima, no tiene toggle).
- **Textura 2/3/4** son opcionales: tildá `Textura N activa` en el material y asignale
  su textura. Apagada, el shader ni siquiera declara ese sampler — no cuesta nada.

Al pintar, elegí la pestaña **Pintar textura** (arriba del pincel) y clickeá el slot que
querés en la paleta (con su miniatura), o usá los **shortcuts `1`/`2`/`3`/`4`** con el
pincel activo para cambiar sin soltar el mouse. Un indicador fijo en la esquina de la
Scene View siempre te dice **con cuál estás pintando ahora**.

Internamente esto es una **splat mask**: cada canal (R/G/B/A) guarda cuánto de la
Textura 1/2/3/4 hay en ese píxel, igual que un splatmap de terreno. Pintar una textura
nueva sobre una zona ya pintada la reemplaza de a poco (no las suma sin límite); borrar
libera peso de vuelta a la base. El total pintado sigue usando el mismo truco de borde
con ruido que antes, así que el límite entre "hay algo pintado" y "es la base" se sigue
viendo prolijo aunque haya 3 o 4 texturas mezclándose.

## Variantes de material completas (clonar)

Si en cambio lo que querés es un material DISTINTO con sus propios valores de tiling/
borde/luz (no solo otra textura en el mismo slot), clonalo:

1. Seleccioná en el Project el material que ya tengas bien ajustado.
2. `Tools > Gekko > Paint Tools > Clonar variante de camino (con nuevas texturas)`.
3. En el clon, cambiale las texturas que quieras — el resto de los valores (tiling,
   borde, triplanar, luz) queda igual.
4. Usá ese clon en un `PathCanvas` nuevo para esa zona.

## Por qué no le importa la cantidad de vértices

Polybrush pinta **colores de vértice**: si la malla tiene pocos vértices, no hay dónde
guardar el detalle, y para pintar un camino fino necesitás subdividir el piso.

Acá se pinta sobre una **textura de máscara proyectada desde arriba**, y el shader la
muestrea por posición de mundo. El detalle lo da la resolución de la textura, no la malla.
Una malla de 100 triángulos y una de 100.000 se pintan exactamente igual.

Y como se muestrea por posición de mundo y no por UV, **tampoco importa cómo estén las
UVs** — que sobre las mallas del Spline Terrain es igual de importante, porque no
garantizan UVs limpias ni sin solapamientos.

> **El precio**: la máscara es plana en XZ, así que dos pisos apilados en la misma
> vertical comparten máscara. La solución es **una zona por piso**, cada una con su
> textura y sus renderers. Por eso el sistema son zonas y no una máscara global.

## El borde estilizado

Una máscara de baja resolución escalada a metros da un borde borroso. Por eso el shader
**rompe el borde con ruido** antes de recortarlo:

```hlsl
coverage = saturate(coverage + (noise - 0.5) * _EdgeNoiseStrength);
coverage = smoothstep(0.5 - width, 0.5 + width, coverage);
```

Ese es el truco que hace que se lea como un borde dibujado a mano en vez de un degradé
sucio. `_EdgeSharpness` controla lo duro del corte y `_EdgeNoiseStrength` lo irregular.
Con esto, 512² suele alcanzar para una zona de 60×60.

El inspector te avisa si bajás de 4 texels por unidad.

## Los pinceles

Creá un **Path Brush Set** (click derecho > Create > Gekko > Paint Tools) y cargale tus
PNGs. Sirve cualquier alpha brush de Photoshop o Krita: blanco pinta, negro no. Funciona
tanto un pincel blanco sobre negro como uno con alpha.

Las texturas necesitan **Read/Write Enabled** — el pincel las lee por CPU. Si falta, el
inspector te lo dice y te ofrece arreglar el importador con un botón.

Sin set cargado se usa un círculo con caída suave, así la herramienta sirve desde el
minuto cero.

`Rotación al azar` gira el stamp en cada aplicación para que no se note repetido.

## Controles y fluidez del pincel

| | |
|---|---|
| Click y arrastrar | pintar |
| Shift + click | borrar |
| Ctrl + rueda, o `[` `]` | radio (proporcional: ~15% por paso) |
| `1`-`4` | textura activa |
| Esc | salir del modo pintura |

- **Trazo continuo**: si movés el mouse rápido, el pincel rellena el tramo entre dos
  eventos en vez de dejar puntos sueltos.
- **Dureza** (pincel circular por defecto): 0 = caída suave desde el centro, alta = disco
  casi pleno. `Fuerza` arranca en 1, así una pasada ya pinta a fondo.
- **Solo sobre el piso destino** (tildado por defecto): el rayo ignora arboles, rocas y
  cualquier collider que no sea uno de los `Target Renderers` del PathCanvas, así no
  pinta en la copa de un árbol ni queda bloqueado por props. Destildalo si tu piso no
  tiene collider propio entre los targets.
- Rendimiento: la subida a la GPU es una por evento (no una por stamp), sin alocaciones
  por píxel, y el cursor ya no fuerza un repintado infinito de la Scene View.

## El tinte (máscara aparte)

Pestaña **Pintar tinte**: pinta sobre una SEGUNDA máscara, independiente de la splat
mask de texturas. RGB = color, A = cuánto de ese color se aplica ahí, y **blanco = sin
cambio** — así podés pintar zonas más rojizas, pálidas o saturadas encima de CUALQUIERA
de las 5 capas (base o Textura 1-4) sin cambiar de material ni pisar la selección de
textura. Van separadas a propósito: pintar textura y pintar tinte no compiten por los
mismos canales.

Internamente se guarda a la mitad y el shader multiplica por 2, que es cómo se mete un
multiplicador neutro en una textura de 8 bits sin canal extra.

## Normal maps (opcional)

El material puede tener relieve además de color: `_BaseNormalMap` para la capa base y
`_Tex1NormalMap` para Textura 1, cada una con su `Strength`. Se mezclan con el mismo peso
que el albedo de Textura 1. **Todavía no cubre Texturas 2/3/4** (para no triplicar samples
sin que lo hayas pedido) — es una extensión chica si hace falta.

Apagado por defecto (`Usar normal maps` destildado): sin texturas asignadas no cuesta
nada, ni siquiera se declaran los samplers extra en esa variante del shader. Para usarlo:

1. Importá tus texturas de normal con el tipo **Normal Map** (Unity las marca con el ícono
   correspondiente y el importer las trata como tangent-space).
2. Asignalas en `_BaseNormalMap` / `_Tex1NormalMap`.
3. Tildá **Usar normal maps** y ajustá la `Fuerza` de cada una.

Con Triplanar activo, el normal map se mezcla por los 3 ejes igual que el color (blend
"whiteout", el estándar para evitar costuras); sin Triplanar usa un solo plano cenital,
igual que el color.

## El desenfoque

Modo `Desenfocar`: box blur dentro del pincel, con radio en píxeles. Además hay un botón
**Desenfocar todo** para suavizar la máscara entera de una.

Se hace sobre una copia de la región, no in-place: si no, el desenfoque se retroalimenta
con los píxeles ya procesados y aparece el arrastre direccional típico.

## Deshacer

El pintado de textura **no va por Ctrl+Z** — `Undo.RecordObject` no maneja bien datos de
textura de varios MB. En su lugar hay un botón **Deshacer trazo**, que restaura el
snapshot tomado al empezar la última pincelada (de la máscara que estés pintando en ese
momento, splat o tinte). Es un solo nivel.

Acordate de **Guardar máscaras** (o Ctrl+S) — guarda las dos (splat y tinte) juntas.

---

# 2 · Dispersión de props

## Cómo usarlo

1. `Tools > Gekko > Paint Tools > Crear campo de props`
2. **Activar modo pintura** → se crea el asset de datos solo.
3. Abrí ese asset y cargale los prefabs en `Prototypes`.
4. Elegí cuáles están activos, ajustá densidad y escala, y pintá.

## Por qué no engorda el proyecto

Polybrush **instancia GameObjects reales**. Cada arbusto que pintás se serializa entero
en el `.unity`: su Transform, su MeshFilter, su MeshRenderer, sus GUIDs. Mil arbustos son
mil objetos que Unity carga, actualiza en la jerarquía y guarda. De ahí viene el peso.

Acá cada prop es **una struct de 40 bytes** en un asset aparte:

```
PrototypeIndex (4) + Position (12) + Rotation (16) + Scale (12)
```

Mil props ≈ **40 KB**. Y no hay Transforms que Unity tenga que actualizar.

El inspector te muestra los KB de datos en vivo.

## Cómo se dibujan

Al construir se aplanan todas las instancias en lotes ya listos — malla + submalla +
material + array de matrices — agrupados por chunk espacial. La transformada local de
cada pieza del prefab se premultiplica ahí, una sola vez.

El trabajo por frame queda en: recorrer chunks, descartar los que no entran en el frustum,
y disparar un `DrawMeshInstanced` por lote visible.

El culling es **por cámara**, enganchado a `beginCameraRendering`, así la vista de escena
y la del juego descartan cada una lo suyo. Hay también un `Max Draw Distance` opcional.

Los prefabs de varias piezas se dibujan enteros: se recogen todos los `MeshFilter` de la
jerarquía, no solo el primero.

> **Requisito**: los materiales necesitan **GPU Instancing** activado, o cada prop es su
> propio draw call y se pierde toda la ventaja. El inspector detecta los que faltan y los
> arregla con un botón.

## Cuando hace falta un collider

Botón **Materializar**: convierte los props de un prefab en GameObjects reales, con sus
colliders y scripts, y los saca de los datos para no dibujarlos dos veces.

Es exactamente el peso que el sistema evita, así que usalo solo con los que lo necesiten
— el árbol contra el que chocás sí, los cien helechos de fondo no.

## Controles

| | |
|---|---|
| Click y arrastrar | dispersar |
| Shift + click | borrar |
| Ctrl + rueda | radio |
| Esc | salir del modo pintura |

`Densidad` es props/m² y se calcula sobre el área barrida, así que no depende de qué tan
rápido arrastres el mouse. `Separación mínima` evita que se amontonen. `Escala` es un
rango. `Alinear a la superficie` va de vertical (0) a perpendicular al piso (1).

---

## Estado de las pruebas

La herramienta de caminos se probó de punta a punta en `PruebasCaminos.unity` (escena de
prueba en esta carpeta), invocando el código real del editor:

- Máscara creada, pintado, borrado con shift, tinte de color y desenfoque: **funcionan**.
- **Independencia de vértices verificada**: el mismo camino cruza un Plane de 121 vértices
  y un Quad de **4 vértices** con resultado idéntico.
- Capturas en `Assets/Screenshots/camino_prueba_*.png`.

Dos bugs encontrados y corregidos durante esa prueba:

1. **Pincel elíptico en zonas no cuadradas.** El radio en píxeles se calculaba con un
   solo eje, así que en una zona de 120×60 el círculo salía aplastado. Ahora se normaliza
   por eje: 34×67 píxeles en la máscara, pero 3.98×3.93 unidades en el mundo.
2. **La máscara desaparecía tras un reimport.** Al reimportarse el asset, la referencia
   guardada dentro del `MaterialPropertyBlock` quedaba apuntando a una textura destruida
   y el camino se iba sin ningún error en consola. Ahora `PathCanvas` detecta el cambio y
   reempuja, y el pincel además reempuja al terminar cada trazo.

El dispersor de props todavía **no se probó**.

**Sistema de splat mask (4 texturas pintables) probado en `PruebasTriplanar.unity`**, vía
MCP: se armó un `PathCanvas` sobre un `Cube` con `M_PruebaCamino` (Base = grasstile,
Textura 1 = groundtile, Textura 2 = ponele, activada por su toggle), se pintaron las 3
zonas con texturas REALES distintas (no tintes) y una franja de tinte violeta encima de
las 3 — las tres texturas y el tinte se ven correctamente en el mismo canvas.

## Pendiente / a decidir
- ~~El shader de camino muestrea las dos capas por XZ del mundo (planar). En paredes se
  estira.~~ Resuelto: `Triplanar` ahora viene tildado por defecto (en el shader y en el
  material que crea el menú), así que paredes/rampas/curvas ya no se estiran. Si un piso
  es 100% horizontal y preferís pagar 1 sample en vez de 3, destildalo a mano.
- **Cambio de formato de máscara (splat mask + tint mask separadas)**: cualquier
  `PathCanvas` pintado con el sistema VIEJO (una sola máscara RGB=tinte/A=cobertura)
  quedó con datos que ya no se leen igual — hace falta recrear sus máscaras
  (`Crear máscaras`) y repintar. El único caso real es el camino de `doubleJump`.
- Normal maps solo cubren Base + Textura 1. Extenderlos a Textura 2/3/4 es directo si
  hace falta, pero suma hasta 6 samples más por pixel en el peor caso (triplanar × 3).
- 4 texturas pintables es el máximo sin agregar una segunda splat mask (usa los 4
  canales RGBA enteros). Si en algún momento hacen falta más, hay que duplicar la
  textura de máscara y el costo de sampleo.
- `PathCanvas` usa `MaterialPropertyBlock`, así que esos renderers salen del SRP Batcher.
  Son pocos (el piso), pero conviene saberlo.
- La separación mínima del dispersor escanea todas las instancias: arriba de 20.000 se
  desactiva sola para no colgar el editor.
- El dispersor no tiene LOD ni impostores. Si un bioma denso pesa, el paso siguiente es
  agregar `LODGroup` por prototipo y elegir malla según distancia al armar los lotes.
