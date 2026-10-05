using System;
using System.Drawing;

namespace ReClassNET.UI.Theme
{
	/// <summary>
	/// The named colors of a theme. All controls of the application are painted with the colors of the current palette
	/// (see <see cref="ThemeManager.Palette"/>).
	/// </summary>
	public sealed class ThemePalette
	{
		/// <summary>The theme this palette belongs to.</summary>
		public AppTheme Theme { get; }

		/// <summary>True if the palette is a dark palette (light text on dark surfaces).</summary>
		public bool IsDark => Theme == AppTheme.Dark;

		/// <summary>Background of forms, tab pages and other plain containers.</summary>
		public Color WindowBackground { get; }

		/// <summary>Background of raised surfaces like menus, tool bars, status bars and headers.</summary>
		public Color Surface { get; }

		/// <summary>Background of input controls (text boxes, combo boxes, numeric fields).</summary>
		public Color ControlBackground { get; }

		/// <summary>Background of content views (tree views, lists, grids, code views).</summary>
		public Color ContentBackground { get; }

		/// <summary>Border color of inputs, buttons and panels.</summary>
		public Color ControlBorder { get; }

		/// <summary>Default text color.</summary>
		public Color Text { get; }

		/// <summary>Text color of disabled controls and items.</summary>
		public Color DisabledText { get; }

		/// <summary>Secondary text, e.g. placeholders and hints.</summary>
		public Color SubtleText { get; }

		/// <summary>The accent color of the edition. Used sparingly for highlights.</summary>
		public Color Accent { get; }

		/// <summary>Text drawn on top of <see cref="Accent"/>.</summary>
		public Color AccentText { get; }

		/// <summary>
		/// Text color for values which need attention (changed values, errors). Readable on the content backgrounds,
		/// unlike <see cref="Accent"/> which is tuned as a fill color.
		/// </summary>
		public Color AttentionText { get; }

		/// <summary>Background of selected rows and items.</summary>
		public Color SelectionBackground { get; }

		/// <summary>Text color of selected rows and items.</summary>
		public Color SelectionForeground { get; }

		/// <summary>Background of selected rows in unfocused controls.</summary>
		public Color InactiveSelectionBackground { get; }

		/// <summary>Background of menu strips, tool strips and status strips.</summary>
		public Color MenuBackground { get; }

		/// <summary>Background of opened menus and drop downs.</summary>
		public Color MenuDropDownBackground { get; }

		/// <summary>Background of the image margin of menus.</summary>
		public Color MenuImageMargin { get; }

		/// <summary>Background of hovered items (menu items, tool bar buttons, list items).</summary>
		public Color Hover { get; }

		/// <summary>Background of pressed items.</summary>
		public Color Pressed { get; }

		/// <summary>Background of checked tool bar buttons.</summary>
		public Color CheckedBackground { get; }

		/// <summary>Border around hovered / checked items.</summary>
		public Color HoverBorder { get; }

		/// <summary>Color of separators between items and the thin lines below tool bars.</summary>
		public Color Separator { get; }

		/// <summary>Color of grid lines in tables.</summary>
		public Color GridLines { get; }

		/// <summary>Background of table and list headers.</summary>
		public Color HeaderBackground { get; }

		/// <summary>Text color of table and list headers.</summary>
		public Color HeaderText { get; }

		/// <summary>Background of every other row in tables.</summary>
		public Color AlternatingRowBackground { get; }

		/// <summary>Color of hyperlinks.</summary>
		public Color Link { get; }

		/// <summary>Background of buttons.</summary>
		public Color ButtonBackground { get; }

		/// <summary>Background of hovered buttons.</summary>
		public Color ButtonHover { get; }

		/// <summary>Background of pressed buttons.</summary>
		public Color ButtonPressed { get; }

		/// <summary>Background of the banner at the top of dialogs.</summary>
		public Color BannerBackground { get; }

		/// <summary>Text color of the banner at the top of dialogs.</summary>
		public Color BannerText { get; }

		/// <summary>Background of tool tips.</summary>
		public Color ToolTipBackground { get; }

		/// <summary>Text color of tool tips.</summary>
		public Color ToolTipText { get; }

