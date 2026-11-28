using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CDT;

/// <summary>A constrained Delaunay triangulation: CDT's Triangulation&lt;double&gt;, with the
/// members PythonCDT gives it, in .NET's way. Insert vertices, then edges, then erase what is not wanted,
/// and read the result.</summary>
/// <remarks><see cref="Vertices"/> and <see cref="Triangles"/> read CDT's own memory, as
/// PythonCDT's do: they are good until the triangulation next changes or is disposed. Call
/// ToArray on them to keep a copy.</remarks>
public sealed class Triangulation : IDisposable
{
	readonly TriangulationHandle _handle;

	/// <summary>An empty triangulation. minDistToConstraintEdge is how close a vertex must be to
	/// a constraint edge to count as on it.</summary>
	public Triangulation(
		VertexInsertionOrder vertexInsertionOrder = VertexInsertionOrder.Auto,
		IntersectingConstraintEdges intersectingConstraintEdges =
			IntersectingConstraintEdges.NotAllowed,
		double minDistToConstraintEdge = 0
	)
	{
		_handle = NativeMethods.Create(
			(int)vertexInsertionOrder,
			(int)intersectingConstraintEdges,
			minDistToConstraintEdge
		);
		if (_handle.IsInvalid)
			throw new TriangulationException("CDT could not create a triangulation.");
	}

	/// <summary>Frees CDT's triangulation. Vertices and Triangles read before this are no longer
	/// good.</summary>
	public void Dispose() => _handle.Dispose();

	// Building

	/// <summary>Inserts vertices. They must not repeat one another or any already inserted.</summary>
	public void InsertVertices(ReadOnlySpan<V2d> vertices) =>
		Check(NativeMethods.InsertVertices(Handle, vertices, vertices.Length));

	/// <summary>Inserts constraint edges, which become edges of the triangulation.</summary>
	public void InsertEdges(ReadOnlySpan<Edge> edges) =>
		Check(NativeMethods.InsertEdges(Handle, edges, edges.Length));

	/// <summary>Inserts edges as a conforming triangulation does: split with new vertices until
	/// every piece is Delaunay.</summary>
	public void ConformToEdges(ReadOnlySpan<Edge> edges) =>
		Check(NativeMethods.ConformToEdges(Handle, edges, edges.Length));

	/// <summary>Finishes with the convex hull of the vertices.</summary>
	public void EraseSuperTriangle() => Check(NativeMethods.EraseSuperTriangle(Handle));

	/// <summary>Finishes with only what the constraint edges enclose.</summary>
	public void EraseOuterTriangles() => Check(NativeMethods.EraseOuterTriangles(Handle));

	/// <summary>Finishes with only what the constraint edges enclose, less the holes inside, by
	/// even and odd depth.</summary>
	public void EraseOuterTrianglesAndHoles() =>
		Check(NativeMethods.EraseOuterTrianglesAndHoles(Handle));

	/// <summary>Whether an erase or FinalizeTriangulation has finished the triangulation.</summary>
	public bool IsFinalized => NativeMethods.IsFinalized(Handle) != 0;

	/// <summary>Removes triangles, without finalizing.</summary>
	public void RemoveTriangles(ReadOnlySpan<uint> triangles) =>
		Check(NativeMethods.RemoveTriangles(Handle, triangles, triangles.Length));

	/// <summary>Removes the super-triangle's vertices and the given triangles, and finishes.</summary>
	public void FinalizeTriangulation(ReadOnlySpan<uint> removedTriangles) =>
		Check(
			NativeMethods.FinalizeTriangulation(Handle, removedTriangles, removedTriangles.Length)
		);

	/// <summary>Flips the edge between two neighbouring triangles. For changing the triangulation
	/// by hand: CDT does not check that it stays Delaunay.</summary>
	public void FlipEdge(uint triangle, uint opposite) =>
		Check(NativeMethods.FlipEdge(Handle, triangle, opposite));

