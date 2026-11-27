# CSharpCDT

C# bindings for [CDT](https://github.com/artem-ogre/CDT), Artem Ogre's constrained Delaunay
triangulation library, as [PythonCDT](https://github.com/artem-ogre/PythonCDT) is for Python.

CDT is used unchanged, as a git submodule. A small C interface, `src/cdt_c`, exposes its
`Triangulation<double>`, and `CSharpCDT` calls it from .NET with the same members PythonCDT has.

```sh
dotnet add package CSharpCDT
```

The package holds cdt_c for macOS (Apple silicon and Intel), Windows and Linux (x64 and Arm),
so there is nothing to build.

```csharp
using CDT;

using var cdt = new Triangulation(
    VertexInsertionOrder.Auto,
    IntersectingConstraintEdges.TryResolve
);
cdt.InsertVertices([new(0, 0), new(1, 0), new(1, 1), new(0, 1)]);
cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)]); // they cross, so CDT splits them
cdt.EraseSuperTriangle();

ReadOnlySpan<V2d> vertices = cdt.Vertices; // the four corners, then the crossing at (0.5, 0.5)
ReadOnlySpan<Triangle> triangles = cdt.Triangles;
var pieces = cdt.PieceToOriginals; // which original edge each split piece came from
```

## Members

Everything PythonCDT binds and the helpers in `CDT.h`, named and shaped as .NET expects:
spans in, arrays out, and no copies where CDT's own memory can be read.

| PythonCDT | C# |
| --- | --- |
| `Triangulation(vertex_insertion_order, intersecting_edges_strategy, min_dist_to_constraint_edge)` | `new Triangulation(...)` |
| `insert_vertices`, `insert_edges`, `conform_to_edges` | `InsertVertices`, `InsertEdges`, `ConformToEdges`, taking spans |
| `erase_super_triangle`, `erase_outer_triangles`, `erase_outer_triangles_and_holes` | `EraseSuperTriangle`, `EraseOuterTriangles`, `EraseOuterTrianglesAndHoles` |
| `is_finalized` | `IsFinalized` |
| `vertices`, `triangles`, `vertices_count`, `triangles_count` | `Vertices`, `Triangles`, `VertexCount`, `TriangleCount` |
| `vertices_array(copy=False)`, `triangles_array(copy=False)` | `Vertices`, `Triangles`: they read CDT's own memory |
| `vertices_array()`, `triangles_array()` | `Vertices.ToArray()`, `Triangles.ToArray()` |
| `fixed_edges`, `overlap_count`, `piece_to_originals` | `FixedEdges`, `OverlapCount`, `PieceToOriginals` |
| `calculate_triangle_depths` | `CalculateTriangleDepths` |
| `remove_triangles`, `finalize_triangulation` | `RemoveTriangles`, `FinalizeTriangulation` |
| `refine_triangles` | `RefineTriangles`, returning `Unrefined` |
| `find_encroached_fixed_edges`, `find_unrefined_triangles` | `FindEncroachedFixedEdges`, `FindUnrefinedTriangles` |
| `collect_super_triangle`, `collect_outer_triangles`, `collect_outer_triangles_and_holes` | `CollectSuperTriangle`, `CollectOuterTriangles`, `CollectOuterTrianglesAndHoles`, as arrays |
| `verify_topology`, `verify_winding` | `CDTUtils.VerifyTopology`, `CDTUtils.VerifyWinding` |
| `area`, `circumcenter`, `smallest_angle` | `CDTUtils.Area`, `CDTUtils.Circumcenter`, `CDTUtils.SmallestAngle` (for `deg_to_rad`, .NET has `double.DegreesToRadians`) |
| C++ `FindDuplicates`, `RemoveDuplicates`, `RemoveDuplicatesAndRemapEdges`, `RemapEdges` | `CDTUtils.FindDuplicates`, `CDTUtils.RemoveDuplicates`, `CDTUtils.RemoveDuplicatesAndRemapEdges`, `CDTUtils.RemapEdges` |
| C++ `extractEdgesFromTriangles`, `calculateTrianglesByVertex` | `CDTUtils.ExtractEdgesFromTriangles`, `CDTUtils.CalculateTrianglesByVertex` |
| C++ `EdgeToPiecesMapping`, `EdgeToSplitVertices` | `EdgeToPieces`, `EdgeToSplitVertices` on the triangulation |
| `VertexInsertionOrder`, `IntersectingConstraintEdges`, `RefinementCriterion`, `RefinementOrder` | the same enums |
| `V2d`, `Edge`, `Triangle`, `Unrefined`, `DuplicatesInfo` | the same types |

`Vertices` and `Triangles` are spans over CDT's memory, as PythonCDT's views are: no copy, and
good until the triangulation next changes or is disposed. `V2d`, `Edge` and `Triangle` are laid
out as CDT's own types, so the memory reads as them directly.

Vertices must not repeat, as CDT requires. CDT's errors, such as duplicate vertices or crossing
edges where they are not allowed, are thrown as `TriangulationException`. A Triangulation is not
safe to use from several threads at once.

## Building from source

Needs the .NET SDK, CMake and a C++17 compiler. Building the C# project builds `cdt_c` with CMake
first and copies it beside the assembly: `libcdt_c.dylib` on macOS (Apple silicon and Intel in
one), `cdt_c.dll` on Windows, `libcdt_c.so` on Linux.

```sh
git clone --recurse-submodules <this repository>
dotnet build CSharpCDT.sln
dotnet test CSharpCDT.sln
```

The assembly finds `cdt_c` beside itself, so it also loads inside hosts that search their own
folders for native libraries, such as Rhino.

## Releasing

Publishing a GitHub release tagged with the version, such as `2.1.0`, runs
`.github/workflows/release.yml`: it builds cdt_c on every platform, packs one package with all of
them, installs and runs it on every platform, and publishes it to nuget.org with trusted
publishing. Run by hand from the Actions tab, it does all of that but publish.

## Versions

As PythonCDT's, the version is the CDT version it wraps, with the last number raised for a
change to the bindings alone. 2.1.0 wraps CDT 2.1.

## Licence

MPL-2.0, as CDT and PythonCDT are.
