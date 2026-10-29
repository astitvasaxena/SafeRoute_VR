using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        const float WallBase = -0.1f;   // walls start inside the floor slab
        const float MaxPiece = 4f;      // long surfaces are split so each piece receives its nearby lights

        enum Finish { None, Interior, Corridor, Exterior }

        struct Opening
        {
            public float from, to, bottom, top;

            public Opening(float from, float to, float bottom, float top)
            {
                this.from = from;
                this.to = to;
                this.bottom = bottom;
                this.top = top;
            }

            public static Opening Door(float from, float to, float height = DoorHeight) => new Opening(from, to, 0f, height);
            public static Opening Window(float from, float to, float sill = 0.9f, float head = 2.4f) => new Opening(from, to, sill, head);
        }

        // Keyed on size (mm), world UV anchor (mm, wrapped to UvPeriod), chamfer (0.1 mm) and whether UVs are world-anchored.
        static readonly Dictionary<(Vector3Int size, Vector3Int anchor, int chamfer, bool world), Mesh> boxMeshes =
            new Dictionary<(Vector3Int, Vector3Int, int, bool), Mesh>();

        const float UvPeriod = 12f;     // every texture repeat (0.4/0.5/0.6/1/1.2/1.5/2/3/4 m) divides 12 m, so anchors can wrap

        // ---------- Primitives ----------

        /// <summary>
        /// Box with UVs in metres (textures keep a constant real-world scale) and a matching collider. With
        /// <paramref name="worldUV"/> the UVs are anchored to world space, so tile grids run on across split pieces.
        /// </summary>
        static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material,
            bool collider = true, bool isStatic = true, Quaternion? localRotation = null, bool worldUV = false)
        {
            // World anchoring only makes sense for axis-aligned boxes; rotated ones keep local UVs.
            Vector3? anchor = worldUV && !localRotation.HasValue && parent.rotation == Quaternion.identity
                ? parent.TransformPoint(localCenter) : (Vector3?)null;
            return MeshBox(name, parent, localCenter, size, material, BoxMesh(size, anchor), collider, isStatic, localRotation);
        }

        /// <summary>
        /// Box with chamfered edges whose normals blend across the chamfer, so edges catch a highlight like eased
        /// joinery. Collision (if any) is the full box. Falls back to <see cref="BoxMesh"/> when the chamfer is too small.
        /// </summary>
        static GameObject ChamferBox(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material, float chamfer,
            bool collider = false, bool isStatic = true, Quaternion? localRotation = null) =>
            MeshBox(name, parent, localCenter, size, material, ChamferBoxMesh(size, chamfer), collider, isStatic, localRotation);

        static GameObject MeshBox(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material, Mesh mesh,
            bool collider, bool isStatic, Quaternion? localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            if (localRotation.HasValue) go.transform.localRotation = localRotation.Value;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            go.isStatic = isStatic;
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material,
            bool collider = false, bool isStatic = true, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (localRotation.HasValue) go.transform.localRotation = localRotation.Value;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = isStatic;
            return go;
        }

        /// <summary>Cylinder between two local points.</summary>
        static GameObject Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material, bool isStatic = true)
        {
            Vector3 d = b - a;
            return Prim(PrimitiveType.Cylinder, name, parent, (a + b) / 2f, new Vector3(radius * 2f, d.magnitude / 2f, radius * 2f),
                material, false, isStatic, Quaternion.FromToRotation(Vector3.up, d));
        }

        static GameObject ColliderBox(string name, Transform parent, Vector3 localCenter, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
            return go;
        }

        static readonly Dictionary<(int, int, int, int), Mesh> decalMeshes = new Dictionary<(int, int, int, int), Mesh>();

        /// <summary>
        /// Flat overlay quad (no collider, no shadows) lying on a surface with outward <paramref name="normal"/>, its texture
        /// v axis along <paramref name="up"/>. <paramref name="uv"/> picks part of the texture (default all of it); when
        /// <paramref name="uAxis"/> is given the u direction is mirrored if needed so u runs along it. The caller places it
        /// 2-3 mm off the surface.
        /// </summary>
        static GameObject Decal(string name, Transform parent, Vector3 center, Vector3 normal, Vector3 up, Vector2 size, Material material,
            Rect? uv = null, Vector3? uAxis = null)
        {
            Quaternion rotation = Quaternion.LookRotation(-normal, up);
            Rect r = uv ?? new Rect(0f, 0f, 1f, 1f);
            if (uAxis.HasValue && Vector3.Dot(rotation * Vector3.right, uAxis.Value) < 0f) r = new Rect(r.xMax, r.y, -r.width, r.height);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = DecalMesh(r);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.isStatic = true;
            return go;
        }

        /// <summary>Unit quad facing -Z (like the built-in Quad) with UVs spanning <paramref name="uv"/>; cached per rect.</summary>
        static Mesh DecalMesh(Rect uv)
        {
            var key = (Mathf.RoundToInt(uv.x * 10000f), Mathf.RoundToInt(uv.y * 10000f), Mathf.RoundToInt(uv.width * 10000f), Mathf.RoundToInt(uv.height * 10000f));
            if (decalMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var verts = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            var normals = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uvs = new List<Vector2> { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            var tris = new List<int>(6);
            Quad(verts, tris, 0, Vector3.back);
            Mesh mesh = FinishMesh($"Decal {uv.x:0.###},{uv.y:0.###} {uv.width:0.###}x{uv.height:0.###}", verts, normals, uvs, tris);
            decalMeshes[key] = mesh;
            return mesh;
        }

        static GameObject NoShadows(GameObject go)
        {
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Vector3Int MmKey(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f));

        static float Wrap(float x) => Mathf.Repeat(Mathf.Round(x * 1000f) / 1000f, UvPeriod);

        /// <summary>Texture axes of the face with outward normal <paramref name="n"/> (same layout for every box mesh).</summary>
        static (Vector3 u, Vector3 v) FaceAxes(Vector3 n)
        {
            if (n.x > 0.5f) return (Vector3.forward, Vector3.up);
            if (n.x < -0.5f) return (Vector3.back, Vector3.up);
            if (n.z > 0.5f) return (Vector3.left, Vector3.up);
            if (n.z < -0.5f) return (Vector3.right, Vector3.up);
            if (n.y > 0.5f) return (Vector3.right, Vector3.forward);
            return (Vector3.right, Vector3.back);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// Metre UVs projected on the face plane: from the face's own corner, or from the world origin when the box
        /// centre <paramref name="anchor"/> is given (coplanar neighbours then share one continuous grid).
        /// </summary>
        static Vector2 FaceUV(Vector3 pos, Vector3 n, Vector3 h, Vector3? anchor)
        {
            (Vector3 u, Vector3 v) = FaceAxes(n);
            if (anchor.HasValue) return new Vector2(Vector3.Dot(pos + anchor.Value, u), Vector3.Dot(pos + anchor.Value, v));
            return new Vector2(Vector3.Dot(pos, u) + Vector3.Dot(Abs(u), h), Vector3.Dot(pos, v) + Vector3.Dot(Abs(v), h));
        }

        static void Quad(List<Vector3> verts, List<int> tris, int start, Vector3 outward)
        {
            bool front = Vector3.Dot(Vector3.Cross(verts[start + 1] - verts[start], verts[start + 2] - verts[start]), outward) > 0f;
            if (front) tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            else tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        static Mesh FinishMesh(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BoxMesh(Vector3 size, Vector3? anchor = null)
        {
            // Wrap the anchor so world-anchored pieces still share meshes; the UV shift is a whole number of repeats.
            if (anchor.HasValue) anchor = new Vector3(Wrap(anchor.Value.x), Wrap(anchor.Value.y), Wrap(anchor.Value.z));
            var key = (MmKey(size), anchor.HasValue ? MmKey(anchor.Value) : Vector3Int.zero, 0, anchor.HasValue);
            if (boxMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);

            foreach (Vector3 n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down })
            {
                (Vector3 u, Vector3 v) = FaceAxes(n);
                int start = verts.Count;
                Vector3 c = Vector3.Scale(n, h), du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                foreach (Vector3 corner in new[] { c - du - dv, c - du + dv, c + du + dv, c + du - dv })
                {
                    verts.Add(corner);
                    normals.Add(n);
                    uvs.Add(FaceUV(corner, n, h, anchor));
                }
                Quad(verts, tris, start, n);
            }

            Mesh mesh = FinishMesh($"Box {size.x:0.###} x {size.y:0.###} x {size.z:0.###}{(anchor.HasValue ? " W" : "")}", verts, normals, uvs, tris);
            boxMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Box with 45 degree chamfers of <paramref name="chamfer"/> metres: 6 inset faces, 12 edge strips and 8 corner
        /// triangles (96 vertices). Strip and corner vertices take the adjacent face normals, so the chamfer shades as a
        /// rounded edge. UVs stay in metres and run continuously over the chamfers.
        /// </summary>
        static Mesh ChamferBoxMesh(Vector3 size, float chamfer)
        {
            float r = Mathf.Min(chamfer, 0.45f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            int rKey = Mathf.RoundToInt(r * 10000f);
            if (rKey < 5) return BoxMesh(size);   // under 0.5 mm: not worth the extra vertices
            var key = (MmKey(size), Vector3Int.zero, rKey, false);
            if (boxMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            r = rKey / 10000f;
            Vector3 h = size * 0.5f, hi = h - Vector3.one * r;
            var verts = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var tris = new List<int>(132);

            static Vector3 Axis(int a, float s) => a == 0 ? new Vector3(s, 0f, 0f) : a == 1 ? new Vector3(0f, s, 0f) : new Vector3(0f, 0f, s);

            Vector3 Pos(int a, float sa, bool aFull, int b, float sb, bool bFull, int c, float sc)
            {
                var p = new Vector3();
                p[a] = sa * (aFull ? h[a] : hi[a]);
                p[b] = sb * (bFull ? h[b] : hi[b]);
                p[c] = sc * hi[c];
                return p;
            }

            void Vert(Vector3 pos, Vector3 n, Vector3 uvFace)
            {
                verts.Add(pos);
                normals.Add(n);
                uvs.Add(FaceUV(pos, uvFace, h, null));
            }

            // Faces, inset by r on both tangent axes.
            for (int a = 0; a < 3; a++)
                foreach (float s in new[] { -1f, 1f })
                {
                    int b = (a + 1) % 3, c = (a + 2) % 3, start = verts.Count;
                    Vector3 n = Axis(a, s);
                    Vert(Pos(a, s, true, b, -1f, false, c, -1f), n, n);
                    Vert(Pos(a, s, true, b, -1f, false, c, 1f), n, n);
                    Vert(Pos(a, s, true, b, 1f, false, c, 1f), n, n);
                    Vert(Pos(a, s, true, b, 1f, false, c, -1f), n, n);
                    Quad(verts, tris, start, n);
                }

            // Edge strips between faces a and b, running along c.
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, c = (a + 2) % 3;
                foreach (float sa in new[] { -1f, 1f })
                    foreach (float sb in new[] { -1f, 1f })
                    {
                        Vector3 na = Axis(a, sa), nb = Axis(b, sb);
                        int start = verts.Count;
                        Vert(Pos(a, sa, true, b, sb, false, c, -1f), na, na);
                        Vert(Pos(a, sa, true, b, sb, false, c, 1f), na, na);
                        Vert(Pos(a, sa, false, b, sb, true, c, 1f), nb, na);
                        Vert(Pos(a, sa, false, b, sb, true, c, -1f), nb, na);
                        Quad(verts, tris, start, na + nb);
                    }
            }

            // Corner triangles.
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        int start = verts.Count;
                        Vector3 nx = Axis(0, sx), ny = Axis(1, sy), nz = Axis(2, sz);
                        Vert(new Vector3(sx * h.x, sy * hi.y, sz * hi.z), nx, nx);
                        Vert(new Vector3(sx * hi.x, sy * h.y, sz * hi.z), ny, nx);
                        Vert(new Vector3(sx * hi.x, sy * hi.y, sz * h.z), nz, nx);
                        bool front = Vector3.Dot(Vector3.Cross(verts[start + 1] - verts[start], verts[start + 2] - verts[start]), new Vector3(sx, sy, sz)) > 0f;
                        tris.AddRange(front ? new[] { start, start + 1, start + 2 } : new[] { start, start + 2, start + 1 });
                    }

            Mesh mesh = FinishMesh($"ChamferBox {size.x:0.###} x {size.y:0.###} x {size.z:0.###} r{r * 1000f:0.#}", verts, normals, uvs, tris);
            boxMeshes[key] = mesh;
            return mesh;
        }

        // ---------- Walls ----------

        static Vector3 WallPoint(bool alongX, float at, float along, float y) =>
            alongX ? new Vector3(along, y, at) : new Vector3(at, y, along);

        static Vector3 WallSize(bool alongX, float length, float height, float depth) =>
            alongX ? new Vector3(length, height, depth) : new Vector3(depth, height, length);

        /// <summary>
        /// Wall run along X (at z = <paramref name="at"/>) or along Z (at x = <paramref name="at"/>), from the floor slab
        /// to <paramref name="height"/>, with door/window openings. <paramref name="neg"/>/<paramref name="pos"/> are the
        /// finishes of the faces pointing to -Z/+Z (or -X/+X).
        /// </summary>
        static void Wall(Transform p, bool alongX, float at, float from, float to, float height, float thickness,
            Finish neg, Finish pos, bool coping, params Opening[] openings)
        {
            float cursor = from;
            foreach (Opening o in openings.OrderBy(o => o.from))
            {
                Solid(p, alongX, at, cursor, o.from, WallBase, height, thickness, neg, pos);
                if (o.bottom > 0f) WallPiece(p, alongX, at, o.from, o.to, WallBase, o.bottom, thickness, neg, pos);
                if (o.top < height) WallPiece(p, alongX, at, o.from, o.to, o.top, height, thickness, neg, pos);
                cursor = o.to;
            }
            Solid(p, alongX, at, cursor, to, WallBase, height, thickness, neg, pos);

            if (coping) Coping(p, alongX, at, from, to, height, thickness);
        }

        /// <summary>Parapet coping, overhanging both faces by 40 mm. It sits on a wall that already casts the shadow.</summary>
        static void Coping(Transform p, bool alongX, float at, float from, float to, float height, float thickness) =>
            NoShadows(ChamferBox("Coping", p, WallPoint(alongX, at, (from + to) / 2f, height + 0.03f), WallSize(alongX, to - from, 0.06f, thickness + 0.08f), mCoping, 0.01f));

        /// <summary>Solid wall section, split into pieces of at most <see cref="MaxPiece"/> metres.</summary>
        static void Solid(Transform p, bool alongX, float at, float a0, float a1, float y0, float y1, float thickness, Finish neg, Finish pos)
        {
            if (a1 - a0 < 0.001f) return;
            int n = Mathf.CeilToInt((a1 - a0) / MaxPiece);
            float step = (a1 - a0) / n;
            for (int i = 0; i < n; i++)
                WallPiece(p, alongX, at, a0 + i * step, a0 + (i + 1) * step, y0, y1, thickness, neg, pos);
        }

        static void WallPiece(Transform p, bool alongX, float at, float a0, float a1, float y0, float y1, float thickness, Finish neg, Finish pos)
        {
            float length = a1 - a0;
            if (length < 0.001f || y1 - y0 < 0.001f) return;
            float mid = (a0 + a1) / 2f;
            Box("Wall", p, WallPoint(alongX, at, mid, (y0 + y1) / 2f), WallSize(alongX, length, y1 - y0, thickness), mWall, worldUV: true);
            ApplyFinish(p, alongX, at, mid, length, y0, y1, thickness, -1f, neg);
            ApplyFinish(p, alongX, at, mid, length, y0, y1, thickness, 1f, pos);
        }

        static void ApplyFinish(Transform p, bool alongX, float at, float mid, float length, float y0, float y1, float thickness, float side, Finish finish)
        {
            float face = at + side * thickness / 2f;

            void Layer(string name, float b0, float b1, float depth, Material material)
            {
                b0 = Mathf.Max(b0, y0);
                b1 = Mathf.Min(b1, y1);
                if (b1 - b0 < 0.001f) return;
                // World UVs keep facade panel joints and paint continuous across pieces; thin layers on a casting wall add no shadow.
                NoShadows(Box(name, p, WallPoint(alongX, face + side * depth / 2f, mid, (b0 + b1) / 2f), WallSize(alongX, length, b1 - b0, depth), material, false, worldUV: true));
            }

            Vector3 normal = alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);
            Vector3 foot = WallPoint(alongX, face, mid, 0f);
            // A piece can run from the closed void into the stairwell, so test both ends as well as the middle.
            bool seen = new[] { -0.45f, 0f, 0.45f }.Any(k => !IsVoid(WallPoint(alongX, face, mid + k * length, 0f) + normal * 0.1f));
            float ceiling = CeilingAt(foot);

            // Inside finishes: a sit-on vinyl skirting in a floor tone with a small cove at the floor, and multiply strips that
            // fake the contact shadow where the wall meets the floor and the ceiling (no SSAO in the shared URP asset).
            void Inside()
            {
                Layer("Skirting", 0f, 0.1f, 0.016f, mSkirting);
                if (!seen) return;
                if (y0 <= 0f)
                {
                    NoShadows(ChamferBox("Skirting Cove", p, WallPoint(alongX, face + side * 0.0235f, mid, 0.0075f), WallSize(alongX, length, 0.015f, 0.015f), mSkirting, 0.007f));
                    Decal("Floor Contact", p, WallPoint(alongX, face + side * 0.106f, mid, 0.002f), Vector3.up, normal, new Vector2(length, 0.18f), mContactAO);
                }
                if (y0 < ceiling && y1 >= ceiling)
                    Decal("Ceiling Contact", p, WallPoint(alongX, face + side * 0.06f, mid, ceiling - 0.002f), Vector3.down, normal, new Vector2(length, 0.12f), mContactAO);
            }

            switch (finish)
            {
                case Finish.Exterior:
                    Layer("Plinth", WallBase, 0.45f, 0.05f, mConcrete);
                    Layer("Cladding", 0.45f, y1, 0.03f, mFacade);
                    break;
                case Finish.Corridor:
                    Layer("Dado", 0.1f, 1.05f, 0.012f, mDado);
                    // PVC buffer rail with eased edges; the 40 mm architraves still stand 5 mm proud of it.
                    if (y0 <= 1.05f && y1 >= 1.1f)
                        NoShadows(ChamferBox("Chair Rail", p, WallPoint(alongX, face + side * 0.0175f, mid, 1.075f), WallSize(alongX, length, 0.05f, 0.035f), mBufferRail, 0.01f));
                    Inside();
                    break;
                case Finish.Interior:
                    Inside();
                    break;
            }
        }

        /// <summary>Underside of the ceiling above a floor point: the stair tower is double height.</summary>
        static float CeilingAt(Vector3 point) =>
            point.x > 12.9f && point.x < 20.1f && point.z > -6.2f && point.z < 0.05f ? StairCeilingHeight : CeilingHeight;

        /// <summary>Closed, unbuilt space south of the corridor (outside the lift alcove and the stair tower): nobody sees in.</summary>
        static bool IsVoid(Vector3 point)
        {
            if (point.z > -0.05f || point.z < -6.2f || point.x < 0f || point.x > 32f) return false;
            bool stairs = point.x > 13f && point.x < 20f, alcove = point.x > 4f && point.x < 7f && point.z > -2f;
            return !stairs && !alcove;
        }

        // ---------- Floors and ceilings ----------

        /// <summary>
        /// Floor finish (top at y = 0). The renderers are split along X into pieces of at most <see cref="MaxPiece"/> m, so
        /// the forward per-object light limit never drops nearby lights. Walking uses one seamless collider for the whole area.
        /// </summary>
        static void FloorArea(Transform p, string name, float x0, float x1, float z0, float z1, Material material, float top = 0f, bool collider = true)
        {
            int n = Mathf.CeilToInt((x1 - x0) / MaxPiece);
            float step = (x1 - x0) / n;
            for (int i = 0; i < n; i++)
            {
                float a = x0 + i * step;
                Box(name, p, new Vector3(a + step / 2f, top - 0.05f, (z0 + z1) / 2f), new Vector3(step, 0.1f, z1 - z0), material, false, worldUV: true);
            }
            if (collider)
                ColliderBox(name + " Collider", p, new Vector3((x0 + x1) / 2f, top - 0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0));
        }

        /// <summary>Suspended ceiling (underside at <paramref name="height"/>), split like floors, with a perimeter wall angle.</summary>
        static void CeilingArea(Transform p, string name, float x0, float x1, float z0, float z1, float height)
        {
            int n = Mathf.CeilToInt((x1 - x0) / MaxPiece);
            float step = (x1 - x0) / n;
            const float angle = 0.022f, angleDrop = 0.024f;   // wall angle flange width and height
            for (int i = 0; i < n; i++)
            {
                float a = x0 + i * step;
                NoShadows(Box(name, p, new Vector3(a + step / 2f, height + 0.01f, (z0 + z1) / 2f), new Vector3(step, 0.02f, z1 - z0), mCeiling, false, worldUV: true));
                // Long sides of the wall angle follow the same split so they get the same lights.
                foreach (float z in new[] { z0 + angle / 2f, z1 - angle / 2f })
                    NoShadows(Box("Wall Angle", p, new Vector3(a + step / 2f, height - angleDrop / 2f, z), new Vector3(step, angleDrop, angle), mPaper, false));
            }
            foreach (float x in new[] { x0 + angle / 2f, x1 - angle / 2f })
                NoShadows(Box("Wall Angle", p, new Vector3(x, height - angleDrop / 2f, (z0 + z1) / 2f), new Vector3(angle, angleDrop, z1 - z0 - 2f * angle), mPaper, false));
        }

        /// <summary>Ceiling-mounted smoke detector or pendent sprinkler head: tiny, no shadows, no colliders.</summary>
        static void CeilingDevice(Transform p, Vector3 ceilingPoint, bool sprinkler)
        {
            float y = ceilingPoint.y;
            if (sprinkler)
            {
                NoShadows(Prim(PrimitiveType.Cylinder, "Sprinkler Escutcheon", p, ceilingPoint + Vector3.down * 0.003f, new Vector3(0.065f, 0.003f, 0.065f), mChrome));
                NoShadows(Rod("Sprinkler Head", p, ceilingPoint, ceilingPoint + Vector3.down * 0.05f, 0.008f, mChrome));
                NoShadows(Prim(PrimitiveType.Cylinder, "Sprinkler Deflector", p, ceilingPoint + Vector3.down * 0.051f, new Vector3(0.04f, 0.0015f, 0.04f), mChrome));
                return;
            }
            NoShadows(Prim(PrimitiveType.Cylinder, "Smoke Detector", p, new Vector3(ceilingPoint.x, y - 0.0175f, ceilingPoint.z), new Vector3(0.11f, 0.0175f, 0.11f), mPaper));
            NoShadows(Prim(PrimitiveType.Cylinder, "Smoke Detector Vent", p, new Vector3(ceilingPoint.x, y - 0.036f, ceilingPoint.z), new Vector3(0.06f, 0.002f, 0.06f), mDark));
        }
    }
}
