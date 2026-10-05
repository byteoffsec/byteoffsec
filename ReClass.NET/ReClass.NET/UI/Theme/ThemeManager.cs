using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReClassNET.Controls;
using ReClassNET.Native;

namespace ReClassNET.UI.Theme
{
	/// <summary>
	/// Applies the current <see cref="ThemePalette"/> to the controls of the application.
	/// <see cref="Apply(Control)"/> walks a control tree and themes every known control kind. The call is idempotent
	/// and containers are observed, so controls added later (plugin items, dynamically created buttons) get themed too.
	/// </summary>
	public static class ThemeManager
	{
		private static readonly HashSet<Form> attachedForms = new HashSet<Form>();
		private static readonly HashSet<Control> excludedControls = new HashSet<Control>();
		private static readonly HashSet<Control> ownerDrawnByTheme = new HashSet<Control>();

		private static readonly ControlEventHandler controlAddedHandler = OnControlAdded;
		private static readonly EventHandler handleCreatedHandler = OnHandleCreated;
		private static readonly EventHandler excludedDisposedHandler = OnExcludedDisposed;
		private static readonly EventHandler ownerDrawnDisposedHandler = OnOwnerDrawnDisposed;
		private static readonly ToolStripItemEventHandler itemAddedHandler = OnToolStripItemAdded;
		private static readonly EventHandler dropDownOpeningHandler = OnDropDownOpening;
		private static readonly PaintEventHandler groupBoxPaintHandler = OnGroupBoxPaint;
		private static readonly PaintEventHandler buttonPaintHandler = OnButtonPaint;
		private static readonly PaintEventHandler checkBoxPaintHandler = OnCheckBoxPaint;
		private static readonly PaintEventHandler radioButtonPaintHandler = OnRadioButtonPaint;
		private static readonly PaintEventHandler labelPaintHandler = OnLabelPaint;
		private static readonly DrawItemEventHandler tabControlDrawItemHandler = OnTabControlDrawItem;
		private static readonly DrawItemEventHandler listBoxDrawItemHandler = OnListBoxDrawItem;
		private static readonly DrawItemEventHandler comboBoxDrawItemHandler = OnComboBoxDrawItem;
		private static readonly DrawTreeNodeEventHandler treeViewDrawNodeHandler = OnTreeViewDrawNode;
		private static readonly DrawToolTipEventHandler toolTipDrawHandler = OnToolTipDraw;

		/// <summary>The active theme.</summary>
		public static AppTheme Current { get; private set; } = AppTheme.Dark;

		/// <summary>The palette of the active theme.</summary>
		public static ThemePalette Palette { get; private set; } = ThemePalette.Dark;

		/// <summary>True if the active theme is the dark theme.</summary>
		public static bool IsDark => Palette.IsDark;

		/// <summary>Raised after the theme was switched. All attached forms are re-themed before the event is raised.</summary>
		public static event EventHandler ThemeChanged;

		/// <summary>Sets the theme without notifying anybody. Call this once at startup before the first form is created.</summary>
		/// <param name="theme">The theme to use.</param>
		public static void Initialize(AppTheme theme)
		{
			Current = theme;
			Palette = ThemePalette.ForTheme(theme);

			ToolStripManager.Renderer = new ThemedToolStripRenderer();
		}

