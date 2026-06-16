# Spline Terrain Tool — User Guide (English)

A Unity editor tool that generates solid terrain meshes from splines. You draw a spline (for
example a circle) and the tool builds a full mesh with a floor, walls and edges, updating in
real time as you edit. It supports three modes, per-surface tileable materials, a curved bevel,
smoothing, reusable profiles, and baking to assets (mesh + optimized collider + a data asset).

> Spanish version: see `README_ES.md`.

---

## Requirements
- **Unity 6** (developed on 6000.0.62f1)
- **URP** (Universal Render Pipeline)
- **com.unity.splines** package (already used by the tool)

Everything lives under `Assets/Spline Terrain/`. Runtime code is in `Runtime/`, editor code in
`Editor/` (each with its own Assembly Definition).

---

## Quick start
1. Open the window: **Tools ▸ Spline Terrain ▸ Manager**.
2. Go to the **Create** tab, choose a **Mode** and a **Start shape** (Circle / Square / Line),
   set the **Size / Radius**, optionally a **Name**, and click **Create new terrain**.
3. A new GameObject is created with a `SplineContainer`, a `SplineTerrain` component, a child
   `TopTransform`, and the default materials. The mesh appears immediately.
4. Select the terrain and **edit the spline knots** in the Scene view — the mesh regenerates live.
5. Use **W / E / R** on the terrain to move / rotate / scale the floor (see *Top transform* below).

---

## The three modes

### Internal (plateau / mesa)
Fills the inside of the (closed) spline: a top floor + sloped walls down to the base outline.
The top floor is a copy of the base outline transformed by the **Top transform** (offset,
rotation and scale). Works with open splines too (the shape is auto-closed).

### Road
A complete raised path that follows the spline: an elevated floor of a configurable **width**,
side walls down to y=0, and end caps when the spline is open (a ring when closed). The Top
transform controls **height (Y)**, **lateral offset (XZ)** and **width (scale)**.

### External (island with a hole)
The interior of the spline becomes a hole; geometry is generated outward as a ring between the
spline (inner edge) and an outer edge = the spline pushed outward by **Outward distance** and
deformed by **noise**. Inner and outer walls are generated; the hole rim wall is optional.

---

## Editing

### Spline
Edit the knots with Unity's normal Spline tools. The terrain regenerates in real time.

### Top transform (the floor handle)
Each terrain has a child `TopTransform`. Selecting the terrain shows a handle on it that follows
the **active Unity tool**:
- **W (Move)** → floor offset (Y = height, XZ = lateral offset).
- **E (Rotate)** → tilts the floor; the walls curve smoothly to follow the tilt.
- **R (Scale)** → uniform XZ scale of the floor (in Road mode this is the width).

You can also edit everything numerically in the **Inspector** or in the window's **Edit** tab —
both stay in sync with the gizmo.

---

## Parameter reference

**General**
- *Resolution (segments)* — points sampled along the spline for the visual mesh. Higher = smoother, heavier. (3–400)
- *Collider = visual shape* — if on, the baked collider is identical to the visual. Turn it off for an optimized collider that keeps the **same shape (perimeter, floor and bevel)** but trims what is redundant.
- *Collider extra simplify (perimeter)* — optional, off by default. The optimized collider already collapses the redundant **vertical wall/bevel rows** to a single row when there is no curvature (the rows are collinear, so this is lossless) — that's the main saving, and it keeps the bevel so walking near the edge stays faithful. The floor cap is already minimal (n-2 triangles for its perimeter), so it can only be reduced further by decimating the perimeter: raise this value to do that (fewer floor triangles and wall columns) at the cost of shape fidelity. 0 = keep the full perimeter. (0–1)
- *Outline smoothing* — Laplacian smoothing of the sampled outline (0 = none). Helps very sharp splines that would otherwise break. Note: many iterations shrink the shape.
- *Smoothing iterations* — (0–50).

**Floor (Internal / External)**
- *Offset (XZ) / Height (Y)* — mirrors the Top transform position.
- *Rotation (tilt)* — mirrors the Top transform rotation.
- *Floor scale (XZ)* — size of the top outline relative to the base.