	/// <summary>Flips the edge between two neighbouring triangles, given their four vertices and
	/// four outer neighbours as CDT's second flipEdge takes them, so CDT need not look them up.</summary>
	public void FlipEdge(
		uint triangle,
		uint opposite,
		uint v1,
		uint v2,
		uint v3,
		uint v4,
		uint n1,
		uint n2,
		uint n3,
		uint n4
	) => Check(NativeMethods.FlipEdge(Handle, triangle, opposite, v1, v2, v3, v4, n1, n2, n3, n4));

	// Refining

	/// <summary>Adds vertices until no triangle is bad by the criterion, or until
	/// maxVerticesToInsert are added. Triangles in toErase are left as they are, and the
	/// triangles that replace them are added to it, so the caller picks the kind of set. Returns
	/// what could not be refined.</summary>
	public Unrefined RefineTriangles(
		uint maxVerticesToInsert,
		RefinementCriterion refinementCriterion = RefinementCriterion.SmallestAngle,
		double refinementThreshold = Math.PI / 9,
		ISet<uint> toErase = null,
		double minEdgeLength = 1e-6,
		RefinementOrder refinementOrder = RefinementOrder.Fifo
	)
	{
		var erase = toErase is null ? [] : new uint[toErase.Count];
		toErase?.CopyTo(erase, 0);
		Check(
			NativeMethods.RefineTriangles(
				Handle,
				maxVerticesToInsert,
				(int)refinementCriterion,
				refinementThreshold,
				erase,
				toErase is null ? -1 : erase.Length,
				minEdgeLength,
				(int)refinementOrder,
				out var unrefined,
				out int kept
			)
		);
		if (toErase is not null)
		{
			toErase.Clear();
			toErase.UnionWith(Buffer(kept));
		}
		return unrefined;
	}

	/// <summary>Fixed edges another vertex encroaches on, which refining would split.</summary>
	public Edge[] FindEncroachedFixedEdges() =>
		Edges(Counted(NativeMethods.FindEncroachedFixedEdges(Handle)));

	/// <summary>Triangles still bad by the criterion.</summary>
	public uint[] FindUnrefinedTriangles(
		RefinementCriterion refinementCriterion = RefinementCriterion.SmallestAngle,
		double refinementThreshold = Math.PI / 9
	) =>
		Buffer(
			Counted(
				NativeMethods.FindUnrefinedTriangles(
					Handle,
					(int)refinementCriterion,
					refinementThreshold
				)
			)
		);

	/// <summary>The triangles that touch the super-triangle's vertices.</summary>
	public uint[] CollectSuperTriangle() =>
		Buffer(Counted(NativeMethods.CollectSuperTriangle(Handle)));

	/// <summary>The triangles outside the constraint edges.</summary>
	public uint[] CollectOuterTriangles() =>
		Buffer(Counted(NativeMethods.CollectOuterTriangles(Handle)));

	/// <summary>The triangles outside the constraint edges and in the holes they enclose.</summary>
	public uint[] CollectOuterTrianglesAndHoles() =>
		Buffer(Counted(NativeMethods.CollectOuterTrianglesAndHoles(Handle)));

	/// <summary>Each triangle's depth: how many constraint boundaries lie between it and the
	/// outside. Calculated from the super-triangle, so before any erase.</summary>
	public ushort[] CalculateTriangleDepths()
	{
		var depths = new ushort[TriangleCount];
		Check(NativeMethods.CalculateTriangleDepths(Handle, depths));
		return depths;
	}

	// Reading

	/// <summary>The vertices: those inserted, in order, then any CDT added, such as where
	/// constraint edges cross. Read after an erase, so the super-triangle's are gone.</summary>
	public unsafe ReadOnlySpan<V2d> Vertices
	{
		get
		{
			var p = NativeMethods.Vertices(Handle, out int count);
			return new ReadOnlySpan<V2d>(p, count);
		}
	}

	/// <summary>How many vertices there are.</summary>
	public int VertexCount => Vertices.Length;

