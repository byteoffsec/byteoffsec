using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;
using ReClassNET.CodeGenerator;
using ReClassNET.Extensions;
using ReClassNET.Logger;
using ReClassNET.Nodes;
using ReClassNET.Project;
using ReClassNET.UI;
using ReClassNET.UI.Theme;
using ReClassNET.Util.Rtf;

namespace ReClassNET.Forms
{
	public partial class CodeForm : IconForm
	{
		private readonly string code;
		private readonly ILanguage language;

		public CodeForm(ICodeGenerator generator, IReadOnlyList<ClassNode> classes, IReadOnlyList<EnumDescription> enums, ILogger logger)
		{
			Contract.Requires(generator != null);
			Contract.Requires(classes != null);
			Contract.Requires(enums != null);

			InitializeComponent();

			// The code box carries its own syntax colors. The generic theming must not touch it: setting the fore color
			// of a rich text box recolors all existing text, which would wipe the highlighting after the RTF was streamed in.
			ThemeManager.Exclude(codeRichTextBox);
			codeRichTextBox.HandleCreated += (_, _2) => NativeTheming.ApplyScrollBars(codeRichTextBox, ThemeManager.IsDark);
			ApplyCodeBoxTheme();

			codeRichTextBox.SetInnerMargin(5, 5, 5, 5);

			code = generator.GenerateCode(classes, enums, logger);
			language = generator.Language == Language.Cpp ? Languages.Cpp : Languages.CSharp;

			UpdateCode();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			GlobalWindowManager.AddWindow(this);

			ThemeManager.ThemeChanged += OnThemeChanged;
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			ThemeManager.ThemeChanged -= OnThemeChanged;

			base.OnFormClosed(e);

			GlobalWindowManager.RemoveWindow(this);
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			ApplyCodeBoxTheme();
			UpdateCode();
		}

		/// <summary>Sets the colors of the code box. Must run before the RTF is streamed in (see the constructor).</summary>
		private void ApplyCodeBoxTheme()
		{
			var palette = ThemeManager.Palette;

			codeRichTextBox.BackColor = palette.ContentBackground;
			codeRichTextBox.ForeColor = palette.Text;

			// Excluded controls don't get the system drawn parts themed by ThemeManager.
			NativeTheming.ApplyScrollBars(codeRichTextBox, palette.IsDark);
		}

		/// <summary>Colorizes the code with the style sheet of the current theme.</summary>
		private void UpdateCode()
		{
			var buffer = new StringBuilder(code.Length * 2);
			using (var writer = new StringWriter(buffer))
			{
				new CodeColorizer().Colorize(
					code,
					language,
					new RtfFormatter(ThemeManager.Palette.Text),
					ThemedStyleSheet.ForCurrentTheme(),
					writer
				);
			}

			codeRichTextBox.Rtf = buffer.ToString();
		}
	}

	internal class RtfFormatter : IFormatter
	{
		private readonly RtfBuilder builder = new RtfBuilder(RtfFont.Consolas, 20);
		private readonly Color plainTextColor;

		public RtfFormatter()
			: this(ThemeManager.Palette.Text)
		{

		}

		/// <param name="plainTextColor">The color of text without a style. Emitted explicitly because text without a color falls back to the system window text color (black) which is unreadable on a dark background.</param>
		public RtfFormatter(Color plainTextColor)
		{
			this.plainTextColor = plainTextColor;
		}

		public void Write(string parsedSourceCode, IList<Scope> scopes, IStyleSheet styleSheet, TextWriter textWriter)
		{
			var color = plainTextColor;

			var scopeName = scopes.FirstOrDefault()?.Name;
			if (scopeName != null && styleSheet.Styles.Contains(scopeName))
			{
				var styleColor = styleSheet.Styles[scopeName].Foreground;
				if (!styleColor.IsEmpty)
				{
					color = styleColor;
				}
			}

			builder.SetForeColor(color).Append(parsedSourceCode);
		}

		public void WriteHeader(IStyleSheet styleSheet, ILanguage language, TextWriter textWriter)
		{

		}

		public void WriteFooter(IStyleSheet styleSheet, ILanguage language, TextWriter textWriter)
		{
			textWriter.Write(builder.ToString());
		}
	}

	/// <summary>
	/// A syntax highlighting style sheet for the dark theme. The light theme uses the default style sheet of ColorCode.
	/// </summary>
	internal sealed class ThemedStyleSheet : IStyleSheet
	{
		private static readonly Lazy<IStyleSheet> dark = new Lazy<IStyleSheet>(CreateDark);

		public StyleDictionary Styles { get; }

		private ThemedStyleSheet(StyleDictionary styles)
		{
			Styles = styles;
		}

		public static IStyleSheet ForCurrentTheme()
		{
			return ThemeManager.IsDark ? dark.Value : StyleSheets.Default;
		}

		private static IStyleSheet CreateDark()
		{
			var styles = new StyleDictionary();

			foreach (var style in StyleSheets.Default.Styles)
			{
				styles.Add(new Style(style.ScopeName)
				{
					Foreground = GetDarkForeground(style.ScopeName),
					Background = Color.Empty,
					Bold = style.Bold,
					Italic = style.Italic,
					CssClassName = style.CssClassName
				});
			}

			return new ThemedStyleSheet(styles);
		}

		private static Color GetDarkForeground(string scopeName)
		{
			switch (scopeName)
			{
				case ScopeName.Keyword:
				case ScopeName.ControlKeyword:
				case ScopeName.PseudoKeyword:
				case ScopeName.Predefined:
				case ScopeName.PreprocessorKeyword:
					return Color.FromArgb(0x56, 0x9C, 0xD6);
				case ScopeName.Comment:
				case ScopeName.XmlDocComment:
				case ScopeName.HtmlComment:
				case ScopeName.XmlComment:
					return Color.FromArgb(0x6A, 0x99, 0x55);
				case ScopeName.XmlDocTag:
					return Color.FromArgb(0x80, 0x80, 0x80);
				case ScopeName.String:
				case ScopeName.StringCSharpVerbatim:
				case ScopeName.StringEscape:
					return Color.FromArgb(0xCE, 0x91, 0x78);
				case ScopeName.ClassName:
				case ScopeName.Type:
				case ScopeName.TypeVariable:
				case ScopeName.Constructor:
				case ScopeName.NameSpace:
					return Color.FromArgb(0x4E, 0xC9, 0xB0);
				case ScopeName.Number:
					return Color.FromArgb(0xB5, 0xCE, 0xA8);
				default:
					return ThemePalette.Dark.Text;
			}
		}
	}
}