**Walls / Smoothing**
- *Vertical subdivisions* — wall resolution; more = smoother walls and better curvature. (1–64)
- *Curvature (bulge)* — bows the walls; >0 convex outward, <0 concave.
- *Smooth shading* — averaged normals vs faceted.
- *Wall–floor bevel* — chamfer between wall and floor (uses the **edge** material).
  - *Bevel size*, *Bevel subdivisions* (1–32), *Bevel curvature* (−1…1, **relative to the bevel size**: +convex / −concave, ~0.55 ≈ rounded quarter).

**Road** — *Width*.

**Island (External)** — *Outward distance*, *Noise amplitude / frequency / seed*, *Inner wall (rim)*.

**UVs / Tiling** — *Floor UV scale*, *Wall UV scale*. Tiling is driven by the mesh UVs (floor =
planar XZ, walls = arc-length × height), so changing these re-tiles without touching materials.

---

## Materials
The mesh is split into **3 submeshes**, each with its own material slot:
- **0 Floor** — planar XZ UVs.
- **1 Wall** — UVs are arc-length along the spline × height.
- **2 Edge** — the bevel (and, when present, special edges).

Defaults `M_Floor`, `M_Wall`, `M_Edge` (URP Lit) are assigned on creation. Assign any tileable
URP material to each slot.

---

## Profiles (reusable presets)
A **Profile** is a ScriptableObject that stores a full set of parameters (and optional materials),
without the spline shape or meshes. Use it to apply a predefined look to any terrain.

In the **Profile** section (inspector or window):
- **Export new** — saves the current parameters as a new profile asset.
- **Profile** field — assign an existing profile asset.
- **Apply profile** — copies the profile's parameters onto this terrain.
- **Save into profile** — overwrites the assigned profile with the current parameters.

Typical flow: tune one terrain → *Export new* → on another terrain, drag the profile and
*Apply profile* — no need to re-enter anything.

---

## Baking
Baking freezes the terrain into assets so it no longer needs runtime regeneration.

In the **Bake** section:
- **Include collider** — whether to generate and assign a `MeshCollider`.
- **Mesh folder** / **Data folder** — where assets are saved (can be the same or different).
- **Bake** — creates `<Name>_Visual.asset`, `<Name>_Collider.asset` (if enabled) and a
  `<Name>_Data.asset` (a `SplineTerrainData` that stores all parameters + a snapshot of the spline
  knots), assigns the visual mesh and the collider, and links the data asset.
- **Load from SO** — restores parameters and spline shape from a baked data asset to keep editing.

> Note: the optimized collider keeps the same shape and bevel as the visual; it only collapses the
> redundant straight vertical rows (lossless). **Collider = visual shape** defaults to on; turn it
> off for the lighter-but-faithful collider, and only raise **Collider extra simplify** if you also
> want to trade footprint fidelity for fewer triangles.

---

## Tips
- For sharp splines that break at the edges, raise **Outline smoothing** a little (keep iterations low, 2–5, to avoid shrinking the shape).
- Vertex count grows with *Resolution × (wall subdivisions + bevel subdivisions)* — the ranges are capped to protect performance.
- Internal/External work with open splines (auto-closed). Road can be open (with end caps) or closed (a ring).

---

## For developers (architecture)
```
Runtime/
  SplineTerrain.cs            Main [ExecuteAlways] component: real-time regen, gizmo sync, bake mesh builders
  SplineTerrainSettings.cs    All serializable parameters (+ BuildTopMatrix, Validate)
  SplineTerrainData.cs        Bake ScriptableObject (settings + knot snapshot + mesh refs)
  SplineTerrainProfile.cs     Reusable parameters profile
  Generation/                 ISplineTerrainGenerator, MeshBuildResult, GeneratorUtils, Internal/Road/External generators
  Util/                       SplineSampler, PolygonOffset, EarClippingTriangulator, TerrainNoise
Editor/
  SplineTerrainInspector.cs   Custom inspector + Scene handles (W/E/R)
  SplineTerrainWindow.cs      Tabbed window (Edit / Create / Scene)
  SplineTerrainGUI.cs         Shared parameter/bake/profile GUI (used by inspector and window)
  SplineTerrainBaker.cs       Bake + load-from-SO
  SplineTerrainProfileIO.cs   Export / save profiles
  SplineShapeFactory.cs       Circle / square / line starter splines
```
Generators output a `MeshBuildResult` with 3 submeshes (floor / wall / edge). Walls and bevels are
built by `GeneratorUtils.AddWall` / `BuildBevel`; the floor cap by `AddFlatCap` (concave-safe ear
clipping). The window's tab system is extensible: add one entry in `BuildTabs()`.
