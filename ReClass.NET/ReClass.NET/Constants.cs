namespace ReClassNET
{
	public class Constants
	{
		public const string ApplicationName = "ReClass.NET";

		public const string ApplicationExecutableName = ApplicationName + ".exe";

		public const string ApplicationVersion = "1.3";

		/// <summary>Name of this custom build. Shown in the window title and the about dialog.</summary>
		public const string Edition = "ByteOffSec Edition";

		/// <summary>Full display name including the edition.</summary>
		public const string ApplicationDisplayName = ApplicationName + " " + Edition;

		public const string LauncherExecutableName = ApplicationName + "_Launcher.exe";

		public const string Author = "KN4CK3R (ReClass.NET), ByteOffSec (this edition)";

		public const string UpstreamHomepageUrl = "https://github.com/ReClassNET/ReClass.NET";

		public const string HomepageUrl = "https://github.com/byteoffsec/byteoffsec/tree/main/ReClass.NET";

		public const string HelpUrl = "https://github.com/byteoffsec/byteoffsec/issues";

		public const string PluginUrl = "https://github.com/ReClassNET/ReClass.NET#plugins";

#if RECLASSNET64
		public const string Platform = "x64";

		public const string AddressHexFormat = "X016";
#else
		public const string Platform = "x86";

		public const string AddressHexFormat = "X08";
#endif

		public const string SettingsFile = "settings.xml";

		public const string PluginsFolder = "Plugins";

		public static class CommandLineOptions
		{
			public const string AttachTo = "attachto";

			public const string FileExtRegister = "registerfileext";
			public const string FileExtUnregister = "unregisterfileext";
		}
	}
}
