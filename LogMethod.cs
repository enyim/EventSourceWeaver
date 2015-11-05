using System;
using System.Diagnostics.Tracing;
using System.Linq;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class LogMethod
	{
		public LogMethod(MethodDefinition method, CustomAttribute a, bool isTemplate = true)
		{
			Method = method;
			IsTemplate = isTemplate;
			EventAttribute = a;

			if (a != null)
			{
				Id = (int)a.ConstructorArguments.First().Value;

				Level = GetProp<EventLevel, EventLevel?>(a, "Level", v => v);
				Keywords = GetProp<EventKeywords, EventKeywords?>(a, "Keywords", v => v);

				Task = GetProp<EventTask, NamedConst<int>>(a, "Task", v => NamedConst.Existing(v.ToString(), (int)v));
				Opcode = GetProp<EventOpcode, NamedConst<int>>(a, "Opcode", v => NamedConst.Existing(v.ToString(), (int)v));

				if (Task != null) Log.Info($"LM: {method} {Task.Name}={Task.Value} - {Task.Exists}");
				if (Opcode != null) Log.Info($"LM: {method} {Opcode.Name}={Opcode.Value} - {Opcode.Exists}");
			}
		}

		public int Id { get; set; }
		public bool IsTemplate { get; }

		public CustomAttribute EventAttribute { get; }
		public MethodDefinition Method { get; }
		public EventLevel? Level { get; }
		public EventKeywords? Keywords { get; }

		public NamedConst<int> Opcode { get; set; }
		public NamedConst<int> Task { get; set; }

		public bool HasLevel
		{
			get { return Level != null; }
		}

		public bool HasKeywords
		{
			get { return Keywords != null; }
		}

		static V GetProp<T, V>(CustomAttribute a, string name, Func<T, V> setter)
		{
			T tmp;
			if (a.TryGetPropertyValue<T>(name, out tmp))
				return setter(tmp);

			return default(V);
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
