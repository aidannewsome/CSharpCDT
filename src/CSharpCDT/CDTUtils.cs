namespace CDT;

/// <summary>CDT's free functions that PythonCDT binds: checks on a triangulation and plane
/// geometry, as CDT computes it.</summary>
public static unsafe class CDTUtils
{
	/// <summary>Whether every triangle's neighbours point back at it.</summary>
	public static bool VerifyTopology(Triangulation triangulation) =>
		NativeMethods.VerifyTopology(triangulation.Handle) != 0;

	/// <summary>Whether every triangle winds counter-clockwise.</summary>
	public static bool VerifyWinding(Triangulation triangulation) =>
		NativeMethods.VerifyWinding(triangulation.Handle) != 0;

	/// <summary>The triangle's area, whichever way its corners run.</summary>
	public static double Area(V2d a, V2d b, V2d c) => NativeMethods.Area(&a, &b, &c);

	/// <summary>The centre of the circle through the triangle's three corners.</summary>
	public static V2d Circumcenter(V2d a, V2d b, V2d c)
	{
		V2d result;
		NativeMethods.Circumcenter(&a, &b, &c, &result);
		return result;
	}

	/// <summary>The smallest of a triangle's angles, in radians.</summary>
	public static double SmallestAngle(V2d a, V2d b, V2d c) =>
		NativeMethods.SmallestAngle(&a, &b, &c);

	/// <summary>Degrees in radians.</summary>
	public static double DegToRad(double degrees) => NativeMethods.DegToRad(degrees);
}
