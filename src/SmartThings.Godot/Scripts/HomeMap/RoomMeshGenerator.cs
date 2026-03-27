// =============================================================================
// RoomMeshGenerator.cs — Generates 3D meshes from SmartRoom polygon data
// Procedural floor + wall generation with shader material assignment
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Generates 3D room meshes procedurally from SmartRoom polygon data.
/// Creates floor (triangulated polygon) and walls (extruded quads) with
/// appropriate shader materials for the Home Map View.
/// </summary>
public static class RoomMeshGenerator
{
    private static readonly GodotNative.Shader? _floorShader;
    private static readonly GodotNative.Shader? _wallShader;

    static RoomMeshGenerator()
    {
        _floorShader = GodotNative.GD.Load<GodotNative.Shader>("res://Shaders/room_floor.gdshader");
        _wallShader = GodotNative.GD.Load<GodotNative.Shader>("res://Shaders/room_wall.gdshader");
    }

    /// <summary>Generates the complete room node with floor and walls.</summary>
    public static GodotNative.Node3D GenerateRoom(SmartRoom room)
    {
        var roomNode = new GodotNative.Node3D();
        roomNode.Name = $"Room_{room.RoomId}";
        roomNode.Position = new GodotNative.Vector3(0, room.FloorY, 0);

        var polygon = room.FloorPolygon;
        if (polygon == null || polygon.Count < 3)
        {
            GodotNative.GD.PushWarning($"Room '{room.Name}' has insufficient polygon points.");
            return roomNode;
        }

        // Generate floor mesh
        var floorMesh = GenerateFloorMesh(polygon);
        if (floorMesh != null)
        {
            var floorInstance = new GodotNative.MeshInstance3D();
            floorInstance.Name = "Floor";
            floorInstance.Mesh = floorMesh;
            floorInstance.MaterialOverride = CreateFloorMaterial(room);
            roomNode.AddChild(floorInstance);

            // Add collision for tap detection
            var floorBody = new GodotNative.StaticBody3D();
            floorBody.Name = "FloorBody";
            floorBody.SetMeta("room_id", room.RoomId);
            floorInstance.AddChild(floorBody);

            var collisionShape = new GodotNative.CollisionShape3D();
            collisionShape.Shape = CreateFloorCollisionShape(polygon);
            floorBody.AddChild(collisionShape);
        }

        // Generate walls
        var walls = room.WallSegments;
        if (walls != null)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                var wallNode = GenerateWallMesh(walls[i], room);
                if (wallNode != null)
                {
                    wallNode.Name = $"Wall_{i}";
                    roomNode.AddChild(wallNode);
                }
            }
        }
        else
        {
            // Auto-generate walls from polygon edges
            for (int i = 0; i < polygon.Count; i++)
            {
                var start = polygon[i];
                var end = polygon[(i + 1) % polygon.Count];
                var segment = new WallSegment(start, end, room.WallHeight);
                var wallNode = GenerateWallMesh(segment, room);
                if (wallNode != null)
                {
                    wallNode.Name = $"Wall_{i}";
                    roomNode.AddChild(wallNode);
                }
            }
        }

        return roomNode;
    }

    /// <summary>Generates a triangulated floor mesh from a polygon.</summary>
    public static GodotNative.ArrayMesh? GenerateFloorMesh(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3) return null;

        var triangles = EarClipTriangulate(polygon);
        if (triangles.Count == 0) return null;

        var vertices = new GodotNative.Vector3[triangles.Count];
        var normals = new GodotNative.Vector3[triangles.Count];
        var uvs = new GodotNative.Vector2[triangles.Count];
        var colors = new GodotNative.Color[triangles.Count];

        // Calculate UV bounds for proper mapping
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in polygon)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minZ = Math.Min(minZ, p.Y);
            maxZ = Math.Max(maxZ, p.Y);
        }
        float rangeX = maxX - minX;
        float rangeZ = maxZ - minZ;
        if (rangeX < 0.001f) rangeX = 1f;
        if (rangeZ < 0.001f) rangeZ = 1f;

        for (int i = 0; i < triangles.Count; i++)
        {
            var p = triangles[i];
            vertices[i] = new GodotNative.Vector3(p.X, 0, p.Y);
            normals[i] = GodotNative.Vector3.Up;
            uvs[i] = new GodotNative.Vector2(
                (p.X - minX) / rangeX,
                (p.Y - minZ) / rangeZ);
            colors[i] = new GodotNative.Color(1, 1, 1, 1); // Full AO (no occlusion on floor center)
        }

        var mesh = new GodotNative.ArrayMesh();
        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)GodotNative.Mesh.ArrayType.Max);
        arrays[(int)GodotNative.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)GodotNative.Mesh.ArrayType.Normal] = normals;
        arrays[(int)GodotNative.Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)GodotNative.Mesh.ArrayType.Color] = colors;

        mesh.AddSurfaceFromArrays(GodotNative.Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Generates a wall quad with optional door cutout.</summary>
    public static GodotNative.MeshInstance3D? GenerateWallMesh(WallSegment segment, SmartRoom room)
    {
        var start3 = new GodotNative.Vector3(segment.Start.X, room.FloorY, segment.Start.Y);
        var end3 = new GodotNative.Vector3(segment.End.X, room.FloorY, segment.End.Y);
        var wallDir = end3 - start3;
        float wallLength = wallDir.Length();
        if (wallLength < 0.01f) return null;

        var instance = new GodotNative.MeshInstance3D();
        GodotNative.ArrayMesh mesh;

        if (segment.HasDoor)
        {
            mesh = CreateWallWithDoor(wallLength, segment.Height, segment.DoorWidth, segment.DoorHeight);
        }
        else
        {
            mesh = CreateSolidWall(wallLength, segment.Height);
        }

        instance.Mesh = mesh;
        instance.MaterialOverride = CreateWallMaterial(room);

        // Position and rotate to match wall segment
        var midpoint = (start3 + end3) * 0.5f;
        instance.Position = new GodotNative.Vector3(midpoint.X, room.FloorY, midpoint.Z);
        float angle = GodotNative.Mathf.Atan2(wallDir.X, wallDir.Z);
        instance.RotationDegrees = new GodotNative.Vector3(0, GodotNative.Mathf.RadToDeg(angle), 0);

        // Add collision
        var body = new GodotNative.StaticBody3D();
        var colShape = new GodotNative.CollisionShape3D();
        var box = new GodotNative.BoxShape3D();
        box.Size = new GodotNative.Vector3(0.3f, segment.Height, wallLength);
        colShape.Shape = box;
        colShape.Position = new GodotNative.Vector3(0, segment.Height * 0.5f, 0);
        body.AddChild(colShape);
        instance.AddChild(body);

        return instance;
    }

    // ── Mesh creation helpers ─────────────────────────────────────────────────

    private static GodotNative.ArrayMesh CreateSolidWall(float length, float height)
    {
        float halfLen = length * 0.5f;
        const float thickness = 0.15f;

        // Front and back faces of a thin wall
        var verts = new GodotNative.Vector3[]
        {
            // Front face
            new(-thickness, 0, -halfLen), new(-thickness, height, -halfLen),
            new(-thickness, height, halfLen), new(-thickness, 0, halfLen),
            // Back face
            new(thickness, 0, halfLen), new(thickness, height, halfLen),
            new(thickness, height, -halfLen), new(thickness, 0, -halfLen),
        };

        var tris = new int[] { 0,1,2, 0,2,3, 4,5,6, 4,6,7 };
        return BuildWallMesh(verts, tris, height);
    }

    private static GodotNative.ArrayMesh CreateWallWithDoor(float length, float height, float doorWidth, float doorHeight)
    {
        float halfLen = length * 0.5f;
        const float thickness = 0.15f;
        float halfDoor = doorWidth * 0.5f;

        // Three sections: left of door, above door, right of door
        var vertsList = new List<GodotNative.Vector3>();
        var trisList = new List<int>();

        // Left section
        AddWallQuad(vertsList, trisList, thickness, 0, height, -halfLen, -halfDoor);
        // Right section
        AddWallQuad(vertsList, trisList, thickness, 0, height, halfDoor, halfLen);
        // Above door
        AddWallQuad(vertsList, trisList, thickness, doorHeight, height, -halfDoor, halfDoor);

        return BuildWallMesh(vertsList.ToArray(), trisList.ToArray(), height);
    }

    private static void AddWallQuad(List<GodotNative.Vector3> verts, List<int> tris,
        float thickness, float bottom, float top, float startZ, float endZ)
    {
        int baseIdx = verts.Count;
        // Front face
        verts.Add(new GodotNative.Vector3(-thickness, bottom, startZ));
        verts.Add(new GodotNative.Vector3(-thickness, top, startZ));
        verts.Add(new GodotNative.Vector3(-thickness, top, endZ));
        verts.Add(new GodotNative.Vector3(-thickness, bottom, endZ));
        tris.AddRange(new[] { baseIdx, baseIdx + 1, baseIdx + 2, baseIdx, baseIdx + 2, baseIdx + 3 });

        baseIdx = verts.Count;
        // Back face
        verts.Add(new GodotNative.Vector3(thickness, bottom, endZ));
        verts.Add(new GodotNative.Vector3(thickness, top, endZ));
        verts.Add(new GodotNative.Vector3(thickness, top, startZ));
        verts.Add(new GodotNative.Vector3(thickness, bottom, startZ));
        tris.AddRange(new[] { baseIdx, baseIdx + 1, baseIdx + 2, baseIdx, baseIdx + 2, baseIdx + 3 });
    }

    private static GodotNative.ArrayMesh BuildWallMesh(GodotNative.Vector3[] verts, int[] tris, float height)
    {
        var vertices = new GodotNative.Vector3[tris.Length];
        var normals = new GodotNative.Vector3[tris.Length];
        var uvs = new GodotNative.Vector2[tris.Length];
        var colors = new GodotNative.Color[tris.Length];

        for (int i = 0; i < tris.Length; i += 3)
        {
            var v0 = verts[tris[i]];
            var v1 = verts[tris[i + 1]];
            var v2 = verts[tris[i + 2]];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = edge1.Cross(edge2).Normalized();

            for (int j = 0; j < 3; j++)
            {
                var v = verts[tris[i + j]];
                vertices[i + j] = v;
                normals[i + j] = normal;
                uvs[i + j] = new GodotNative.Vector2(v.Z, v.Y / height);
                // AO: darken at floor level (y=0) for grounding effect
                float ao = GodotNative.Mathf.Clamp(v.Y / (height * 0.3f), 0.5f, 1.0f);
                colors[i + j] = new GodotNative.Color(ao, ao, ao, 1);
            }
        }

        var mesh = new GodotNative.ArrayMesh();
        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)GodotNative.Mesh.ArrayType.Max);
        arrays[(int)GodotNative.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)GodotNative.Mesh.ArrayType.Normal] = normals;
        arrays[(int)GodotNative.Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)GodotNative.Mesh.ArrayType.Color] = colors;

        mesh.AddSurfaceFromArrays(GodotNative.Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // ── Material creation ─────────────────────────────────────────────────────

    public static GodotNative.ShaderMaterial CreateFloorMaterial(SmartRoom room)
    {
        var mat = new GodotNative.ShaderMaterial();
        if (_floorShader != null) mat.Shader = _floorShader;

        var color = RoomColorPalette.GetColor(room.RoomType);
        mat.SetShaderParameter("room_color", new GodotNative.Color(color.R, color.G, color.B, color.A));
        mat.SetShaderParameter("is_selected", false);
        mat.SetShaderParameter("grid_opacity", 0.08f);
        mat.SetShaderParameter("edge_glow_width", 0.05f);

        return mat;
    }

    public static GodotNative.ShaderMaterial CreateWallMaterial(SmartRoom room)
    {
        var mat = new GodotNative.ShaderMaterial();
        if (_wallShader != null) mat.Shader = _wallShader;

        var color = RoomColorPalette.GetWallColor(room.RoomType);
        // Walls are always white with full opacity (SmartThings style)
        mat.SetShaderParameter("wall_color", new GodotNative.Color(0.95f, 0.95f, 0.97f, 1.0f));
        mat.SetShaderParameter("wall_opacity", 1.0f);

        return mat;
    }

    // ── Collision helper ──────────────────────────────────────────────────────

    private static GodotNative.ConvexPolygonShape3D CreateFloorCollisionShape(IReadOnlyList<Vector2> polygon)
    {
        var shape = new GodotNative.ConvexPolygonShape3D();
        var points = new GodotNative.Vector3[polygon.Count * 2];
        for (int i = 0; i < polygon.Count; i++)
        {
            points[i] = new GodotNative.Vector3(polygon[i].X, 0.01f, polygon[i].Y);
            points[i + polygon.Count] = new GodotNative.Vector3(polygon[i].X, -0.01f, polygon[i].Y);
        }
        shape.Points = points;
        return shape;
    }

    // ── Ear-clipping triangulation ────────────────────────────────────────────

    /// <summary>Simple ear-clipping triangulation for convex/simple polygons.</summary>
    public static List<Vector2> EarClipTriangulate(IReadOnlyList<Vector2> polygon)
    {
        var result = new List<Vector2>();
        if (polygon.Count < 3) return result;

        var indices = new List<int>();
        for (int i = 0; i < polygon.Count; i++) indices.Add(i);

        // Ensure counter-clockwise winding
        if (CalculateSignedArea(polygon) > 0)
            indices.Reverse();

        int safety = polygon.Count * 3;
        while (indices.Count > 2 && safety-- > 0)
        {
            bool earFound = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];

                var a = polygon[prev];
                var b = polygon[curr];
                var c = polygon[next];

                // Check if this is a convex vertex (ear tip)
                if (Cross2D(b - a, c - a) <= 0) continue;

                // Check no other vertex is inside this triangle
                bool containsOther = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    if (j == (i - 1 + indices.Count) % indices.Count || j == i || j == (i + 1) % indices.Count)
                        continue;
                    if (PointInTriangle(polygon[indices[j]], a, b, c))
                    {
                        containsOther = true;
                        break;
                    }
                }

                if (!containsOther)
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound) break;
        }

        return result;
    }

    private static float CalculateSignedArea(IReadOnlyList<Vector2> polygon)
    {
        float area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += (b.X - a.X) * (b.Y + a.Y);
        }
        return area * 0.5f;
    }

    private static float Cross2D(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross2D(b - a, p - a);
        float d2 = Cross2D(c - b, p - b);
        float d3 = Cross2D(a - c, p - c);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
}
