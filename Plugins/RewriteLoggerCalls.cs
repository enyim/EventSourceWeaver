using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weavers.Internal
{
	internal class RewriteLoggerCalls : IProcessEventSources
	{
		public void Rewrite(ModuleDefinition module, IEnumerable<ImplementedEventSource> loggers)
		{
			var loggerMethods = loggers.SelectMany(ies => ies.Methods).ToDictionary(m => m.Old.FullName, m => m.New);
			var moduleMethods = module.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();

			MethodDefinition impl;

			foreach (var method in moduleMethods)
			{
				var ops = method.GetOpsOf(OpCodes.Callvirt).ToArray();

				foreach (var o in ops)
				{
					var fullName = (o.Operand as MethodReference)?.FullName;

					if (fullName != null && loggerMethods.TryGetValue(fullName, out impl))
					{
						//o.OpCode = OpCodes.Call;
						o.Operand = impl;
					}
				}
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
