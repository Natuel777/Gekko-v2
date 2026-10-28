# Guía para Claude Code — Purificación por partes (GEKKO)

> **Cómo usar este archivo**
> 1. Copialo a la raíz del proyecto de Unity (o a `Docs/`).
> 2. Abrí Claude Code en la carpeta del proyecto y decile:
>    *"Leé `PURIFICACION_ZONA_GUIA.md` y hacé la Fase 0. No sigas a la siguiente hasta que te confirme."*
> 3. Después de cada fase, probá en Unity y recién ahí pedile la siguiente
>    (*"Fase 0 ok, seguí con la Fase 1"*).
>
> Ir fase por fase importa: si algo se rompe, sabés exactamente en qué paso fue.

---

## Contexto para Claude Code

- Proyecto: **GEKKO**, plataformero 3D en **Unity con URP y Shader Graph**. Protagonista: un gecko que purifica la corrupción con la lengua. Los arándanos refuerzan la purificación (reducen los toques de lengua necesarios).
- Referencia visual: la purificación de zonas muertas de *Kena: Bridge of Spirits*. Una onda que se expande desde un punto: el entorno pasa de corrupto (desaturado, oscuro, tinte violáceo, niebla) a sano, las plantas corruptas se queman y brotan flores.

## ⚠️ Concepto clave: la zona se purifica POR PARTES

**La zona NUNCA se purifica entera de una sola vez.** Está dividida en **regiones de purificación** (por ejemplo: entrada, claro, sección de copas, puzzle, arena del jefe). Cada región:

- Tiene su **propio disparador** (su corazón corrupto, un evento de gameplay, etc.).
- Tiene su **propio centro y radio máximo**, ajustados para que la onda cubra **solo esa parte**.
- Se purifica de forma **independiente**: purificar la región A **no cambia nada** en la región B (ni materiales, ni plantas, ni atmósfera).
- Una vez purificada, **queda así** y se guarda.

Todo el sistema tiene que estar pensado para N regiones desde el principio. Si en algún momento una implementación afecta a toda la escena de golpe, es un error.

## Reglas de trabajo (importante)

- **Primero entender, después escribir.** Antes de crear cualquier cosa, hacé el inventario de la Fase 0 completo: escena, scripts, materiales y shaders. Todo lo que hagas después tiene que respetar lo que encontraste ahí.
- **Leer** escenas y prefabs (`.unity`, `.prefab`) para inventariarlos está bien. **Editarlos a mano no.** Tampoco `.shadergraph`, `.mat` ni `.asset`: son YAML/JSON frágiles. Todo lo que haya que tocar en el editor, escribilo como **pasos manuales** para que los haga una persona.
- La lógica de shader va en **archivos `.hlsl`** que se usan desde un nodo **Custom Function** de Shader Graph (modo *File*).
- **No modifiques scripts existentes** sin antes mostrar qué cambiarías y por qué. Preferí engancharte por eventos (`UnityEvent` / `event Action`).
- Código nuevo en `Assets/_Gekko/Purification/` (Scripts, Shaders, Editor), namespace `Gekko.Purification`. Si el proyecto ya usa otra convención, seguí la del proyecto y avisá.
- Sin dependencias nuevas. DOTween solo si ya está instalado; si no, corrutinas + `AnimationCurve`.
- Comentarios y campos del Inspector en **español**, con `[Tooltip]` en todo campo serializado.
- Si algo de esta guía no encaja con lo que encontraste en el proyecto, **no lo fuerces**: avisá y proponé una alternativa.
- Al final de cada fase: listá archivos creados/modificados y los pasos manuales pendientes.

---

## Fase 0 — Inventario completo (sin escribir código del sistema)

El resultado de esta fase es un archivo **`Docs/Purificacion_Inventario.md`** que después se usa como referencia en todas las fases. Tiene que incluir:

### 0.1 Proyecto
- Versión de Unity (`ProjectSettings/ProjectVersion.txt`) y de URP (`Packages/manifest.json`).
- Paquetes relevantes instalados: Input System, Cinemachine, DOTween, VFX Graph, ProBuilder, Polybrush, cualquier paquete de vegetación/viento.

