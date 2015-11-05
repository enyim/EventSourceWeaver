using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weavers.Internal
{
	[Order(Int32.MinValue)]
	internal class FactoryImplementer : IProcessEventSources
	{
		public void Rewrite(ModuleDefinition module, IEnumerable<ImplementedEventSource> loggers)
		{
			var fullNameOfGet = module
									.ImportReference(typeof(Enyim.EventSourceFactory)).Resolve()
									.FindMethod("Get").FullName;
			var methods = module.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();
			var implMap = loggers.ToDictionary(l => l.Old.FullName);
			var localRemap = new Dictionary<string, TypeDefinition>();

			foreach (var method in methods)
			{
				var factoryCalls = (from i in method.GetOpsOf(OpCodes.Call)
									let mr = i.Operand as MethodReference
									where mr.IsGenericInstance
											&& mr.Resolve()
													?.GetElementMethod()
													?.FullName == fullNameOfGet
									select new
									{
										Instruction = i,
										Wanted = ((GenericInstanceMethod)mr).GenericArguments.First().Resolve()
									}).ToArray();

				if (factoryCalls.Length > 0)
				{
					var ilp = method.Body.GetILProcessor();

					foreach (var tmp in factoryCalls)
					{
						ImplementedEventSource ies;
						var rewriteTo = tmp.Wanted.IsClass
											? tmp.Wanted.Resolve()
											: implMap.TryGetValue(tmp.Wanted.FullName, out ies)
												? ies.New
												: null;

						if (rewriteTo == null)
						{
							Log.Warn($"Factory: cannot rewrite {tmp.Wanted.FullName}");
							continue;
						}

						var ctor = rewriteTo.FindConstructor();
						if (ctor == null) throw new InvalidOperationException($"{rewriteTo.FullName} has no constructor");

						var newobj = Instruction.Create(OpCodes.Newobj, ctor);
						newobj.SequencePoint = tmp.Instruction.SequencePoint;
						ilp.Replace(tmp.Instruction, newobj);

						Log.Info($"Factory: {tmp.Wanted.FullName} -> {rewriteTo.FullName}");

						localRemap[tmp.Wanted.FullName] = rewriteTo;
					}
				}

				RewriteLocalVariables(method, localRemap);
			}
		}

		private static void RewriteLocalVariables(MethodDefinition method, IReadOnlyDictionary<string, TypeDefinition> implMap)
		{
			foreach (var v in method.Body.Variables)
			{
				TypeDefinition target;

				if (implMap.TryGetValue(v.VariableType.FullName, out target))
					v.VariableType = target;
			}
		}
	}
}

#region [ License information          ]

/* ************************************************************
 *
 *    Copyright (c) Attila Kiskó, enyim.com
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
