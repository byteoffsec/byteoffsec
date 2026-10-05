using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Xml.Linq;
using ReClassNET.UI.Theme;

namespace ReClassNET.Util
{
	public sealed class SettingsSerializer
	{
		private const string XmlRootElement = "Settings";
		private const string XmlGeneralElement = "General";
		private const string XmlDisplayElement = "Display";
		private const string XmlColorsElement = "Colors";
		private const string XmlCustomDataElement = "CustomData";

		#region Read Settings

		/// <summary>Loads the settings from the settings file. Returns the defaults if the file does not exist or is unreadable.</summary>
		public static Settings Load()
		{
			EnsureSettingsDirectoryAvailable();

			try
			{
				var path = Path.Combine(PathUtil.SettingsFolderPath, Constants.SettingsFile);
				if (!File.Exists(path))
				{
					return CreateDefault();
				}

				using var sr = new StreamReader(path);

				return Load(XDocument.Load(sr));
			}
			catch
			{
				return CreateDefault();
			}
		}

		/// <summary>Creates the settings used on the first start: the default theme with its matching node colors.</summary>
		public static Settings CreateDefault()
		{
			var settings = new Settings();
			settings.ApplyNodeColorPreset(settings.Theme);

			return settings;
		}

		/// <summary>Reads the settings from the given document.</summary>
		/// <param name="document">The settings document.</param>
		/// <returns>The settings. Values missing in the document keep their defaults.</returns>
		public static Settings Load(XDocument document)
		{
			Contract.Requires(document != null);

			var settings = new Settings();

			var themeElementPresent = false;

			try
			{
				var root = document.Root;

				var general = root?.Element(XmlGeneralElement);
				if (general != null)
				{
					XElementSerializer.TryRead(general, nameof(settings.LastProcess), e => settings.LastProcess = XElementSerializer.ToString(e));
					XElementSerializer.TryRead(general, nameof(settings.StayOnTop), e => settings.StayOnTop = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(general, nameof(settings.RunAsAdmin), e => settings.RunAsAdmin = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(general, nameof(settings.RandomizeWindowTitle), e => settings.RandomizeWindowTitle = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(general, nameof(settings.Theme), e =>
					{
						// An unknown value counts as missing, so the theme is inferred from the colors below.
						if (Enum.TryParse<AppTheme>(XElementSerializer.ToString(e), true, out var theme))
						{
							settings.Theme = theme;

							themeElementPresent = true;
						}
					});
				}
				var display = root?.Element(XmlDisplayElement);
				if (display != null)
				{
					XElementSerializer.TryRead(display, nameof(settings.ShowNodeAddress), e => settings.ShowNodeAddress = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowNodeOffset), e => settings.ShowNodeOffset = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowNodeText), e => settings.ShowNodeText = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.HighlightChangedValues), e => settings.HighlightChangedValues = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.PreserveNodeOffsetsOnResize), e => settings.PreserveNodeOffsetsOnResize = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentFloat), e => settings.ShowCommentFloat = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentInteger), e => settings.ShowCommentInteger = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentPointer), e => settings.ShowCommentPointer = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentRtti), e => settings.ShowCommentRtti = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentSymbol), e => settings.ShowCommentSymbol = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentString), e => settings.ShowCommentString = XElementSerializer.ToBool(e));
					XElementSerializer.TryRead(display, nameof(settings.ShowCommentPluginInfo), e => settings.ShowCommentPluginInfo = XElementSerializer.ToBool(e));
				}
				var colors = root?.Element(XmlColorsElement);
				if (colors != null)
				{
					XElementSerializer.TryRead(colors, nameof(settings.BackgroundColor), e => settings.BackgroundColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.SelectedColor), e => settings.SelectedColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.HiddenColor), e => settings.HiddenColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.OffsetColor), e => settings.OffsetColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.AddressColor), e => settings.AddressColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.HexColor), e => settings.HexColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.TypeColor), e => settings.TypeColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.NameColor), e => settings.NameColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.ValueColor), e => settings.ValueColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.IndexColor), e => settings.IndexColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.CommentColor), e => settings.CommentColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.TextColor), e => settings.TextColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.VTableColor), e => settings.VTableColor = XElementSerializer.ToColor(e));
					XElementSerializer.TryRead(colors, nameof(settings.PluginColor), e => settings.PluginColor = XElementSerializer.ToColor(e));
				}
				var customData = root?.Element(XmlCustomDataElement);
				if (customData != null)
				{
					settings.CustomData.Deserialize(customData);
				}
			}
			catch
			{
				// ignored
			}

			if (!themeElementPresent)
			{
				// Settings written by a version without themes: keep the look the user had, which is determined by the node colors.
				settings.Theme = NodeColorPresets.GuessTheme(settings);
			}

			return settings;
		}

		#endregion

		#region Write Settings

		/// <summary>Writes the settings to the settings file.</summary>
		/// <param name="settings">The settings to save.</param>
		public static void Save(Settings settings)
		{
			Contract.Requires(settings != null);

			EnsureSettingsDirectoryAvailable();

			var path = Path.Combine(PathUtil.SettingsFolderPath, Constants.SettingsFile);

			using var sw = new StreamWriter(path);

			ToDocument(settings).Save(sw);
		}

		/// <summary>Converts the settings to a document.</summary>
		/// <param name="settings">The settings to convert.</param>
		/// <returns>The settings document.</returns>
		public static XDocument ToDocument(Settings settings)
		{
			Contract.Requires(settings != null);

			return new XDocument(
				new XComment($"{Constants.ApplicationName} {Constants.ApplicationVersion} by {Constants.Author}"),
				new XComment($"Website: {Constants.HomepageUrl}"),
				new XElement(
					XmlRootElement,
					new XElement(
						XmlGeneralElement,
						XElementSerializer.ToXml(nameof(settings.LastProcess), settings.LastProcess),
						XElementSerializer.ToXml(nameof(settings.StayOnTop), settings.StayOnTop),
						XElementSerializer.ToXml(nameof(settings.RunAsAdmin), settings.RunAsAdmin),
						XElementSerializer.ToXml(nameof(settings.RandomizeWindowTitle), settings.RandomizeWindowTitle),
						XElementSerializer.ToXml(nameof(settings.Theme), settings.Theme.ToString())
					),
					new XElement(
						XmlDisplayElement,
						XElementSerializer.ToXml(nameof(settings.ShowNodeAddress), settings.ShowNodeAddress),
						XElementSerializer.ToXml(nameof(settings.ShowNodeOffset), settings.ShowNodeOffset),
						XElementSerializer.ToXml(nameof(settings.ShowNodeText), settings.ShowNodeText),
						XElementSerializer.ToXml(nameof(settings.HighlightChangedValues), settings.HighlightChangedValues),
						XElementSerializer.ToXml(nameof(settings.PreserveNodeOffsetsOnResize), settings.PreserveNodeOffsetsOnResize),
						XElementSerializer.ToXml(nameof(settings.ShowCommentFloat), settings.ShowCommentFloat),
						XElementSerializer.ToXml(nameof(settings.ShowCommentInteger), settings.ShowCommentInteger),
						XElementSerializer.ToXml(nameof(settings.ShowCommentPointer), settings.ShowCommentPointer),
						XElementSerializer.ToXml(nameof(settings.ShowCommentRtti), settings.ShowCommentRtti),
						XElementSerializer.ToXml(nameof(settings.ShowCommentSymbol), settings.ShowCommentSymbol),
						XElementSerializer.ToXml(nameof(settings.ShowCommentString), settings.ShowCommentString),
						XElementSerializer.ToXml(nameof(settings.ShowCommentPluginInfo), settings.ShowCommentPluginInfo)
					),
					new XElement(
						XmlColorsElement,
						XElementSerializer.ToXml(nameof(settings.BackgroundColor), settings.BackgroundColor),
						XElementSerializer.ToXml(nameof(settings.SelectedColor), settings.SelectedColor),
						XElementSerializer.ToXml(nameof(settings.HiddenColor), settings.HiddenColor),
						XElementSerializer.ToXml(nameof(settings.OffsetColor), settings.OffsetColor),
						XElementSerializer.ToXml(nameof(settings.AddressColor), settings.AddressColor),
						XElementSerializer.ToXml(nameof(settings.HexColor), settings.HexColor),
						XElementSerializer.ToXml(nameof(settings.TypeColor), settings.TypeColor),
						XElementSerializer.ToXml(nameof(settings.NameColor), settings.NameColor),
						XElementSerializer.ToXml(nameof(settings.ValueColor), settings.ValueColor),
						XElementSerializer.ToXml(nameof(settings.IndexColor), settings.IndexColor),
						XElementSerializer.ToXml(nameof(settings.CommentColor), settings.CommentColor),
						XElementSerializer.ToXml(nameof(settings.TextColor), settings.TextColor),
						XElementSerializer.ToXml(nameof(settings.VTableColor), settings.VTableColor),
						XElementSerializer.ToXml(nameof(settings.PluginColor), settings.PluginColor)
					),
					settings.CustomData.Serialize(XmlCustomDataElement)
				)
			);
		}

		#endregion

		private static void EnsureSettingsDirectoryAvailable()
		{
			try
			{
				if (Directory.Exists(PathUtil.SettingsFolderPath) == false)
				{
					Directory.CreateDirectory(PathUtil.SettingsFolderPath);
				}
			}
			catch
			{
				// ignored
			}
		}
	}
}
