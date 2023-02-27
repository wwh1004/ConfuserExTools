using System;
using System.IO;

namespace ConfuserExTools.ConstantKiller {
	public sealed class ConstantKillerSettings {
		private string _assemblyPath;
		private bool _preserveAll;

		[Option("-f", IsRequired = true, Description = "程序集路径")]
		internal string AssemblyPathCliSetter {
			set => AssemblyPath = value;
		}

		[Option("--preserve-all", Description = "是否保留全部，仅还原代理方法")]
		internal bool PreserveAllCliSetter {
			set => PreserveAll = value;
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

		public bool PreserveAll {
			get => _preserveAll;
			set => _preserveAll = value;
		}
	}
}
