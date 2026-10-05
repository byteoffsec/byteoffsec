using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;

namespace ReClassNET.UI.Theme
{
	/// <summary>A set of colors used to paint the nodes in the memory view.</summary>
	public sealed class NodeColorPreset
	{
		public AppTheme Theme { get; }

		public Color BackgroundColor { get; }
		public Color SelectedColor { get; }
		public Color HiddenColor { get; }
		public Color OffsetColor { get; }
		public Color AddressColor { get; }
		public Color HexColor { get; }
		public Color TypeColor { get; }
		public Color NameColor { get; }
		public Color ValueColor { get; }
		public Color IndexColor { get; }
		public Color CommentColor { get; }
		public Color TextColor { get; }
		public Color VTableColor { get; }
		public Color PluginColor { get; }

		internal NodeColorPreset(
			AppTheme theme,
			Color backgroundColor,
			Color selectedColor,
			Color hiddenColor,
			Color offsetColor,
			Color addressColor,
			Color hexColor,
			Color typeColor,
			Color nameColor,
			Color valueColor,
			Color indexColor,
			Color commentColor,
			Color textColor,
			Color vtableColor,
			Color pluginColor)
		{
			Theme = theme;
			BackgroundColor = backgroundColor;
			SelectedColor = selectedColor;
			HiddenColor = hiddenColor;
			OffsetColor = offsetColor;
			AddressColor = addressColor;
			HexColor = hexColor;
			TypeColor = typeColor;
			NameColor = nameColor;
			ValueColor = valueColor;
			IndexColor = indexColor;
			CommentColor = commentColor;
			TextColor = textColor;
			VTableColor = vtableColor;
			PluginColor = pluginColor;
		}

		/// <summary>Copies the colors of this preset into the given settings.</summary>
		/// <param name="settings">The settings to update.</param>
		public void ApplyTo(Settings settings)
		{
			Contract.Requires(settings != null);

			settings.BackgroundColor = BackgroundColor;
			settings.SelectedColor = SelectedColor;
			settings.HiddenColor = HiddenColor;
			settings.OffsetColor = OffsetColor;
			settings.AddressColor = AddressColor;
			settings.HexColor = HexColor;
			settings.TypeColor = TypeColor;
			settings.NameColor = NameColor;
			settings.ValueColor = ValueColor;
			settings.IndexColor = IndexColor;
			settings.CommentColor = CommentColor;
			settings.TextColor = TextColor;
			settings.VTableColor = VTableColor;
			settings.PluginColor = PluginColor;
		}

		/// <summary>Checks if the node colors of the given settings are equal to the colors of this preset.</summary>
		/// <param name="settings">The settings to compare.</param>
		/// <returns>True if all node colors match the preset, false otherwise.</returns>
		public bool Matches(Settings settings)
		{
			Contract.Requires(settings != null);

			return Equal(settings.BackgroundColor, BackgroundColor)
				&& Equal(settings.SelectedColor, SelectedColor)
				&& Equal(settings.HiddenColor, HiddenColor)
				&& Equal(settings.OffsetColor, OffsetColor)
				&& Equal(settings.AddressColor, AddressColor)
				&& Equal(settings.HexColor, HexColor)
				&& Equal(settings.TypeColor, TypeColor)
				&& Equal(settings.NameColor, NameColor)
				&& Equal(settings.ValueColor, ValueColor)
				&& Equal(settings.IndexColor, IndexColor)
				&& Equal(settings.CommentColor, CommentColor)
				&& Equal(settings.TextColor, TextColor)
				&& Equal(settings.VTableColor, VTableColor)
				&& Equal(settings.PluginColor, PluginColor);
		}

		/// <summary>Enumerates all colors of the preset.</summary>
		public IEnumerable<Color> Colors
		{
			get
			{
				yield return BackgroundColor;
				yield return SelectedColor;
				yield return HiddenColor;
				yield return OffsetColor;
				yield return AddressColor;
				yield return HexColor;
				yield return TypeColor;
				yield return NameColor;
				yield return ValueColor;
				yield return IndexColor;
				yield return CommentColor;
				yield return TextColor;
				yield return VTableColor;
				yield return PluginColor;
			}
		}

