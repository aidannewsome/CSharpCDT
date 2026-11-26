using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CDT;

/// <summary>The functions src/cdt_c exports, as .NET calls them. One class holds every native
/// call, as .NET's design guidelines ask. Each is declared once here and LibraryImport's source
/// generator writes its body when the project builds.</summary>
static unsafe partial class NativeMethods
{
	const string Library = "cdt_c";

	// cdt_c is looked for beside this assembly first, where a host such as Rhino needs it, as it
	// searches only its own folders, then under runtimes/<platform>/native, where a NuGet package
	// puts it, and last where .NET would look anyway. A static constructor runs before any call.
	static NativeMethods() =>
		NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);

	static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (name != Library)
			return IntPtr.Zero;
		var directory = Path.GetDirectoryName(assembly.Location) ?? "";
		foreach (var path in Candidates(directory))
			if (NativeLibrary.TryLoad(path, out var handle))
				return handle;
		return IntPtr.Zero;
	}

	/// <summary>Where cdt_c may be, for the platform this runs on, in the order tried.</summary>
	internal static string[] Candidates(string directory)
	{
		var file =
			OperatingSystem.IsWindows() ? "cdt_c.dll"
			: OperatingSystem.IsMacOS() ? "libcdt_c.dylib"
			: "libcdt_c.so";
		var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
		var platform =
			OperatingSystem.IsWindows() ? $"win-{architecture}"
			: OperatingSystem.IsMacOS() ? "osx"
			: $"linux-{architecture}";
		return
		[
			Path.Combine(directory, file),
			Path.Combine(directory, "runtimes", platform, "native", file),
		];
	}

	// Lifetime and errors

	[LibraryImport(Library, EntryPoint = "cdt_create")]
	internal static partial TriangulationHandle Create(
		int vertexInsertionOrder,
		int intersectingConstraintEdges,
		double minDistToConstraintEdge
	);

	[LibraryImport(Library, EntryPoint = "cdt_destroy")]
	internal static partial void Destroy(IntPtr handle);

	[LibraryImport(Library, EntryPoint = "cdt_last_error")]
	internal static partial IntPtr LastError(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_copy_buffer")]
	internal static partial void CopyBuffer(TriangulationHandle handle, uint* values);

	// Building

	[LibraryImport(Library, EntryPoint = "cdt_insert_vertices")]
	internal static partial int InsertVertices(TriangulationHandle handle, V2d* xy, int count);

	[LibraryImport(Library, EntryPoint = "cdt_insert_edges")]
	internal static partial int InsertEdges(TriangulationHandle handle, Edge* edges, int count);

	[LibraryImport(Library, EntryPoint = "cdt_conform_to_edges")]
	internal static partial int ConformToEdges(TriangulationHandle handle, Edge* edges, int count);

	[LibraryImport(Library, EntryPoint = "cdt_erase_super_triangle")]
	internal static partial int EraseSuperTriangle(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_erase_outer_triangles")]
	internal static partial int EraseOuterTriangles(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_erase_outer_triangles_and_holes")]
	internal static partial int EraseOuterTrianglesAndHoles(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_is_finalized")]
	internal static partial int IsFinalized(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_remove_triangles")]
	internal static partial int RemoveTriangles(
		TriangulationHandle handle,
		uint* triangles,
		int count
	);

	[LibraryImport(Library, EntryPoint = "cdt_finalize_triangulation")]
	internal static partial int FinalizeTriangulation(
		TriangulationHandle handle,
		uint* removedTriangles,
		int count
	);

	// Refining

	[LibraryImport(Library, EntryPoint = "cdt_refine_triangles")]
	internal static partial int RefineTriangles(
		TriangulationHandle handle,
		uint maxVerticesToInsert,
		int refinementCriterion,
		double refinementThreshold,
		uint* toErase,
		int count,
		double minEdgeLength,
		int refinementOrder,
		ulong* unrefined,
		int* toEraseCount
	);

	[LibraryImport(Library, EntryPoint = "cdt_find_encroached_fixed_edges")]
	internal static partial int FindEncroachedFixedEdges(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_find_unrefined_triangles")]
	internal static partial int FindUnrefinedTriangles(
		TriangulationHandle handle,
		int refinementCriterion,
		double refinementThreshold
	);

	[LibraryImport(Library, EntryPoint = "cdt_collect_super_triangle")]
	internal static partial int CollectSuperTriangle(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_collect_outer_triangles")]
	internal static partial int CollectOuterTriangles(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_collect_outer_triangles_and_holes")]
	internal static partial int CollectOuterTrianglesAndHoles(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_calculate_triangle_depths")]
	internal static partial int CalculateTriangleDepths(TriangulationHandle handle, ushort* depths);

	// Reading

	[LibraryImport(Library, EntryPoint = "cdt_vertices")]
	internal static partial V2d* Vertices(TriangulationHandle handle, int* count);

	[LibraryImport(Library, EntryPoint = "cdt_triangles")]
	internal static partial Triangle* Triangles(TriangulationHandle handle, int* count);

	[LibraryImport(Library, EntryPoint = "cdt_fixed_edges")]
	internal static partial int FixedEdges(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_overlap_count")]
	internal static partial int OverlapCount(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_piece_to_originals")]
	internal static partial int PieceToOriginals(TriangulationHandle handle);

	// Checks and geometry

	[LibraryImport(Library, EntryPoint = "cdt_verify_topology")]
	internal static partial int VerifyTopology(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_verify_winding")]
	internal static partial int VerifyWinding(TriangulationHandle handle);

	[LibraryImport(Library, EntryPoint = "cdt_area")]
	internal static partial double Area(V2d* a, V2d* b, V2d* c);

	[LibraryImport(Library, EntryPoint = "cdt_circumcenter")]
	internal static partial void Circumcenter(V2d* a, V2d* b, V2d* c, V2d* result);

	[LibraryImport(Library, EntryPoint = "cdt_smallest_angle")]
	internal static partial double SmallestAngle(V2d* a, V2d* b, V2d* c);

	[LibraryImport(Library, EntryPoint = "cdt_deg_to_rad")]
	internal static partial double DegToRad(double degrees);
}

/// <summary>A CDT triangulation on the native side, destroyed when released.</summary>
sealed class TriangulationHandle : SafeHandle
{
	public TriangulationHandle()
		: base(IntPtr.Zero, true) { }

	public override bool IsInvalid => handle == IntPtr.Zero;

	protected override bool ReleaseHandle()
	{
		NativeMethods.Destroy(handle);
		return true;
	}
}
