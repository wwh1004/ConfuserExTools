using System.IO;
using System.Reflection;
using Tool;
using Tool.Interface;
using Tool.Logging;

namespace ConfuserExTools.AntiTamperKiller {
	public sealed class AntiTamperKillerTool : ITool<AntiTamperKillerSettings> {
		public string Title => GetTitle();

		public void Execute(AntiTamperKillerSettings settings) {
			byte[] peImage = AntiTamperKillerImpl.Execute(Assembly.LoadFile(settings.AssemblyPath).ManifestModule, File.ReadAllBytes(settings.AssemblyPath));
			SaveAs(PathInsertSuffix(settings.AssemblyPath, ".atk"), peImage);
			Logger.Flush();
		}

		private static string PathInsertSuffix(string path, string suffix) {
			return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + suffix + Path.GetExtension(path));
		}

		private static void SaveAs(string filePath, byte[] peImage) {
			Logger.Info($"正在保存: {filePath}");
			Logger.Info("请手动移除AntiTamper初始化代码");
			Logger.Info();
			File.WriteAllBytes(filePath, peImage);
		}

		private static string GetTitle() {
			string productName = GetAssemblyAttribute<AssemblyProductAttribute>().Product;
			string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			string copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright.Substring(12);
			int firstBlankIndex = copyright.IndexOf(' ');
			string copyrightOwnerName = copyright.Substring(firstBlankIndex + 1);
			string copyrightYear = copyright.Substring(0, firstBlankIndex);
			return $"{productName} v{version} by {copyrightOwnerName} {copyrightYear}";
		}

		private static T GetAssemblyAttribute<T>() {
			return (T)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false)[0];
		}
	}
}
