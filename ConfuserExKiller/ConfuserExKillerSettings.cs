using System;
using System.Cli;
using System.IO;

namespace ConfuserExTools.ConfuserExKiller {
	public sealed class ConfuserExKillerSettings {
		private string _assemblyPath;

		[Argument("-f", IsRequired = true, Type = "FILE", Description = "程序集路径")]
		internal string AssemblyPathCliSetter {
			set => AssemblyPath = value;
		}

		public string AssemblyPath {
			get => _assemblyPath;
			set {
				if (string.IsNullOrEmpty(value))
					throw new ArgumentNullException(nameof(value));
				if (!File.Exists(value))
					throw new FileNotFoundException($"{value} 不存在");

				_assemblyPath = Path.GetFullPath(value);
			}
		}
	}
}
