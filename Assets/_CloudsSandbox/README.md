# Nubes estilizadas — carpeta sandbox

Todo lo relacionado con las nubes vive acá y no toca nada del proyecto. Cuando esté
aprobado, se reubica (`Assets/Scripts/...`, `Assets/Shaders/...`) y se borra esta carpeta.

```
Assets/_CloudsSandbox/
├─ Shaders/
│  ├─ GekkoCloudNoise.hlsl        ruido value/FBM 3D compartido
│  └─ GekkoStylizedCloud.shader   shader URP (ForwardLit + ShadowCaster)
├─ Editor/
│  ├─ CloudMeshBuilder.cs         genera las mallas de nube (.asset)
│  └─ CloudSetupMenu.cs           crea el material y el CloudField
├─ Runtime/
│  ├─ CloudField.cs               reparte nubes en grilla (mar de nubes)
│  ├─ CloudSeaScroll.cs           MonoBehaviour fino
│  ├─ CloudSeaScrollMotion.cs     deriva + wrap toroidal (clase plana)
│  ├─ CloudDrift.cs               MonoBehaviour fino  ─┐ para una nube suelta,
│  └─ CloudDriftMotion.cs         movimiento simple   ─┘ fuera del mar
└─ Meshes/                        salida del Cloud Mesh Builder
```

## Cómo probarlo (5 pasos)

1. `Tools > Gekko > Clouds > Cloud Mesh Builder` → **Generar set de 5 variantes**.
   Quedan en `Meshes/`.
2. `Tools > Gekko > Clouds > Crear material de nube` → `Materials/M_StylizedCloud.mat`.
3. `Tools > Gekko > Clouds > Crear campo de nubes en la escena`.
4. En el `CloudField`: asigná las 5 mallas y el material.
5. Click derecho en el componente → **Rebuild**. Dale **Play** para ver la deriva.

## La distancia entre nubes

Es la distancia entre centros de nubes vecinas. En modo `Por Separación` la ponés a
mano; en `Por Cantidad` sale de dividir el área por la cantidad, y el inspector te la
muestra.

Por debajo la distribución es siempre **grilla + jitter**, nunca random puro. Con random
puro no hay forma de gobernar la separación: salen huecos y encimadas por azar. Con
grilla, la separación es un parámetro real, y `Jitter` desordena cada nube dentro de su
celda para que no se lea como cuadrícula (0 = grilla perfecta, 1 = puede tocar la celda
vecina).

La regla para que se lea como **mar continuo** y no como nubes sueltas:

> separación < diámetro promedio de la nube, o sea `Escala mín + Escala máx`

Con los defaults: escala 7–13 → diámetro promedio 20, y 40 nubes en 60×48 dan una
separación de ~7.5. Se pisan bastante, que es lo que arma la masa continua. Si te pasás,
el inspector te lo avisa con un warning.

El gizmo amarillo que se dibuja al seleccionar el `CloudField` es **una celda**, para
ver de un vistazo cuánta separación quedó.

## Cuántas nubes: los dos modos

Cantidad, área y separación son tres caras de lo mismo — fijás dos y la tercera sale
sola. El `Modo` decide cuáles dos fijás vos:

| Modo | Vos ponés | Sale solo |
|---|---|---|
| **`Por Cantidad`** (default) | `Cantidad` + `Área` | la separación |
| `Por Separación` | `Grilla` + `Separación` + `Relleno` | la cantidad |

En `Por Cantidad`, **`Cantidad` es el número exacto de nubes en escena**. La grilla
interna se arma sola: se busca la que tenga las celdas lo más cuadradas posible dentro
del área pedida, así la separación queda pareja en X y en Z.

El inspector muestra el resultado **antes** de generar (cantidad, área y separación), y
te avisa si las nubes quedaron más separadas que su propio diámetro — que es cuando
dejan de leerse como mar y pasan a ser nubes sueltas.

Los campos del modo que no está activo se ocultan, para que no haya dudas sobre cuál
manda.

## Cómo se eligen las celdas

Cuando sobran celdas (siempre pasa: la grilla se redondea hacia arriba, y en
`Por Separación` está el `Relleno`), hay que decidir cuáles se ocupan.

**No alcanza con saltear al azar**: el azar hace grumos y claros grandes, justo lo que
no querés en un mar. Se usa **inserción por punto más lejano** — cada nube nueva va a la
celda que esté más lejos de todas las ya elegidas. Da un reparto parejo tipo blue noise
para cualquier cantidad, sin apelmazamientos.

Las distancias son **toroidales**, en línea con el wrap del scroller: si no, al dar la
vuelta el borde quedaría más denso o más ralo que el resto del mar.

Es O(cantidad × celdas). Por encima de 20.000 celdas cae a un salteo barato para no
colgar el Editor.

## El movimiento en un sentido, sin perder las nubes

`CloudSeaScroll` mueve todo el mar en una dirección y hace **wrap toroidal** en el
espacio local del padre: cuando una nube cruza `+extent/2` en X reaparece en
`-extent/2`, y lo mismo en Z.

Ninguna nube se destruye ni se instancia — son siempre las mismas mallas dando la
vuelta. El movimiento es infinito y el costo es constante.

La costura no se nota justamente **porque** la distribución es en grilla: la celda que
sale por un lado es equivalente a la que entra por el otro. Con random puro el borde se
leería al dar la vuelta. Por eso las dos decisiones van juntas.

