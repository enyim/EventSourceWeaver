using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class EventSourceTemplate
	{
		protected static readonly HashSet<string> SpecialMethods = new HashSet<string>(new[] { "IsEnabled" });
		protected const string GuardPrefix = "Can";

		protected readonly IEventSourceTypeDefs typeDefs;

		private readonly Lazy<IReadOnlyList<LogMethod>> loggers;
		private readonly Lazy<IReadOnlyList<GuardMethod>> guards;

		public EventSourceTemplate(TypeDefinition type, IEventSourceTypeDefs typeDefs)
		{
			this.typeDefs = typeDefs;

			Type = type;
			TypeDefs = typeDefs;

			Keywords = GetNamedNestedType(type, "Keywords");
			Tasks = GetNamedNestedType(type, "Tasks");
			Opcodes = GetNamedNestedType(type, "Opcodes");

			loggers = new Lazy<IReadOnlyList<LogMethod>>(() => GetLogMethods().ToArray());
			guards = new Lazy<IReadOnlyList<GuardMethod>>(() => GetGuardMethods().ToArray());
		}

		private static TypeDefinition GetNamedNestedType(TypeDefinition type, string name)
		{
			return type.NestedTypes.FirstOrDefault(n => n.Name == name);
		}

		public TypeDefinition Type { get; private set; }
		public IEventSourceTypeDefs TypeDefs { get; private set; }

		public TypeDefinition Opcodes { get; private set; }
		public TypeDefinition Tasks { get; private set; }
		public TypeDefinition Keywords { get; private set; }

		public IReadOnlyList<LogMethod> Loggers { get { return loggers.Value; } }
		public IReadOnlyList<GuardMethod> Guards { get { return guards.Value; } }

		protected virtual IEnumerable<LogMethod> GetLogMethods()
		{
			var lastId = 0;
			var methods = Type.Methods.Where(IsLogMethod);
			var noId = new List<LogMethod>();
			var retvals = new List<LogMethod>();

			foreach (var m in methods)
			{
				var ca = m.CustomAttributes.Named("EventAttribute");
				var logMethod = new LogMethod(m, ca, isTemplate: !m.HasBody);

				if (ca == null)
					noId.Add(logMethod);
				else if (logMethod.Id > lastId)
					lastId = logMethod.Id;

				retvals.Add(logMethod);
			}

			foreach (var lm in noId)
				lm.Id = (++lastId);

			TryGenerateTasks(retvals);

			return retvals;
		}

		protected virtual bool IsLogMethod(MethodDefinition m)
		{
			return !m.IsSpecialName
					&& !IsGuardMethod(m)
					&& !SpecialMethods.Contains(m.Name);
		}

		const int MinTask = 11;
		const int MaxTask = 238;

		private void TryGenerateTasks(IEnumerable<LogMethod> methods)
		{
			var toFix = (from method in methods
						 where method.Task == null
						 let match = Regex.Match(method.Method.Name, "^(?'Task'[A-Z]([a-z0-9]*))(?'Op'[A-Z]([a-z0-9]*))$")
						 where match.Success
						 select new
						 {
							 Log = method,
							 Task = match.Groups["Task"].ToString(),
							 Op = match.Groups["Op"].ToString()
						 }).ToArray();

			var maxTask = (Tasks == null ? 0 : MaxConst(Tasks, "EventTask")) + 1;
			var maxOp = (Opcodes == null ? 0 : MaxConst(Opcodes, "EventOpcode")) + 1;
			var systemOps = MapEnumMembers<int>(typeDefs.EventOpcode.Resolve());

			if (maxOp < MinTask) maxOp = MinTask;
			if (maxOp + toFix.Length > MaxTask) throw new ArgumentException("too much op");

			var knownTasks = MapEnumMembers<int>(Tasks);
			var knownOps = MapEnumMembers<int>(Opcodes);

			foreach (var a in toFix)
			{
				int taskId;
				int op;

				a.Log.Task = knownTasks.TryGetValue(a.Task, out taskId)
								? new NamedConst<int>(a.Task, taskId) { Exists = true }
								: new NamedConst<int>(a.Task, knownTasks[a.Task] = maxTask++);

				a.Log.Opcode = systemOps.TryGetValue(a.Op, out op)
								|| knownOps.TryGetValue(a.Op, out op)
								? new NamedConst<int>(a.Op, op) { Exists = true }
								: new NamedConst<int>(a.Op, knownOps[a.Op] = maxOp++);
			}
		}

		private static Dictionary<string, T> MapEnumMembers<T>(TypeDefinition type)
		{
			if (type == null) return new Dictionary<string, T>();

			return type.Fields
						.Where(f => f.IsStatic && f.HasConstant)
						.ToDictionary(f => f.Name, f => (T)f.Constant);
		}

		private static TValue GetOrCreate<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, Func<TValue> factory)
		{
			TValue retval;

			return dict.TryGetValue(key, out retval)
					? retval
					: dict[key] = factory();
		}

		private int MaxConst(TypeDefinition type, string constType)
		{
			var ff = (from f in type.Fields
					  where f.IsStatic
					  && f.FieldType.Name == constType
					  select (int)f.Constant).ToArray();

			if (ff.Length == 0) return 0;

			return ff.Max();
		}

		protected virtual IEnumerable<GuardMethod> GetGuardMethods()
		{
			var loggersByName = Loggers.ToDictionary(m => m.Method.Name);
			var guards = Type.Methods.Where(IsGuardMethod);

			foreach (var g in guards)
			{
				LogMethod lm;

				if (loggersByName.TryGetValue(g.Name.Substring(GuardPrefix.Length), out lm))
				{
					yield return new GuardMethod { LoggerTemplate = lm, Template = g, IsTemplate = !g.HasBody };
				}
			}
		}

		protected virtual bool IsGuardMethod(MethodDefinition m)
		{
			return m.Name.StartsWith(GuardPrefix, StringComparison.Ordinal);
		}

		public TypeDefinition EnsureTasks()
		{
			return Tasks ?? (Tasks = MkNested("Tasks"));
		}

		public TypeDefinition EnsureOpcodes()
		{
			return Opcodes ?? (Opcodes = MkNested("Opcodes"));
		}

		private TypeDefinition MkNested(string name)
		{
			var retval = new TypeDefinition(Type.Name, name, TypeAttributes.NestedPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, Type.Module.TypeSystem.Object);
			Type.NestedTypes.Add(retval);

			return retval;
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