### 0.2 Escenas
- Listar todas las escenas en `Build Settings` y en `Assets/`. Identificar la escena (o escenas) de la **zona bosque**.
- Para la escena del bosque, parsear el `.unity` (solo lectura) y armar un árbol de la jerarquía con los GameObjects raíz y sus hijos principales. Para resolver qué script es cada componente, cruzar el `guid` de cada `MonoBehaviour` con los `.meta` de `Assets/`.
- Agrupar los objetos por tipo (ver la tabla de 0.4).
- Contar cuántos hay de cada tipo, para poder estimar el costo de los scripts reactivos.

### 0.3 Terreno, vegetación e iluminación
- ¿El piso es un **Unity Terrain** o mallas? Si es Terrain: qué capas (Terrain Layers) tiene, si hay **detalles pintados** (pasto) y **árboles pintados** en el Terrain, y con qué shader se dibujan.
- ¿Cómo está hecho el pasto? ¿Detalles del Terrain, prefabs sueltos, un sistema de instancing propio o de un asset?
- ¿Cómo están hechos los árboles? ¿Prefabs con materiales propios, SpeedTree, árboles del Terrain? ¿Las hojas usan alpha clip? ¿Hay viento en el shader?
- Luces: modo de la Directional Light (Baked / Mixed / Realtime), si hay Lighting Data Asset y lightmaps, Light Probes y Reflection Probes.
- Post-proceso: qué **Volumes** existen (globales o locales) y qué overrides tiene cada uno. Configuración de la niebla en Lighting Settings.
- Partículas ambientales existentes (ceniza, polen, luciérnagas, etc.).

### 0.4 Materiales y shaders
Tabla con **cada material usado en la escena del bosque**: nombre, shader (Lit estándar de URP, Shader Graph propio, shader de asset, shader de Terrain), qué objetos lo usan y en qué categoría cae:

| Categoría | Ejemplos |
|---|---|
| Piso / terreno | Terrain, mallas de suelo, caminos |
| Árboles: tronco | troncos, raíces, ramas |
| Árboles: follaje | hojas, copas |
| Pasto | detalles del Terrain, mallas de pasto |
| Rocas y estructuras | rocas, muros de piedra, ruinas |
| Vegetación corrupta | espinas rojas, plantas muertas |
| Vegetación nueva | flores, musgo, hongos que aparecen al purificar |
| Agua | si existe |
| Interactivos | plataformas, anclajes de lengua, obstáculos |
| Corruptos con colisión | espinas o barreras que bloquean el paso |
| Personajes y enemigos | Gekko, bichitos corrompidos, jefe |
| Otros | cualquier cosa que no encaje |

### 0.5 Scripts
- Recorrer **todos los scripts propios** de `Assets/` (ignorar `Packages/` y carpetas de assets de terceros) y armar una tabla: script, responsabilidad en una línea, en qué objetos de la escena está y de qué depende.
- Explicar en detalle:
  - Cómo funciona hoy la **purificación con la lengua** (buscar `Purif`, `Tongue`, `Lengua`, `Corrupt`, `Heart`, `Corazon`): qué método se llama, qué datos maneja, cómo se cuentan los toques.
  - El sistema de **arándanos** (contador, cómo se lee).
  - El sistema de **guardado** o checkpoints, si existe.
  - Cámara, screen shake y hitstop, si existen.
  - Cualquier `Manager`/singleton global.

### 0.6 Propuesta de regiones y preguntas
Con todo lo anterior:
1. Proponer una **división de la zona en regiones** según la estructura del nivel que se ve en la escena (centros aproximados en coordenadas de mundo y radio estimado de cada una). Si hay grupos de objetos o carpetas de jerarquía que ya separan partes del nivel, usarlos.
2. Proponer **en qué categoría de la Fase 2 cae cada tipo de objeto** y qué tratamiento recibe.
3. Hacer las preguntas que hagan falta antes de seguir. Como mínimo:
   - ¿Qué dispara cada región? ¿Un corazón por región, varios puntos que hay que purificar, un evento de gameplay?
   - ¿El jefe purifica la última región o tiene un efecto distinto?
   - ¿Las regiones se purifican en orden fijo o en cualquier orden?
   - Para cada material de terceros o Terrain: ¿se puede reemplazar el shader o hay que buscar otra técnica?

