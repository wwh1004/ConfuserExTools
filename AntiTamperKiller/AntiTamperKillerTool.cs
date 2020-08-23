using System;
using System.IO;
using System.Reflection;
using Tool.Interface;

namespace ConfuserExTools.AntiTamperKiller {
	public sealed class AntiTamperKillerTool : ITool<AntiTamperKillerSettings> {
		public string Title => GetTitle();

		public void Execute(AntiTamperKillerSettings settings) {
			byte[] peImage = AntiTamperKillerImpl.Execute(File.ReadAllBytes(settings.AssemblyPath));
			SaveAs(PathInsertPostfix(settings.AssemblyPath, ".atk"), peImage);
		}

		private static string PathInsertPostfix(string path, string postfix) {
			return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + postfix + Path.GetExtension(path));
		}

		private static void SaveAs(string filePath, byte[] peImage) {
			Console.WriteLine("正在保存: " + Path.GetFullPath(filePath));
			Console.WriteLine("请手动移除AntiTamper初始化代码！");
			Console.WriteLine();
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
