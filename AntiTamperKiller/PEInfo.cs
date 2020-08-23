using System.Runtime.InteropServices;

namespace ConfuserExTools.AntiTamperKiller {
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct IMAGE_SECTION_HEADER {
		public fixed byte Name[8];
		public uint VirtualSize;
		public uint VirtualAddress;
		public uint SizeOfRawData;
		public uint PointerToRawData;
		public uint PointerToRelocations;
		public uint PointerToLinenumbers;
		public ushort NumberOfRelocations;
		public ushort NumberOfLinenumbers;
		public uint Characteristics;
	}

	internal sealed unsafe class PEInfo {
		private readonly void* _pPEImage;
		private readonly uint _sectionsCount;
		private readonly IMAGE_SECTION_HEADER* _pSectionHeaders;

		public void* PEImage => _pPEImage;

		public uint SectionsCount => _sectionsCount;

		public IMAGE_SECTION_HEADER* SectionHeaders => _pSectionHeaders;

		public PEInfo(void* pPEImage) {
			_pPEImage = pPEImage;
			byte* p = (byte*)pPEImage;
			p += *(uint*)(p + 0x3C);
			// NtHeader
			p += 4 + 2;
			// 跳过 Signature + Machine
			_sectionsCount = *(ushort*)p;
			p += 2 + 4 + 4 + 4;
			// 跳过 NumberOfSections + TimeDateStamp + PointerToSymbolTable + NumberOfSymbols
			ushort optionalHeaderSize = *(ushort*)p;
			p += 2 + 2;
			// 跳过 SizeOfOptionalHeader + Characteristics
			p += optionalHeaderSize;
			// 跳过 OptionalHeader
			_pSectionHeaders = (IMAGE_SECTION_HEADER*)p;
		}
	}
}
