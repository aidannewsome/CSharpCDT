using System;
using System.Linq;
using CDT;
using NUnit.Framework;

namespace CSharpCDT.Tests;

public class TriangulationTests
{
	static readonly V2d[] Square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

	static readonly Edge[] SquareEdges = [new(0, 1), new(1, 2), new(2, 3), new(3, 0)];

	[Test]
	public void SquareMakesTwoTriangles()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.EraseSuperTriangle();
		Assert.That(cdt.IsFinalized);
		Assert.That(cdt.VertexCount, Is.EqualTo(4));
		Assert.That(cdt.TriangleCount, Is.EqualTo(2));
	}

	[Test]
	public void FlippingTheDiagonalSwapsIt()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.EraseSuperTriangle();
		static Edge Shared(Triangle a, Triangle b)
		{
			var common = new[] { a.V0, a.V1, a.V2 }.Intersect([b.V0, b.V1, b.V2]).ToArray();
			return new Edge(common[0], common[1]);
		}
		var before = Shared(cdt.Triangles[0], cdt.Triangles[1]);
		cdt.FlipEdge(0, 1);
		Assert.That(CDTUtils.VerifyTopology(cdt), Is.True);
		Assert.That(Shared(cdt.Triangles[0], cdt.Triangles[1]), Is.Not.EqualTo(before));
	}

	[Test]
	public void NeighborsPointAcrossSharedEdges()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.EraseSuperTriangle();
		var t = cdt.Triangles[0];
		uint[] neighbors = [t.N0, t.N1, t.N2];
		Assert.That(neighbors.Count(n => n == 1), Is.EqualTo(1));
		Assert.That(neighbors.Count(n => n == Triangle.NoNeighbor), Is.EqualTo(2));
	}

	[Test]
	public void ConstraintEdgeIsKept()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.InsertEdges([new Edge(1, 3)]);
		cdt.EraseSuperTriangle();
		Assert.That(cdt.FixedEdges, Does.Contain(new Edge(1, 3)));
	}

	[Test]
	public void CrossingEdgesAreSplitWhenResolved()
	{
		using var cdt = new Triangulation(
			intersectingConstraintEdges: IntersectingConstraintEdges.TryResolve
		);
		cdt.InsertVertices(Square);
		cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)]);
		cdt.EraseSuperTriangle();
		var vertices = cdt.Vertices.ToArray();
		Assert.That(vertices, Has.Length.EqualTo(5));
		Assert.That(vertices[4].X, Is.EqualTo(0.5).Within(1e-12));
		Assert.That(vertices[4].Y, Is.EqualTo(0.5).Within(1e-12));
		Assert.That(cdt.TriangleCount, Is.EqualTo(4));
		var originals = cdt.PieceToOriginals.Values.SelectMany(o => o).Distinct().ToArray();
		Edge[] crossing = [new(0, 2), new(1, 3)];
		Assert.That(originals, Is.EquivalentTo(crossing));
	}

	[Test]
	public void CrossingEdgesAreAnErrorWhenNotAllowed()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		Assert.Throws<TriangulationException>(() =>
			cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)])
		);
	}

	[Test]
	public void ConformingSplitsALongEdge()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices([new(0, 0), new(10, 0), new(5, 1), new(5, -1)]);
		cdt.ConformToEdges([new Edge(0, 1)]);
		cdt.EraseSuperTriangle();
		Assert.That(cdt.VertexCount, Is.GreaterThan(4));
	}

	[Test]
	public void OuterTrianglesAreErasedOutsideTheEdges()
	{
		// A square with a point outside it: only the square's two triangles stay.
		using var cdt = new Triangulation();
		cdt.InsertVertices([.. Square, new(2, 0.5)]);
		cdt.InsertEdges(SquareEdges);
		cdt.EraseOuterTriangles();
		Assert.That(cdt.TriangleCount, Is.EqualTo(2));
	}

	[Test]
	public void HolesAreErasedInsideInnerEdges()
	{
		// A 3 by 3 square around a 1 by 1 square: the inner square is a hole.
		using var cdt = new Triangulation();
		cdt.InsertVertices([
			.. Square.Select(v => new V2d(v.X * 3, v.Y * 3)),
			.. Square.Select(v => new V2d(v.X + 1, v.Y + 1)),
		]);
		cdt.InsertEdges([.. SquareEdges, .. SquareEdges.Select(e => new Edge(e.V1 + 4, e.V2 + 4))]);
		cdt.EraseOuterTrianglesAndHoles();
		Assert.That(cdt.TriangleCount, Is.EqualTo(8));
	}

	[Test]
	public void UsingADisposedTriangulationThrows()
	{
		var cdt = new Triangulation();
		cdt.Dispose();
		Assert.Throws<ObjectDisposedException>(() => cdt.InsertVertices(Square));
	}

	[Test]
	public void RefiningRaisesTheSmallestAngle()
	{
		// A long thin rectangle makes slivers until refined.
		using var cdt = new Triangulation();
		cdt.InsertVertices([new(0, 0), new(10, 0), new(10, 1), new(0, 1)]);
		cdt.InsertEdges(SquareEdges);
		var threshold = double.DegreesToRadians(20);
		Assert.That(
			cdt.FindUnrefinedTriangles(RefinementCriterion.SmallestAngle, threshold),
			Is.Not.Empty
		);
		cdt.RefineTriangles(1000, RefinementCriterion.SmallestAngle, threshold);
		cdt.EraseOuterTriangles();
		Assert.That(
			cdt.FindUnrefinedTriangles(RefinementCriterion.SmallestAngle, threshold),
			Is.Empty
		);
		Assert.That(CDTUtils.VerifyTopology(cdt) && CDTUtils.VerifyWinding(cdt));
	}

	[Test]
	public void CollectingAndFinalizingMatchesErasing()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices([.. Square, new(2, 0.5)]);
		cdt.InsertEdges(SquareEdges);
		var outer = cdt.CollectOuterTriangles();
		Assert.That(outer, Is.Not.Empty);
		cdt.FinalizeTriangulation([.. outer]);
		Assert.That(cdt.IsFinalized);
		Assert.That(cdt.TriangleCount, Is.EqualTo(2));
	}

	[Test]
	public void DepthsCountBoundariesCrossed()
	{
		// Outside the outer square 0, between the squares 1, inside the inner square 2.
		using var cdt = new Triangulation();
		cdt.InsertVertices([
			.. Square.Select(v => new V2d(v.X * 3, v.Y * 3)),
			.. Square.Select(v => new V2d(v.X + 1, v.Y + 1)),
		]);
		cdt.InsertEdges([.. SquareEdges, .. SquareEdges.Select(e => new Edge(e.V1 + 4, e.V2 + 4))]);
		var depths = cdt.CalculateTriangleDepths();
		Assert.That(depths, Has.Length.EqualTo(cdt.TriangleCount));
		Assert.That(depths.Distinct().Order(), Is.EqualTo(new ushort[] { 0, 1, 2 }));
		cdt.EraseSuperTriangle();
		Assert.Throws<TriangulationException>(() => cdt.CalculateTriangleDepths());
	}

	[Test]
	public void OverlappingEdgesAreCounted()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.InsertEdges([new Edge(0, 2), new Edge(0, 2)]);
		cdt.EraseSuperTriangle();
		Assert.That(cdt.OverlapCount[new Edge(0, 2)], Is.EqualTo(1));
	}

	[Test]
	public void GeometryMatchesCdt()
	{
		V2d a = new(0, 0),
			b = new(2, 0),
			c = new(0, 2);
		Assert.That(CDTUtils.Area(a, b, c), Is.EqualTo(2).Within(1e-12));
		Assert.That(CDTUtils.Circumcenter(a, b, c), Is.EqualTo(new V2d(1, 1)));
		Assert.That(CDTUtils.SmallestAngle(a, b, c), Is.EqualTo(Math.PI / 4).Within(1e-12));
	}

	[Test]
	public void VerticesReadCdtsOwnMemory()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices(Square);
		cdt.EraseSuperTriangle();
		Assert.That(cdt.Vertices[2], Is.EqualTo(new V2d(1, 1)));
		Assert.That(cdt.Triangles[0].V0, Is.LessThan(4));
	}

	[Test]
	public void RefiningAddsReplacementsToTheSetGiven()
	{
		using var cdt = new Triangulation();
		cdt.InsertVertices([new(0, 0), new(10, 0), new(10, 1), new(0, 1), new(20, 0.5)]);
		cdt.InsertEdges(SquareEdges);
		var outer = new System.Collections.Generic.SortedSet<uint>(cdt.CollectOuterTriangles());
		cdt.RefineTriangles(1000, toErase: outer);
		Assert.That(outer, Is.Not.Empty);
		cdt.FinalizeTriangulation([.. outer, .. cdt.CollectSuperTriangle()]);
		Assert.That(CDTUtils.VerifyTopology(cdt));
	}

	[Test]
	public void SplitVerticesRunAlongEachSplitEdge()
	{
		using var cdt = new Triangulation(
			intersectingConstraintEdges: IntersectingConstraintEdges.TryResolve
		);
		cdt.InsertVertices(Square);
		cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)]);
		cdt.EraseSuperTriangle();
		var split = cdt.EdgeToSplitVertices();
		Assert.That(split[new Edge(0, 2)], Does.Contain(4u));
		Assert.That(split[new Edge(1, 3)], Does.Contain(4u));
		Assert.That(cdt.EdgeToPieces()[new Edge(0, 2)], Has.Length.EqualTo(2));
	}
}
