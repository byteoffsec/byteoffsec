using System.Drawing;
using System.Xml.Linq;
using NFluent;
using ReClassNET;
using ReClassNET.UI.Theme;
using ReClassNET.Util;
using Xunit;

namespace ReClass.NET_Tests.UI.Theme
{
	public class SettingsSerializerThemeTest
	{
		private static XDocument CreateDocument(Settings settings, bool includeTheme)
		{
			var document = SettingsSerializer.ToDocument(settings);

			if (!includeTheme)
			{
				document.Root?.Element("General")?.Element(nameof(Settings.Theme))?.Remove();
			}

			return document;
		}

		[Fact]
		public void DefaultSettingsUseTheDarkThemeWithMatchingNodeColors()
		{
			var settings = SettingsSerializer.CreateDefault();

			Check.That(settings.Theme).IsEqualTo(AppTheme.Dark);
			Check.That(settings.NodeColorsMatchPreset(AppTheme.Dark)).IsTrue();
		}

		[Fact]
		public void LegacyFileWithLightColorsLoadsAsLightTheme()
		{
			var saved = new Settings();
			saved.ApplyNodeColorPreset(AppTheme.Light);

			var loaded = SettingsSerializer.Load(CreateDocument(saved, false));

			Check.That(loaded.Theme).IsEqualTo(AppTheme.Light);
			Check.That(loaded.NodeColorsMatchPreset(AppTheme.Light)).IsTrue();
		}

		[Fact]
		public void LegacyFileWithDarkColorsLoadsAsDarkTheme()
		{
			var saved = new Settings();
			saved.ApplyNodeColorPreset(AppTheme.Dark);

			var loaded = SettingsSerializer.Load(CreateDocument(saved, false));

			Check.That(loaded.Theme).IsEqualTo(AppTheme.Dark);
			Check.That(loaded.NodeColorsMatchPreset(AppTheme.Dark)).IsTrue();
		}

		[Fact]
		public void LegacyFileWithCustomColorsKeepsThemAndGuessesTheThemeFromTheBackground()
		{
			var saved = new Settings();
			saved.ApplyNodeColorPreset(AppTheme.Light);
			saved.NameColor = Color.FromArgb(0x12, 0x34, 0x56);

			var loaded = SettingsSerializer.Load(CreateDocument(saved, false));

			Check.That(loaded.Theme).IsEqualTo(AppTheme.Light);
			Check.That(loaded.NameColor.ToArgb()).IsEqualTo(Color.FromArgb(0x12, 0x34, 0x56).ToArgb());
			Check.That(loaded.NodeColorsMatchPreset(AppTheme.Light)).IsFalse();
		}

		[Theory]
		[InlineData(AppTheme.Light)]
		[InlineData(AppTheme.Dark)]
		public void ThemeElementWinsOverTheColors(AppTheme theme)
		{
			var saved = new Settings
			{
				Theme = theme
			};
			saved.ApplyNodeColorPreset(theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

			var loaded = SettingsSerializer.Load(CreateDocument(saved, true));

			Check.That(loaded.Theme).IsEqualTo(theme);
		}

		[Fact]
		public void ThemeAndNodeColorsRoundTrip()
		{
			var saved = new Settings
			{
				Theme = AppTheme.Dark,
				StayOnTop = true
			};
			saved.ApplyNodeColorPreset(AppTheme.Dark);
			saved.PluginColor = Color.FromArgb(0x11, 0x22, 0x33);

			var loaded = SettingsSerializer.Load(SettingsSerializer.ToDocument(saved));

			Check.That(loaded.Theme).IsEqualTo(AppTheme.Dark);
			Check.That(loaded.StayOnTop).IsTrue();
			Check.That(loaded.PluginColor.ToArgb()).IsEqualTo(saved.PluginColor.ToArgb());
			Check.That(loaded.BackgroundColor.ToArgb()).IsEqualTo(NodeColorPresets.Dark.BackgroundColor.ToArgb());
		}

		[Fact]
		public void EmptyDocumentLoadsAsLightTheme()
		{
			// A file without colors was written for a white memory view.
			var loaded = SettingsSerializer.Load(new XDocument(new XElement("Settings")));

			Check.That(loaded.Theme).IsEqualTo(AppTheme.Light);
			Check.That(loaded.NodeColorsMatchPreset(AppTheme.Light)).IsTrue();
		}
	}
}
