﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.Win32.DesktopWindowManager
{
	/// <summary>Main DWM class, provides glass sheet effect and blur behind.</summary>
	[System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
	public static class DesktopWindowManager
	{
		static object ColorizationColorChangedKey = new object();
		static object CompositionChangedKey = new object();
		static EventHandlerList eventHandlerList;
		static object NonClientRenderingChangedKey = new object();
		//static object WindowMaximizedChangedKey = new object();
		static object[] keys = new object[] { CompositionChangedKey, NonClientRenderingChangedKey, ColorizationColorChangedKey/*, WindowMaximizedChangedKey*/ };
		static object _lock = new object();
		static MessageWindow _window;

		/// <summary>
		/// Occurs when the colorization color has changed.
		/// </summary>
		public static event EventHandler ColorizationColorChanged
		{
			add { AddEventHandler(ColorizationColorChangedKey, value); }
			remove { RemoveEventHandler(ColorizationColorChangedKey, value); }
		}

		/// <summary>
		/// Occurs when the desktop window composition has been enabled or disabled.
		/// </summary>
		public static event EventHandler CompositionChanged
		{
			add { AddEventHandler(CompositionChangedKey, value); }
			remove { RemoveEventHandler(CompositionChangedKey, value); }
		}

		/// <summary>
		/// Occurs when the non-client area rendering policy has changed.
		/// </summary>
		public static event EventHandler NonClientRenderingChanged
		{
			add { AddEventHandler(NonClientRenderingChangedKey, value); }
			remove { RemoveEventHandler(NonClientRenderingChangedKey, value); }
		}

		/// <summary>
		/// Gets or sets the current color used for Desktop Window Manager (DWM) glass composition. This value is based on the current color scheme and can be modified by the user.
		/// </summary>
		/// <value>The color of the glass composition.</value>
		public static Color CompositionColor
		{
			get
			{
				if (!CompositionSupported)
					return Color.Transparent;
				int value = (int)Microsoft.Win32.Registry.CurrentUser.GetValue(@"Software\Microsoft\Windows\DWM\ColorizationColor", 0);
				return Color.FromArgb(value);
			}
			set
			{
				if (!CompositionSupported)
					return;
				NativeMethods.ColorizationParams p = new NativeMethods.ColorizationParams();
				NativeMethods.DwmGetColorizationParameters(ref p);
				p.Color1 = (uint)value.ToArgb();
				NativeMethods.DwmSetColorizationParameters(ref p, 1);
				Microsoft.Win32.Registry.CurrentUser.SetValue(@"Software\Microsoft\Windows\DWM\ColorizationColor", value.ToArgb(), Microsoft.Win32.RegistryValueKind.DWord);
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether composition (Windows Aero) is enabled.
		/// </summary>
		/// <value><c>true</c> if composition is enabled; otherwise, <c>false</c>.</value>
		public static bool CompositionEnabled
		{
			get { return IsCompositionEnabled(); }
			set { if (CompositionSupported) EnableComposition(value); }
		}

		/// <summary>
		/// Gets or sets a value indicating whether composition (Windows Aero) is supported.
		/// </summary>
		/// <value><c>true</c> if composition is supported; otherwise, <c>false</c>.</value>
		public static bool CompositionSupported
		{
			get { return System.Environment.OSVersion.Version.Major >= 6; }
		}

		/// <summary>
		/// Gets or sets a value that indicates whether the <see cref="CompositionColor"/> is transparent.
		/// </summary>
		/// <value><c>true</c> if transparent; otherwise, <c>false</c>.</value>
		public static bool TransparencyEnabled
		{
			get
			{
				if (!CompositionSupported)
					return false;
				int value = (int)Microsoft.Win32.Registry.CurrentUser.GetValue(@"Software\Microsoft\Windows\DWM\ColorizationOpaqueBlend", 1);
				return value == 0;
			}
			set
			{
				if (!CompositionSupported)
					return;
				NativeMethods.ColorizationParams p = new NativeMethods.ColorizationParams();
				NativeMethods.DwmGetColorizationParameters(ref p);
				p.Opaque = value ? 0u : 1u;
				NativeMethods.DwmSetColorizationParameters(ref p, 1);
				Microsoft.Win32.Registry.CurrentUser.SetValue(@"Software\Microsoft\Windows\DWM\ColorizationOpaqueBlend", p.Opaque, Microsoft.Win32.RegistryValueKind.DWord);
			}
		}

		/*/// <summary>
		/// Occurs when a Desktop Window Manager (DWM) composed window is maximized.
		/// </summary>
		public static event EventHandler WindowMaximizedChanged
		{
			add { AddEventHandler(WindowMaximizedChangedKey, value); }
			remove { RemoveEventHandler(WindowMaximizedChangedKey, value); }
		}*/

		/// <summary>
		/// Enable the Aero "Blur Behind" effect on the whole client area. Background must be black.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="enabled"><c>true</c> to enable blur behind for this window, <c>false</c> to disable it.</param>
		public static void EnableBlurBehind(this IWin32Window window, bool enabled)
		{
			EnableBlurBehind(window, null, null, enabled, false);
		}

		/// <summary>
		/// Enable the Aero "Blur Behind" effect on a specific region of a drawing area. Background must be black.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="graphics">The graphics area on which the region resides.</param>
		/// <param name="region">The region within the client area to apply the blur behind.</param>
		/// <param name="enabled"><c>true</c> to enable blur behind for this region, <c>false</c> to disable it.</param>
		/// <param name="transitionOnMaximized"><c>true</c> if the window's colorization should transition to match the maximized windows; otherwise, <c>false</c>.</param>
		public static void EnableBlurBehind(this IWin32Window window, System.Drawing.Graphics graphics, System.Drawing.Region region, bool enabled, bool transitionOnMaximized)
		{
			NativeMethods.BlurBehind bb = new NativeMethods.BlurBehind(enabled);
			if (graphics != null && region != null)
				bb.SetRegion(graphics, region);
			if (transitionOnMaximized)
				bb.TransitionOnMaximized = true;
			NativeMethods.DwmEnableBlurBehindWindow(window.Handle, ref bb);
		}

		/// <summary>
		/// Enables or disables Desktop Window Manager (DWM) composition.
		/// </summary>
		/// <param name="value"><c>true</c> to enable DWM composition; <c>false</c> to disable composition.</param>
		public static void EnableComposition(bool value)
		{
			NativeMethods.DwmEnableComposition(value ? 1 : 0);
		}

		/// <summary>
		/// Excludes the specified child control from the glass effect.
		/// </summary>
		/// <param name="parent">The parent control.</param>
		/// <param name="control">The control to exclude.</param>
		/// <exception cref="ArgumentNullException">Occurs if control is null.</exception>
		/// <exception cref="ArgumentException">Occurs if control is not a child control.</exception>
		public static void ExcludeChildFromGlass(this Control parent, Control control)
		{
			if (control == null)
				throw new ArgumentNullException("control");
			if (!parent.Contains(control))
				throw new ArgumentException("Control must be a child control.");

			if (IsCompositionEnabled())
			{
				System.Drawing.Rectangle clientScreen = parent.RectangleToScreen(parent.ClientRectangle);
				System.Drawing.Rectangle controlScreen = control.RectangleToScreen(control.ClientRectangle);

				NativeMethods.Margins margins = new NativeMethods.Margins(controlScreen.Left - clientScreen.Left, controlScreen.Top - clientScreen.Top,
					clientScreen.Right - controlScreen.Right, clientScreen.Bottom - controlScreen.Bottom);

				// Extend the Frame into client area
				NativeMethods.DwmExtendFrameIntoClientArea(parent.Handle, ref margins);
			}
		}

		/// <summary>
		/// Extends the window frame beyond the client area.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="padding">The padding to use as the area into which the frame is extended.</param>
		public static void ExtendFrameIntoClientArea(this IWin32Window window, Padding padding)
		{
			NativeMethods.Margins m = new NativeMethods.Margins(padding);
			NativeMethods.DwmExtendFrameIntoClientArea(window.Handle, ref m);
		}

		/// <summary>
		/// Indicates whether Desktop Window Manager (DWM) composition is enabled.
		/// </summary>
		/// <returns><c>true</c> if is composition enabled; otherwise, <c>false</c>.</returns>
		public static bool IsCompositionEnabled()
		{
			if (!System.IO.File.Exists(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System), NativeMethods.DWMAPI)))
				return false;
			int res = 0;
			NativeMethods.DwmIsCompositionEnabled(ref res);
			return res != 0;
		}

		private static void AddEventHandler(object id, EventHandler value)
		{
			lock (_lock)
			{
				if (_window == null)
					_window = new MessageWindow();
				if (eventHandlerList == null)
					eventHandlerList = new EventHandlerList();
				eventHandlerList.AddHandler(id, value);
			}
		}

		private static void RemoveEventHandler(object id, EventHandler value)
		{
			lock (_lock)
			{
				if (eventHandlerList != null)
				{
					eventHandlerList.RemoveHandler(id, value);
				}
			}
		}

		[System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
		private class MessageWindow : NativeWindow, IDisposable
		{
			const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
			const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
			const int WM_DWMNCRENDERINGCHANGED = 0x031F;
			//const int WM_DWMWINDOWMAXIMIZEDCHANGE = 0x0321;

			public MessageWindow()
			{
				CreateParams cp = new CreateParams() { Style = 0, ExStyle = 0, ClassStyle = 0, Parent = IntPtr.Zero };
				cp.Caption = base.GetType().Name;
				this.CreateHandle(cp);
			}

			public void Dispose()
			{
				this.DestroyHandle();
			}

			[System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
			protected override void WndProc(ref Message m)
			{
				if (m.Msg >= WM_DWMCOMPOSITIONCHANGED && m.Msg <= WM_DWMCOLORIZATIONCOLORCHANGED)
					ExecuteEvents(m.Msg - WM_DWMCOMPOSITIONCHANGED);

				base.WndProc(ref m);
			}

			private void ExecuteEvents(int idx)
			{
				if (eventHandlerList != null)
				{
					lock (_lock)
					{
						try { ((EventHandler)eventHandlerList[keys[idx]]).Invoke(null, EventArgs.Empty); }
						catch { };
					}
				}
			}
		}
	}
}