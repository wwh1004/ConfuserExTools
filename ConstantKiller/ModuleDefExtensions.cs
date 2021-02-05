using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace ConfuserExTools.ConstantKiller {
	internal static class ModuleDefExtensions {
		public static IEnumerable<MethodDef> EnumerateMethods(this ModuleDef module) {
			if (module is ModuleDefMD moduleDefMD) {
				uint methodTableLength = moduleDefMD.TablesStream.MethodTable.Rows;
				for (uint rid = 1; rid <= methodTableLength; rid++)
					yield return moduleDefMD.ResolveMethod(rid);
			}
			else {
				for (uint rid = 1; ; rid++) {
					if (!(module.ResolveToken(new MDToken(Table.Method, rid)) is MethodDef method))
						yield break;
					yield return method;
				}
			}
		}
	}
}
