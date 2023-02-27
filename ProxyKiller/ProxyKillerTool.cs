using System.IO;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Tool;
using Tool.Interface;
using Tool.Logging;

namespace ConfuserExTools.ProxyKiller {
	public sealed class ProxyKillerTool : ITool<ProxyKillerSettings> {
		private ProxyKillerSettings _settings;
		private ModuleDef _module;
		private int _count;

		public string Title => GetTitle();

		public void Execute(ProxyKillerSettings settings) {
			_settings = settings;
			using (var module = ModuleDefMD.Load(settings.AssemblyPath)) {
				_module = module;
				_count = ProxyKillerImpl.Execute(module, settings.IgnoreAccess, !settings.PreserveProxyMethods && !settings.PreserveAll);
				SaveAs(PathInsertSuffix(settings.AssemblyPath, ".pk"));
			}
			Logger.Flush();
		}

		private static string PathInsertSuffix(string path, string suffix) {
			return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + suffix + Path.GetExtension(path));
		}

		private void SaveAs(string filePath) {
			Logger.Info($"共 {_count} 个代理方法被还原");
			Logger.Info($"正在保存: {filePath}");
			Logger.Info();
			var options = new ModuleWriterOptions(_module);
			if (_settings.PreserveAll)
				options.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
			options.Logger = DnlibLogger.Instance;
			_module.Write(filePath, options);
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