**No pasar a la Fase 1 hasta que se confirme el inventario y la división en regiones.**

---

## Fase 1 — Sistema de regiones

### 1a. `Shaders/PurifyMask.hlsl` — máscara con varias regiones

Lee un **array global** de esferas. Cada esfera es `xyz = centro`, `w = radio actual`. Una región con forma irregular puede usar **varias esferas** (sub-nodos) para cubrir bien su área sin invadir la de al lado.

```hlsl
#define PURIFY_MAX_SPHERES 32

float4 _PurifySpheres[PURIFY_MAX_SPHERES]; // xyz centro, w radio actual (0 = sin purificar)
float  _PurifySphereCount;
float  _PurifyEdgeWidth;

void PurifyMask_float(float3 WorldPos, float NoiseValue, float NoiseAmount,
                      out float Mask, out float Edge)
{
    Mask = 0;
    int count = (int)_PurifySphereCount;
    [loop] for (int i = 0; i < PURIFY_MAX_SPHERES; i++)
    {
        if (i >= count) break;
        float4 s = _PurifySpheres[i];
        if (s.w <= 0.001) continue;                        // esfera todavía no activada
        float r = s.w + (NoiseValue - 0.5) * 2.0 * NoiseAmount;
        float m = saturate((r - distance(WorldPos, s.xyz)) / max(_PurifyEdgeWidth, 0.001));
        Mask = max(Mask, m);                               // 1 = sano, 0 = corrupto
    }
    Edge = Mask * (1.0 - Mask) * 4.0;                      // banda brillante en el frente
}
```

- Agregar la variante `_half`.
- **Ojo con Unity:** `Shader.SetGlobalVectorArray` fija el tamaño del array la primera vez que se llama. Siempre enviar un array de **exactamente `PURIFY_MAX_SPHERES`** elementos (rellenar con ceros).

### 1b. `Shaders/PurifyLook.hlsl`
Genera el look corrupto a partir del color sano, sin texturas extra:
- Entradas: `HealthyColor`, `Mask`, `Edge`, `CorruptTint`, `CorruptSaturation` (≈0.2), `CorruptDarken` (≈0.45), `EdgeColor`, `EdgeIntensity`.
- Salidas: `BaseColor = lerp(corrupto, sano, Mask)`, `Emission = Edge * EdgeColor * EdgeIntensity`.
- Hacer que los parámetros de corrupción sean de **material**, porque las hojas, el tronco y el piso necesitan valores distintos (ver Fase 2).

### 1c. `Scripts/PurifyRegion.cs` — una por parte de la zona
- `string regionId` (único, para el guardado), `string nombreVisible`.
- `List<Transform> nodos`: uno o más centros (las esferas). Cada nodo con su `radioMaximo` (componente auxiliar `PurifyNode` o lista serializada de structs).
- `float duracion`, `AnimationCurve curva` (por defecto ease-in: arranca lenta y acelera, **no lineal**), `float retrasoInicio`.
- `bool purificada` (solo lectura) y `float progreso` (0–1).
- `void Purify()`: público, con guard. Anima el radio de **sus** nodos y nada más.
- `void SetPurifiedInstant()`: deja la región purificada sin animación (para cargar partidas).
- `bool Contiene(Vector3 pos)`: si la posición está dentro del área máxima de la región. Sirve para asignar objetos a regiones.
- `bool OndaAlcanzo(Vector3 pos)`: si el radio actual ya llegó a esa posición.
- Eventos: `OnPurifyStarted`, `UnityEvent<float> OnProgress`, `OnPurifyCompleted`.
- `[ContextMenu("Probar purificación")]`, `[ContextMenu("Resetear a corrupta")]`.
- Gizmos: un color por región, esfera de alambre con el radio máximo de cada nodo y el radio actual relleno semitransparente, nombre de la región en la escena (`Handles.Label`, dentro de `#if UNITY_EDITOR`).

