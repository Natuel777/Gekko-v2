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
   textura base y **tu** textura de camino.
2. Poné ese material en los renderers del piso.
3. `Tools > Gekko > Paint Tools > Crear zona de camino` → ubicalo sobre la zona, ajustá
   el tamaño, y arrastrá los renderers del piso a `Target Renderers`.
4. **Crear máscara** (elegí resolución).
5. **Activar modo pintura** y pintar.

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

## El color

La máscara guarda **RGB = tinte** y **A = cobertura**. El tinte multiplica al color del
material del camino, y **blanco = sin cambio** — así podés pintar zonas más rojizas,
más pálidas o más saturadas del mismo camino sin cambiar de material.

Internamente se guarda a la mitad y el shader multiplica por 2, que es cómo se mete un
multiplicador neutro en una textura de 8 bits sin canal extra.

## El desenfoque

Modo `Desenfocar`: box blur dentro del pincel, con radio en píxeles. Además hay un botón
**Desenfocar todo** para suavizar la máscara entera de una.

Se hace sobre una copia de la región, no in-place: si no, el desenfoque se retroalimenta
con los píxeles ya procesados y aparece el arrastre direccional típico.

## Deshacer

El pintado de textura **no va por Ctrl+Z** — `Undo.RecordObject` no maneja bien datos de
textura de varios MB. En su lugar hay un botón **Deshacer trazo**, que restaura el
snapshot tomado al empezar la última pincelada. Es un solo nivel.

Acordate de **Guardar máscara** (o Ctrl+S).

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

## Pendiente / a decidir
- El shader de camino muestrea las dos capas por XZ del mundo (planar). En paredes se
  estira. Para paredes haría falta triplanar, que es un cambio chico si lo necesitás.
- `PathCanvas` usa `MaterialPropertyBlock`, así que esos renderers salen del SRP Batcher.
  Son pocos (el piso), pero conviene saberlo.
- La separación mínima del dispersor escanea todas las instancias: arriba de 20.000 se
  desactiva sola para no colgar el editor.
- El dispersor no tiene LOD ni impostores. Si un bioma denso pesa, el paso siguiente es
  agregar `LODGroup` por prototipo y elegir malla según distancia al armar los lotes.
