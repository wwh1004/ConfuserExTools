using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ConfuserExTools.ProxyKiller {
	internal static class Logger {
		private const int INTERVAL = 5; // 间隔多少毫秒再次检测是否有新文本
		private const int MAX_INTERVAL = 200; // 间隔多少毫秒后强制输出
		private const int MAX_TEXT_COUNT = 5000; // 超过多少条文本后后强制输出

		private static bool _isSyncMode;
		private static volatile Thread _singleThread;
		private static bool _isIdle = true;
		private static readonly Queue<ColorfulText> _queue = new Queue<ColorfulText>();
		private static readonly object _ioLock = new object();
		private static readonly object _stLock = new object();
		private static ConsoleColor _lastColor;
		private static bool _isInitialized;

		/// <summary>
		/// 设置只允许指定线程写入控制台
		/// </summary>
		public static Thread SingleThread {
			get => _singleThread;
			set {
			relock:
				lock (_stLock) {
					var singleThread = _singleThread;
					if (!(singleThread is null) && Thread.CurrentThread != singleThread) {
						Monitor.Wait(_stLock);
						goto relock;
					}
					// 如果不符合设置设置SingleThread的条件，需要等待
					if (singleThread is null || Thread.CurrentThread == singleThread) {
						_singleThread = value;
						if (value is null)
							Monitor.PulseAll(_stLock);
						// 设置为null则取消阻塞其它线程
					}
				}
			}
		}

		/// <summary>
		/// 单线程锁，化简 <see cref="SingleThread"/>
		/// </summary>
		public static IDisposable SingleThreadLock => new AutoSingleThreadLock();

		public static bool IsIdle => _isIdle;

		public static int QueueCount => _queue.Count;

		public static void Initialize() {
			if (_isInitialized)
				return;

			bool isSyncMode = Debugger.IsAttached;
			_isSyncMode = isSyncMode;
			if (!isSyncMode) {
				new Thread(IOLoop) {
					Name = $"{nameof(Logger)}.{nameof(IOLoop)}",
					IsBackground = true
				}.Start();
			}
			_isInitialized = true;
		}

		public static void LogNewLine() {
			LogLine(string.Empty, ConsoleColor.Gray);
		}

		public static void LogInfo(string value) {
			LogLine(value, ConsoleColor.Gray);
		}

		public static void LogWarning(string value) {
			LogLine(value, ConsoleColor.Yellow);
		}

		public static void LogError(string value) {
			LogLine(value, ConsoleColor.Red);
		}

		public static void LogLine(string value, ConsoleColor color) {
			Log(value + Environment.NewLine, color);
		}

		public static void Log(string value, ConsoleColor color) {
			if (_isSyncMode) {
				var oldColor = Console.ForegroundColor;
				Console.ForegroundColor = color;
				Console.Write(value);
				Console.ForegroundColor = oldColor;
				return;
			}
		relock:
			lock (_stLock) {
				var singleThread = _singleThread;
				if (!(singleThread is null) && Thread.CurrentThread != singleThread) {
					Monitor.Wait(_stLock);
					goto relock;
				}
				lock (((ICollection)_queue).SyncRoot) {
					if (string.IsNullOrEmpty(value))
						color = _lastColor;
					// 优化空行显示
					_queue.Enqueue(new ColorfulText(value, color));
					_lastColor = color;
				}
				lock (_ioLock)
					Monitor.Pulse(_ioLock);
			}
		}

		public static void LogException(Exception value) {
			if (value is null)
				throw new ArgumentNullException(nameof(value));

			LogError(ExceptionToString(value));
		}

		public static void LogDebugInfo(string value) {
			LogLine(value, ConsoleColor.Gray);
		}

		public static void Synchronize() {
			if (_isSyncMode)
				return;
			while (!_isIdle || _queue.Count != 0)
				Thread.Sleep(INTERVAL / 3);
		}

		private static string ExceptionToString(Exception exception) {
			if (exception is null)
				throw new ArgumentNullException(nameof(exception));

			var sb = new StringBuilder();
			DumpException(exception, sb);
			return sb.ToString();
		}

		private static void DumpException(Exception exception, StringBuilder sb) {
			sb.AppendLine($"Type: {Environment.NewLine}{exception.GetType().FullName}");
			sb.AppendLine($"Message: {Environment.NewLine}{exception.Message}");
			sb.AppendLine($"Source: {Environment.NewLine}{exception.Source}");
			sb.AppendLine($"StackTrace: {Environment.NewLine}{exception.StackTrace}");
			sb.AppendLine($"TargetSite: {Environment.NewLine}{exception.TargetSite}");
			sb.AppendLine("----------------------------------------");
			if (!(exception.InnerException is null))
				DumpException(exception.InnerException, sb);
			if (exception is ReflectionTypeLoadException reflectionTypeLoadException) {
				foreach (var loaderException in reflectionTypeLoadException.LoaderExceptions)
					DumpException(loaderException, sb);
			}
		}

		private static void IOLoop() {
			if (_isSyncMode)
				throw new InvalidOperationException();

			var sb = new StringBuilder();
			while (true) {
				_isIdle = true;
				if (_queue.Count == 0) {
					lock (_ioLock)
						Monitor.Wait(_ioLock);
				}
				_isIdle = false;
				// 等待输出被触发

				int delayCount = 0;
				int oldCount;
				do {
					oldCount = _queue.Count;
					Thread.Sleep(INTERVAL);
					delayCount++;
				} while (_queue.Count > oldCount && delayCount < MAX_INTERVAL / INTERVAL && _queue.Count < MAX_TEXT_COUNT);
				// 也许此时有其它要输出的内容

				var currents = default(Queue<ColorfulText>);
				lock (((ICollection)_queue).SyncRoot) {
					currents = new Queue<ColorfulText>(_queue);
					_queue.Clear();
				}
				// 获取全部要输出的内容

				do {
					var current = currents.Dequeue();
					sb.Length = 0;
					sb.Append(current.Text);
					while (true) {
						if (currents.Count == 0)
							break;
						var next = currents.Peek();
						if (next.Color != current.Color)
							break;
						currents.Dequeue();
						sb.Append(next.Text);
					}
					// 合并颜色相同，减少重绘带来的性能损失
					var oldColor = Console.ForegroundColor;
					Console.ForegroundColor = current.Color;
					Console.Write(sb.ToString());
					Console.ForegroundColor = oldColor;
				} while (currents.Count > 0);
			}
		}

		private struct ColorfulText {
			public string Text;
			public ConsoleColor Color;

			public ColorfulText(string text, ConsoleColor color) {
				Text = text;
				Color = color;
			}
		}

		private sealed class AutoSingleThreadLock : IDisposable {
			public AutoSingleThreadLock() {
				SingleThread = Thread.CurrentThread;
				Synchronize();
			}

			void IDisposable.Dispose() {
				if (SingleThread is null)
					throw new InvalidOperationException();
				SingleThread = null;
			}
		}
	}
}