### 1d. `Scripts/PurifyManager.cs` — único en la escena
- Registra todas las `PurifyRegion` de la escena.
- Cada frame en el que alguna región esté animando, arma el array de esferas y lo sube con `Shader.SetGlobalVectorArray` / `SetGlobalFloat`. Si nada cambia, no sube nada.
- Al iniciar, sube todo en 0 (todo corrupto), después aplica el estado guardado.
- Expone `float NivelPurificacionGlobal` (regiones purificadas / total), que se usa en la atmósfera.
- Avisa con un warning si la cantidad total de nodos supera `PURIFY_MAX_SPHERES`.

**Prueba de la Fase 1:** con un material de prueba conectado (Pasos manuales A), al probar la región 1 solo se limpia esa parte. Al probar la 2 se limpia la otra. Las demás siguen corruptas.

---

## Fase 2 — Tratamiento por tipo de elemento

Cada objeto reactivo pertenece a **una región**. Se asigna automáticamente por posición (`PurifyRegion.Contiene`) al iniciar, con la opción de forzarlo a mano en el Inspector. Ningún objeto tiene que reaccionar a la onda de una región que no es la suya.

### `Scripts/PurifyReactive.cs` (clase base)
- `PurifyRegion regionOverride` (opcional) → si está vacía, busca la región por posición.
- Se suscribe a su región. Mientras la región anima, chequea cada ~0.1 s si `OndaAlcanzo(transform.position)`. La primera vez que da true, llama a `protected abstract void OnReached()` una sola vez. Después se deshabilita.
- `void ApplyPurifiedInstant()` para cargar partidas.
- `float retrasoAleatorio` (0–0.3 s) para que no reaccione todo en el mismo frame.

### Guía por tipo

**Piso / terreno**
- *Mallas:* su Shader Graph usa `SG_PurifyMask` + `SG_PurifyLook`. El cambio es por píxel, así que la onda se ve continua sobre el suelo.
- *Unity Terrain:* modificar el shader de Terrain es más delicado. Según la versión de Unity encontrada en la Fase 0, proponer una de estas opciones y **consultar antes de implementar**:
  1. Shader Graph con target de Terrain, si la versión lo soporta.
  2. Copia del shader TerrainLit de URP con la máscara agregada.
  3. Dejar el Terrain sano y poner encima la "corrupción" como **Decal Projectors** de URP (manchas oscuras) que se desvanecen por región. Es la opción más barata y segura.
- Caminos: igual que el piso, pero con menos desaturación, para que el camino se siga leyendo bien al jugar.

**Árboles — tronco**
- Mismo tratamiento que el piso (look por píxel). Corrupto: gris oscuro, poca saturación.
- Opcional: musgo que "sube" por el tronco cuando se purifica. Una segunda máscara usa la altura en mundo (`WorldPos.y`) contra el progreso, mezclando hacia un color de musgo en las caras que miran hacia arriba (normal Y).

**Árboles — follaje**
- Corrupto: hojas marrones/grises, mucho más oscuras. Sano: el verde original.
- Respetar el alpha clip y el **viento** existente: la máscara solo afecta el color y no toca la posición de vértices que ya use el shader.
- Si los árboles son de un asset o SpeedTree y no se puede modificar el shader, alternativa: **dos materiales** (corrupto/sano) y `PurifyMaterialSwap`, que hace un crossfade por `MaterialPropertyBlock`. Si no hay forma de hacer crossfade, un swap con partículas de hojas que tapen el cambio.
- Opcional: partículas de hojas secas cayendo en las regiones corruptas, que se apagan al purificar.

**Pasto**
- *Mallas o instancing propio:* la máscara en el shader. El corrupto además puede ir más **bajo y aplastado**: escalar la posición de vértices en Y por `lerp(0.4, 1, Mask)`, así el pasto "se levanta" con la onda.
- *Detalles pintados en el Terrain:* son difíciles de shaderear. Opciones para consultar: cambiar el shader de detalles, o tener dos capas de detalles (pasto seco y pasto vivo) y cambiar la densidad por región desde código (`TerrainData.SetDetailLayer` en el área de la región, con una copia del TerrainData para no modificar el asset original).
- Cuidar el rendimiento: el pasto es lo más numeroso, así que **nunca** un script por cada mata de pasto. Todo se resuelve por shader.