	/// <summary>The triangles, counter-clockwise, each with its neighbours.</summary>
	public unsafe ReadOnlySpan<Triangle> Triangles
	{
		get
		{
			var p = NativeMethods.Triangles(Handle, out int count);
			return new ReadOnlySpan<Triangle>(p, count);
		}
	}

	/// <summary>How many triangles there are.</summary>
	public int TriangleCount => Triangles.Length;

	/// <summary>The constraint edges in the triangulation, split where they were split.</summary>
	public Edge[] FixedEdges => Edges(NativeMethods.FixedEdges(Handle));

	/// <summary>Fixed edges that more than one constraint edge overlaps, and by how many more.</summary>
	public Dictionary<Edge, ushort> OverlapCount
	{
		get
		{
			var values = Buffer(NativeMethods.OverlapCount(Handle));
			var map = new Dictionary<Edge, ushort>(values.Length / 3);
			for (int i = 0; i < values.Length; i += 3)
				map[new Edge(values[i], values[i + 1])] = (ushort)values[i + 2];
			return map;
		}
	}

	/// <summary>Each piece of a split or overlapping constraint edge, and the original edges it
	/// came from.</summary>
	public Dictionary<Edge, List<Edge>> PieceToOriginals
	{
		get
		{
			var values = Buffer(NativeMethods.PieceToOriginals(Handle));
			var map = new Dictionary<Edge, List<Edge>>();
			for (int i = 0; i < values.Length; i += 4)
			{
				var piece = new Edge(values[i], values[i + 1]);
				if (!map.TryGetValue(piece, out var originals))
					map[piece] = originals = [];
				originals.Add(new Edge(values[i + 2], values[i + 3]));
			}
			return map;
		}
	}

	/// <summary>Each constraint edge that was split or overlapped, and the pieces it became, as
	/// CDT's EdgeToPiecesMapping gives them.</summary>
	public Dictionary<Edge, Edge[]> EdgeToPieces()
	{
		var values = Buffer(NativeMethods.EdgeToPieces(Handle));
		var map = new Dictionary<Edge, Edge[]>();
		for (int i = 0; i < values.Length; )
		{
			var edge = new Edge(values[i], values[i + 1]);
			var pieces = new Edge[values[i + 2]];
			i += 3;
			for (int j = 0; j < pieces.Length; j++, i += 2)
				pieces[j] = new Edge(values[i], values[i + 1]);
			map[edge] = pieces;
		}
		return map;
	}

	/// <summary>Each constraint edge that was split, and the vertices along it from its V1 to its
	/// V2, as CDT's EdgeToSplitVertices gives them: what ties data on an edge given, such as
	/// heights, to the triangulation.</summary>
	public Dictionary<Edge, uint[]> EdgeToSplitVertices()
	{
		var values = Buffer(NativeMethods.EdgeToSplitVertices(Handle));
		var map = new Dictionary<Edge, uint[]>();
		for (int i = 0; i < values.Length; )
		{
			var edge = new Edge(values[i], values[i + 1]);
			int count = (int)values[i + 2];
			map[edge] = values.AsSpan(i + 3, count).ToArray();
			i += 3 + count;
		}
		return map;
	}

	internal TriangulationHandle Handle =>
		_handle.IsClosed ? throw new ObjectDisposedException(nameof(Triangulation)) : _handle;

	void Check(int status)
	{
		if (status != 0)
			throw new TriangulationException(
				Marshal.PtrToStringUTF8(NativeMethods.LastError(_handle)) ?? "CDT failed."
			);
	}

	int Counted(int count)
	{
		if (count < 0)
			Check(1);
		return count;
	}

	uint[] Buffer(int count)
	{
		var values = new uint[count];
		NativeMethods.CopyBuffer(Handle, values);
		return values;
	}

	/// <summary>The buffer's vertex pairs, copied straight into edges: CDT keeps each edge's lower
	/// index first, as Edge does.</summary>
	Edge[] Edges(int count)
	{
		var edges = new Edge[count / 2];
		NativeMethods.CopyBuffer(Handle, MemoryMarshal.Cast<Edge, uint>(edges.AsSpan()));
		return edges;
	}
}
