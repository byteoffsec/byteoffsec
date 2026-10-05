using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReClassNET.UI.Theme
{
	/// <summary>
	/// Creates light variants of dark glyphs (like the monochrome node type icons of the tool bar) so they stay legible
	/// on dark surfaces. The lightness of every pixel is inverted while hue and saturation are kept, so a dark grey
	/// glyph becomes light grey and a dark blue glyph becomes light blue. Colored icons with highlights are left untouched.
	/// </summary>
	public static class ThemedGlyphs
	{
		private const int OpaqueAlpha = 128;
		private const double DarkMeanLuminance = 128.0;
		private const double HighlightLuminance = 160.0;

		private static readonly ConditionalWeakTable<Image, Image> cache = new ConditionalWeakTable<Image, Image>();

		/// <summary>
		/// Gets the variant of the image to draw on a dark surface. Dark glyphs are converted once per image instance
		/// and cached, all other images are returned unchanged.
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns>The light variant or the image itself.</returns>
		public static Image ForDarkSurface(Image image)
		{
			if (image == null)
			{
				return null;
			}

			try
			{
				return cache.GetValue(image, source => (Image)CreateLightVariant(source) ?? source);
			}
			catch
			{
				return image;
			}
		}

		/// <summary>
		/// Checks if the image is a dark glyph: the opaque pixels are dark on average and the image contains no highlights.
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns>True if the image needs a light variant on dark surfaces.</returns>
		public static bool IsDarkGlyph(Image image)
		{
			if (image == null)
			{
				return false;
			}

			var pixels = ReadPixels(image);

			return IsDarkGlyph(pixels);
		}

		/// <summary>Creates a copy of the image with inverted lightness. Returns null if the image is not a dark glyph.</summary>
		/// <param name="image">The image.</param>
		/// <returns>The light variant or null.</returns>
		public static Bitmap CreateLightVariant(Image image)
		{
			if (image == null)
			{
				return null;
			}

			var pixels = ReadPixels(image);
			if (!IsDarkGlyph(pixels))
			{
				return null;
			}

			for (var i = 0; i + 3 < pixels.Length; i += 4)
			{
				if (pixels[i + 3] == 0)
				{
					continue;
				}

				var light = InvertLightness(Color.FromArgb(pixels[i + 2], pixels[i + 1], pixels[i]));

				pixels[i] = light.B;
				pixels[i + 1] = light.G;
				pixels[i + 2] = light.R;
			}

			var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
			var data = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
			}
			finally
			{
				result.UnlockBits(data);
			}

			return result;
		}

		/// <summary>Inverts the lightness of the color (HSL) and keeps hue and saturation.</summary>
		/// <param name="color">The color.</param>
		/// <returns>The color with inverted lightness.</returns>
		public static Color InvertLightness(Color color)
		{
			var r = color.R / 255.0;
			var g = color.G / 255.0;
			var b = color.B / 255.0;

			var max = Math.Max(r, Math.Max(g, b));
			var min = Math.Min(r, Math.Min(g, b));
			var lightness = (max + min) / 2.0;
			var delta = max - min;

			var newLightness = 1.0 - lightness;
			if (delta < 1e-6)
			{
				var gray = ToByte(newLightness);

				return Color.FromArgb(color.A, gray, gray, gray);
			}

			var saturation = lightness <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);
			var hue = color.GetHue() / 360.0;

			var q = newLightness < 0.5 ? newLightness * (1.0 + saturation) : newLightness + saturation - (newLightness * saturation);
			var p = (2.0 * newLightness) - q;

			return Color.FromArgb(
				color.A,
				ToByte(HueToChannel(p, q, hue + (1.0 / 3.0))),
				ToByte(HueToChannel(p, q, hue)),
				ToByte(HueToChannel(p, q, hue - (1.0 / 3.0)))
			);
		}

		private static bool IsDarkGlyph(byte[] pixels)
		{
			var count = 0;
			var sum = 0.0;
			var max = 0.0;

			for (var i = 0; i + 3 < pixels.Length; i += 4)
			{
				if (pixels[i + 3] < OpaqueAlpha)
				{
					continue;
				}

				var luminance = (0.2126 * pixels[i + 2]) + (0.7152 * pixels[i + 1]) + (0.0722 * pixels[i]);

				count++;
				sum += luminance;
				if (luminance > max)
				{
					max = luminance;
				}
			}

			if (count == 0)
			{
				return false;
			}

			return sum / count < DarkMeanLuminance && max < HighlightLuminance;
		}

		/// <summary>Reads the pixels of the image as 32bpp BGRA rows without padding.</summary>
		private static byte[] ReadPixels(Image image)
		{
			using var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
			using (var g = Graphics.FromImage(bitmap))
			{
				g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
				g.DrawImage(image, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
			}

			var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				var rowLength = bitmap.Width * 4;
				var pixels = new byte[rowLength * bitmap.Height];
				for (var y = 0; y < bitmap.Height; ++y)
				{
					Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowLength, rowLength);
				}

				return pixels;
			}
			finally
			{
				bitmap.UnlockBits(data);
			}
		}

		private static double HueToChannel(double p, double q, double t)
		{
			if (t < 0.0)
			{
				t += 1.0;
			}
			if (t > 1.0)
			{
				t -= 1.0;
			}

			if (t < 1.0 / 6.0)
			{
				return p + ((q - p) * 6.0 * t);
			}
			if (t < 0.5)
			{
				return q;
			}
			if (t < 2.0 / 3.0)
			{
				return p + ((q - p) * ((2.0 / 3.0) - t) * 6.0);
			}

			return p;
		}

		private static int ToByte(double value)
		{
			return Math.Max(0, Math.Min(255, (int)Math.Round(value * 255.0)));
		}
	}
}
