# Purificación por regiones

La zona se purifica **por partes**. Cada región define un área corrupta. Cuando se dispara, una onda sale desde su
centro y la limpia. Purificar una región no cambia nada en las otras. **Lo que no está dentro de ninguna región se
ve sano**, así que en el nivel puede haber zonas que nunca estuvieron corruptas.

Escena de ejemplo armada y probada: **`Assets/Scenes/BossLvlOne.unity`** (objetos `PurifyManager`, `Regiones` y
`Atmosfera`).

---

## Agregar una región nueva (5 pasos)

1. **Manager:** si la escena no tiene uno, creá un GameObject vacío con **PurifyManager**. Va uno solo por escena.
2. **Región:** creá un GameObject en el centro de la zona, con **PurifyRegion**.
   - `Region Id`: único, sin espacios (por ejemplo `sector_norte`).
   - `Nodos`: uno por cada círculo. Si dejás `Centro` vacío, usa la posición de la región. Una zona alargada usa
     varios nodos.
   - Ajustá `Radio Maximo` mirando el gizmo en la vista de escena, para que no invada la región vecina. Si dos
     regiones se superponen, esa parte queda corrupta hasta purificar las dos.
3. **Disparador:** en el mismo objeto agregá **PurifyTrigger** y asignale lo que la purifica:
   - `Nucleo`: un `PurificationCore`, que dispara al romperse.
   - `Desafio`: un `PurificationChallenge`, que dispara cuando la planta queda purificada.
   - Sin nada asignado, se dispara desde un UnityEvent llamando a `PurifyTrigger.Disparar()`. Sirve para el fin
     de un puzzle o un trigger de zona.
4. **Materiales:** lo que tiene que verse corrupto tiene que usar un shader con purificación (tabla de abajo).
5. **Probar:** abrí **Tools > Gekko > Purificación**:
   - tildá *Vista previa* y mové el slider de la región, sin dar Play;
   - apretá **Validar escena**;
   - en Play, rompé el núcleo o usá el botón *Purificar* de la ventana.

---

## Shaders con purificación

| Para | Shader | Notas |
|---|---|---|
| Piso pintado (SplineTerrain, plataformas) | `Gekko/Path Blend` | Sección *Purificación* del material. Ojo: el `SplineTerrain` reaplica sus propios materiales (`floorMaterial`/`wallMaterial`/`edgeMaterial`), así que cambialos ahí |
| Pasto (`GrassField`) | `Gekko/Grass` | El pasto corrupto queda caído y se levanta con la onda |
| Lo que antes era URP Lit (paredes, hongos, props) | `Gekko/Purification/SG_PurifyLit` | Mismos nombres que URP Lit (`_BaseMap`, `_BumpMap`, `_MetallicGlossMap`, `_EmissionMap`...): al cambiar el shader el material conserva sus texturas. Activá GPU Instancing si lo dibuja un `ScatterField` |
| Hojas | `Shader Graphs/S_Leaf_Purify`, `S_LeafPine_Purify` | Copias de `S_Leaf`/`S_LeafPine`. El viento y el alpha clip no cambian |
| Agua | `Shader Graphs/Stylized_Water_Purify` | Copia de `Stylized_Water` |
| Prueba | `Gekko/Purification/Debug Mascara` | Verde = sano, violeta = corrupto, amarillo = frente de la onda |

Todos comparten los parámetros de corrupción por material: `_PurifyCorruptTint`, `_PurifyCorruptSaturation`,
`_PurifyCorruptDarken`, `_PurifyEdgeColor`, `_PurifyEdgeIntensity`, `_PurifyNoiseScale` y `_PurifyNoiseAmount`.

**Shader Graph propio:** usá los Sub Graphs `SG_PurifyMask` (da Mask y Edge) y `SG_PurifyLook` (da BaseColor y
Emission). No agregues `_PurifySpheres` al Blackboard: son globales y los sube el `PurifyManager`.

---

## Objetos que reaccionan a la onda

Se asignan solos a la región que los contiene (o a mano con `Region Override`) y reaccionan cuando les llega la onda:

| Componente | Qué hace |
|---|---|
| `CorruptedPlant` | Se quema (anima `_Dissolve`; si el material no tiene esa propiedad, se encoge) y se oculta |
| `CorruptedBarrier` | Igual que la planta, y además apaga sus colliders: abre el camino |
| `PurifyGrowth` | Flores, musgo, hongos: brotan de escala 0 con un rebote |
| `PurifyParticleSwitch` | Apaga partículas corruptas y prende las sanas |

Para cientos de objetos (pasto, flores dispersas con `ScatterField`), usá el shader y no un script por objeto.

---

## Atmósfera

- **`PurifyAtmosphere`** (global): mueve suave el peso del Volume corrupto (perfil `Settings/VP_Corrupto`), la
  niebla y la luz direccional, según cuántas regiones están purificadas. El estado "sano" son los valores que ya
  tiene la escena.
- **`PurifyRegionVolume`** (opcional): un Volume local por región que se apaga con su onda.

---

## Cosas a saber

- **Sin `MaterialPropertyBlock`:** el GPU Resident Drawer de `PC_RPAsset` no es compatible, así que los scripts
  usan instancias de material.
- **Límite de 32 nodos** en total por escena. El validador avisa si se pasa.
- **No se guarda en disco.** Al reaparecer en un checkpoint la escena no se recarga, así que las regiones siguen
  purificadas. Si la escena se recarga, vuelve todo a corrupto, igual que el resto del nivel.
- Los `.shadergraph` `*_Purify` se generaron con un script a partir de los originales. Se pueden abrir y editar
  normalmente en Shader Graph.
