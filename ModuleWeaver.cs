using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Weavers.Internal
{
	public class ModuleWeaver
	{
		public ModuleDefinition ModuleDefinition { get; set; }
		public Action<string> LogInfo { get; set; }
		public Action<string> LogWarning { get; set; }
		public Action<string> LogError { get; set; }
#if truea
		public void Execute() { }

		public void Execute2()
#else
		public void Execute()
#endif
		{
			Log.Info = LogInfo;
			Log.Warn = LogWarning;
			Log.Error = LogError;

			var sources = ImplementAbstracts().Concat(ImplementInterfaces()).ToArray();
			var p = from t in typeof(ModuleWeaver).Assembly.GetTypes()
					where t.IsClass && typeof(IProcessEventSources).IsAssignableFrom(t)
					orderby t.GetCustomAttribute<OrderAttribute>()?.Order
					select t;

			foreach (var t in p.ToArray())
				((IProcessEventSources)Activator.CreateInstance(t)).Rewrite(ModuleDefinition, sources);
		}

		private IEnumerable<ImplementedEventSource> ImplementAbstracts()
		{
			var builder = new AbstractEventSourceBuilder(ModuleDefinition);
			var types = from type in ModuleDefinition.Types
						where type.IsAbstract
								&& type.BaseType?.Name == "EventSource"
						select builder.Implement(type);

			return types;
		}

		private IEnumerable<ImplementedEventSource> ImplementInterfaces()
		{
			var builder = new InterfaceBasedEventSourceBuilder(ModuleDefinition);
			var types = (from type in ModuleDefinition.Types
						 where type.IsInterface
						 let a = type.GetAttr<Enyim.AsEventSourceAttribute>()
						 where a != null
						 select new { Type = type, Attr = a }).ToArray();

			foreach (var template in types)
			{
				var name = template.Attr.GetPropertyValue<string>("Name");
				var guid = template.Attr.GetPropertyValue<string>("Guid");

				yield return builder.Implement(name, guid, template.Type);
			}
		}

		//private IEnumerable<ImplementedEventSource> ImplementAll()
		//{
		//	var builder = new EventSourceBuilder_Abstract(ModuleDefinition);
		//	var templates = (from t in ModuleDefinition.Types
		//					 where t.IsAbstract
		//					 let a = t.GetAttr<EventSourceAttribute>()
		//					 where a != null
		//					 select new { Type = t, Attr = a }).ToArray();

		//	foreach (var template in templates)
		//	{
		//		var name = template.Attr.GetPropertyValue<string>("Name");
		//		var guid = template.Attr.GetPropertyValue<string>("Guid");

		//		yield return builder.Implement(name, guid, template.Type);
		//	}
		//}
	}

	static class Log
	{
		public static Action<string> Warn;
		public static Action<string> Error;
		public static Action<string> Info;
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
