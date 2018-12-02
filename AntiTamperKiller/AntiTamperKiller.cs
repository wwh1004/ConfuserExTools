using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tool.Interface;

namespace ConfuserExTools {
	public sealed unsafe class AntiTamperKiller : ITool<ToolSettings> {
		private Module _module;
		private byte[] _peImage;

		public string Title => GetAssemblyAttribute<AssemblyProductAttribute>().Product + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " by " + GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright.Substring(17);

		private static T GetAssemblyAttribute<T>() => (T)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false)[0];

		public void Execute(ToolSettings settings) {
			_module = Assembly.LoadFile(settings.AssemblyPath).ManifestModule;
			_peImage = File.ReadAllBytes(settings.AssemblyPath);
			ExecuteImpl();
			SaveAs(Path.Combine(Path.GetDirectoryName(settings.AssemblyPath), Path.GetFileNameWithoutExtension(settings.AssemblyPath) + ".atk" + Path.GetExtension(settings.AssemblyPath)));
		}

		private void ExecuteImpl() {
			PEInfo peInfo;
			IMAGE_SECTION_HEADER sectionHeader;
			byte[] section;

			peInfo = new PEInfo((void*)Marshal.GetHINSTANCE(_module));
			sectionHeader = peInfo.SectionHeaders[0];
			section = new byte[sectionHeader.SizeOfRawData];
			RuntimeHelpers.RunModuleConstructor(_module.ModuleHandle);
			Marshal.Copy((IntPtr)((byte*)peInfo.PEImage + sectionHeader.VirtualAddress), _peImage, (int)sectionHeader.PointerToRawData, (int)sectionHeader.SizeOfRawData);
		}

		private void SaveAs(string filePath) {
			Console.WriteLine("Saving: " + Path.GetFullPath(filePath));
			Console.WriteLine("You should patch AntiTamper initializer manually!");
			Console.WriteLine();
			File.WriteAllBytes(filePath, _peImage);
		}
	}
}
