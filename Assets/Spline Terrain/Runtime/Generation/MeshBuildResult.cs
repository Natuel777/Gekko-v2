using System.Collections.Generic;
using UnityEngine;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// Accumulates vertices, normals, UVs and triangles separated by submesh
    /// (0 = floor, 1 = wall, 2 = edge) and dumps them into a <see cref="Mesh"/>.
    /// It is the common output of all generators.
    /// </summary>
    public class MeshBuildResult
    {
        public const int SubmeshFloor = 0;
        public const int SubmeshWall = 1;
        public const int SubmeshEdge = 2;
        public const int SubmeshCount = 3;

        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();

        public readonly List<int> FloorTriangles = new List<int>();
        public readonly List<int> WallTriangles = new List<int>();
        public readonly List<int> EdgeTriangles = new List<int>();

        /// <summary>Adds a vertex and returns its index.</summary>
        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            int index = Vertices.Count;
            Vertices.Add(position);
            Normals.Add(normal);
            Uvs.Add(uv);
            return index;
        }

        /// <summary>Adds a triangle to the indicated submesh.</summary>
        public void AddTriangle(int submesh, int a, int b, int c)
        {
            List<int> list = TrianglesFor(submesh);
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        /// <summary>Adds a quad (two triangles) with winding a,b,c,d (CCW seen from the front).</summary>
        public void AddQuad(int submesh, int a, int b, int c, int d)
        {
            AddTriangle(submesh, a, b, c);
            AddTriangle(submesh, a, c, d);
        }

        private List<int> TrianglesFor(int submesh)
        {
            switch (submesh)
            {
                case SubmeshWall: return WallTriangles;
                case SubmeshEdge: return EdgeTriangles;
                default: return FloorTriangles;
            }
        }

        public bool IsEmpty => Vertices.Count == 0;

        public bool HasFloor => FloorTriangles.Count > 0;
        public bool HasWall => WallTriangles.Count > 0;
        public bool HasEdge => EdgeTriangles.Count > 0;

        /// <summary>
        /// Dumps only the requested submesh groups into a mesh, compacting (re-indexing) so the
        /// output only keeps the vertices actually used by those groups. Submeshes are appended in
        /// the fixed order floor, wall, edge for those included that have triangles. Reuses the
        /// received instance. Returns null if the requested groups have no triangles.
        /// </summary>
        public Mesh ToMeshSubset(Mesh reuse, bool includeFloor, bool includeWall, bool includeEdge)
        {
            // Decide which groups are actually present.
            bool floor = includeFloor && HasFloor;
            bool wall = includeWall && HasWall;
            bool edge = includeEdge && HasEdge;
            if (!floor && !wall && !edge) return null;

            // Remap used vertices to a compact range.
            var remap = new int[Vertices.Count];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            int Map(int oldIndex)
            {
                int r = remap[oldIndex];
                if (r >= 0) return r;
                r = verts.Count;
                remap[oldIndex] = r;
                verts.Add(Vertices[oldIndex]);
                normals.Add(Normals[oldIndex]);
                uvs.Add(Uvs[oldIndex]);
                return r;
            }

            List<int> Remapped(List<int> src)
            {
                var dst = new List<int>(src.Count);
                for (int i = 0; i < src.Count; i++) dst.Add(Map(src[i]));
                return dst;
            }

            List<int> floorTris = floor ? Remapped(FloorTriangles) : null;
            List<int> wallTris = wall ? Remapped(WallTriangles) : null;
            List<int> edgeTris = edge ? Remapped(EdgeTriangles) : null;

            Mesh mesh = reuse != null ? reuse : new Mesh();
            mesh.Clear();
            if (mesh.name == null || mesh.name.Length == 0) mesh.name = "SplineTerrainMesh (subset)";

            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            int sub = 0;
            int subCount = (floor ? 1 : 0) + (wall ? 1 : 0) + (edge ? 1 : 0);
            mesh.subMeshCount = subCount;
            if (floor) mesh.SetTriangles(floorTris, sub++);
            if (wall) mesh.SetTriangles(wallTris, sub++);
            if (edge) mesh.SetTriangles(edgeTris, sub++);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Dumps the result into a mesh. Reuses the received instance (clears it first)
        /// to avoid generating garbage or Mesh leaks on repeated regenerations.
        /// </summary>
        public Mesh ToMesh(Mesh reuse)
        {
            Mesh mesh = reuse != null ? reuse : new Mesh();
            mesh.Clear();
            mesh.name = "SplineTerrainMesh";

            // Supports more than 65k vertices in case the user raises the resolution a lot.
            mesh.indexFormat = Vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);

            mesh.subMeshCount = SubmeshCount;
            mesh.SetTriangles(FloorTriangles, SubmeshFloor);
            mesh.SetTriangles(WallTriangles, SubmeshWall);
            mesh.SetTriangles(EdgeTriangles, SubmeshEdge);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
