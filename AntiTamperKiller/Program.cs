using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ConfuserExTools {
    internal static class Program {
        private static void Main(string[] args) => StandaloneLoader.Execute(args, false);

        private static unsafe class StandaloneLoader {
            [DllImport("shell32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "CommandLineToArgvW", ExactSpelling = true, SetLastError = true)]
            private static extern char** CommandLineToArgv(string lpCmdLine, int* pNumArgs);

            private static readonly string ConsoleTitle = GetAssemblyAttribute<AssemblyProductAttribute>().Product + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " by " + GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright.Substring(17);

            private static T GetAssemblyAttribute<T>() => (T)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false)[0];

            public static void Execute(string[] args, bool needOtherArgs) {
                string filePath;
                string[] otherArgs;

                Console.Title = ConsoleTitle;
                if (args == null || args.Length == 0) {
                    // 直接运行加载器或调试时使用
                    StringBuilder commandLine;

                    commandLine = new StringBuilder();
                    Console.WriteLine("Enter .NET Assembly path:");
                    commandLine.Append(Console.ReadLine());
                    if (needOtherArgs) {
                        commandLine.Append(" ");
                        Console.WriteLine("Enter other args:");
                        commandLine.Append(Console.ReadLine());
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    Execute(CommandLineToArgs(commandLine.ToString()), needOtherArgs);
                    return;
                }
                filePath = args[0];
                otherArgs = new string[args.Length - 1];
                if (otherArgs.Length != 0)
                    Array.Copy(args, 1, otherArgs, 0, otherArgs.Length);
                new AntiTamperKiller().Execute(filePath, otherArgs);
                if (IsN00bUser() || Debugger.IsAttached) {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Press any key to exit...");
                    try {
                        Console.ReadKey(true);
                    }
                    catch (InvalidOperationException) {
                    }
                }
            }

            private static string[] CommandLineToArgs(string commandLine) {
                char** pArgs;
                int length;
                string[] args;

                pArgs = CommandLineToArgv(commandLine, &length);
                if (pArgs == null)
                    return null;
                args = new string[length];
                for (int i = 0; i < length; i++)
                    args[i] = new string(pArgs[i]);
                return args;
            }

            private static bool IsN00bUser() {
                if (HasEnv("VisualStudioDir"))
                    return false;
                if (HasEnv("SHELL"))
                    return false;
                return HasEnv("windir") && !HasEnv("PROMPT");
            }

            private static bool HasEnv(string name) {
                foreach (object key in Environment.GetEnvironmentVariables().Keys) {
                    string env;

                    env = key as string;
                    if (env == null)
                        continue;
                    if (string.Equals(env, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }
    }
}
