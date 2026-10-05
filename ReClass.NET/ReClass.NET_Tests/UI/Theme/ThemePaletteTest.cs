using System.Drawing;
using System.Linq;
using System.Reflection;
using NFluent;
using ReClassNET.UI.Theme;
using Xunit;

namespace ReClass.NET_Tests.UI.Theme
{
	public class ThemePaletteTest
	{
		private static PropertyInfo[] ColorProperties => typeof(ThemePalette)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType == typeof(Color))
			.ToArray();

		[Fact]
		public void PresetsAreNotNullAndDistinct()
		{
			Check.That(ThemePalette.Light).IsNotNull();
			Check.That(ThemePalette.Dark).IsNotNull();
			Check.That(ThemePalette.Light).Not.IsSameReferenceAs(ThemePalette.Dark);

			Check.That(ThemePalette.Light.Theme).IsEqualTo(AppTheme.Light);
			Check.That(ThemePalette.Dark.Theme).IsEqualTo(AppTheme.Dark);
			Check.That(ThemePalette.Light.IsDark).IsFalse();
			Check.That(ThemePalette.Dark.IsDark).IsTrue();
		}

		[Fact]
		public void ForThemeReturnsMatchingPreset()
		{
			Check.That(ThemePalette.ForTheme(AppTheme.Light)).IsSameReferenceAs(ThemePalette.Light);
			Check.That(ThemePalette.ForTheme(AppTheme.Dark)).IsSameReferenceAs(ThemePalette.Dark);
		}

		[Fact]
		public void PresetsDefineEveryColor()
		{
			Check.That(ColorProperties).Not.IsEmpty();

			foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
			{
				foreach (var property in ColorProperties)
				{
					var color = (Color)property.GetValue(palette);

					Check.WithCustomMessage($"{palette.Theme}.{property.Name} is empty").That(color.IsEmpty).IsFalse();
					Check.WithCustomMessage($"{palette.Theme}.{property.Name} is not opaque").That(color.A).IsEqualTo((byte)255);
				}
			}
		}

		[Fact]
		public void PresetsDifferInBackgroundAndText()
		{
			Check.That(ThemePalette.Light.WindowBackground).IsNotEqualTo(ThemePalette.Dark.WindowBackground);
			Check.That(ThemePalette.Light.Text).IsNotEqualTo(ThemePalette.Dark.Text);

			Check.That(ThemePalette.IsDarkColor(ThemePalette.Dark.WindowBackground)).IsTrue();
			Check.That(ThemePalette.IsDarkColor(ThemePalette.Light.WindowBackground)).IsFalse();
		}

		[Fact]
		public void TextContrastsWithBackground()
		{
			foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
			{
				Check.That(ThemePalette.IsDarkColor(palette.Text)).IsNotEqualTo(ThemePalette.IsDarkColor(palette.WindowBackground));
				Check.That(ThemePalette.IsDarkColor(palette.Text)).IsNotEqualTo(ThemePalette.IsDarkColor(palette.ControlBackground));
				Check.That(ThemePalette.IsDarkColor(palette.SelectionForeground)).IsNotEqualTo(ThemePalette.IsDarkColor(palette.SelectionBackground));
			}
		}

		[Theory]
		[InlineData(0, 0, 0, true)]
		[InlineData(0x1E, 0x1E, 0x1E, true)]
		[InlineData(0xFF, 0xFF, 0xFF, false)]
		[InlineData(0xF5, 0xF5, 0xF5, false)]
		public void IsDarkColorAndContrastText(int r, int g, int b, bool expectedDark)
		{
			var color = Color.FromArgb(r, g, b);

			Check.That(ThemePalette.IsDarkColor(color)).IsEqualTo(expectedDark);
			Check.That(ThemePalette.IsDarkColor(ThemePalette.GetContrastText(color))).IsEqualTo(!expectedDark);
		}
	}
}
