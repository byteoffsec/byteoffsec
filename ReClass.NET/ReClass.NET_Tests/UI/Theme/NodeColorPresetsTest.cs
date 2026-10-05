using System.Drawing;
using System.Linq;
using NFluent;
using ReClassNET;
using ReClassNET.UI.Theme;
using Xunit;

namespace ReClass.NET_Tests.UI.Theme
{
	public class NodeColorPresetsTest
	{
		[Fact]
		public void PresetsAreNotNullAndDistinct()
		{
			Check.That(NodeColorPresets.Light).IsNotNull();
			Check.That(NodeColorPresets.Dark).IsNotNull();
			Check.That(NodeColorPresets.Light).Not.IsSameReferenceAs(NodeColorPresets.Dark);

			Check.That(NodeColorPresets.Light.Theme).IsEqualTo(AppTheme.Light);
			Check.That(NodeColorPresets.Dark.Theme).IsEqualTo(AppTheme.Dark);
			Check.That(NodeColorPresets.Light.Colors.SequenceEqual(NodeColorPresets.Dark.Colors)).IsFalse();
		}

		[Fact]
		public void LightPresetMatchesTheClassicDefaults()
		{
			var settings = new Settings();

			Check.That(settings.NodeColorsMatchPreset(AppTheme.Light)).IsTrue();
			Check.That(settings.NodeColorsMatchPreset(AppTheme.Dark)).IsFalse();
		}

		[Fact]
		public void DarkPresetIsReadableOnItsBackground()
		{
			var preset = NodeColorPresets.Dark;

			Check.That(ThemePalette.IsDarkColor(preset.BackgroundColor)).IsTrue();

			foreach (var color in new[] { preset.OffsetColor, preset.AddressColor, preset.HexColor, preset.TypeColor, preset.NameColor, preset.ValueColor, preset.IndexColor, preset.CommentColor, preset.TextColor, preset.VTableColor, preset.PluginColor })
			{
				Check.That(ThemePalette.IsDarkColor(color)).IsFalse();
			}
		}

		[Theory]
		[InlineData(AppTheme.Light)]
		[InlineData(AppTheme.Dark)]
		public void ApplyThenMatchRoundTrip(AppTheme theme)
		{
			var settings = new Settings();

			settings.ApplyNodeColorPreset(theme);

			Check.That(settings.NodeColorsMatchPreset(theme)).IsTrue();
			Check.That(settings.NodeColorsMatchPreset(theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light)).IsFalse();
			Check.That(NodeColorPresets.FindMatching(settings)).IsSameReferenceAs(NodeColorPresets.ForTheme(theme));
			Check.That(NodeColorPresets.GuessTheme(settings)).IsEqualTo(theme);
		}

		[Theory]
		[InlineData(AppTheme.Light)]
		[InlineData(AppTheme.Dark)]
		public void MatchFailsAfterASingleChange(AppTheme theme)
		{
			var settings = new Settings();

			settings.ApplyNodeColorPreset(theme);
			settings.CommentColor = Color.FromArgb(1, 2, 3);

			Check.That(settings.NodeColorsMatchPreset(theme)).IsFalse();
			Check.That(NodeColorPresets.FindMatching(settings)).IsNull();
		}

		[Fact]
		public void MatchIgnoresNamedColors()
		{
			var settings = new Settings();

			settings.ApplyNodeColorPreset(AppTheme.Light);
			settings.BackgroundColor = Color.White;
			settings.OffsetColor = Color.Red;

			Check.That(settings.NodeColorsMatchPreset(AppTheme.Light)).IsTrue();
		}

		[Fact]
		public void GuessThemeUsesBackgroundForCustomColors()
		{
			var settings = new Settings();

			settings.ApplyNodeColorPreset(AppTheme.Light);
			settings.BackgroundColor = Color.FromArgb(10, 10, 10);

			Check.That(NodeColorPresets.GuessTheme(settings)).IsEqualTo(AppTheme.Dark);

			settings.ApplyNodeColorPreset(AppTheme.Dark);
			settings.BackgroundColor = Color.FromArgb(250, 250, 250);

			Check.That(NodeColorPresets.GuessTheme(settings)).IsEqualTo(AppTheme.Light);
		}
	}
}
