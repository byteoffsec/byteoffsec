using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ReClassNET.UI.Theme
{
	/// <summary>
	/// A flat <see cref="ProfessionalColorTable"/> built from the current <see cref="ThemePalette"/>.
	/// All gradients use the same begin, middle and end color.
	/// </summary>
	public class ThemedColorTable : ProfessionalColorTable
	{
		private static ThemePalette P => ThemeManager.Palette;

		public ThemedColorTable()
		{
			UseSystemColors = false;
		}

		public override Color ButtonSelectedHighlight => P.Hover;
		public override Color ButtonSelectedHighlightBorder => P.HoverBorder;
		public override Color ButtonPressedHighlight => P.Pressed;
		public override Color ButtonPressedHighlightBorder => P.HoverBorder;
		public override Color ButtonCheckedHighlight => P.CheckedBackground;
		public override Color ButtonCheckedHighlightBorder => P.HoverBorder;
		public override Color ButtonPressedBorder => P.HoverBorder;
		public override Color ButtonSelectedBorder => P.HoverBorder;
		public override Color ButtonCheckedGradientBegin => P.CheckedBackground;
		public override Color ButtonCheckedGradientMiddle => P.CheckedBackground;
		public override Color ButtonCheckedGradientEnd => P.CheckedBackground;
		public override Color ButtonSelectedGradientBegin => P.Hover;
		public override Color ButtonSelectedGradientMiddle => P.Hover;
		public override Color ButtonSelectedGradientEnd => P.Hover;
		public override Color ButtonPressedGradientBegin => P.Pressed;
		public override Color ButtonPressedGradientMiddle => P.Pressed;
		public override Color ButtonPressedGradientEnd => P.Pressed;
		public override Color CheckBackground => P.CheckedBackground;
		public override Color CheckSelectedBackground => P.Pressed;
		public override Color CheckPressedBackground => P.Pressed;
		public override Color GripDark => P.Separator;
		public override Color GripLight => P.MenuBackground;
		public override Color ImageMarginGradientBegin => P.MenuImageMargin;
		public override Color ImageMarginGradientMiddle => P.MenuImageMargin;
		public override Color ImageMarginGradientEnd => P.MenuImageMargin;
		public override Color ImageMarginRevealedGradientBegin => P.MenuImageMargin;
		public override Color ImageMarginRevealedGradientMiddle => P.MenuImageMargin;
		public override Color ImageMarginRevealedGradientEnd => P.MenuImageMargin;
		public override Color MenuStripGradientBegin => P.MenuBackground;
		public override Color MenuStripGradientEnd => P.MenuBackground;
		public override Color MenuItemSelected => P.Hover;
		public override Color MenuItemBorder => P.Hover;
		public override Color MenuBorder => P.ControlBorder;
		public override Color MenuItemSelectedGradientBegin => P.Hover;
		public override Color MenuItemSelectedGradientEnd => P.Hover;
		public override Color MenuItemPressedGradientBegin => P.MenuDropDownBackground;
		public override Color MenuItemPressedGradientMiddle => P.MenuDropDownBackground;
		public override Color MenuItemPressedGradientEnd => P.MenuDropDownBackground;
		public override Color RaftingContainerGradientBegin => P.MenuBackground;
		public override Color RaftingContainerGradientEnd => P.MenuBackground;
		public override Color SeparatorDark => P.Separator;
		public override Color SeparatorLight => P.MenuBackground;
		public override Color StatusStripGradientBegin => P.MenuBackground;
		public override Color StatusStripGradientEnd => P.MenuBackground;
		public override Color ToolStripBorder => P.MenuBackground;
		public override Color ToolStripDropDownBackground => P.MenuDropDownBackground;
		public override Color ToolStripGradientBegin => P.MenuBackground;
		public override Color ToolStripGradientMiddle => P.MenuBackground;
		public override Color ToolStripGradientEnd => P.MenuBackground;
		public override Color ToolStripContentPanelGradientBegin => P.MenuBackground;
		public override Color ToolStripContentPanelGradientEnd => P.MenuBackground;
		public override Color ToolStripPanelGradientBegin => P.MenuBackground;
		public override Color ToolStripPanelGradientEnd => P.MenuBackground;
		public override Color OverflowButtonGradientBegin => P.MenuBackground;
		public override Color OverflowButtonGradientMiddle => P.MenuBackground;
		public override Color OverflowButtonGradientEnd => P.MenuBackground;
	}

	/// <summary>
	/// A flat renderer for menus, tool bars, status bars, context menus and drop downs which paints everything
	/// with the colors of the current <see cref="ThemePalette"/>. Arrows, check marks, separators and item texts
	/// are rendered manually so they stay readable in the dark theme.
	/// </summary>
	public class ThemedToolStripRenderer : ToolStripProfessionalRenderer
	{
		private static ThemePalette P => ThemeManager.Palette;

		/// <summary>True to draw the grip of tool bars. The default is false for a cleaner look.</summary>
		public bool ShowGrip { get; set; }

		/// <summary>True to draw a thin separator line below tool bars.</summary>
		public bool RenderToolBarSeparatorLine { get; set; } = true;

		public ThemedToolStripRenderer()
			: base(new ThemedColorTable())
		{
			RoundedEdges = false;
		}

		#region Backgrounds and borders

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			var color = GetBackgroundColor(e.ToolStrip);

			using var brush = new SolidBrush(color);

			e.Graphics.FillRectangle(brush, e.AffectedBounds);
		}

		protected override void OnRenderToolStripPanelBackground(ToolStripPanelRenderEventArgs e)
		{
			using var brush = new SolidBrush(P.MenuBackground);

			e.Graphics.FillRectangle(brush, e.ToolStripPanel.ClientRectangle);

			e.Handled = true;
		}

		protected override void OnRenderToolStripContentPanelBackground(ToolStripContentPanelRenderEventArgs e)
		{
			using var brush = new SolidBrush(P.MenuBackground);

			e.Graphics.FillRectangle(brush, e.ToolStripContentPanel.ClientRectangle);

			e.Handled = true;
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			var strip = e.ToolStrip;
			var bounds = new Rectangle(Point.Empty, strip.Size);

			if (strip is ToolStripDropDown)
			{
				using var pen = new Pen(P.ControlBorder);

				e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

				return;
			}

			if (strip is MenuStrip || strip is StatusStrip)
			{
				return;
			}

			if (RenderToolBarSeparatorLine && strip.Orientation == Orientation.Horizontal)
			{
				using var pen = new Pen(P.Separator);

				e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
			}
		}

		protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
		{
			using var brush = new SolidBrush(P.MenuImageMargin);

			e.Graphics.FillRectangle(brush, e.AffectedBounds);
		}

		protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
		{
			if (!ShowGrip)
			{
				return;
			}

			var bounds = e.GripBounds;
			using var brush = new SolidBrush(P.Separator);

			if (e.GripDisplayStyle == ToolStripGripDisplayStyle.Vertical)
			{
				var x = bounds.Left + (bounds.Width / 2) - 1;
				for (var y = bounds.Top + 4; y < bounds.Bottom - 3; y += 3)
				{
					e.Graphics.FillRectangle(brush, x, y, 2, 2);
				}
			}
			else
			{
				var y = bounds.Top + (bounds.Height / 2) - 1;
				for (var x = bounds.Left + 4; x < bounds.Right - 3; x += 3)
				{
					e.Graphics.FillRectangle(brush, x, y, 2, 2);
				}
			}
		}

		protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
		{
			var strip = e.ToolStrip;
			var bounds = new Rectangle(strip.Width - 14, strip.Height - 14, 12, 12);

			using var brush = new SolidBrush(P.Separator);

			for (var i = 0; i < 3; ++i)
			{
				for (var j = 0; j <= i; ++j)
				{
					var x = bounds.Right - 2 - (j * 4);
					var y = bounds.Bottom - 2 - ((i - j) * 4);
					e.Graphics.FillRectangle(brush, x, y, 2, 2);
				}
			}
		}

		#endregion

		#region Items

		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
		{
			var item = e.Item;
			if (!item.Enabled)
			{
				return;
			}

			var bounds = new Rectangle(Point.Empty, item.Size);

			if (item.IsOnDropDown)
			{
				if (item.Selected)
				{
					bounds.Inflate(-1, 0);

					using var brush = new SolidBrush(P.Hover);

					e.Graphics.FillRectangle(brush, bounds);
				}
			}
			else if (item.Pressed)
			{
				using var brush = new SolidBrush(P.Pressed);

				e.Graphics.FillRectangle(brush, bounds);
			}
			else if (item.Selected)
			{
				using var brush = new SolidBrush(P.Hover);

				e.Graphics.FillRectangle(brush, bounds);
			}
		}

		protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
		{
			var button = e.Item as ToolStripButton;
			if (button == null)
			{
				base.OnRenderButtonBackground(e);

				return;
			}

			var bounds = new Rectangle(Point.Empty, button.Size);

			if (button.Pressed && button.Enabled)
			{
				FillItem(e.Graphics, bounds, P.Pressed, P.HoverBorder);
			}
			else if (button.Checked)
			{
				FillItem(e.Graphics, bounds, button.Selected && button.Enabled ? P.Pressed : P.CheckedBackground, P.HoverBorder);
			}
			else if (button.Selected && button.Enabled)
			{
				FillItem(e.Graphics, bounds, P.Hover, P.HoverBorder);
			}
		}

		protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
		{
			var item = e.Item;
			var bounds = new Rectangle(Point.Empty, item.Size);

			if (item.Pressed && item.Enabled)
			{
				FillItem(e.Graphics, bounds, P.Pressed, P.HoverBorder);
			}
			else if (item.Selected && item.Enabled)
			{
				FillItem(e.Graphics, bounds, P.Hover, P.HoverBorder);
			}
		}

		protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
		{
			var item = e.Item as ToolStripSplitButton;
			if (item == null)
			{
				base.OnRenderSplitButtonBackground(e);

				return;
			}

			var bounds = new Rectangle(Point.Empty, item.Size);

			if (item.Enabled && (item.Selected || item.Pressed || item.DropDownButtonPressed))
			{
				FillItem(e.Graphics, bounds, item.Pressed || item.DropDownButtonPressed ? P.Pressed : P.Hover, P.HoverBorder);

				if (item.ButtonPressed)
				{
					using var brush = new SolidBrush(P.Pressed);

					e.Graphics.FillRectangle(brush, Inflate(item.ButtonBounds, -1));
				}

				using var pen = new Pen(P.HoverBorder);

				var x = item.DropDownButtonBounds.Left;
				e.Graphics.DrawLine(pen, x, bounds.Top + 1, x, bounds.Bottom - 2);
			}

			DrawArrow(new ToolStripArrowRenderEventArgs(e.Graphics, item, item.DropDownButtonBounds, GetTextColor(item), ArrowDirection.Down));
		}

		protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e)
		{
			var item = e.Item;
			var bounds = new Rectangle(Point.Empty, item.Size);

			if (item.Enabled && (item.Selected || item.Pressed))
			{
				FillItem(e.Graphics, bounds, item.Pressed ? P.Pressed : P.Hover, P.HoverBorder);
			}

			var arrowBounds = new Rectangle(bounds.Left, bounds.Bottom - 10, bounds.Width, 8);

			DrawArrow(new ToolStripArrowRenderEventArgs(e.Graphics, item, arrowBounds, GetTextColor(item), ArrowDirection.Down));
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
		{
			var item = e.Item;
			var bounds = new Rectangle(Point.Empty, item.Size);

			using var pen = new Pen(P.Separator);

			if (e.Vertical)
			{
				var x = bounds.Left + (bounds.Width / 2);
				e.Graphics.DrawLine(pen, x, bounds.Top + 3, x, bounds.Bottom - 4);
			}
			else
			{
				var y = bounds.Top + (bounds.Height / 2);
				var left = bounds.Left;

				if (item.IsOnDropDown && e.ToolStrip is ToolStripDropDownMenu menu && menu.ShowImageMargin)
				{
					left += menu.ImageScalingSize.Width + 8;
				}

				e.Graphics.DrawLine(pen, left, y, bounds.Right - 1, y);
			}
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			e.TextColor = GetTextColor(e.Item);

			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = GetTextColor(e.Item);

			base.OnRenderArrow(e);
		}

		protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
		{
			var bounds = e.ImageRectangle;
			if (bounds.Width <= 0 || bounds.Height <= 0)
			{
				return;
			}

			var box = Inflate(bounds, -1);

			using (var brush = new SolidBrush(P.CheckedBackground))
			{
				e.Graphics.FillRectangle(brush, box);
			}
			using (var pen = new Pen(P.HoverBorder))
			{
				e.Graphics.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
			}

			var color = GetTextColor(e.Item);
			using (var pen = new Pen(color, 2.0f))
			{
				var oldMode = e.Graphics.SmoothingMode;
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

				var w = box.Width;
				var h = box.Height;
				var x = box.X;
				var y = box.Y;

				e.Graphics.DrawLines(pen, new[]
				{
					new PointF(x + (w * 0.25f), y + (h * 0.52f)),
					new PointF(x + (w * 0.43f), y + (h * 0.70f)),
					new PointF(x + (w * 0.76f), y + (h * 0.30f))
				});

				e.Graphics.SmoothingMode = oldMode;
			}
		}

		#endregion

		/// <summary>Gets the background color used for the given strip.</summary>
		/// <param name="strip">The strip.</param>
		/// <returns>The background color.</returns>
		public static Color GetBackgroundColor(ToolStrip strip)
		{
			return strip is ToolStripDropDown ? P.MenuDropDownBackground : P.MenuBackground;
		}

		/// <summary>Gets the text color used for the given item.</summary>
		/// <param name="item">The item.</param>
		/// <returns>The text color.</returns>
		public static Color GetTextColor(ToolStripItem item)
		{
			return item != null && item.Enabled ? P.Text : P.DisabledText;
		}

		private static void FillItem(Graphics g, Rectangle bounds, Color fill, Color border)
		{
			using (var brush = new SolidBrush(fill))
			{
				g.FillRectangle(brush, bounds);
			}
			using (var pen = new Pen(border))
			{
				g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
			}
		}

		private static Rectangle Inflate(Rectangle rect, int amount)
		{
			rect.Inflate(amount, amount);

			return rect;
		}
	}
}
