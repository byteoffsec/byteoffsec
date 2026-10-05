using System.Collections.Generic;
using System.IO;
using NFluent;
using ReClassNET.Util;
using Xunit;

namespace ReClass.NET_Tests.Util
{
	public class PathUtilTest
	{
		public static IEnumerable<object[]> GetTestConversionData()
		{
			yield return new object[] { string.Empty, string.Empty };
			yield return new object[] { "C:/", "C:" + Path.DirectorySeparatorChar };
			yield return new object[] { "C:/test.test", Path.Combine("C:" + Path.DirectorySeparatorChar, "test.test") };
			yield return new object[] { "file:///C:/test.test", Path.Combine("C:" + Path.DirectorySeparatorChar, "test.test") };

			if (Path.DirectorySeparatorChar == '\\')
			{
				// Backslashes are only path separators on Windows.
				yield return new object[] { @"C:\", "C:" + Path.DirectorySeparatorChar };
			}
		}

		[Theory]
		[MemberData(nameof(GetTestConversionData))]
		public void TestConversion(string input, string expected)
		{
			Check.That(PathUtil.FileUrlToPath(input)).IsEqualTo(expected);
		}
	}
}
