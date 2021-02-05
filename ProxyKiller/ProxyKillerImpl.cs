using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Tool;

namespace ConfuserExTools.ProxyKiller {
	public static class ProxyKillerImpl {
		public static int Execute(ModuleDef module, bool ignoreAccess, bool removeProxyMethods) {
			if (module is null)
				throw new ArgumentNullException(nameof(module));

			var proxyMethods = new Dictionary<MethodDef, Instruction>();
			foreach (var method in module.EnumerateMethods()) {
				if (!method.HasBody)
					continue;
				if (!(ignoreAccess || method.IsPrivateScope))
					continue;

				bool isProxy = true;
				var realInstruction = default(Instruction);
				foreach (var instruction in method.Body.Instructions) {
					switch (instruction.OpCode.Code) {
					case Code.Nop:
					case Code.Ldarg:
					case Code.Ldarg_0:
					case Code.Ldarg_1:
					case Code.Ldarg_2:
					case Code.Ldarg_3:
					case Code.Ldarg_S:
					case Code.Ldarga:
					case Code.Ldarga_S:
					case Code.Ret:
						continue;
					case Code.Call:
					case Code.Callvirt:
					case Code.Newobj:
						if (realInstruction is null) {
							realInstruction = instruction;
							continue;
						}
						break;
					}
					isProxy = false;
					break;
				}
				if (!isProxy) {
					Logger.LogWarning($"[0x{method.MDToken.Raw:X8}] {method} 不是代理方法（可能判断错误）");
					continue;
				}

				proxyMethods.Add(method, realInstruction);
			}

			var oldAssemblyResolver = module.Context.AssemblyResolver;
			var oldResolver = module.Context.Resolver;
			module.Context.AssemblyResolver = NullResolver.Instance;
			module.Context.Resolver = new Resolver(NullResolver.Instance);
			foreach (var method in module.EnumerateMethods()) {
				if (!method.HasBody)
					continue;
				if (proxyMethods.ContainsKey(method))
					continue;

				foreach (var instruction in method.Body.Instructions) {
					if (instruction.OpCode.Code != Code.Call)
						continue;
					var operandMethod = ((IMethod)instruction.Operand).ResolveMethodDef();
					if (operandMethod is null)
						continue;
					if (!proxyMethods.TryGetValue(operandMethod, out var realCall))
						continue;

					instruction.OpCode = realCall.OpCode;
					instruction.Operand = realCall.Operand;
				}
			}
			module.Context.AssemblyResolver = oldAssemblyResolver;
			module.Context.Resolver = oldResolver;

			if (removeProxyMethods) {
				foreach (var proxyMethod in proxyMethods.Keys)
					proxyMethod.DeclaringType.Methods.Remove(proxyMethod);
			}

			return proxyMethods.Count;
		}
	}
}
