using System.Runtime.InteropServices;

namespace CDT;

/// <summary>How vertices are ordered as they are inserted.</summary>
public enum VertexInsertionOrder
{
	/// <summary>Breadth-first through a k-d tree for the first bulk load, randomized after. The
	/// fastest.</summary>
	Auto,

	/// <summary>In the order given.</summary>
	AsProvided,
}

/// <summary>What happens when constraint edges cross.</summary>
public enum IntersectingConstraintEdges
{
	/// <summary>Crossing edges are an error.</summary>
	NotAllowed,

	/// <summary>Each crossing becomes a new vertex that splits both edges.</summary>
	TryResolve,

	/// <summary>Not checked: slightly faster, for input known to have no crossings.</summary>
	DontCheck,
}

/// <summary>What makes a triangle bad enough to refine.</summary>
public enum RefinementCriterion
{
	/// <summary>An angle smaller than the threshold, in radians.</summary>
	SmallestAngle,

	/// <summary>An area larger than the threshold.</summary>
	LargestArea,
}

/// <summary>The order bad triangles are refined in.</summary>
public enum RefinementOrder
{
	/// <summary>In the order found.</summary>
	Fifo,

	/// <summary>Worst first.</summary>
	WorstFirst,
}

/// <summary>A point in the plane. Laid out as CDT's V2d&lt;double&gt;, so CDT's own memory
/// reads as these.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct V2d(double X, double Y);

/// <summary>An edge between two vertices, stored with the lower index first, as CDT's Edge
/// is, and laid out as it is.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Edge
{
	/// <summary>The lower vertex index.</summary>
	public uint V1 { get; }

	/// <summary>The higher vertex index.</summary>
	public uint V2 { get; }

	/// <summary>The edge between two vertices, in either order.</summary>
	public Edge(uint v1, uint v2) => (V1, V2) = v1 < v2 ? (v1, v2) : (v2, v1);
}

/// <summary>A triangle's three vertices, counter-clockwise, and the triangle across from each
/// of its edges, <see cref="NoNeighbor"/> where there is none. Laid out as CDT's Triangle.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Triangle(uint V0, uint V1, uint V2, uint N0, uint N1, uint N2)
{
	/// <summary>Stands for no triangle across an edge.</summary>
	public const uint NoNeighbor = uint.MaxValue;
}

/// <summary>Counts of what <see cref="Triangulation.RefineTriangles"/> could not refine, as
/// CDT's Unrefined, and laid out as cdt_c passes it.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Unrefined(
	ulong ShortEdgeTriangles,
	ulong CircumcenterOutside,
	ulong CircumcenterOnVertex,
	ulong SharpFixedCorner,
	ulong ShortEdges,
	ulong SplitVertexInvalid
);

/// <summary>Which vertices repeat an earlier one: each vertex's index among those kept, and the
/// indices of those that repeat, as CDT's DuplicatesInfo.</summary>
public sealed record DuplicatesInfo(uint[] Mapping, uint[] Duplicates);

/// <summary>An error CDT reported, such as duplicate vertices or crossing edges where they are
/// not allowed.</summary>
public sealed class TriangulationException(string message) : System.Exception(message);
