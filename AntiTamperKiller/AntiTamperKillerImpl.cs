using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConfuserExTools.AntiTamperKiller {
	public static unsafe class AntiTamperKillerImpl {
		public static byte[] Execute(byte[] peImage) {
			var module = Assembly.Load(peImage).ManifestModule;
			var peInfo = new PEInfo((void*)Marshal.GetHINSTANCE(module));
			var sectionHeader = peInfo.SectionHeaders[0];
			byte[] section = new byte[sectionHeader.SizeOfRawData];
			RuntimeHelpers.RunModuleConstructor(module.ModuleHandle);
			byte[] result = new byte[peImage.Length];
			Buffer.BlockCopy(peImage, 0, result, 0, peImage.Length);
			Marshal.Copy((IntPtr)((byte*)peInfo.PEImage + sectionHeader.VirtualAddress), result, (int)sectionHeader.PointerToRawData, (int)sectionHeader.SizeOfRawData);
			return result;
		}
	}
}