La deriva es sólo XZ. La componente Y se ignora a propósito: no hay periodo vertical
sobre el que envolver, así que una velocidad en Y haría que el mar se escape para
siempre. La variación de altura la da el cabeceo, que se reescribe desde la altura base
cada frame en vez de integrarse, para que no acumule deriva.

## Por qué está hecho así

### El ruido se evalúa en espacio local, no en mundo

Es el punto central de la técnica. Si el ruido se muestrea con la posición de mundo,
al mover la nube el patrón se queda quieto en el espacio y la geometría "nada" por
adentro. Muestreándolo con `positionOS`, el patrón queda clavado a la malla: la nube
se traslada como un objeto sólido y lo único que cambia con el tiempo es un offset
que se suma al ruido — ese offset es el *rolling*, el hervor lento de la superficie.

```hlsl
float3 offset = GekkoRollOffset(_RollDirection.xyz, _RollSpeed, _Time.y);
float  n = GekkoFbm(positionOS * _NoiseScale + offset);
```

La dirección del roll es local, así que rota con la nube. Es la razón por la que
`CloudDrift` usa una velocidad de giro muy baja por defecto.

### Dos muestreos de ruido, con propósitos distintos

- **Vértice**: desplaza a lo largo de la normal → da el volumen abultado. Se usa
  interpolación quíntica en el value noise justamente acá; con la cúbica clásica las
  derivadas no son continuas y el desplazamiento deja facetas visibles.
- **Fragment**: a mayor frecuencia (`_DetailScale`), rompe la silueta y modula el
  sombreado. Se muestrea con la posición **sin desplazar**, si no el patrón se
  arrastraría con su propia deformación.

### La silueta sale del ángulo de vista, no de una textura

```hlsl
float facing = saturate(dot(N, V));
float mask   = saturate(facing * _Solidity + (noise - 0.5) * _NoiseInfluence);
float alpha  = smoothstep(_Cutoff - _EdgeSoftness, _Cutoff + _EdgeSoftness, mask);
```

Donde la superficie mira de frente a la cámara hay más "espesor" detrás, así que va
más opaca; en el borde se adelgaza y el ruido la come de forma irregular. Es una
aproximación barata de volumen: cuesta dos productos escalares y ningún raymarch.

### Sombreado en bandas con wrap

`_LightWrap` empuja la luz más allá del terminador (las nubes reales dispersan luz
hacia el lado oscuro) y `smoothstep` la corta en dos bandas. Encima va un degradé
vertical en espacio local que oscurece la panza — por eso `CloudField` sólo aplica
yaw a las nubes: si las inclinara, ese degradé apuntaría a cualquier lado.

### Las mallas: icosfera proyectada sobre metaballs

El shader necesita normales suaves y continuas. Combinar esferas sueltas deja
costuras y vértices duplicados, así que `CloudMeshBuilder` parte de una icosfera
(topología uniforme, vértices compartidos por el caché de puntos medios) y empuja
cada vértice a lo largo de su dirección hasta cruzar el umbral del campo de
metaballs — barrido grueso hacia adentro + bisección. Sale un blob cerrado, sin
interiores, y `RecalculateNormals` da normales suaves directo.

**Limitación**: la proyección es radial desde el origen, así que la forma tiene que
ser *star-shaped* respecto del centro. Con `Spread` por encima de ~1.5 aparecen
bultos que el rayo no alcanza y se pierden.

### Estado de render

Cola `Transparent` con `ZWrite On` por defecto. Los blobs son convexos y se cullea la
cara de atrás, así que el orden interno no da problemas y las nubes se ocluyen bien
entre sí. Si se quieren superponer y mezclar (banco de niebla), poner `_ZWrite` en
`Off` y bajar `_Solidity`.

El pase de ShadowCaster aplica **el mismo desplazamiento** que el pase de color; si no,
la sombra proyectada tendría la silueta de la esfera sin deformar.

## Ajustes rápidos

| Quiero… | Toco |
|---|---|
| Tantas nubes exactas | `Modo: Por Cantidad` → `Cantidad` |
| Mar más denso, misma superficie | `Cantidad` ↑ (CloudField) |
| Cubrir más superficie | `Área` ↑ (CloudField) |
| Que no se lea la cuadrícula | `Jitter` ↑ (CloudField) |
| Que se mueva más rápido | `Velocidad` ↑ (CloudSeaScroll) |
| Nubes más deshilachadas | `_NoiseInfluence` ↑, `_Cutoff` ↑ |
| Nubes más sólidas | `_Solidity` ↑, `_EdgeSoftness` ↓ |
| Que hierva más rápido | `_RollSpeed` ↑ |
| Más abultadas | `_Displacement` ↑, `_NoiseScale` ↓ |
| Contraste de tormenta | `_ShadeThreshold` ↑, `_ShadeSmooth` ↓ |
| Contraluz | `_RimStrength` ↑, `_RimPower` ↓ |

## Pendiente / a decidir

- **La referencia real es un volumen raymarcheado**, no blobs de malla: en el GIF se ve
  el slider *Step Jitter*, que es de raymarch. Esto es una aproximación con geometría,
  elegida a propósito por costo. No va a llegar a la densidad del original — si en algún
  momento hace falta, el camino es un shader nuevo de raymarch dentro de una caja.
- El post de MinionsArt es de pago (devuelve 403), así que el shader está hecho sobre la
  técnica pública, no es una réplica de ese post.
- El shader, las mallas y el material ya se generaron bien en el Editor (están en
  `Meshes/` y `Materials/`, y hay una escena de prueba `PruebasClouds.unity`).
  `CloudSeaScroll` es lo último que se agregó y todavía no se corrió.
