using System.Drawing;
using NFluent;
using ReClassNET.UI.Theme;
using Xunit;

namespace ReClass.NET_Tests.UI.Theme
{
	public class ThemedGlyphsTest
	{
		private static Bitmap CreateGlyph(Color color)
		{
			// A 16x16 glyph like the node type icons: a transparent background with a solid shape.
			var bitmap = new Bitmap(16, 16);
			using var g = Graphics.FromImage(bitmap);
			using var brush = new SolidBrush(color);

			g.Clear(Color.Transparent);
			g.FillRectangle(brush, 3, 3, 10, 10);

			return bitmap;
		}

		private static double MeanLuminance(Bitmap bitmap)
		{
			var sum = 0.0;
			var count = 0;

			for (var y = 0; y < bitmap.Height; ++y)
			{
				for (var x = 0; x < bitmap.Width; ++x)
				{
					var pixel = bitmap.GetPixel(x, y);
					if (pixel.A < 128)
					{
						continue;
					}

					sum += (0.2126 * pixel.R) + (0.7152 * pixel.G) + (0.0722 * pixel.B);
					count++;
				}
			}

			return count == 0 ? 0.0 : sum / count;
		}

		[Fact]
		public void InvertLightnessKeepsGreyNeutral()
		{
			var light = ThemedGlyphs.InvertLightness(Color.FromArgb(0x4E, 0x4E, 0x4E));

			Check.That(light.R).IsEqualTo((byte)0xB1);
			Check.That(light.G).IsEqualTo((byte)0xB1);
			Check.That(light.B).IsEqualTo((byte)0xB1);
			Check.That(light.A).IsEqualTo((byte)255);
		}

		[Fact]
		public void InvertLightnessKeepsHue()
		{
			var light = ThemedGlyphs.InvertLightness(Color.FromArgb(0, 0, 128));

			Check.That(light.B).IsEqualTo((byte)255);
			Check.That(light.R).IsEqualTo(light.G);
			Check.That(light.R).IsStrictlyLessThan(light.B);
			Check.That(light.GetHue()).IsCloseTo(240.0f, 1.0f);
		}

		[Fact]
		public void InvertLightnessIsAnInvolution()
		{
			var color = Color.FromArgb(0x12, 0x80, 0x34);

			var twice = ThemedGlyphs.InvertLightness(ThemedGlyphs.InvertLightness(color));

			Check.That((double)twice.R).IsCloseTo(color.R, 1.0);
			Check.That((double)twice.G).IsCloseTo(color.G, 1.0);
			Check.That((double)twice.B).IsCloseTo(color.B, 1.0);
		}

		[Fact]
		public void DarkMonochromeGlyphGetsALightVariant()
		{
			using var glyph = CreateGlyph(Color.FromArgb(0x4E, 0x4E, 0x4E));

			Check.That(ThemedGlyphs.IsDarkGlyph(glyph)).IsTrue();

			using var variant = ThemedGlyphs.CreateLightVariant(glyph);

			Check.That(variant).IsNotNull();
			Check.That(variant.Size).IsEqualTo(glyph.Size);
			var luminance = MeanLuminance(variant);
			Check.WithCustomMessage($"mean luminance {luminance:F1}").That(luminance >= 170.0).IsTrue();
			Check.That(variant.GetPixel(0, 0).A).IsEqualTo((byte)0);
			Check.That(variant.GetPixel(8, 8).A).IsEqualTo((byte)255);
		}

		[Fact]
		public void BlackGlyphBecomesWhite()
		{
			using var glyph = CreateGlyph(Color.Black);
			using var variant = ThemedGlyphs.CreateLightVariant(glyph);

			Check.That(variant).IsNotNull();
			Check.That(variant.GetPixel(8, 8).ToArgb()).IsEqualTo(Color.White.ToArgb());
		}

		[Fact]
		public void ColoredIconWithHighlightsIsLeftAlone()
		{
			using var icon = CreateGlyph(Color.FromArgb(0xFF, 0xCC, 0x00));

			Check.That(ThemedGlyphs.IsDarkGlyph(icon)).IsFalse();
			Check.That(ThemedGlyphs.CreateLightVariant(icon)).IsNull();
			Check.That(ThemedGlyphs.ForDarkSurface(icon)).IsSameReferenceAs(icon);
		}

		[Fact]
		public void DarkIconWithBrightDetailsIsLeftAlone()
		{
			using var icon = CreateGlyph(Color.FromArgb(0x30, 0x30, 0x30));
			using (var g = Graphics.FromImage(icon))
			{
				g.FillRectangle(Brushes.White, 5, 5, 3, 3);
			}

			Check.That(ThemedGlyphs.IsDarkGlyph(icon)).IsFalse();
		}

		[Fact]
		public void ForDarkSurfaceCachesTheVariantPerImage()
		{
			using var glyph = CreateGlyph(Color.FromArgb(0x4E, 0x4E, 0x4E));

			var first = ThemedGlyphs.ForDarkSurface(glyph);
			var second = ThemedGlyphs.ForDarkSurface(glyph);

			Check.That(first).Not.IsSameReferenceAs(glyph);
			Check.That(second).IsSameReferenceAs(first);
			Check.That(ThemedGlyphs.ForDarkSurface(null)).IsNull();
		}

		[Fact]
		public void EmptyImageIsNotAGlyph()
		{
			using var empty = new Bitmap(16, 16);

			Check.That(ThemedGlyphs.IsDarkGlyph(empty)).IsFalse();
			Check.That(ThemedGlyphs.IsDarkGlyph(null)).IsFalse();
		}
	}
}
