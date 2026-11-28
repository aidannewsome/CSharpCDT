using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CDT;

/// <summary>CDT's free functions: checks on a triangulation, plane geometry as CDT computes it,
/// and the helpers in CDT.h for duplicates and for mapping edges and triangles.</summary>
public static class CDTUtils
{
	/// <summary>Whether every triangle's neighbours point back at it.</summary>
	public static bool VerifyTopology(Triangulation triangulation) =>
		NativeMethods.VerifyTopology(triangulation.Handle) != 0;

	/// <summary>Whether every triangle winds counter-clockwise.</summary>
	public static bool VerifyWinding(Triangulation triangulation) =>
		NativeMethods.VerifyWinding(triangulation.Handle) != 0;

	/// <summary>The triangle's area, whichever way its corners run.</summary>
	public static double Area(V2d a, V2d b, V2d c) => NativeMethods.Area(a, b, c);

	/// <summary>The centre of the circle through the triangle's three corners.</summary>
	public static V2d Circumcenter(V2d a, V2d b, V2d c)
	{
		NativeMethods.Circumcenter(a, b, c, out var result);
		return result;
	}

	/// <summary>The smallest of a triangle's angles, in radians.</summary>
	public static double SmallestAngle(V2d a, V2d b, V2d c) => NativeMethods.SmallestAngle(a, b, c);

	/// <summary>Finds vertices that repeat an earlier one exactly, as CDT's FindDuplicates does,
	/// without changing them.</summary>
	public static DuplicatesInfo FindDuplicates(ReadOnlySpan<V2d> vertices)
	{
		var mapping = new uint[vertices.Length];
		var duplicates = new uint[vertices.Length];
		var error = new byte[ErrorLength];
		Check(
			NativeMethods.FindDuplicates(
				vertices,
				vertices.Length,
				mapping,
				duplicates,
				out int count,
				error,
				error.Length
			),
			error
		);
		return new DuplicatesInfo(mapping, duplicates[..count]);
	}

	// RemoveDuplicates, RemapEdges and RemoveDuplicatesAndRemapEdges are done here, on the
	// caller's own memory, as CDT.h does them: CDT takes std::vectors for them, so calling it
	// would copy the arrays in and out.

	/// <summary>Removes vertices that repeat an earlier one, in place, as CDT's RemoveDuplicates
	/// does. Insert what is left: CDT does not accept duplicates.</summary>
	public static DuplicatesInfo RemoveDuplicates(List<V2d> vertices)
	{
		var info = FindDuplicates(CollectionsMarshal.AsSpan(vertices));
		RemoveDuplicates(vertices, info.Duplicates);
		return info;
	}

	/// <summary>Removes the vertices at the indices given, in increasing order, in place,
	/// keeping the rest in order, as CDT's RemoveDuplicates with duplicates given does.</summary>
	public static void RemoveDuplicates(List<V2d> vertices, ReadOnlySpan<uint> duplicates)
	{
		var span = CollectionsMarshal.AsSpan(vertices);
		int kept = 0,
			next = 0;
		for (int i = 0; i < span.Length; i++)
		{
			if (next < duplicates.Length && duplicates[next] == i)
			{
				next++;
				continue;
			}
			span[kept++] = span[i];
		}
		vertices.RemoveRange(kept, vertices.Count - kept);
	}

	/// <summary>Removes vertices that repeat an earlier one and points the edges at the vertices
	/// kept, both in place, as CDT's RemoveDuplicatesAndRemapEdges does. The mapping it returns
	/// ties data on the vertices given to the vertices kept.</summary>
	public static DuplicatesInfo RemoveDuplicatesAndRemapEdges(List<V2d> vertices, Span<Edge> edges)
	{
		var info = RemoveDuplicates(vertices);
		RemapEdges(edges, info.Mapping);
		return info;
	}

	/// <summary>Points edges, in place, at new vertex indices, as CDT's RemapEdges does.</summary>
	public static void RemapEdges(Span<Edge> edges, ReadOnlySpan<uint> mapping)
	{
		for (int i = 0; i < edges.Length; i++)
			edges[i] = new Edge(mapping[(int)edges[i].V1], mapping[(int)edges[i].V2]);
	}

	/// <summary>Every edge of the triangles once, as CDT's extractEdgesFromTriangles gives
	/// them.</summary>
	public static Edge[] ExtractEdgesFromTriangles(ReadOnlySpan<Triangle> triangles)
	{
		var edges = new Edge[triangles.Length * 3];
		int count = NativeMethods.ExtractEdgesFromTriangles(triangles, triangles.Length, edges);
		return edges[..count];
	}

	/// <summary>The triangles touching each vertex, as CDT's calculateTrianglesByVertex gives
	/// them.</summary>
	public static uint[][] CalculateTrianglesByVertex(
		ReadOnlySpan<Triangle> triangles,
		int vertexCount
	)
	{
		var offsets = new int[vertexCount + 1];
		var flat = new uint[triangles.Length * 3];
		NativeMethods.CalculateTrianglesByVertex(
			triangles,
			triangles.Length,
			vertexCount,
			offsets,
			flat
		);
		var byVertex = new uint[vertexCount][];
		for (int i = 0; i < vertexCount; i++)
			byVertex[i] = flat[offsets[i]..offsets[i + 1]];
		return byVertex;
	}

	const int ErrorLength = 512;

	static void Check(int status, byte[] error)
	{
		if (status != 0)
			throw new TriangulationException(
				Encoding.UTF8.GetString(error, 0, Math.Max(0, Array.IndexOf(error, (byte)0)))
			);
	}
}
