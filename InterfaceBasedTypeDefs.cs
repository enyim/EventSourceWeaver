using System.Linq;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class InterfaceBasedTypeDefs : IEventSourceTypeDefs
	{
		public InterfaceBasedTypeDefs(ModuleDefinition module)
		{
			const string MS = "Microsoft.Diagnostics.Tracing";
			const string SYS = "System.Diagnostics.Tracing";

			BaseTypeRef = GuessBaseType(module).ImportInto(module);
			BaseTypeImpl = BaseTypeRef.Resolve();
			WriteEventFallback = BaseTypeImpl.FindMethod("WriteEvent", module.TypeSystem.Int32, module.ImportReference(typeof(object[]))).ImportInto(module);

			var baseModule = BaseTypeImpl.Module;

			EventLevel = FindOne(baseModule, "EventLevel", MS, SYS).ImportInto(module);
			EventKeywords = FindOne(baseModule, "EventKeywords", MS, SYS).ImportInto(module);
			EventOpcode = FindOne(baseModule, "EventOpcode", MS, SYS).ImportInto(module);
			EventTask = FindOne(baseModule, "EventTask", MS, SYS).ImportInto(module);

			EventSourceAttribute = FindOne(baseModule, "EventSourceAttribute", MS, SYS).ImportInto(module);
			EventAttribute = FindOne(baseModule, "EventAttribute", MS, SYS).ImportInto(module);
			NonEventAttribute = FindOne(baseModule, "NonEventAttribute", MS, SYS).ImportInto(module);

			IsEnabledSpecific = BaseTypeImpl.FindMethod("IsEnabled", EventLevel, EventKeywords).ImportInto(module);
			IsEnabledFallback = BaseTypeImpl.FindMethod("IsEnabled").ImportInto(module);
		}

		public TypeReference BaseTypeRef { get; private set; }
		public TypeDefinition BaseTypeImpl { get; private set; }
		public MethodReference IsEnabledFallback { get; private set; }
		public MethodReference WriteEventFallback { get; private set; }
		public MethodReference IsEnabledSpecific { get; private set; }

		public TypeReference EventLevel { get; private set; }
		public TypeReference EventKeywords { get; private set; }
		public TypeReference EventOpcode { get; private set; }
		public TypeReference EventTask { get; private set; }

		public TypeReference EventSourceAttribute { get; private set; }
		public TypeReference EventAttribute { get; private set; }
		public TypeReference NonEventAttribute { get; private set; }

		private static TypeReference GuessBaseType(ModuleDefinition module)
		{
			const string MS_EVENT_SOURCE = "Microsoft.Diagnostics.Tracing.EventSource";
			const string MS_ASSEMBLY = "Microsoft.Diagnostics.Tracing.EventSource";

			var msAssemblyRef = module.AssemblyReferences.FirstOrDefault(r => r.Name == MS_ASSEMBLY);
			if (msAssemblyRef != null)
			{
				var msAssembly = module.AssemblyResolver.Resolve(msAssemblyRef);
				var baseType = msAssembly.Modules
				.SelectMany(m => m.Types)
				.FirstOrDefault(t => t.FullName == MS_EVENT_SOURCE);

				if (baseType != null)
					return module.ImportReference(baseType);
			}

			return module.ImportReference(typeof(System.Diagnostics.Tracing.EventSource));
		}

		private static TypeReference FindOne(ModuleDefinition module, string name, params string[] namespaces)
		{
			return namespaces.Select(n => module.FindType(n + "." + name)).First(t => t != null);
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
