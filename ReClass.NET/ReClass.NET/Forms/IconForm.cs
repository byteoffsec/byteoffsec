using System;
using System.Windows.Forms;
using ReClassNET.UI.Theme;

namespace ReClassNET.Forms
{
	/// <summary>
	/// Base class of all forms. Sets the application icon and keeps the form themed (see <see cref="ThemeManager"/>).
	/// </summary>
	public class IconForm : Form
	{
		public IconForm()
		{
			Icon = Properties.Resources.ReClassNet;
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			if (Program.DesignMode)
			{
				return;
			}

			// Theme as early as possible so the window never flashes in the wrong colors.
			ThemeManager.Apply(this);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			if (Program.DesignMode)
			{
				return;
			}

			ThemeManager.Attach(this);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			ThemeManager.Detach(this);

			base.OnFormClosed(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ThemeManager.Detach(this);
			}

			base.Dispose(disposing);
		}
	}
}
