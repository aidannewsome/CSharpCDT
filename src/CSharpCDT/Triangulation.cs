using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CDT;

/// <summary>A constrained Delaunay triangulation: CDT's Triangulation&lt;double&gt;, with the
/// members PythonCDT gives it. Insert vertices, then edges, then erase what is not wanted,
/// and read the result.</summary>
/// <remarks><see cref="Vertices"/> and <see cref="Triangles"/> read CDT's own memory, as
/// PythonCDT's do: they are good until the triangulation next changes or is disposed. Call
/// ToArray on them to keep a copy.</remarks>
public sealed unsafe class Triangulation : IDisposable
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
	public void InsertVertices(ReadOnlySpan<V2d> vertices)
	{
		fixed (V2d* p = vertices)
			Check(NativeMethods.InsertVertices(Handle, p, vertices.Length));
	}

	/// <summary>Inserts constraint edges, which become edges of the triangulation.</summary>
	public void InsertEdges(ReadOnlySpan<Edge> edges)
	{
		fixed (Edge* p = edges)
			Check(NativeMethods.InsertEdges(Handle, p, edges.Length));
	}

	/// <summary>Inserts edges as a conforming triangulation does: split with new vertices until
	/// every piece is Delaunay.</summary>
	public void ConformToEdges(ReadOnlySpan<Edge> edges)
	{
		fixed (Edge* p = edges)
			Check(NativeMethods.ConformToEdges(Handle, p, edges.Length));
	}

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
	public void RemoveTriangles(ReadOnlySpan<uint> triangles)
	{
		fixed (uint* p = triangles)
			Check(NativeMethods.RemoveTriangles(Handle, p, triangles.Length));
	}

	/// <summary>Removes the super-triangle's vertices and the given triangles, and finishes.</summary>
	public void FinalizeTriangulation(ReadOnlySpan<uint> removedTriangles)
	{
		fixed (uint* p = removedTriangles)
			Check(NativeMethods.FinalizeTriangulation(Handle, p, removedTriangles.Length));
	}

	// Refining

	/// <summary>Adds vertices until no triangle is bad by the criterion, or until
	/// maxVerticesToInsert are added. Triangles in toErase are left as they are, and the
	/// triangles that replace them are added to it. Returns what could not be refined.</summary>
	public Unrefined RefineTriangles(
		uint maxVerticesToInsert,
		RefinementCriterion refinementCriterion = RefinementCriterion.SmallestAngle,
		double refinementThreshold = Math.PI / 9,
		HashSet<uint> toErase = null,
		double minEdgeLength = 1e-6,
		RefinementOrder refinementOrder = RefinementOrder.Fifo
	)
	{
		var erase = toErase is null ? [] : new uint[toErase.Count];
		toErase?.CopyTo(erase);
		var counts = stackalloc ulong[6];
		int kept;
		fixed (uint* p = erase)
			Check(
				NativeMethods.RefineTriangles(
					Handle,
					maxVerticesToInsert,
					(int)refinementCriterion,
					refinementThreshold,
					p,
					toErase is null ? -1 : erase.Length,
					minEdgeLength,
					(int)refinementOrder,
					counts,
					&kept
				)
			);
		if (toErase is not null)
		{
			toErase.Clear();
			toErase.UnionWith(Buffer(kept));
		}
		return new Unrefined(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5]);
	}

	/// <summary>Fixed edges another vertex encroaches on, which refining would split.</summary>
	public Edge[] FindEncroachedFixedEdges() =>
		Edges(Buffer(Counted(NativeMethods.FindEncroachedFixedEdges(Handle))));

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
	public HashSet<uint> CollectSuperTriangle() =>
		[.. Buffer(Counted(NativeMethods.CollectSuperTriangle(Handle)))];

	/// <summary>The triangles outside the constraint edges.</summary>
	public HashSet<uint> CollectOuterTriangles() =>
		[.. Buffer(Counted(NativeMethods.CollectOuterTriangles(Handle)))];

	/// <summary>The triangles outside the constraint edges and in the holes they enclose.</summary>
	public HashSet<uint> CollectOuterTrianglesAndHoles() =>
		[.. Buffer(Counted(NativeMethods.CollectOuterTrianglesAndHoles(Handle)))];

	/// <summary>Each triangle's depth: how many constraint boundaries lie between it and the
	/// outside. Calculated from the super-triangle, so before any erase.</summary>
	public ushort[] CalculateTriangleDepths()
	{
		var depths = new ushort[TriangleCount];
		fixed (ushort* p = depths)
			Check(NativeMethods.CalculateTriangleDepths(Handle, p));
		return depths;
	}

	// Reading

	/// <summary>The vertices: those inserted, in order, then any CDT added, such as where
	/// constraint edges cross. Read after an erase, so the super-triangle's are gone.</summary>
	public ReadOnlySpan<V2d> Vertices
	{
		get
		{
			int count;
			var p = NativeMethods.Vertices(Handle, &count);
			return new ReadOnlySpan<V2d>(p, count);
		}
	}

	/// <summary>How many vertices there are.</summary>
	public int VertexCount => Vertices.Length;

	/// <summary>The triangles, counter-clockwise, each with its neighbours.</summary>
	public ReadOnlySpan<Triangle> Triangles
	{
		get
		{
			int count;
			var p = NativeMethods.Triangles(Handle, &count);
			return new ReadOnlySpan<Triangle>(p, count);
		}
	}

	/// <summary>How many triangles there are.</summary>
	public int TriangleCount => Triangles.Length;

	/// <summary>The constraint edges in the triangulation, split where they were split.</summary>
	public Edge[] FixedEdges => Edges(Buffer(NativeMethods.FixedEdges(Handle)));

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
		fixed (uint* p = values)
			NativeMethods.CopyBuffer(Handle, p);
		return values;
	}

	static Edge[] Edges(uint[] pairs)
	{
		var edges = new Edge[pairs.Length / 2];
		for (int i = 0; i < edges.Length; i++)
			edges[i] = new Edge(pairs[2 * i], pairs[2 * i + 1]);
		return edges;
	}
}