**Rocas, muros, ruinas**
- Solo la máscara de look (desaturar/oscurecer). Corrupción leve: sirven de referencia visual estable para que el jugador se ubique.

**Vegetación corrupta (espinas, plantas muertas)** → `CorruptedPlant : PurifyReactive`
- Dissolve con borde ardiente (`Dissolve.hlsl`, propiedad `_Dissolve` animada por `MaterialPropertyBlock`) + partículas de brasas opcionales + desactivar al final.

**Corruptos con colisión (barreras, espinas que bloquean)** → `CorruptedBarrier : CorruptedPlant`
- Lo mismo, pero además **apaga sus colliders** cuando termina el dissolve. Esto es gameplay: purificar una región abre caminos. Marcar estos objetos claramente en el inventario.

**Vegetación nueva (flores, musgo, hongos)** → `PurifyGrowth : PurifyReactive`
- Arranca con escala 0 y crece a su escala original con overshoot (≈0.6 s). Guarda la escala original en `Awake`.
- Si son muchas (más de ~200 por región), proponer hacerlo por shader (escala de vértices por máscara) en lugar de un script por objeto.

**Agua** (si existe)
- Corrupta: oscura, opaca, con tinte violáceo. Sana: clara. La misma máscara en su shader.

**Partículas ambientales** → `PurifyParticleSwitch : PurifyReactive`
- Ceniza o esporas corruptas: dejar de emitir (`emission.enabled = false`) y dejar que las que ya están se apaguen solas.
- Luciérnagas o polen: empezar a emitir.

**Enemigos / bichitos corrompidos**
- **No** los toca la onda automáticamente: tienen su propia purificación con la lengua. Solo exponer un evento por si se quiere que, al purificar la región, los que queden huyan o se purifiquen. **Preguntar antes.**

**Gekko, jefe, UI**
- Fuera del sistema. No deben usar la máscara.

---

## Fase 3 — Atmósfera por partes

La atmósfera no puede pasar de "corrupta" a "sana" de golpe cuando se purifica una sola región. Dos capas:

1. **Global gradual** — `PurifyAtmosphere.cs`: lee `PurifyManager.NivelPurificacionGlobal` y anima suavemente (sin saltos) hacia ese nivel:
   - Peso del Volume global "Corrupto" = `1 - nivel`.
   - `RenderSettings.fogDensity` / `fogColor` entre valores corrupto y sano.
   - Intensidad y color de la Directional Light.
   Así, con 1 de 5 regiones purificadas, todo se ve un 20% menos corrupto.
2. **Local por región** (opcional, recomendado): cada región puede tener un **Volume local** (Is Global apagado, con collider y Blend Distance) con su propia corrupción, que `PurifyRegion` lleva a peso 0 al purificarse. Así, si el jugador está dentro de una región ya purificada se ve sana, aunque el resto siga oscuro.

Si en la Fase 0 resultó que la luz es **bakeada**, avisar: cambiar la Directional Light no va a afectar las sombras bakeadas, y el cambio se tiene que vender con post-proceso y niebla.

---

## Fase 4 — Disparadores e impacto

### `Scripts/PurifyHeart.cs`
El corazón corrupto de **una** región (campo `PurifyRegion region`):
- Toques de lengua necesarios, enganchado al sistema existente que se encontró en la Fase 0 (**mostrar cómo antes de modificarlo**). Los arándanos reducen los toques (usar el sistema existente o dejar `ReducirToques(int)`).
- Último toque:
  1. Hitstop: `Time.timeScale = 0.05` durante ~0.08 s reales (`WaitForSecondsRealtime`) y restaurar.
  2. Evento `OnHeartDestroyed` (VFX de astillas/flash, sonido).
  3. `CinemachineImpulseSource.GenerateImpulse()` solo si Cinemachine está instalado.
  4. `region.Purify()`.

