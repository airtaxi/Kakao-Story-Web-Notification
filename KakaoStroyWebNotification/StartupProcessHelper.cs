using Microsoft.Win32;
using Org.BouncyCastle.Asn1.Crmf;

namespace KakaoStroyWebNotification;

public static class StartupProcessHelper
{
	private static readonly string AppName = "KakaoStroyWebNotification";
	private static readonly string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location[..^4] + ".exe";

	public static bool IsStartupProcess
	{
		get
		{
			using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			return key.GetValue(AppName) != null;
		}
	}

	public static void SetupStartupProcess()
	{
		using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
		if (key.GetValue(AppName) != null) return;
		key.SetValue(AppName, AppPath);
	}

	public static void RemoveStartupProcess()
	{
		using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
		if (key.GetValue(AppName) == null) return;
		key.DeleteValue(AppName);
	}
}
