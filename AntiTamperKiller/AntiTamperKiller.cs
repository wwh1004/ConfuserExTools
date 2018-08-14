using System;
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserExTools {
	public sealed unsafe class AntiTamperKiller {
		private string _filePath;

		private byte[] _peImage;

		public void Execute(string filePath, string[] otherArgs) {
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			_filePath = filePath;
			ExecuteImpl();
			SaveAs(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".atk" + Path.GetExtension(filePath)));
		}

		private void ExecuteImpl() {
			using (ModuleDef moduleDef = ModuleDefMD.Load(_filePath)) {
				MethodDef cctor;
				MethodDef decryptor;
				IList<Instruction> instructionList;
				int length;
				uint? key1;
				uint? key2;
				uint? key3;
				uint? key4;
				uint? nameHash;
				uint? internalKey;

				cctor = moduleDef.GlobalType.FindStaticConstructor();
				decryptor = (MethodDef)cctor.Body.Instructions[0].Operand;
				Decflower.Decflow(decryptor);
				decryptor.Body.SimplifyMacros(decryptor.Parameters);
				instructionList = decryptor.Body.Instructions;
				length = instructionList.Count;
				key1 = null;
				key2 = null;
				key3 = null;
				key4 = null;
				nameHash = null;
				internalKey = null;
				for (int i = 0; i < length; i++) {
					int j;
					int k;

					if (instructionList[i].OpCode != OpCodes.Call || instructionList[i].Operand.ToString() != "System.Void* System.IntPtr::op_Explicit(System.IntPtr)")
						// call void* [mscorlib]System.IntPtr::op_Explicit(native int)
						continue;
					for (j = i + 30; j < length; j++) {
						if (instructionList[j].OpCode != OpCodes.Ldc_I4 || instructionList[j + 1].OpCode != OpCodes.Stloc)
							continue;
						if (key1 == null)
							key1 = (uint)(int)instructionList[j].Operand;
						else if (key2 == null)
							key2 = (uint)(int)instructionList[j].Operand;
						else if (key3 == null)
							key3 = (uint)(int)instructionList[j].Operand;
						else {
							key4 = (uint)(int)instructionList[j].Operand;
							break;
						}
					}
					// keyx
					for (k = length - 1; k >= j; k--)
						if (instructionList[k - 1].OpCode == OpCodes.Xor && instructionList[k].OpCode == OpCodes.Ldc_I4) {
							internalKey = (uint)(int)instructionList[k].Operand;
							break;
						}
					// internalKey
					for (int m = j; m < k; m++)
						if (instructionList[m].OpCode == OpCodes.Ldc_I4 && instructionList[m + 1].OpCode == OpCodes.Bne_Un) {
							nameHash = (uint)(int)instructionList[m].Operand;
							break;
						}
					// nameHash
				}
				Console.WriteLine($"Key1:        {key1.Value.ToString("X8")} ({key1.Value.ToString()})");
				Console.WriteLine($"Key2:        {key2.Value.ToString("X8")} ({key2.Value.ToString()})");
				Console.WriteLine($"Key3:        {key3.Value.ToString("X8")} ({key3.Value.ToString()})");
				Console.WriteLine($"Key4:        {key4.Value.ToString("X8")} ({key4.Value.ToString()})");
				Console.WriteLine($"NameHash:    {nameHash.Value.ToString("X8")} ({nameHash.Value.ToString()})");
				Console.WriteLine($"InternalKey: {internalKey.Value.ToString("X8")} ({internalKey.Value.ToString()})");
				_peImage = File.ReadAllBytes(_filePath);
				fixed (byte* pPEImage = _peImage) {
					uint offset;

					DecryptAntiTamper(pPEImage, key1.Value, key2.Value, key3.Value, key4.Value, nameHash.Value, internalKey.Value, (data, key) => {
						data[0] = data[0] ^ key[0];
						data[1] = data[1] * key[1];
						data[2] = data[2] + key[2];
						data[3] = data[3] ^ key[3];
						data[4] = data[4] * key[4];
						data[5] = data[5] + key[5];
						data[6] = data[6] ^ key[6];
						data[7] = data[7] * key[7];
						data[8] = data[8] + key[8];
						data[9] = data[9] ^ key[9];
						data[10] = data[10] * key[10];
						data[11] = data[11] + key[11];
						data[12] = data[12] ^ key[12];
						data[13] = data[13] * key[13];
						data[14] = data[14] + key[14];
						data[15] = data[15] ^ key[15];
					});
					// 解密节
					offset = (uint)(((ModuleDefMD)moduleDef).Metadata.PEImage.ToFileOffset(cctor.RVA) + cctor.Body.HeaderSize);
					*(pPEImage + offset) = 0;
					*(uint*)(pPEImage + offset + 1) = 0;
					// nop掉call AntiTamperNormal::Initialize()
				}
			}
		}

		private static void DecryptAntiTamper(byte* pPEImage, uint key1, uint key2, uint key3, uint key4, uint nameHash, uint internalKey, Action<uint[], uint[]> encryptor) {
			byte* b = pPEImage;
			byte* p = b + *(uint*)(b + 0x3c);
			// pNtHeader
			ushort s = *(ushort*)(p + 0x6);
			// Machine
			ushort o = *(ushort*)(p + 0x14);
			// SizeOfOptHdr

			uint* e = null;
			uint l = 0;
			uint* r = (uint*)(p + 0x18 + o);
			// pFirstSectHdr
			uint z = key1, x = key2, c = key3, v = key4;
			for (int i = 0; i < s; i++) {
				uint g = (*r++) * (*r++);
				// SectionHeader.Name => nameHash
				// 此时r指向SectionHeader.VirtualSize
				if (g == nameHash) {
					// 查看Confuser.Protections.AntiTamper.NormalMode
					// 这里的Mutation.KeyI0是nameHash
					// 这个if的意思是判断是否为ConfuserEx用来存放加密后方法体的节
					e = (uint*)(b + (*(r + 3)));
					l = (*(r + 2)) >> 2;
				}
				else if (g != 0) {
					uint* q = (uint*)(b + (*(r + 3)));
					uint j = *(r + 2) >> 2;
					// l等于VirtualSize >> 2
					for (uint k = 0; k < j; k++) {
						// 比如VirtualSize=0x200，那这里就循环0x20次
						uint t = (z ^ (*q++)) + x + c * v;
						z = x;
						x = c;
						x = v;
						v = t;
						// 加密运算本身，不需要做分析
					}
				}
				r += 8;
				// 让下一次循环时r依然指向SectionHeader的开头
			}

			uint[] y = new uint[0x10], d = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				y[i] = v;
				d[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}
			// 加密运算本身，不需要做分析
			encryptor(y, d);
			// 这里会ConfuserEx替换成真正的加密算法，大概是这样：
			// data[0] = data[0] ^ key[0];
			// data[1] = data[1] * key[1];
			// data[2] = data[2] + key[2];
			// data[3] = data[3] ^ key[3];
			// data[4] = data[4] * key[4];
			// data[5] = data[5] + key[5];
			// 然后这样循环下去

			uint h = 0;
			for (uint i = 0; i < l; i++) {
				*e ^= y[h & 0xf];
				y[h & 0xf] = (y[h & 0xf] ^ (*e++)) + internalKey;
				h++;
			}
		}

		private void SaveAs(string filePath) {
			Console.WriteLine("Saving: " + Path.GetFullPath(filePath));
			Console.WriteLine();
			File.WriteAllBytes(filePath, _peImage);
		}
	}
}
