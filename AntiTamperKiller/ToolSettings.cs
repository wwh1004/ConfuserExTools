using System.IO;
using Tool.Interface;

namespace ConfuserExTools {
	public sealed class ToolSettings {
		private string _assemblyPath;

		[CliArgument("-f", IsRequired = true)]
		private string CliAssemblyPath {
			set => _assemblyPath = Path.GetFullPath(value);
		}

		public string AssemblyPath => _assemblyPath;
	}
}
