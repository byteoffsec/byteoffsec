using System.Drawing;
using System.Linq;
using NFluent;
using ReClassNET.Nodes;
using ReClassNET.UI.Theme;
using Xunit;

namespace ReClass.NET_Tests.UI.Theme
{
	public class HighlightColorsTest
	{
		[Fact]
		public void LightBackgroundKeepsTheClassicHighlightColors()
		{
			var colors = BaseHexNode.GetHighlightColors(NodeColorPresets.Light.BackgroundColor);

			Check.That(colors.Select(c => c.ToArgb())).Contains(Color.Blue.ToArgb(), Color.BlueViolet.ToArgb(), Color.Crimson.ToArgb());
		}

		[Fact]
		public void DarkBackgroundHighlightColorsAreVisible()
		{
			var background = NodeColorPresets.Dark.BackgroundColor;
			var colors = BaseHexNode.GetHighlightColors(background);

			Check.That(colors).Not.IsEmpty();
			foreach (var color in colors)
			{
				var ratio = ThemePalette.GetContrastRatio(color, background);
				Check.WithCustomMessage($"{color} on the dark background: contrast {ratio:F2}:1").That(ratio >= 3.0).IsTrue();
			}
		}

		[Fact]
		public void HighlightColorsFollowTheBackgroundNotTheTheme()
		{
			Check.That(BaseHexNode.GetHighlightColors(Color.Black)).IsSameReferenceAs(BaseHexNode.GetHighlightColors(NodeColorPresets.Dark.BackgroundColor));
			Check.That(BaseHexNode.GetHighlightColors(Color.White)).IsSameReferenceAs(BaseHexNode.GetHighlightColors(NodeColorPresets.Light.BackgroundColor));
			Check.That(BaseHexNode.GetHighlightColors(Color.White)).Not.IsSameReferenceAs(BaseHexNode.GetHighlightColors(Color.Black));
		}
	}
}
