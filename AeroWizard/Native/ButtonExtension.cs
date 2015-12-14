﻿using System.Runtime.InteropServices;

namespace System.Windows.Forms
{
	internal static class ButtonExtension
	{
		public static void SetElevationRequiredState(this ButtonBase btn, bool required = true)
		{
			if (System.Environment.OSVersion.Version.Major >= 6)
			{
				const uint BCM_SETSHIELD = 0x160C;    //Elevated button
				btn.FlatStyle = required ? FlatStyle.System : FlatStyle.Standard;
				SendMessage(btn.Handle, BCM_SETSHIELD, IntPtr.Zero, required ? new IntPtr(1) : IntPtr.Zero);
				btn.Invalidate();
			}
			else
				throw new PlatformNotSupportedException();
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
		private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
	}
}