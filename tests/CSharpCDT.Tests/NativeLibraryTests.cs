using System.IO;
using CDT;
using NUnit.Framework;

namespace CSharpCDT.Tests;

public class NativeLibraryTests
{
	[Test]
	public void ItIsLookedForBesideTheAssemblyThenWhereNuGetPutsIt()
	{
		var candidates = NativeMethods.Candidates("lib");
		Assert.That(Path.GetDirectoryName(candidates[0]), Is.EqualTo("lib"));
		Assert.That(candidates[1], Does.StartWith(Path.Combine("lib", "runtimes")));
		Assert.That(
			candidates[1],
			Does.Contain(Path.Combine("native", Path.GetFileName(candidates[0])))
		);
	}
}
