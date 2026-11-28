using System.Linq;
using CDT;
using NUnit.Framework;

namespace CSharpCDT.Tests;

public class HelperTests
{
	static readonly V2d[] WithRepeat = [new(0, 0), new(1, 0), new(1, 1), new(0, 0), new(0, 1)];

	[Test]
	public void DuplicatesAreFoundWithoutChangingTheVertices()
	{
		var info = CDTUtils.FindDuplicates(WithRepeat);
		Assert.That(info.Mapping, Is.EqualTo(new uint[] { 0, 1, 2, 0, 3 }));
		Assert.That(info.Duplicates, Is.EqualTo(new uint[] { 3 }));
	}

	[Test]
	public void DuplicatesAreRemovedAndEdgesRemapped()
	{
		var vertices = WithRepeat.ToList();
		Edge[] edges = [new(3, 4), new(1, 2)];
		var info = CDTUtils.RemoveDuplicatesAndRemapEdges(vertices, edges);
		Assert.That(vertices, Has.Count.EqualTo(4));
		Assert.That(vertices[3], Is.EqualTo(new V2d(0, 1)));
		Assert.That(edges, Is.EqualTo(new[] { new Edge(0, 3), new Edge(1, 2) }));
		Assert.That(info.Duplicates, Is.EqualTo(new uint[] { 3 }));
	}

	[Test]
	public void EdgesAreRemappedInPlace()
	{
		Edge[] edges = [new(3, 4)];
		CDTUtils.RemapEdges(edges, [0, 1, 2, 0, 3]);
		Assert.That(edges[0], Is.EqualTo(new Edge(0, 3)));
	}

	[Test]
	public void TrianglesGiveTheirEdgesOnceAndTheirVertices()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices([new(0, 0), new(1, 0), new(1, 1), new(0, 1)]);
		cdt.EraseSuperTriangle();
		Assert.That(CDTUtils.ExtractEdgesFromTriangles(cdt.Triangles), Has.Length.EqualTo(5));
		var byVertex = CDTUtils.CalculateTrianglesByVertex(cdt.Triangles, cdt.VertexCount);
		Assert.That(byVertex.Sum(t => t.Length), Is.EqualTo(6));
		Assert.That(byVertex.Count(t => t.Length == 2), Is.EqualTo(2));
	}
}