		private ThemePalette(
			AppTheme theme,
			Color windowBackground,
			Color surface,
			Color controlBackground,
			Color contentBackground,
			Color controlBorder,
			Color text,
			Color disabledText,
			Color subtleText,
			Color accent,
			Color accentText,
			Color attentionText,
			Color selectionBackground,
			Color selectionForeground,
			Color inactiveSelectionBackground,
			Color menuBackground,
			Color menuDropDownBackground,
			Color menuImageMargin,
			Color hover,
			Color pressed,
			Color checkedBackground,
			Color hoverBorder,
			Color separator,
			Color gridLines,
			Color headerBackground,
			Color headerText,
			Color alternatingRowBackground,
			Color link,
			Color buttonBackground,
			Color buttonHover,
			Color buttonPressed,
			Color bannerBackground,
			Color bannerText,
			Color toolTipBackground,
			Color toolTipText)
		{
			Theme = theme;
			WindowBackground = windowBackground;
			Surface = surface;
			ControlBackground = controlBackground;
			ContentBackground = contentBackground;
			ControlBorder = controlBorder;
			Text = text;
			DisabledText = disabledText;
			SubtleText = subtleText;
			Accent = accent;
			AccentText = accentText;
			AttentionText = attentionText;
			SelectionBackground = selectionBackground;
			SelectionForeground = selectionForeground;
			InactiveSelectionBackground = inactiveSelectionBackground;
			MenuBackground = menuBackground;
			MenuDropDownBackground = menuDropDownBackground;
			MenuImageMargin = menuImageMargin;
			Hover = hover;
			Pressed = pressed;
			CheckedBackground = checkedBackground;
			HoverBorder = hoverBorder;
			Separator = separator;
			GridLines = gridLines;
			HeaderBackground = headerBackground;
			HeaderText = headerText;
			AlternatingRowBackground = alternatingRowBackground;
			Link = link;
			ButtonBackground = buttonBackground;
			ButtonHover = buttonHover;
			ButtonPressed = buttonPressed;
			BannerBackground = bannerBackground;
			BannerText = bannerText;
			ToolTipBackground = toolTipBackground;
			ToolTipText = toolTipText;
		}

		/// <summary>A clean, neutral light palette: white and very light grey surfaces with dark grey text.</summary>
		public static ThemePalette Light { get; } = new ThemePalette(
			AppTheme.Light,
			windowBackground: Rgb(0xF5, 0xF5, 0xF5),
			surface: Rgb(0xEC, 0xEC, 0xEC),
			controlBackground: Rgb(0xFF, 0xFF, 0xFF),
			contentBackground: Rgb(0xFF, 0xFF, 0xFF),
			controlBorder: Rgb(0xC6, 0xC6, 0xC6),
			text: Rgb(0x1E, 0x1E, 0x1E),
			disabledText: Rgb(0x68, 0x68, 0x68),
			subtleText: Rgb(0x75, 0x75, 0x75),
			accent: Rgb(0xC6, 0x28, 0x28),
			accentText: Rgb(0xFF, 0xFF, 0xFF),
			attentionText: Rgb(0xC6, 0x28, 0x28),
			selectionBackground: Rgb(0xF5, 0xD5, 0xD5),
			selectionForeground: Rgb(0x1E, 0x1E, 0x1E),
			inactiveSelectionBackground: Rgb(0xE4, 0xE4, 0xE4),
			menuBackground: Rgb(0xEC, 0xEC, 0xEC),
			menuDropDownBackground: Rgb(0xFA, 0xFA, 0xFA),
			menuImageMargin: Rgb(0xF2, 0xF2, 0xF2),
			hover: Rgb(0xDC, 0xDC, 0xDC),
			pressed: Rgb(0xCF, 0xCF, 0xCF),
			checkedBackground: Rgb(0xD6, 0xD6, 0xD6),
			hoverBorder: Rgb(0xB4, 0xB4, 0xB4),
			separator: Rgb(0xD0, 0xD0, 0xD0),
			gridLines: Rgb(0xE0, 0xE0, 0xE0),
			headerBackground: Rgb(0xEA, 0xEA, 0xEA),
			headerText: Rgb(0x1E, 0x1E, 0x1E),
			alternatingRowBackground: Rgb(0xF7, 0xF7, 0xF7),
			link: Rgb(0xB7, 0x1C, 0x1C),
			buttonBackground: Rgb(0xFA, 0xFA, 0xFA),
			buttonHover: Rgb(0xEA, 0xEA, 0xEA),
			buttonPressed: Rgb(0xD8, 0xD8, 0xD8),
			bannerBackground: Rgb(0xFF, 0xFF, 0xFF),
			bannerText: Rgb(0x1E, 0x1E, 0x1E),
			toolTipBackground: Rgb(0xFF, 0xFF, 0xFF),
			toolTipText: Rgb(0x1E, 0x1E, 0x1E)
		);