		// Compares the RGB values only. Named colors and colors created with FromArgb are equal for our purpose.
		private static bool Equal(Color a, Color b) => (a.ToArgb() & 0x00FFFFFF) == (b.ToArgb() & 0x00FFFFFF);
	}

	/// <summary>The node color presets shipped with the application.</summary>
	public static class NodeColorPresets
	{
		/// <summary>The classic colors of ReClass.NET for a white memory view.</summary>
		public static NodeColorPreset Light { get; } = new NodeColorPreset(
			AppTheme.Light,
			backgroundColor: Color.FromArgb(255, 255, 255),
			selectedColor: Color.FromArgb(240, 240, 240),
			hiddenColor: Color.FromArgb(240, 240, 240),
			offsetColor: Color.FromArgb(255, 0, 0),
			addressColor: Color.FromArgb(0, 200, 0),
			hexColor: Color.FromArgb(0, 0, 0),
			typeColor: Color.FromArgb(0, 0, 255),
			nameColor: Color.FromArgb(32, 32, 128),
			valueColor: Color.FromArgb(255, 128, 0),
			indexColor: Color.FromArgb(32, 200, 200),
			commentColor: Color.FromArgb(0, 200, 0),
			textColor: Color.FromArgb(0, 0, 255),
			vtableColor: Color.FromArgb(0, 255, 0),
			pluginColor: Color.FromArgb(255, 0, 255)
		);

		/// <summary>Colors for a dark memory view, modelled after common dark code editor themes.</summary>
		public static NodeColorPreset Dark { get; } = new NodeColorPreset(
			AppTheme.Dark,
			backgroundColor: Color.FromArgb(0x1E, 0x1E, 0x1E),
			selectedColor: Color.FromArgb(0x2A, 0x2D, 0x2E),
			hiddenColor: Color.FromArgb(0x33, 0x33, 0x33),
			offsetColor: Color.FromArgb(0xF1, 0x4C, 0x4C),
			addressColor: Color.FromArgb(0x4E, 0xC9, 0xB0),
			hexColor: Color.FromArgb(0xD4, 0xD4, 0xD4),
			typeColor: Color.FromArgb(0x56, 0x9C, 0xD6),
			nameColor: Color.FromArgb(0x9C, 0xDC, 0xFE),
			valueColor: Color.FromArgb(0xCE, 0x91, 0x78),
			indexColor: Color.FromArgb(0x4F, 0xC1, 0xFF),
			commentColor: Color.FromArgb(0x6A, 0x99, 0x55),
			textColor: Color.FromArgb(0xDC, 0xDC, 0xAA),
			vtableColor: Color.FromArgb(0xB5, 0xCE, 0xA8),
			pluginColor: Color.FromArgb(0xC5, 0x86, 0xC0)
		);

		/// <summary>All shipped presets.</summary>
		public static IReadOnlyList<NodeColorPreset> All { get; } = new[] { Light, Dark };

		/// <summary>Gets the preset for the given theme.</summary>
		/// <param name="theme">The theme.</param>
		/// <returns>The preset.</returns>
		public static NodeColorPreset ForTheme(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;

		/// <summary>Finds the preset the node colors of the settings are equal to.</summary>
		/// <param name="settings">The settings to inspect.</param>
		/// <returns>The matching preset or null if the colors are customized.</returns>
		public static NodeColorPreset FindMatching(Settings settings)
		{
			Contract.Requires(settings != null);

			return All.FirstOrDefault(p => p.Matches(settings));
		}

		/// <summary>
		/// Guesses the theme the node colors of the settings were made for. Used for settings files
		/// written by versions without a theme setting.
		/// </summary>
		/// <param name="settings">The settings to inspect.</param>
		/// <returns>The theme which fits the node colors best.</returns>
		public static AppTheme GuessTheme(Settings settings)
		{
			Contract.Requires(settings != null);

			var preset = FindMatching(settings);
			if (preset != null)
			{
				return preset.Theme;
			}

			return ThemePalette.IsDarkColor(settings.BackgroundColor) ? AppTheme.Dark : AppTheme.Light;
		}
	}
}
