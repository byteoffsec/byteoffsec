using System.Drawing;
using System.Windows.Forms;
using ReClassNET.UI.Theme;

namespace ReClassNET.UI
{
	/// <summary>
	/// Legacy renderer kept for plugins. It now delegates to the theme aware <see cref="ThemedToolStripRenderer"/>.
	/// </summary>
	internal class CustomToolStripProfessionalRenderer : ThemedToolStripRenderer
	{
		public CustomToolStripProfessionalRenderer(bool renderGrip, bool renderBorder)
		{
			ShowGrip = renderGrip;
			RenderToolBarSeparatorLine = renderBorder;
		}
	}

	/// <summary>
	/// Legacy color table kept for plugins. It follows the current <see cref="ThemePalette"/>.
	/// </summary>
	internal class CustomProfessionalColorTable : ThemedColorTable
	{
		public override Color MenuStripGradientBegin => ThemeManager.Palette.MenuBackground;

		public override Color MenuStripGradientEnd => ThemeManager.Palette.MenuBackground;

		public override Color ToolStripGradientBegin => ThemeManager.Palette.MenuBackground;

		public override Color ToolStripGradientMiddle => ThemeManager.Palette.MenuBackground;

		public override Color ToolStripGradientEnd => ThemeManager.Palette.MenuBackground;
	}
}
