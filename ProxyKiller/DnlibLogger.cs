using System;
using dnlib.DotNet;
using Tool;
using Tool.Logging;
using ILogger = dnlib.DotNet.ILogger;

namespace ConfuserExTools.ProxyKiller {
	internal sealed class DnlibLogger : ILogger {
		private static readonly DnlibLogger _instance = new DnlibLogger();

		private DnlibLogger() {
		}

		public static DnlibLogger Instance => _instance;

		public bool IgnoresEvent(LoggerEvent loggerEvent) {
			return false;
		}

		public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args) {
			string text = $"{loggerEvent}: {string.Format(format, args)}";
			switch (loggerEvent) {
			case LoggerEvent.Error: Logger.Error(text); break;
			case LoggerEvent.Warning: Logger.Warning(text); break;
			case LoggerEvent.Info: Logger.Info(text); break;
			case LoggerEvent.Verbose:
			case LoggerEvent.VeryVerbose: Logger.Info(text); break;
			default: throw new ArgumentOutOfRangeException(nameof(loggerEvent));
			}
		}
	}
}
