using System;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class AbstractClassBasedTypeDefs : IEventSourceTypeDefs
	{
		public AbstractClassBasedTypeDefs(TypeDefinition type)
		{
			if (type.BaseType.Name != "EventSource")
				throw new InvalidOperationException("Base type must be EventSource");

			var ns = type.BaseType.Namespace + ".";
			var module = type.Module;

			BaseTypeRef = type.BaseType;
			BaseTypeImpl = type.BaseType.Resolve();
			WriteEventFallback = BaseTypeImpl.FindMethod("WriteEvent", module.TypeSystem.Int32, module.ImportReference(typeof(object[]))).ImportInto(module);

			var baseModule = BaseTypeImpl.Module;

			EventLevel = baseModule.FindType(ns + "EventLevel").ImportInto(module);
			EventKeywords = baseModule.FindType(ns + "EventKeywords").ImportInto(module);
			EventOpcode = baseModule.FindType(ns + "EventOpcode").ImportInto(module);
			EventTask = baseModule.FindType(ns + "EventTask").ImportInto(module);

			EventSourceAttribute = baseModule.FindType(ns + "EventSourceAttribute").ImportInto(module);
			EventAttribute = baseModule.FindType(ns + "EventAttribute").ImportInto(module);
			NonEventAttribute = baseModule.FindType(ns + "NonEventAttribute").ImportInto(module);

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