		/// <summary>Switches the theme and re-themes all attached forms.</summary>
		/// <param name="theme">The new theme.</param>
		public static void SetTheme(AppTheme theme)
		{
			if (theme == Current && ToolStripManager.Renderer is ThemedToolStripRenderer)
			{
				return;
			}

			Initialize(theme);

			foreach (var form in attachedForms.ToList())
			{
				if (form.IsDisposed)
				{
					attachedForms.Remove(form);

					continue;
				}

				Apply(form);

				form.Invalidate(true);
				form.Update();
			}

			ThemeChanged?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>Themes the form and keeps it themed when the theme is switched later.</summary>
		/// <param name="form">The form.</param>
		public static void Attach(Form form)
		{
			Contract.Requires(form != null);

			if (attachedForms.Add(form))
			{
				form.Disposed += OnAttachedFormDisposed;
			}

			Apply(form);
		}

		/// <summary>Stops re-theming the form.</summary>
		/// <param name="form">The form.</param>
		public static void Detach(Form form)
		{
			Contract.Requires(form != null);

			if (attachedForms.Remove(form))
			{
				form.Disposed -= OnAttachedFormDisposed;
			}
		}

		/// <summary>
		/// Excludes a control and all its children (including children added later) from theming. Useful for plugin
		/// controls which paint themselves. Hooks installed by an earlier <see cref="Apply(Control)"/> are removed,
		/// colors which were already applied are not reverted, so exclude controls before the form is shown.
		/// </summary>
		/// <param name="control">The control.</param>
		public static void Exclude(Control control)
		{
			Contract.Requires(control != null);

			if (excludedControls.Add(control))
			{
				control.Disposed += excludedDisposedHandler;
			}

			Unhook(control);
		}

		/// <summary>Removes an exclusion made with <see cref="Exclude"/> and themes the control again.</summary>
		/// <param name="control">The control.</param>
		public static void Include(Control control)
		{
			Contract.Requires(control != null);

			if (excludedControls.Remove(control))
			{
				control.Disposed -= excludedDisposedHandler;
			}

			Apply(control);
		}

		/// <summary>Checks if the control or one of its ancestors was excluded from theming.</summary>
		/// <param name="control">The control.</param>
		/// <returns>True if the control is excluded.</returns>
		public static bool IsExcluded(Control control)
		{
			if (excludedControls.Count == 0)
			{
				return false;
			}

			for (var current = control; current != null; current = current.Parent)
			{
				if (excludedControls.Contains(current))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Applies the current theme to the control and all its children.</summary>
		/// <param name="root">The root of the control tree.</param>
		public static void Apply(Control root)
		{
			if (root == null)
			{
				return;
			}

			ApplyControl(root, new HashSet<Control>());
		}

		/// <summary>Makes the tool tip draw itself with the colors of the current theme.</summary>
		/// <param name="toolTip">The tool tip.</param>
		public static void Apply(ToolTip toolTip)
		{
			if (toolTip == null)
			{
				return;
			}

			toolTip.OwnerDraw = true;
			toolTip.Draw -= toolTipDrawHandler;
			toolTip.Draw += toolTipDrawHandler;
		}

		#region Control tree

		private static void ApplyControl(Control control, HashSet<Control> visited)
		{
			if (control == null || control.IsDisposed || !visited.Add(control) || IsExcluded(control))
			{
				return;
			}

			var p = Palette;

			switch (control)
			{
				case Form form:
					form.BackColor = p.WindowBackground;
					form.ForeColor = p.Text;
					NativeTheming.ApplyWindowFrame(form, p.IsDark);
					break;

				case ToolStrip strip:
					ApplyToolStrip(strip, visited);
					break;

				case ToolStripPanel toolStripPanel:
					toolStripPanel.BackColor = p.MenuBackground;
					toolStripPanel.ForeColor = p.Text;
					if (toolStripPanel.RenderMode != ToolStripRenderMode.Custom && toolStripPanel.RenderMode != ToolStripRenderMode.ManagerRenderMode)
					{
						toolStripPanel.RenderMode = ToolStripRenderMode.ManagerRenderMode;
					}
					break;

				case ToolStripContentPanel contentPanel:
					contentPanel.BackColor = p.MenuBackground;
					contentPanel.ForeColor = p.Text;
					if (contentPanel.RenderMode != ToolStripRenderMode.Custom && contentPanel.RenderMode != ToolStripRenderMode.ManagerRenderMode)
					{
						contentPanel.RenderMode = ToolStripRenderMode.ManagerRenderMode;
					}
					break;

				case HotSpotTextBox _:
					// Managed by the memory view, it uses the node colors.
					return;

				case MemoryViewControl memoryView:
					memoryView.BackColor = p.ContentBackground;
					NativeTheming.ApplyScrollBars(memoryView, p.IsDark);
					ApplyControl(memoryView.NodeContextMenuStrip, visited);
					break;

				case ProjectView projectView:
					projectView.BackColor = p.ContentBackground;
					ApplyControl(projectView.ClassContextMenuStrip, visited);
					ApplyControl(projectView.ClassesContextMenuStrip, visited);
					ApplyControl(projectView.EnumContextMenuStrip, visited);
					ApplyControl(projectView.EnumsContextMenuStrip, visited);
					break;

				case ColorBox colorBox:
					colorBox.BackColor = GetContainerBackground(colorBox.Parent);
					colorBox.ForeColor = p.Text;
					foreach (var textBox in colorBox.Controls.OfType<TextBoxBase>())
					{
						ApplyTextBox(textBox);
					}
					HookContainer(colorBox);
					return;

				case BannerBox bannerBox:
					bannerBox.BackColor = p.BannerBackground;
					bannerBox.RefreshBanner();
					break;

				case IconButton iconButton:
					iconButton.BackColor = GetContainerBackground(iconButton.Parent);
					break;

				case PlaceholderTextBox placeholderTextBox:
					ApplyTextBox(placeholderTextBox);
					placeholderTextBox.PlaceholderColor = p.SubtleText;
					break;

				case TextBoxBase textBox:
					ApplyTextBox(textBox);
					break;

				case ComboBox comboBox:
					ApplyComboBox(comboBox);
					break;

				case NumericUpDown numericUpDown:
					numericUpDown.BorderStyle = BorderStyle.FixedSingle;
					numericUpDown.BackColor = p.ControlBackground;
					numericUpDown.ForeColor = p.Text;
					break;

				case ListBox listBox:
					listBox.BackColor = p.ContentBackground;
					listBox.ForeColor = p.Text;
					if (listBox.BorderStyle == BorderStyle.Fixed3D)
					{
						listBox.BorderStyle = BorderStyle.FixedSingle;
					}
					if (!(listBox is CheckedListBox) && ClaimOwnerDraw(listBox, listBox.DrawMode == DrawMode.Normal))
					{
						// Selected items would use the system highlight colors otherwise.
						listBox.DrawMode = DrawMode.OwnerDrawFixed;
						listBox.DrawItem -= listBoxDrawItemHandler;
						listBox.DrawItem += listBoxDrawItemHandler;
					}
					NativeTheming.ApplyScrollBars(listBox, p.IsDark);
					break;

				case TreeView treeView:
					treeView.BackColor = p.ContentBackground;
					treeView.ForeColor = p.Text;
					treeView.LineColor = p.Separator;
					if (treeView.BorderStyle == BorderStyle.Fixed3D)
					{
						treeView.BorderStyle = BorderStyle.FixedSingle;
					}
					if (ClaimOwnerDraw(treeView, treeView.DrawMode == TreeViewDrawMode.Normal))
					{
						// Selected nodes (especially in unfocused trees) would use the system highlight colors otherwise.
						treeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
						treeView.DrawNode -= treeViewDrawNodeHandler;
						treeView.DrawNode += treeViewDrawNodeHandler;
					}
					NativeTheming.ApplyScrollBars(treeView, p.IsDark);
					break;

				case DataGridView dataGridView:
					ApplyDataGridView(dataGridView);
					break;

				case Button button:
					ApplyButton(button);
					break;

				case CheckBox checkBox:
					checkBox.ForeColor = p.Text;
					checkBox.UseVisualStyleBackColor = false;
					checkBox.BackColor = GetContainerBackground(checkBox.Parent);
					checkBox.FlatStyle = p.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
					checkBox.Paint -= checkBoxPaintHandler;
					checkBox.Paint += checkBoxPaintHandler;
					break;

				case RadioButton radioButton:
					radioButton.ForeColor = p.Text;
					radioButton.UseVisualStyleBackColor = false;
					radioButton.BackColor = GetContainerBackground(radioButton.Parent);
					radioButton.FlatStyle = p.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
					radioButton.Paint -= radioButtonPaintHandler;
					radioButton.Paint += radioButtonPaintHandler;
					break;

				case LinkLabel linkLabel:
					linkLabel.ForeColor = p.Text;
					linkLabel.LinkColor = p.Link;
					linkLabel.ActiveLinkColor = p.Accent;
					linkLabel.VisitedLinkColor = p.Link;
					linkLabel.DisabledLinkColor = p.DisabledText;
					break;

				case Label label:
					label.ForeColor = p.Text;
					label.Paint -= labelPaintHandler;
					label.Paint += labelPaintHandler;
					break;

				case GroupBox groupBox:
					groupBox.ForeColor = p.Text;
					groupBox.BackColor = GetContainerBackground(groupBox.Parent);
					groupBox.Paint -= groupBoxPaintHandler;
					groupBox.Paint += groupBoxPaintHandler;
					break;

				case TabControl tabControl:
					if (ClaimOwnerDraw(tabControl, tabControl.DrawMode == TabDrawMode.Normal))
					{
						tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
						tabControl.DrawItem -= tabControlDrawItemHandler;
						tabControl.DrawItem += tabControlDrawItemHandler;
					}
					break;

				case TabPage tabPage:
					tabPage.UseVisualStyleBackColor = false;
					tabPage.BackColor = p.WindowBackground;
					tabPage.ForeColor = p.Text;
					tabPage.BorderStyle = BorderStyle.None;
					break;

				case SplitContainer splitContainer:
					splitContainer.BackColor = p.WindowBackground;
					splitContainer.ForeColor = p.Text;
					break;

				case ProgressBar progressBar:
					// Ignored by the system drawn bar on Windows, used by Mono.
					progressBar.BackColor = p.ControlBackground;
					progressBar.ForeColor = p.Accent;
					break;

				case ScrollBar scrollBar:
					// The scroll bars of a DataGridView (and of plugin controls) are separate child controls.
					NativeTheming.ApplyScrollBars(scrollBar, p.IsDark);
					break;

				case ScrollableControl scrollable:
					// Panel, TableLayoutPanel, FlowLayoutPanel, SplitterPanel, UserControl, ...
					scrollable.BackColor = p.WindowBackground;
					scrollable.ForeColor = p.Text;
					if (scrollable is Panel panel && panel.BorderStyle == BorderStyle.Fixed3D)
					{
						panel.BorderStyle = BorderStyle.FixedSingle;
					}
					if (scrollable.AutoScroll)
					{
						NativeTheming.ApplyScrollBars(scrollable, p.IsDark);
					}
					break;

				default:
					control.ForeColor = p.Text;
					break;
			}

			AttachOverlays(control);

			if (control.ContextMenuStrip != null)
			{
				ApplyControl(control.ContextMenuStrip, visited);
			}

			foreach (Control child in control.Controls)
			{
				ApplyControl(child, visited);
			}

			HookContainer(control);
		}

		private static void HookContainer(Control control)
		{
			control.ControlAdded -= controlAddedHandler;
			control.ControlAdded += controlAddedHandler;

			control.HandleCreated -= handleCreatedHandler;
			control.HandleCreated += handleCreatedHandler;
		}

		/// <summary>Removes every hook and handler the theme installed on the control and its children.</summary>
		private static void Unhook(Control control)
		{
			control.ControlAdded -= controlAddedHandler;
			control.HandleCreated -= handleCreatedHandler;

			control.Paint -= groupBoxPaintHandler;
			control.Paint -= buttonPaintHandler;
			control.Paint -= checkBoxPaintHandler;
			control.Paint -= radioButtonPaintHandler;
			control.Paint -= labelPaintHandler;

			switch (control)
			{
				case ToolStrip strip:
					strip.ItemAdded -= itemAddedHandler;
					foreach (var dropDownItem in strip.Items.OfType<ToolStripDropDownItem>())
					{
						dropDownItem.DropDownOpening -= dropDownOpeningHandler;
					}
					break;
				case ListBox listBox:
					listBox.DrawItem -= listBoxDrawItemHandler;
					break;
				case ComboBox comboBox:
					comboBox.DrawItem -= comboBoxDrawItemHandler;
					break;
				case TreeView treeView:
					treeView.DrawNode -= treeViewDrawNodeHandler;
					break;
				case TabControl tabControl:
					tabControl.DrawItem -= tabControlDrawItemHandler;
					break;
			}

			ControlOverlay.Detach(control);

			foreach (Control child in control.Controls)
			{
				Unhook(child);
			}
		}

		/// <summary>
		/// Remembers that the theme switched the control to owner drawing. Returns false if the control is owner drawn
		/// by someone else (a plugin), in which case the theme leaves its painting alone.
		/// </summary>
		/// <param name="control">The control.</param>
		/// <param name="isStockDrawMode">True if the control still uses the default draw mode.</param>
		/// <returns>True if the theme owns the drawing of the control.</returns>
		private static bool ClaimOwnerDraw(Control control, bool isStockDrawMode)
		{
			if (ownerDrawnByTheme.Contains(control))
			{
				return true;
			}

			if (!isStockDrawMode)
			{
				return false;
			}

			ownerDrawnByTheme.Add(control);
			control.Disposed += ownerDrawnDisposedHandler;

			return true;
		}

		private static bool IsOwnerDrawnByTheme(Control control)
		{
			return control != null && ownerDrawnByTheme.Contains(control);
		}

		private static void AttachOverlays(Control control)
		{
			switch (control)
			{
				case TabControl tabControl when IsOwnerDrawnByTheme(tabControl):
					TabControlOverlay.Attach(tabControl);
					break;
				case ComboBox comboBox:
					ComboBoxOverlay.Attach(comboBox);
					break;
				case DataGridView grid when grid.BorderStyle == BorderStyle.FixedSingle:
					BorderOverlay.Attach(grid, true);
					break;
				case TextBoxBase textBox when textBox.BorderStyle == BorderStyle.FixedSingle:
				case ListBox listBox when listBox.BorderStyle == BorderStyle.FixedSingle:
				case TreeView treeView when treeView.BorderStyle == BorderStyle.FixedSingle:
				case NumericUpDown numericUpDown when numericUpDown.BorderStyle == BorderStyle.FixedSingle:
				case Panel panel when panel.BorderStyle == BorderStyle.FixedSingle:
					BorderOverlay.Attach(control, false);
					break;
			}
		}

		private static void OnControlAdded(object sender, ControlEventArgs e)
		{
			Apply(e.Control);
		}

		private static void OnHandleCreated(object sender, EventArgs e)
		{
			// Native tweaks need a window handle. Controls themed before their handle existed get them now.
			var p = Palette;

			switch (sender)
			{
				case Form form:
					NativeTheming.ApplyWindowFrame(form, p.IsDark);
					break;
				case ComboBox comboBox:
					NativeTheming.ApplyComboBox(comboBox, p.IsDark);
					break;
				case TreeView _:
				case ListBox _:
				case TextBoxBase _:
				case ScrollBar _:
				case MemoryViewControl _:
					NativeTheming.ApplyScrollBars((Control)sender, p.IsDark);
					break;
				case ScrollableControl scrollable when scrollable.AutoScroll:
					NativeTheming.ApplyScrollBars(scrollable, p.IsDark);
					break;
			}

			if (sender is Control control && !IsExcluded(control))
			{
				AttachOverlays(control);
			}
		}

		private static void OnAttachedFormDisposed(object sender, EventArgs e)
		{
			if (sender is Form form)
			{
				Detach(form);
			}
		}

		private static void OnExcludedDisposed(object sender, EventArgs e)
		{
			if (sender is Control control)
			{
				excludedControls.Remove(control);
			}
		}

		private static void OnOwnerDrawnDisposed(object sender, EventArgs e)
		{
			if (sender is Control control)
			{
				ownerDrawnByTheme.Remove(control);
			}
		}

		private static Color GetContainerBackground(Control parent)
		{
			var p = Palette;

			switch (parent)
			{
				case null:
					return p.WindowBackground;
				case ToolStrip _:
				case ToolStripPanel _:
					return p.MenuBackground;
				case BannerBox _:
					return p.BannerBackground;
				case GroupBox _:
				case TabPage _:
				case Form _:
				case ScrollableControl _:
					return parent.BackColor;
				default:
					return p.WindowBackground;
			}
		}

		#endregion

		#region Tool strips

		private static void ApplyToolStrip(ToolStrip strip, HashSet<Control> visited)
		{
			var p = Palette;

			strip.BackColor = ThemedToolStripRenderer.GetBackgroundColor(strip);
			strip.ForeColor = p.Text;

			if (strip.RenderMode == ToolStripRenderMode.Custom)
			{
				// Keep custom renderers (plugins), ours already follow the palette.
			}
			else if (strip.RenderMode != ToolStripRenderMode.ManagerRenderMode)
			{
				strip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
			}

			if (!(strip is MenuStrip) && !(strip is StatusStrip) && !(strip is ToolStripDropDown))
			{
				strip.GripStyle = ToolStripGripStyle.Hidden;
			}

			foreach (ToolStripItem item in strip.Items)
			{
				ApplyToolStripItem(item, visited);
			}

			strip.ItemAdded -= itemAddedHandler;
			strip.ItemAdded += itemAddedHandler;
		}

		private static void ApplyToolStripItem(ToolStripItem item, HashSet<Control> visited)
		{
			switch (item)
			{
				case ToolStripControlHost host when host.Control != null:
					ApplyControl(host.Control, visited);
					break;
				case ToolStripDropDownItem dropDownItem:
					if (dropDownItem.HasDropDownItems)
					{
						ApplyControl(dropDownItem.DropDown, visited);
					}
					// Drop downs which are filled later (plugin menus) are themed when they open.
					dropDownItem.DropDownOpening -= dropDownOpeningHandler;
					dropDownItem.DropDownOpening += dropDownOpeningHandler;
					break;
			}
		}

		private static void OnToolStripItemAdded(object sender, ToolStripItemEventArgs e)
		{
			ApplyToolStripItem(e.Item, new HashSet<Control>());
		}

		private static void OnDropDownOpening(object sender, EventArgs e)
		{
			if (sender is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
			{
				Apply(dropDownItem.DropDown);
			}
		}

		#endregion

		#region Specific controls

		private static void ApplyTextBox(TextBoxBase textBox)
		{
			var p = Palette;

			textBox.BackColor = textBox is RichTextBox ? p.ContentBackground : p.ControlBackground;
			textBox.ForeColor = p.Text;
			if (textBox.BorderStyle == BorderStyle.Fixed3D)
			{
				textBox.BorderStyle = BorderStyle.FixedSingle;
			}

			NativeTheming.ApplyScrollBars(textBox, p.IsDark);
		}

		private static void ApplyComboBox(ComboBox comboBox)
		{
			var p = Palette;

			comboBox.FlatStyle = FlatStyle.Flat;
			comboBox.BackColor = p.ControlBackground;
			comboBox.ForeColor = p.Text;

			if (ClaimOwnerDraw(comboBox, comboBox.DrawMode == DrawMode.Normal))
			{
				// The selected item and the focused edit area would use the system highlight colors otherwise.
				comboBox.DrawMode = DrawMode.OwnerDrawFixed;
				comboBox.DrawItem -= comboBoxDrawItemHandler;
				comboBox.DrawItem += comboBoxDrawItemHandler;
			}

			NativeTheming.ApplyComboBox(comboBox, p.IsDark);
		}

		private static void ApplyButton(Button button)
		{
			var p = Palette;

			button.FlatStyle = FlatStyle.Flat;
			button.FlatAppearance.BorderSize = 1;
			button.FlatAppearance.BorderColor = p.ControlBorder;
			button.FlatAppearance.MouseOverBackColor = p.ButtonHover;
			button.FlatAppearance.MouseDownBackColor = p.ButtonPressed;
			button.FlatAppearance.CheckedBackColor = p.CheckedBackground;
			button.UseVisualStyleBackColor = false;
			button.BackColor = p.ButtonBackground;
			button.ForeColor = p.Text;

			button.Paint -= buttonPaintHandler;
			button.Paint += buttonPaintHandler;
		}

		private static void ApplyDataGridView(DataGridView grid)
		{
			var p = Palette;

			grid.BackColor = p.ContentBackground;
			grid.ForeColor = p.Text;
			grid.BackgroundColor = p.ContentBackground;
			grid.GridColor = p.GridLines;
			grid.EnableHeadersVisualStyles = false;
			if (grid.ColumnHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised)
			{
				grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			}
			if (grid.RowHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised)
			{
				grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			}

			ApplyHeaderStyle(grid.ColumnHeadersDefaultCellStyle);
			ApplyHeaderStyle(grid.RowHeadersDefaultCellStyle);

			var cellStyle = grid.DefaultCellStyle;
			cellStyle.BackColor = p.ContentBackground;
			cellStyle.ForeColor = p.Text;
			cellStyle.SelectionBackColor = p.SelectionBackground;
			cellStyle.SelectionForeColor = p.SelectionForeground;

			grid.RowsDefaultCellStyle.BackColor = p.ContentBackground;
			grid.AlternatingRowsDefaultCellStyle.BackColor = p.AlternatingRowBackground;

			// The scroll bars are child controls of the grid, they are themed by the control walk (see the ScrollBar case).
		}

		private static void ApplyHeaderStyle(DataGridViewCellStyle style)
		{
			var p = Palette;

			style.BackColor = p.HeaderBackground;
			style.ForeColor = p.HeaderText;
			style.SelectionBackColor = p.HeaderBackground;
			style.SelectionForeColor = p.HeaderText;
		}

		#endregion

		#region Owner painting

		private static void OnGroupBoxPaint(object sender, PaintEventArgs e)
		{
			if (!(sender is GroupBox groupBox))
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var bounds = groupBox.ClientRectangle;

			var textSize = TextRenderer.MeasureText(g, groupBox.Text, groupBox.Font, Size.Empty, TextFormatFlags.HidePrefix | TextFormatFlags.SingleLine);
			var textHeight = string.IsNullOrEmpty(groupBox.Text) ? groupBox.Font.Height : textSize.Height;

			using (var brush = new SolidBrush(groupBox.BackColor))
			{
				g.FillRectangle(brush, bounds);
			}

			var frame = new Rectangle(bounds.X, bounds.Y + (textHeight / 2), bounds.Width - 1, bounds.Height - (textHeight / 2) - 1);

			using (var pen = new Pen(p.ControlBorder))
			{
				g.DrawRectangle(pen, frame);
			}

			if (!string.IsNullOrEmpty(groupBox.Text))
			{
				var textRect = new Rectangle(bounds.X + 8, bounds.Y, Math.Min(textSize.Width + 4, bounds.Width - 16), textHeight);

				using (var brush = new SolidBrush(groupBox.BackColor))
				{
					g.FillRectangle(brush, textRect);
				}

				var color = groupBox.Enabled ? groupBox.ForeColor : p.DisabledText;
				TextRenderer.DrawText(g, groupBox.Text, groupBox.Font, textRect, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix);
			}
		}

		private static void OnButtonPaint(object sender, PaintEventArgs e)
		{
			if (!(sender is Button button) || button.Enabled || !Palette.IsDark)
			{
				return;
			}

			// The stock painting derives the disabled text color from the (dark) background which makes it unreadable.
			var p = Palette;
			var g = e.Graphics;
			var bounds = button.ClientRectangle;

			using (var brush = new SolidBrush(p.ButtonBackground))
			{
				g.FillRectangle(brush, bounds);
			}
			using (var pen = new Pen(p.ControlBorder))
			{
				g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
			}

			var textRect = bounds;
			if (button.Image != null)
			{
				var image = button.Image;
				var imageRect = new Rectangle(bounds.X + 4, bounds.Y + ((bounds.Height - image.Height) / 2), image.Width, image.Height);
				if (button.TextImageRelation == TextImageRelation.Overlay || string.IsNullOrEmpty(button.Text))
				{
					imageRect.X = bounds.X + ((bounds.Width - image.Width) / 2);
				}
				else
				{
					textRect = new Rectangle(imageRect.Right + 2, bounds.Y, bounds.Width - imageRect.Right - 2, bounds.Height);
				}

				using var disabledImage = ToolStripRenderer.CreateDisabledImage(image);

				g.DrawImage(disabledImage, imageRect);
			}

			TextRenderer.DrawText(g, button.Text, button.Font, textRect, p.DisabledText, GetTextFormatFlags(button.TextAlign));
		}

		private static void OnCheckBoxPaint(object sender, PaintEventArgs e)
		{
			if (!(sender is CheckBox checkBox) || !Palette.IsDark || checkBox.Appearance != Appearance.Normal || checkBox.CheckAlign != ContentAlignment.MiddleLeft)
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var bounds = checkBox.ClientRectangle;

			using (var brush = new SolidBrush(checkBox.BackColor))
			{
				g.FillRectangle(brush, bounds);
			}

			var boxSize = DpiUtil.ScaleIntX(13);
			var box = new Rectangle(bounds.X, bounds.Y + ((bounds.Height - boxSize) / 2), boxSize, boxSize);
			var enabled = checkBox.Enabled;
			var foreColor = enabled ? checkBox.ForeColor : p.DisabledText;

			using (var brush = new SolidBrush(enabled ? p.ControlBackground : checkBox.BackColor))
			{
				g.FillRectangle(brush, box);
			}
			using (var pen = new Pen(enabled ? p.SubtleText : p.DisabledText))
			{
				g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
			}

			if (checkBox.CheckState == CheckState.Indeterminate)
			{
				using var brush = new SolidBrush(foreColor);

				var inset = Math.Max(2, boxSize / 4);
				g.FillRectangle(brush, box.X + inset, box.Y + inset, box.Width - (2 * inset), box.Height - (2 * inset));
			}
			else if (checkBox.Checked)
			{
				DrawCheckMark(g, box, foreColor);
			}

			var gap = DpiUtil.ScaleIntX(3);
			var textRect = new Rectangle(box.Right + gap, bounds.Y, bounds.Width - box.Right - gap, bounds.Height);
			TextRenderer.DrawText(g, checkBox.Text, checkBox.Font, textRect, foreColor, GetTextFormatFlags(checkBox.TextAlign));

			if (checkBox.Focused)
			{
				ControlPaint.DrawFocusRectangle(g, textRect, foreColor, checkBox.BackColor);
			}
		}

		private static void OnRadioButtonPaint(object sender, PaintEventArgs e)
		{
			if (!(sender is RadioButton radioButton) || !Palette.IsDark || radioButton.Appearance != Appearance.Normal || radioButton.CheckAlign != ContentAlignment.MiddleLeft)
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var bounds = radioButton.ClientRectangle;

			using (var brush = new SolidBrush(radioButton.BackColor))
			{
				g.FillRectangle(brush, bounds);
			}

			var circleSize = DpiUtil.ScaleIntX(12);
			var circle = new Rectangle(bounds.X, bounds.Y + ((bounds.Height - circleSize) / 2), circleSize, circleSize);
			var enabled = radioButton.Enabled;
			var foreColor = enabled ? radioButton.ForeColor : p.DisabledText;

			var oldMode = g.SmoothingMode;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			using (var brush = new SolidBrush(enabled ? p.ControlBackground : radioButton.BackColor))
			{
				g.FillEllipse(brush, circle);
			}
			using (var pen = new Pen(enabled ? p.SubtleText : p.DisabledText))
			{
				g.DrawEllipse(pen, circle.X, circle.Y, circle.Width - 1, circle.Height - 1);
			}
			if (radioButton.Checked)
			{
				using var brush = new SolidBrush(foreColor);

				var dot = circle;
				var inset = Math.Max(2, circleSize / 4);
				dot.Inflate(-inset, -inset);
				g.FillEllipse(brush, dot);
			}

			g.SmoothingMode = oldMode;

			var gap = DpiUtil.ScaleIntX(4);
			var textRect = new Rectangle(circle.Right + gap, bounds.Y, bounds.Width - circle.Right - gap, bounds.Height);
			TextRenderer.DrawText(g, radioButton.Text, radioButton.Font, textRect, foreColor, GetTextFormatFlags(radioButton.TextAlign) | TextFormatFlags.NoClipping);

			if (radioButton.Focused)
			{
				ControlPaint.DrawFocusRectangle(g, textRect, foreColor, radioButton.BackColor);
			}
		}

		private static void OnLabelPaint(object sender, PaintEventArgs e)
		{
			if (!(sender is Label label) || label.Enabled || !Palette.IsDark || label.Image != null)
			{
				return;
			}

			// Disabled labels are drawn with an etched effect which looks broken on dark backgrounds.
			var g = e.Graphics;
			var bounds = label.ClientRectangle;

			using (var brush = new SolidBrush(label.BackColor))
			{
				g.FillRectangle(brush, bounds);
			}

			var flags = GetTextFormatFlags(label.TextAlign);
			if (!label.AutoSize)
			{
				flags = (flags & ~TextFormatFlags.SingleLine) | TextFormatFlags.WordBreak;
			}

			TextRenderer.DrawText(g, label.Text, label.Font, bounds, Palette.DisabledText, flags);
		}

		private static void OnTabControlDrawItem(object sender, DrawItemEventArgs e)
		{
			if (!(sender is TabControl tabControl) || !IsOwnerDrawnByTheme(tabControl) || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var page = tabControl.TabPages[e.Index];
			var selected = e.Index == tabControl.SelectedIndex;
			var bounds = e.Bounds;

			using (var brush = new SolidBrush(selected ? p.WindowBackground : p.Surface))
			{
				g.FillRectangle(brush, bounds);
			}

			if (selected)
			{
				using var brush = new SolidBrush(p.Accent);

				g.FillRectangle(brush, bounds.X, bounds.Y, bounds.Width, Math.Max(1, DpiUtil.ScaleIntY(2)));
			}
			else
			{
				using var pen = new Pen(p.Separator);

				g.DrawLine(pen, bounds.Right - 1, bounds.Y + 4, bounds.Right - 1, bounds.Bottom - 4);
			}

			var textRect = bounds;

			var image = GetTabImage(tabControl, page);
			if (image != null)
			{
				var imageRect = new Rectangle(bounds.X + 6, bounds.Y + ((bounds.Height - image.Height) / 2) + (selected ? 1 : 0), image.Width, image.Height);
				g.DrawImage(image, imageRect);

				textRect = new Rectangle(imageRect.Right + 2, bounds.Y, bounds.Right - imageRect.Right - 4, bounds.Height);
			}

			if (selected)
			{
				textRect.Y += 1;
			}

			var color = tabControl.Enabled ? p.Text : p.DisabledText;
			TextRenderer.DrawText(g, page.Text, tabControl.Font, textRect, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
		}

		private static void OnListBoxDrawItem(object sender, DrawItemEventArgs e)
		{
			if (!(sender is ListBox listBox) || !IsOwnerDrawnByTheme(listBox) || listBox.DrawMode != DrawMode.OwnerDrawFixed)
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
			var enabled = listBox.Enabled;

			var backColor = selected ? (listBox.Focused ? p.SelectionBackground : p.InactiveSelectionBackground) : listBox.BackColor;
			var foreColor = !enabled ? p.DisabledText : (selected ? p.SelectionForeground : listBox.ForeColor);

			using (var brush = new SolidBrush(backColor))
			{
				g.FillRectangle(brush, e.Bounds);
			}

			if (e.Index >= 0 && e.Index < listBox.Items.Count)
			{
				var text = listBox.GetItemText(listBox.Items[e.Index]);
				var textRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 2, e.Bounds.Height);

				TextRenderer.DrawText(g, text, e.Font ?? listBox.Font, textRect, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
			}

			if ((e.State & DrawItemState.Focus) == DrawItemState.Focus && (e.State & DrawItemState.NoFocusRect) != DrawItemState.NoFocusRect)
			{
				ControlPaint.DrawFocusRectangle(g, e.Bounds, foreColor, backColor);
			}
		}

		private static void OnComboBoxDrawItem(object sender, DrawItemEventArgs e)
		{
			if (!(sender is ComboBox comboBox) || !IsOwnerDrawnByTheme(comboBox))
			{
				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var isEditArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
			var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected && !isEditArea;
			var enabled = comboBox.Enabled;

			// The edit area keeps the control colors, the focus is indicated by the frame (see PaintComboBoxFrame).
			var backColor = !enabled ? p.WindowBackground : (selected ? p.SelectionBackground : comboBox.BackColor);
			var foreColor = !enabled ? p.DisabledText : (selected ? p.SelectionForeground : comboBox.ForeColor);

			using (var brush = new SolidBrush(backColor))
			{
				g.FillRectangle(brush, e.Bounds);
			}

			if (e.Index >= 0 && e.Index < comboBox.Items.Count)
			{
				var text = comboBox.GetItemText(comboBox.Items[e.Index]);
				var textRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 2), e.Bounds.Height);

				TextRenderer.DrawText(g, text, e.Font ?? comboBox.Font, textRect, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
			}

			if (!isEditArea && (e.State & DrawItemState.Focus) == DrawItemState.Focus && (e.State & DrawItemState.NoFocusRect) != DrawItemState.NoFocusRect)
			{
				ControlPaint.DrawFocusRectangle(g, e.Bounds, foreColor, backColor);
			}
		}

		private static void OnTreeViewDrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			if (!(sender is TreeView treeView) || !IsOwnerDrawnByTheme(treeView) || treeView.DrawMode != TreeViewDrawMode.OwnerDrawText || e.Node == null)
			{
				return;
			}

			var bounds = e.Bounds;
			if (bounds.Width <= 0 || bounds.Height <= 0)
			{
				e.DrawDefault = true;

				return;
			}

			var p = Palette;
			var g = e.Graphics;
			var selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
			var font = e.Node.NodeFont ?? treeView.Font;

			var backColor = selected ? (treeView.Focused ? p.SelectionBackground : p.InactiveSelectionBackground) : treeView.BackColor;
			var foreColor = !treeView.Enabled ? p.DisabledText : (selected ? p.SelectionForeground : (e.Node.ForeColor.IsEmpty ? treeView.ForeColor : e.Node.ForeColor));

			if (NativeMethods.IsUnix())
			{
				// Mono pre-fills the node with white, starting 3px left of and 1px above the bounds it hands out.
				using var brush = new SolidBrush(treeView.BackColor);

				g.FillRectangle(brush, new Rectangle(bounds.X - 4, bounds.Y - 1, bounds.Width + 4, bounds.Height + 2));
			}

			using (var brush = new SolidBrush(backColor))
			{
				g.FillRectangle(brush, bounds);
			}

			TextRenderer.DrawText(g, e.Node.Text, font, bounds, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

			if ((e.State & TreeNodeStates.Focused) == TreeNodeStates.Focused && treeView.Focused)
			{
				ControlPaint.DrawFocusRectangle(g, bounds, foreColor, backColor);
			}
		}

		/// <summary>
		/// Paints the parts of a tab control the stock control always draws in system colors: the strip behind the tabs
		/// and the frame around the pages.
		/// </summary>
		/// <param name="tabControl">The tab control.</param>
		/// <param name="g">The graphics to paint on.</param>
		public static void PaintTabControlFrame(TabControl tabControl, Graphics g)
		{
			Contract.Requires(tabControl != null);
			Contract.Requires(g != null);

			var client = tabControl.ClientRectangle;
			if (client.Width <= 0 || client.Height <= 0)
			{
				return;
			}

			using var region = new Region(client);

			region.Exclude(tabControl.DisplayRectangle);
			for (var i = 0; i < tabControl.TabCount; ++i)
			{
				region.Exclude(tabControl.GetTabRect(i));
			}

			using var brush = new SolidBrush(Palette.WindowBackground);

			g.FillRegion(brush, region);

			var display = tabControl.DisplayRectangle;
			var y = display.Top - 1;
			using var pen = new Pen(Palette.Separator);

			var selectedTab = tabControl.Alignment == TabAlignment.Top && tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < tabControl.TabCount
				? tabControl.GetTabRect(tabControl.SelectedIndex)
				: Rectangle.Empty;

			if (selectedTab.IsEmpty)
			{
				g.DrawLine(pen, display.Left, y, display.Right - 1, y);

				return;
			}

			// The selected tab merges with its page, so the line is left out below it.
			if (selectedTab.Left > display.Left)
			{
				g.DrawLine(pen, display.Left, y, selectedTab.Left - 1, y);
			}
			if (selectedTab.Right < display.Right)
			{
				g.DrawLine(pen, selectedTab.Right, y, display.Right - 1, y);
			}
		}

		/// <summary>
		/// Paints the frame and the drop down button of a flat combo box. WinForms draws them in system colors
		/// (a white outer border and a light button) which does not fit a dark palette.
		/// </summary>
		/// <param name="comboBox">The combo box.</param>
		/// <param name="g">The graphics to paint on.</param>
		public static void PaintComboBoxFrame(ComboBox comboBox, Graphics g)
		{
			Contract.Requires(comboBox != null);
			Contract.Requires(g != null);

			var client = comboBox.ClientRectangle;
			if (client.Width <= 4 || client.Height <= 4 || comboBox.DropDownStyle == ComboBoxStyle.Simple)
			{
				return;
			}

			var p = Palette;
			var enabled = comboBox.Enabled;
			var active = enabled && (comboBox.Focused || comboBox.DroppedDown);

			var buttonWidth = Math.Min(SystemInformation.HorizontalScrollBarArrowWidth, client.Width / 2);
			var button = new Rectangle(client.Right - buttonWidth - 1, client.Top + 1, buttonWidth, client.Height - 2);

			if (enabled)
			{
				using var brush = new SolidBrush(comboBox.BackColor);

				g.FillRectangle(brush, button);
			}
			else
			{
				// The system paints a disabled combo box in its own light colors, repaint the whole control.
				using var brush = new SolidBrush(p.WindowBackground);

				g.FillRectangle(brush, client);

				var textRect = new Rectangle(client.Left + 3, client.Top, Math.Max(0, button.Left - client.Left - 4), client.Height);
				TextRenderer.DrawText(g, comboBox.Text, comboBox.Font, textRect, p.DisabledText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
			}

			var arrowColor = enabled ? p.Text : p.DisabledText;
			var middle = new Point(button.Left + (button.Width / 2), button.Top + (button.Height / 2));
			using (var brush = new SolidBrush(arrowColor))
			{
				g.FillPolygon(brush, new[]
				{
					new Point(middle.X - 3, middle.Y - 1),
					new Point(middle.X + 4, middle.Y - 1),
					new Point(middle.X, middle.Y + 3)
				});
			}

			using (var pen = new Pen(active ? p.HoverBorder : p.ControlBorder))
			{
				g.DrawRectangle(pen, client.X, client.Y, client.Width - 1, client.Height - 1);
			}
		}

		#endregion

		#region Overlays

		/// <summary>
		/// Subclasses a control to paint over parts the stock control always draws in system colors.
		/// The overlay follows handle re-creation (see <see cref="OnHandleCreated"/>) and is released with the control.
		/// </summary>
		private abstract class ControlOverlay : NativeWindow
		{
			private const int WM_PAINT = 0x000F;
			private const int WM_NCPAINT = 0x0085;

			private static readonly Dictionary<Control, ControlOverlay> overlays = new Dictionary<Control, ControlOverlay>();

			protected Control Control { get; }

			protected ControlOverlay(Control control)
			{
				Control = control;

				control.HandleDestroyed += OnControlHandleDestroyed;
				control.Disposed += OnControlDisposed;
			}

			protected static void Attach(Control control, Func<Control, ControlOverlay> factory)
			{
				if (control == null || control.IsDisposed)
				{
					return;
				}

				if (!overlays.TryGetValue(control, out var overlay))
				{
					overlay = factory(control);
					if (overlay == null)
					{
						return;
					}

					overlays.Add(control, overlay);
				}

				if (control.IsHandleCreated && overlay.Handle == IntPtr.Zero)
				{
					try
					{
						overlay.AssignHandle(control.Handle);
					}
					catch
					{
						// ignored
					}
				}
			}

			public static void Detach(Control control)
			{
				if (control == null || !overlays.TryGetValue(control, out var overlay))
				{
					return;
				}

				overlays.Remove(control);

				control.HandleDestroyed -= overlay.OnControlHandleDestroyed;
				control.Disposed -= overlay.OnControlDisposed;

				overlay.ReleaseHandle();
			}

			protected override void WndProc(ref Message m)
			{
				base.WndProc(ref m);

				if (Control.IsDisposed || !Control.IsHandleCreated)
				{
					return;
				}

				try
				{
					if (m.Msg == WM_PAINT)
					{
						AfterPaint();
					}
					else if (m.Msg == WM_NCPAINT)
					{
						AfterNonClientPaint();
					}
				}
				catch
				{
					// ignored
				}
			}

			/// <summary>Called after the control painted its client area.</summary>
			protected virtual void AfterPaint()
			{

			}

			/// <summary>Called after the control painted its non-client area (borders).</summary>
			protected virtual void AfterNonClientPaint()
			{

			}

			private void OnControlHandleDestroyed(object sender, EventArgs e)
			{
				ReleaseHandle();
			}

			private void OnControlDisposed(object sender, EventArgs e)
			{
				Detach(Control);
			}
		}

		/// <summary>Paints the themed strip and frame of a tab control after the stock painting.</summary>
		private sealed class TabControlOverlay : ControlOverlay
		{
			private TabControlOverlay(TabControl tabControl)
				: base(tabControl)
			{

			}

			public static void Attach(TabControl tabControl)
			{
				Attach(tabControl, c => new TabControlOverlay((TabControl)c));
			}

			protected override void AfterPaint()
			{
				using var g = Graphics.FromHwnd(Control.Handle);

				PaintTabControlFrame((TabControl)Control, g);
			}
		}

		/// <summary>Paints the themed frame and drop down button of a flat combo box after the stock painting.</summary>
		private sealed class ComboBoxOverlay : ControlOverlay
		{
			private ComboBoxOverlay(ComboBox comboBox)
				: base(comboBox)
			{

			}

			public static void Attach(ComboBox comboBox)
			{
				Attach(comboBox, c => new ComboBoxOverlay((ComboBox)c));
			}

			protected override void AfterPaint()
			{
				using var g = Graphics.FromHwnd(Control.Handle);

				PaintComboBoxFrame((ComboBox)Control, g);
			}
		}

		/// <summary>
		/// Repaints the 1px border of controls with <see cref="BorderStyle.FixedSingle"/> in the palette color.
		/// Windows draws it in the system frame color; Mono draws its own border, so the overlay is Windows only.
		/// </summary>
		private sealed class BorderOverlay : ControlOverlay
		{
			private readonly bool clientArea;

			private BorderOverlay(Control control, bool clientArea)
				: base(control)
			{
				this.clientArea = clientArea;
			}

			/// <summary>Attaches the overlay.</summary>
			/// <param name="control">The control.</param>
			/// <param name="clientArea">True if the control paints its border inside the client area (DataGridView), false for a window border.</param>
			public static void Attach(Control control, bool clientArea)
			{
				if (!NativeTheming.IsSupported)
				{
					return;
				}

				Attach(control, c => new BorderOverlay(c, clientArea));
			}

			protected override void AfterPaint()
			{
				if (!clientArea)
				{
					return;
				}

				var client = Control.ClientRectangle;
				if (client.Width <= 1 || client.Height <= 1)
				{
					return;
				}

				using var g = Graphics.FromHwnd(Control.Handle);
				using var pen = new Pen(Palette.ControlBorder);

				g.DrawRectangle(pen, client.X, client.Y, client.Width - 1, client.Height - 1);
			}

			protected override void AfterNonClientPaint()
			{
				if (!clientArea)
				{
					NativeTheming.DrawWindowFrame(Control, Palette.ControlBorder);
				}
			}
		}

		#endregion

		#region Helpers

		private static Image GetTabImage(TabControl tabControl, TabPage page)
		{
			var images = tabControl.ImageList;
			if (images == null)
			{
				return null;
			}

			if (page.ImageIndex >= 0 && page.ImageIndex < images.Images.Count)
			{
				return images.Images[page.ImageIndex];
			}
			if (!string.IsNullOrEmpty(page.ImageKey) && images.Images.ContainsKey(page.ImageKey))
			{
				return images.Images[page.ImageKey];
			}

			return null;
		}

		private static void OnToolTipDraw(object sender, DrawToolTipEventArgs e)
		{
			var p = Palette;
			var g = e.Graphics;
			var bounds = e.Bounds;

			using (var brush = new SolidBrush(p.ToolTipBackground))
			{
				g.FillRectangle(brush, bounds);
			}
			using (var pen = new Pen(p.ControlBorder))
			{
				g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
			}

			var textRect = new Rectangle(bounds.X + 3, bounds.Y + 2, bounds.Width - 6, bounds.Height - 4);
			TextRenderer.DrawText(g, e.ToolTipText, e.Font, textRect, p.ToolTipText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
		}

		private static void DrawCheckMark(Graphics g, Rectangle box, Color color)
		{
			var oldMode = g.SmoothingMode;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			using (var pen = new Pen(color, Math.Max(1.5f, box.Width / 6.5f)))
			{
				var w = box.Width;
				var h = box.Height;
				var x = box.X;
				var y = box.Y;

				g.DrawLines(pen, new[]
				{
					new PointF(x + (w * 0.22f), y + (h * 0.52f)),
					new PointF(x + (w * 0.42f), y + (h * 0.72f)),
					new PointF(x + (w * 0.80f), y + (h * 0.28f))
				});
			}

			g.SmoothingMode = oldMode;
		}

		private static TextFormatFlags GetTextFormatFlags(ContentAlignment alignment)
		{
			var flags = TextFormatFlags.SingleLine;

			switch (alignment)
			{
				case ContentAlignment.TopLeft:
				case ContentAlignment.TopCenter:
				case ContentAlignment.TopRight:
					flags |= TextFormatFlags.Top;
					break;
				case ContentAlignment.BottomLeft:
				case ContentAlignment.BottomCenter:
				case ContentAlignment.BottomRight:
					flags |= TextFormatFlags.Bottom;
					break;
				default:
					flags |= TextFormatFlags.VerticalCenter;
					break;
			}

			switch (alignment)
			{
				case ContentAlignment.TopCenter:
				case ContentAlignment.MiddleCenter:
				case ContentAlignment.BottomCenter:
					flags |= TextFormatFlags.HorizontalCenter;
					break;
				case ContentAlignment.TopRight:
				case ContentAlignment.MiddleRight:
				case ContentAlignment.BottomRight:
					flags |= TextFormatFlags.Right;
					break;
				default:
					flags |= TextFormatFlags.Left;
					break;
			}

			return flags;
		}

		#endregion
	}
}
