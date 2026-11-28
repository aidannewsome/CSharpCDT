# CSharpCDT

C# bindings for [CDT](https://github.com/artem-ogre/CDT), Artem Ogre's constrained Delaunay
triangulation library, as [PythonCDT](https://github.com/artem-ogre/PythonCDT) is for Python.

```sh
dotnet add package CSharpCDT
```

The package includes the native library for macOS, Windows and Linux, so there is nothing to build.

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
```

## API

The same members as PythonCDT, in .NET's names (`insert_edges` is `InsertEdges`), plus
`FlipEdge` and the helpers in `CDT.h`, in `CDTUtils`. Spans go in and arrays come out.

`Vertices` and `Triangles` read CDT's own memory, as PythonCDT's views do, and stay good until
the triangulation next changes. Call `ToArray()` to keep a copy. CDT's errors are thrown as
`TriangulationException`. A Triangulation is not safe to use from several threads at once.

## Building from source

Needs the .NET SDK, CMake and a C++17 compiler. CDT is a git submodule, used unchanged, and the
C interface over it is `src/cdt_c`.

```sh
git clone --recurse-submodules https://github.com/aidannewsome/CSharpCDT
dotnet test CSharpCDT.sln
```

## Versions and licence

The version is the CDT version it wraps, as PythonCDT's is, with the last number raised for a
change to the bindings alone. MPL-2.0, as CDT and PythonCDT are.
