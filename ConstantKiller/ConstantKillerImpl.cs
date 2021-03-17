using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Tool;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace ConfuserExTools.ConstantKiller {
	public static class ConstantKillerImpl {
		public static int Execute(ModuleDef module, Module reflModule) {
			if (module is null)
				throw new ArgumentNullException(nameof(module));
			if (reflModule is null)
				throw new ArgumentNullException(nameof(reflModule));

			var decrypters = new HashSet<MethodDef>();
			foreach (var method in module.GlobalType.Methods) {
				if (!method.HasBody)
					continue;
				if (!method.IsStatic || !method.HasGenericParameters)
					continue;
				if (method.ReturnType.ElementType != ElementType.MVar || method.Parameters.Count != 1 || method.Parameters[0].Type.ElementType != ElementType.U4)
					continue;

				decrypters.Add(method);
			}

			var oldAssemblyResolver = module.Context.AssemblyResolver;
			var oldResolver = module.Context.Resolver;
			module.Context.AssemblyResolver = NullResolver.Instance;
			module.Context.Resolver = new Resolver(NullResolver.Instance);
			int count = 0;
			foreach (var method in module.EnumerateMethods()) {
				if (!method.HasBody)
					continue;
				if (decrypters.Contains(method))
					continue;

				method.Body.SimplifyMacros(method.Parameters);
				for (int i = 1; i < method.Body.Instructions.Count; i++) {
					var instructions = method.Body.Instructions;
					var instruction = instructions[i];
					if (instruction.OpCode.Code != Code.Call)
						continue;
					if (!(instruction.Operand is MethodSpec operandMethod))
						continue;
					if (!(operandMethod.Method is MethodDef operandMethodDef))
						continue;
					if (!decrypters.Contains(operandMethodDef))
						continue;

					var ldKeyInstr = default(Instruction);
					int key = 0;
					for (int j = 1; j <= i; j++) {
						var instr = instructions[i - j];
						if (instr.OpCode.Code == Code.Nop)
							continue;
						if (instr.OpCode.Code != Code.Ldc_I4)
							break;
						ldKeyInstr = instr;
						key = (int)instr.Operand;
						break;
					}
					if (ldKeyInstr is null) {
						Logger.LogError($"[0x{method.MDToken.Raw:X8}] 无法找到常量解密器参数");
						continue;
					}

					var constantType = operandMethod.GenericInstMethodSig.GenericArguments[0].RemoveModifiers();
					var elementType = constantType.ElementType;
					var arrayType = default(TypeSig);
					var arrayElementType = default(ElementType);
					var reflType = ToType(elementType);
					if (reflType is null) {
						if (elementType != ElementType.SZArray) {
							Logger.LogError($"[0x{method.MDToken.Raw:X8}] 无效常量解密器泛型参数");
							continue;
						}

						var arraySig = (SZArraySig)operandMethod.GenericInstMethodSig.GenericArguments[0];
						arrayType = arraySig.Next.RemoveModifiers();
						arrayElementType = arrayType.ElementType;
						reflType = ToType(arrayElementType);
						if (reflType is null) {
							Logger.LogError($"[0x{method.MDToken.Raw:X8}] 无效常量解密器泛型参数");
							continue;
						}
					}

					object value;
					try {
						var reflMethod = reflModule.ResolveMethod(operandMethod.MDToken.ToInt32());
						value = reflMethod.Invoke(null, new object[] { (uint)key });
					}
					catch (Exception ex) {
						Logger.LogError($"[0x{method.MDToken.Raw:X8}] 调用常量解密器失败");
						Logger.LogException(ex);
						continue;
					}
					if (value is null) {
						Logger.LogError($"[0x{method.MDToken.Raw:X8}] 常量解密器返回值为空");
						continue;
					}

					if (elementType != ElementType.SZArray) {
						switch (elementType) {
						case ElementType.Boolean:
						case ElementType.Char:
						case ElementType.I1:
						case ElementType.U1:
						case ElementType.I2:
						case ElementType.U2:
						case ElementType.I4:
						case ElementType.U4:
							instruction.OpCode = OpCodes.Ldc_I4;
							instruction.Operand = Convert.ToInt32(value);
							break;
						case ElementType.I8:
						case ElementType.U8:
							instruction.OpCode = OpCodes.Ldc_I8;
							instruction.Operand = Convert.ToInt64(value);
							break;
						case ElementType.R4:
							instruction.OpCode = OpCodes.Ldc_R4;
							instruction.Operand = value;
							break;
						case ElementType.R8:
							instruction.OpCode = OpCodes.Ldc_R8;
							instruction.Operand = value;
							break;
						case ElementType.String:
							instruction.OpCode = OpCodes.Ldstr;
							instruction.Operand = value;
							break;
						default:
							throw new InvalidOperationException();
						}
					}
					else {
						int elementSize;
						switch (arrayElementType) {
						case ElementType.Boolean:
						case ElementType.I1:
						case ElementType.U1:
							elementSize = 1;
							break;
						case ElementType.Char:
						case ElementType.I2:
						case ElementType.U2:
							elementSize = 2;
							break;
						case ElementType.I4:
						case ElementType.U4:
						case ElementType.R4:
							elementSize = 4;
							break;
						case ElementType.I8:
						case ElementType.U8:
						case ElementType.R8:
							elementSize = 8;
							break;
						default:
							throw new InvalidOperationException();
						}
						byte[] data = new byte[((Array)value).Length * elementSize];
						Buffer.BlockCopy((Array)value, 0, data, 0, data.Length);
						var arrayInitializer = CreateArrayInitializer(module, arrayType.ToTypeDefOrRef(), ((Array)value).Length, data);
						instructions.InsertRange(i, arrayInitializer);
						instruction.OpCode = OpCodes.Nop;
						instruction.Operand = null;
						i += arrayInitializer.Count;
					}
					ldKeyInstr.OpCode = OpCodes.Nop;
					ldKeyInstr.Operand = null;
					count++;
				}
			}
			module.Context.AssemblyResolver = oldAssemblyResolver;
			module.Context.Resolver = oldResolver;

			foreach (var decrypter in decrypters)
				decrypter.DeclaringType.Methods.Remove(decrypter);

			return count;
		}

		private static Type ToType(ElementType elementType) {
			switch (elementType) {
			case ElementType.Boolean: return typeof(bool);
			case ElementType.Char: return typeof(char);
			case ElementType.I1: return typeof(sbyte);
			case ElementType.U1: return typeof(byte);
			case ElementType.I2: return typeof(short);
			case ElementType.U2: return typeof(ushort);
			case ElementType.I4: return typeof(int);
			case ElementType.U4: return typeof(uint);
			case ElementType.I8: return typeof(long);
			case ElementType.U8: return typeof(ulong);
			case ElementType.R4: return typeof(float);
			case ElementType.R8: return typeof(double);
			case ElementType.String: return typeof(string);
			default: return null;
			}
		}

		private static List<Instruction> CreateArrayInitializer(ModuleDef module, ITypeDefOrRef arrayType, int arrayLength, byte[] data) {
			return CreateArrayInitializer(module, arrayType, arrayLength, GetOrCreateDataField(module, data));
		}

		private static List<Instruction> CreateArrayInitializer(ModuleDef module, ITypeDefOrRef arrayType, int arrayLength, FieldDef dataField) {
			var instructions = new List<Instruction> {
				new Instruction(OpCodes.Ldc_I4, arrayLength),
				new Instruction(OpCodes.Newarr, arrayType),
				new Instruction(OpCodes.Dup),
				new Instruction(OpCodes.Ldtoken, dataField),
				new Instruction(OpCodes.Call, module.Import(typeof(RuntimeHelpers).GetMethod("InitializeArray"))),
			};
			return instructions;
		}

		private static FieldDef GetOrCreateDataField(ModuleDef module, byte[] data) {
			var privateImplementationDetails = module.FindNormal("<PrivateImplementationDetails>");
			if (privateImplementationDetails is null) {
				privateImplementationDetails = new TypeDefUser(UTF8String.Empty, "<PrivateImplementationDetails>", module.CorLibTypes.Object.TypeRef) {
					Attributes = TypeAttributes.NotPublic | TypeAttributes.Sealed
				};
				var compilerGeneratedAttribute = module.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "CompilerGeneratedAttribute");
				var ca = new CustomAttribute(new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), compilerGeneratedAttribute));
				privateImplementationDetails.CustomAttributes.Add(ca);
				module.Types.Add(privateImplementationDetails);
			}
			string storageStructName = $"__StaticArrayInitTypeSize={data.Length}";
			var storageStruct = privateImplementationDetails.NestedTypes.FirstOrDefault(t => t.Name == storageStructName);
			if (storageStruct is null) {
				storageStruct = new TypeDefUser(string.Empty, storageStructName, module.CorLibTypes.GetTypeRef("System", "ValueType")) {
					Attributes = TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed,
					ClassLayout = new ClassLayoutUser(1, (uint)data.Length)
				};
				privateImplementationDetails.NestedTypes.Add(storageStruct);
			}
			string dataFieldName;
			using (var sha256 = SHA256.Create())
				dataFieldName = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty);
			var dataField = privateImplementationDetails.FindField(dataFieldName);
			if (!(dataField is null))
				return dataField;
			dataField = new FieldDefUser(dataFieldName, new FieldSig(storageStruct.ToTypeSig())) {
				Attributes = FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA,
				InitialValue = data
			};
			privateImplementationDetails.Fields.Add(dataField);
			return dataField;
		}

		private static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection) {
			if (!(collection is ICollection<T> c))
				c = new List<T>(collection);
			if (list is List<T> list2) {
				list2.InsertRange(index, c);
			}
			else {
				int length = list.Count;
				for (int i = 0; i < c.Count; i++)
					list.Add(default);
				for (int i = index; i < length; i++)
					list[i + c.Count] = list[i];
				int n = 0;
				foreach (var item in c)
					list[index + n++] = item;
			}
		}
	}
}
