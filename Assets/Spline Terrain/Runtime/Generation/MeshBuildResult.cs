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