		/// <summary>A neutral dark palette with light text and a restrained red accent.</summary>
		public static ThemePalette Dark { get; } = new ThemePalette(
			AppTheme.Dark,
			windowBackground: Rgb(0x25, 0x25, 0x26),
			surface: Rgb(0x2D, 0x2D, 0x30),
			controlBackground: Rgb(0x33, 0x33, 0x37),
			contentBackground: Rgb(0x1E, 0x1E, 0x1E),
			controlBorder: Rgb(0x3F, 0x3F, 0x46),
			text: Rgb(0xDC, 0xDC, 0xDC),
			disabledText: Rgb(0x80, 0x80, 0x80),
			subtleText: Rgb(0xA0, 0xA0, 0xA0),
			accent: Rgb(0xE5, 0x39, 0x35),
			accentText: Rgb(0xFF, 0xFF, 0xFF),
			attentionText: Rgb(0xF1, 0x4C, 0x4C),
			selectionBackground: Rgb(0x5A, 0x2A, 0x2A),
			selectionForeground: Rgb(0xFF, 0xFF, 0xFF),
			inactiveSelectionBackground: Rgb(0x3F, 0x3F, 0x46),
			menuBackground: Rgb(0x2D, 0x2D, 0x30),
			menuDropDownBackground: Rgb(0x1B, 0x1B, 0x1C),
			menuImageMargin: Rgb(0x1B, 0x1B, 0x1C),
			hover: Rgb(0x3E, 0x3E, 0x42),
			pressed: Rgb(0x4A, 0x4A, 0x50),
			checkedBackground: Rgb(0x44, 0x44, 0x4A),
			hoverBorder: Rgb(0x55, 0x55, 0x5C),
			separator: Rgb(0x3F, 0x3F, 0x46),
			gridLines: Rgb(0x3A, 0x3A, 0x3E),
			headerBackground: Rgb(0x2D, 0x2D, 0x30),
			headerText: Rgb(0xDC, 0xDC, 0xDC),
			alternatingRowBackground: Rgb(0x24, 0x24, 0x24),
			link: Rgb(0xF2, 0x8B, 0x82),
			buttonBackground: Rgb(0x33, 0x33, 0x37),
			buttonHover: Rgb(0x3F, 0x3F, 0x46),
			buttonPressed: Rgb(0x50, 0x50, 0x58),
			bannerBackground: Rgb(0x2D, 0x2D, 0x30),
			bannerText: Rgb(0xF1, 0xF1, 0xF1),
			toolTipBackground: Rgb(0x2D, 0x2D, 0x30),
			toolTipText: Rgb(0xF1, 0xF1, 0xF1)
		);

		/// <summary>Gets the palette for the given theme.</summary>
		/// <param name="theme">The theme.</param>
		/// <returns>The palette.</returns>
		public static ThemePalette ForTheme(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;

		/// <summary>Returns true if the color is perceived as dark.</summary>
		/// <param name="color">The color to test.</param>
		/// <returns>True if the color is dark, false otherwise.</returns>
		public static bool IsDarkColor(Color color)
		{
			return GetRelativeLuminance(color) < 0.4;
		}

		/// <summary>Picks a readable text color (black or white) for the given background color.</summary>
		/// <param name="background">The background color.</param>
		/// <returns>A readable text color.</returns>
		public static Color GetContrastText(Color background)
		{
			return IsDarkColor(background) ? Rgb(0xF1, 0xF1, 0xF1) : Rgb(0x1E, 0x1E, 0x1E);
		}

		/// <summary>Calculates the WCAG contrast ratio (1:1 to 21:1) between two opaque colors.</summary>
		/// <param name="a">The first color.</param>
		/// <param name="b">The second color.</param>
		/// <returns>The contrast ratio, a value between 1 and 21.</returns>
		public static double GetContrastRatio(Color a, Color b)
		{
			var la = GetLinearLuminance(a) + 0.05;
			var lb = GetLinearLuminance(b) + 0.05;

			return la > lb ? la / lb : lb / la;
		}

		private static double GetRelativeLuminance(Color color)
		{
			return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
		}

		private static double GetLinearLuminance(Color color)
		{
			return 0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);
		}

		private static double Linearize(byte channel)
		{
			var c = channel / 255.0;

			return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
		}

		private static Color Rgb(int r, int g, int b) => Color.FromArgb(255, r, g, b);
	}
}
