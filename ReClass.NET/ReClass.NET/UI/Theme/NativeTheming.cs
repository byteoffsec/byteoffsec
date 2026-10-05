using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ReClassNET.Native;

namespace ReClassNET.UI.Theme
{
	/// <summary>
	/// Windows only enhancements of the theme: dark title bars (DWM) and dark scroll bars (uxtheme).
	/// Every call is skipped on other platforms and guarded against missing APIs on older Windows builds.
	/// </summary>
	public static class NativeTheming
	{
		#region Imports

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

		[DllImport("dwmapi.dll", ExactSpelling = true)]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

		[DllImport("user32.dll", ExactSpelling = true)]
		private static extern IntPtr SendMessageW(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

		private const int WM_THEMECHANGED = 0x031A;

		#endregion

		private static bool dwmUnavailable;
		private static bool uxThemeUnavailable;

		/// <summary>True if the native enhancements can be used on this platform.</summary>
		public static bool IsSupported => !NativeMethods.IsUnix();

		/// <summary>Switches the title bar of the form between the light and the dark Windows style.</summary>
		/// <param name="form">The form.</param>
		/// <param name="dark">True for a dark title bar.</param>
		public static void ApplyWindowFrame(Form form, bool dark)
		{
			if (form == null || !IsSupported || dwmUnavailable)
			{
				return;
			}

			try
			{
				if (!form.IsHandleCreated || form.IsDisposed)
				{
					return;
				}

				var value = dark ? 1 : 0;
				var result = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
				if (result != 0)
				{
					// Builds before Windows 10 20H1 used an undocumented attribute id.
					DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
				}
			}
			catch
			{
				dwmUnavailable = true;
			}
		}

		/// <summary>
		/// Switches the scroll bars (and other system drawn parts) of the control between the light and the dark explorer style.
		/// </summary>
		/// <param name="control">The control.</param>
		/// <param name="dark">True for the dark style.</param>
		public static void ApplyScrollBars(Control control, bool dark)
		{
			if (control == null || !IsSupported || uxThemeUnavailable)
			{
				return;
			}

			try
			{
				if (!control.IsHandleCreated || control.IsDisposed)
				{
					return;
				}

				var handle = control.Handle;

				SetWindowTheme(handle, dark ? "DarkMode_Explorer" : "Explorer", null);
				SendMessageW(handle, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
			}
			catch
			{
				uxThemeUnavailable = true;
			}
		}

		/// <summary>Switches a combo box between the light and the dark system style.</summary>
		/// <param name="control">The combo box.</param>
		/// <param name="dark">True for the dark style.</param>
		public static void ApplyComboBox(Control control, bool dark)
		{
			if (control == null || !IsSupported || uxThemeUnavailable)
			{
				return;
			}

			try
			{
				if (!control.IsHandleCreated || control.IsDisposed)
				{
					return;
				}

				SetWindowTheme(control.Handle, dark ? "DarkMode_CFD" : "CFD", null);
			}
			catch
			{
				uxThemeUnavailable = true;
			}
		}
	}
}
