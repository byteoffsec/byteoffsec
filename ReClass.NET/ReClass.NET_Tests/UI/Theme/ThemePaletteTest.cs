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

		[Fact]
		public void ContrastRatioFollowsTheWcagDefinition()
		{
			Check.That(ThemePalette.GetContrastRatio(Color.Black, Color.White)).IsCloseTo(21.0, 0.01);
			Check.That(ThemePalette.GetContrastRatio(Color.White, Color.Black)).IsCloseTo(21.0, 0.01);
			Check.That(ThemePalette.GetContrastRatio(Color.White, Color.White)).IsCloseTo(1.0, 0.01);
			Check.That(ThemePalette.GetContrastRatio(Color.FromArgb(0x77, 0x77, 0x77), Color.White)).IsCloseTo(4.48, 0.02);
		}

		[Fact]
		public void TextIsReadableOnEverySurface()
		{
			foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
			{
				foreach (var background in Surfaces(palette))
				{
					CheckContrast($"{palette.Theme}.Text on {background.Name}", palette.Text, background.Color, 4.5);
				}

				CheckContrast($"{palette.Theme}.AttentionText", palette.AttentionText, palette.ContentBackground, 4.5);
				CheckContrast($"{palette.Theme}.SelectionForeground", palette.SelectionForeground, palette.SelectionBackground, 4.5);
				CheckContrast($"{palette.Theme}.AccentText", palette.AccentText, palette.Accent, 3.0);
			}
		}

		[Fact]
		public void DisabledTextStaysLegible()
		{
			// Disabled items have to be recognizable as disabled but still readable.
			foreach (var background in Surfaces(ThemePalette.Light))
			{
				CheckContrast($"Light.DisabledText on {background.Name}", ThemePalette.Light.DisabledText, background.Color, 4.5);
			}
			foreach (var background in Surfaces(ThemePalette.Dark))
			{
				CheckContrast($"Dark.DisabledText on {background.Name}", ThemePalette.Dark.DisabledText, background.Color, 3.0);
			}

			foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
			{
				Check.That(ThemePalette.GetContrastRatio(palette.DisabledText, palette.WindowBackground)).IsStrictlyLessThan(ThemePalette.GetContrastRatio(palette.Text, palette.WindowBackground));
			}
		}

		private static void CheckContrast(string what, Color foreground, Color background, double minimumRatio)
		{
			var ratio = ThemePalette.GetContrastRatio(foreground, background);

			Check.WithCustomMessage($"{what}: contrast {ratio:F2}:1 is below {minimumRatio:F1}:1").That(ratio >= minimumRatio).IsTrue();
		}

		private static (string Name, Color Color)[] Surfaces(ThemePalette palette) => new[]
		{
			(nameof(palette.WindowBackground), palette.WindowBackground),
			(nameof(palette.Surface), palette.Surface),
			(nameof(palette.ControlBackground), palette.ControlBackground),
			(nameof(palette.ContentBackground), palette.ContentBackground),
			(nameof(palette.MenuBackground), palette.MenuBackground),
			(nameof(palette.MenuDropDownBackground), palette.MenuDropDownBackground),
			(nameof(palette.ButtonBackground), palette.ButtonBackground),
			(nameof(palette.HeaderBackground), palette.HeaderBackground)
		};

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