### Otros disparadores
`PurifyTrigger.cs` genérico: llama a `Purify()` de una región desde un `UnityEvent` (fin de un puzzle, X bichitos purificados, entrar a un trigger, etc.). Así el equipo puede decidir el disparador de cada región sin código nuevo.

Si las regiones tienen orden fijo, una región puede tener `List<PurifyRegion> requiere`: no se puede purificar hasta que estén purificadas las anteriores.

---

## Fase 5 — Guardado, herramientas y cierre

- **Guardado:** integrarse con el sistema existente (Fase 0). Si no hay ninguno, `PurifySaveData` con la lista de `regionId` purificados en `PlayerPrefs` o JSON, y al cargar llamar `SetPurifiedInstant()` en cada una (y `ApplyPurifiedInstant()` en sus reactivos).
- **`Editor/PurifyDebugWindow.cs`:** lista de regiones con un slider de progreso cada una para previsualizar **en Edit Mode**, botones "Purificar todo / Resetear todo" (solo para debug), y un contador de objetos reactivos por región.
- **Validador:** botón que revisa la escena y avisa de reactivos fuera de toda región, regiones solapadas, `regionId` duplicados y nodos por encima del máximo.
- **Rendimiento:** nada de `GetComponent`/`Find` en `Update`, IDs de shader cacheados, reactivos deshabilitados después de disparar, el array global solo se sube cuando algo cambia.
- **README** en `Assets/_Gekko/Purification/README.md`: cómo agregar una región nueva en pocos pasos.

---

## Pasos manuales (los hace una persona en el editor)

### A. Sub Graphs
1. `SG_PurifyMask`: **Position** (World) → `WorldPos` de un **Custom Function** (File: `PurifyMask.hlsl`, Name: `PurifyMask`). **Position** (World) → **Swizzle** `xz` → **Simple Noise** (Scale ≈ 0.3) → `NoiseValue`. Float `NoiseAmount` (≈2.5). Salidas `Mask` y `Edge`.
2. `SG_PurifyLook` con el Custom Function de `PurifyLook.hlsl`, con los parámetros de corrupción expuestos.
3. **No** crear `_PurifySpheres`/`_PurifySphereCount` en el Blackboard: son globales y vienen del `.hlsl`.

### B. Aplicar a cada categoría
Seguir la tabla del inventario (`Docs/Purificacion_Inventario.md`). En cada Shader Graph de entorno: color actual → `SG_PurifyLook` → Base Color, y sumar su Emission. Los materiales con Lit estándar se pasan primero a un Shader Graph Lit simple que replique Base Map + Normal + Smoothness.

### C. Dissolve
Shader Graph Lit con Alpha Clipping y propiedad `_Dissolve` (Exposed, referencia exacta `_Dissolve`), Custom Function de `Dissolve.hlsl` → Alpha, `EdgeGlow` × color HDR naranja → Emission.

### D. Escena
1. Un `PurifyManager`.
2. Una `PurifyRegion` por parte del nivel, con sus nodos ubicados y radios ajustados viendo los gizmos para que **no invadan** la región vecina.
3. Volume global "Corrupto" + Volumes locales por región (opcional).
4. Componentes reactivos en plantas, barreras, flores y partículas.
5. Un `PurifyHeart` o `PurifyTrigger` por región.

---

## Checklist de aceptación

- [ ] `Docs/Purificacion_Inventario.md` existe y está confirmado.
- [ ] Al entrar a la escena, todas las regiones se ven corruptas.
- [ ] **Purificar una región solo cambia esa región**: materiales, plantas, flores, barreras y partículas de las demás siguen igual.
- [ ] La onda arranca lenta, acelera y tiene un borde irregular y brillante.
- [ ] Las plantas corruptas se queman en el orden en que las alcanza la onda; las barreras liberan el paso.
- [ ] Las flores brotan detrás de la onda.
- [ ] La atmósfera global mejora un poco con cada región, sin saltos bruscos.
- [ ] El viento y el alpha clip de los árboles siguen funcionando igual que antes.
- [ ] Guardar/cargar respeta qué regiones estaban purificadas.
- [ ] El validador no reporta errores.
- [ ] Sin errores en consola ni allocations por frame del sistema.
