using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Weavers.Internal
{
	internal abstract class EventSourceImplementerBase
	{
		protected static readonly HashSet<string> SpecialMethods = new HashSet<string>(new[] { "IsEnabled" });
		protected const string GuardPrefix = "Can";

		protected readonly ModuleDefinition module;
		protected readonly EventSourceTemplate template;
		protected readonly IEventSourceTypeDefs typeDefs;

		protected EventSourceImplementerBase(ModuleDefinition module, EventSourceTemplate template)
		{
			this.module = module;
			this.template = template;
			this.typeDefs = this.template.TypeDefs;
		}

		protected abstract MethodDefinition ImplementLogMethod(LogMethod metadata);
		protected abstract MethodDefinition ImplementGuardMethod(GuardMethod metadata);

		public virtual Implemented<MethodDefinition>[] Implement()
		{
			// build all the logger methos
			var logImpls = template.Loggers.Select(meta => Implemented.Create(meta.Method, ImplementLogMethod(meta)));
			// build the guard methods (CanXXX)
			var guardImpls = template.Guards.Select(meta => Implemented.Create(meta.Template, ImplementGuardMethod(meta)));

			// remember them all
			var impls = logImpls.Concat(guardImpls).ToArray();

			// optimize the methods (e.g. "Ldc.i4 1" -> "Ldc.i4.1")
			foreach (var impl in impls)
				impl.New.Body.OptimizeMacros();

			return impls;
		}

		protected void UpdateEventAttribute(MethodDefinition target, LogMethod metadata)
		{
			var ea = metadata.EventAttribute;
			if (ea == null)
				target.CustomAttributes.Add(ea = module.NewAttr(typeDefs.EventAttribute, metadata.Id));

			if (metadata.Task != null)
			{
				if (!metadata.Task.Exists) AddConst(template.EnsureTasks(), typeDefs.EventTask.Resolve(), metadata.Task);
				ea.SetPropertyValue("Task", typeDefs.EventTask, metadata.Task.Value);
			}

			if (metadata.Opcode != null)
			{
				if (!metadata.Opcode.Exists) AddConst(template.EnsureOpcodes(), typeDefs.EventOpcode.Resolve(), metadata.Opcode);
				ea.SetPropertyValue("Opcode", typeDefs.EventOpcode, metadata.Opcode.Value);
			}
		}

		private void AddConst<T>(TypeDefinition target, TypeDefinition type, NamedConst<T> c)
		{
			var field = new FieldDefinition(c.Name, FieldAttributes.Static
													| FieldAttributes.Public
													| FieldAttributes.Literal,
											target.Module.ImportReference(type))
			{
				Constant = c.Value
			};

			target.Fields.Add(field);
		}

		protected void SetLogMethodBody(MethodDefinition target, LogMethod metadata)
		{
			target.CustomAttributes.Add(module.NewAttr(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));

			// implement the logic
			var body = target.Body.Instructions;
			var ret = Instruction.Create(OpCodes.Ret);

			// CODE if (IsEnabled(...)) { WriteEvent(...); }
#if USE_SPECIFIC_GUARD
			body.Add(EmitIsEnabled(metadata));
#else
			body.Add(EmitIsEnabledFallback());
#endif
			body.Add(Instruction.Create(OpCodes.Brfalse, ret));
			body.Add(WriteEvent(target, metadata));
			body.Add(ret);
		}

		#region  guard method

		protected void SetGuardMethodBody(MethodDefinition method, EventLevel? level, EventKeywords? keywords)
		{
			method.CustomAttributes.Add(module.NewAttr(typeDefs.NonEventAttribute),
			module.NewAttr(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));

			var body = method.Body.Instructions;
			// CODE return IsEnabled(...);
			body.Add(EmitIsEnabled(level, keywords));
			body.Add(Instruction.Create(OpCodes.Ret));
		}

		protected IEnumerable<Instruction> EmitIsEnabled(EventLevel? level, EventKeywords? keywords)
		{
			return level.HasValue && keywords.HasValue
			? EmitSpecificIsEnabled(level.Value, keywords.Value)
			: EmitIsEnabledFallback();
		}

		#endregion
		#region isenabled

		private IEnumerable<Instruction> EmitIsEnabledFallback()
		{
			// CODE: if (IsEnabled())
			yield return Instruction.Create(OpCodes.Ldarg_0);
			yield return Instruction.Create(OpCodes.Call, typeDefs.IsEnabledFallback);
		}

		private IEnumerable<Instruction> EmitSpecificIsEnabled(EventLevel level, EventKeywords keywords)
		{
			// CODE: if (IsEnabled(level, keyword))
			yield return Instruction.Create(OpCodes.Ldarg_0);
			yield return Instruction.Create(OpCodes.Ldc_I4, (int)level);

			var kw = (long)keywords;

			// CODE: ((EventKeywords)keyword)
			if (kw >= Int32.MinValue && kw <= Int32.MaxValue)
			{
				yield return Instruction.Create(OpCodes.Ldc_I4, (int)kw);
				yield return Instruction.Create(OpCodes.Conv_I8);
			}
			else
			{
				yield return Instruction.Create(OpCodes.Ldc_I8, kw);
			}

			yield return Instruction.Create(OpCodes.Call, typeDefs.IsEnabledSpecific);
		}

		#endregion
		#region writeevent

		private IEnumerable<Instruction> WriteEvent(MethodDefinition method, LogMethod metadata)
		{
			// looking for WriteEvent(int eventId, arg1, arg2, ..., argN)
			var specificArgs = module.TypeSystem.Int32.Once().Concat(method.Parameters.Select(p => GetEnumBaseType(p.ParameterType.Resolve())));
			var specific = typeDefs.BaseTypeImpl.FindMethod("WriteEvent", specificArgs);

			return specific == null
					? EmitWriteEventFallback(method, metadata)
					: EmitSpecificWriteEvent(method, specific, metadata);
		}

		private IEnumerable<Instruction> EmitWriteEventFallback(MethodDefinition method, LogMethod metadata)
		{
			Log.Warn($"Using WriteEvent fallback for {method.FullName}");

			// push "this" to the stack
			yield return Instruction.Create(OpCodes.Ldarg_0);
			// push the eventId to the stack
			yield return Instruction.Create(OpCodes.Ldc_I4, metadata.Id);

			var parameters = method.Parameters;
			var paramCount = parameters.Count;

			// CODE: var arr = new object[];
			yield return Instruction.Create(OpCodes.Ldc_I4, paramCount);
			yield return Instruction.Create(OpCodes.Newarr, module.TypeSystem.Object);

			// CODE: arr[0] = param1;
			// CODE: arr[1] = param2;
			// CODE: ...
			for (var i = 0; i < paramCount; i++)
			{
				// duplicate the array ref so it remains on the stack for the method call
				yield return Instruction.Create(OpCodes.Dup);

				// index and value goes to stack
				yield return i.AsLdc_I();
				yield return Instruction.Create(OpCodes.Ldarg, parameters[i]);

				// box value types
				if (parameters[i].ParameterType.IsValueType)
					yield return Instruction.Create(OpCodes.Box, parameters[i].ParameterType);

				// store the item in the array
				yield return Instruction.Create(OpCodes.Stelem_Ref);
			}

			// CODE: [this.]WriteEvent(eventId, arr);
			yield return Instruction.Create(OpCodes.Call, typeDefs.WriteEventFallback);
		}

		private IEnumerable<Instruction> EmitSpecificWriteEvent(MethodDefinition method, MethodReference writeEvent, LogMethod metadata)
		{
			// push "this" to the stack
			yield return Instruction.Create(OpCodes.Ldarg_0);
			// push the eventId to the stack
			yield return Instruction.Create(OpCodes.Ldc_I4, metadata.Id);

			var parameters = method.Parameters;
			var paramCount = parameters.Count;

			// our method has the same parameters as one of the WriteEvent overloads
			// so put all parameters to the stack, and done
			for (var i = 0; i < paramCount; i++)
			{
				yield return Instruction.Create(OpCodes.Ldarg, parameters[i]);
				foreach (var _ in EmitConvertCode(parameters[i].ParameterType.Resolve())) yield return _;
			}

			// CODE: WriteEvent(eventId, arg1, arg2, ..., argN);
			yield return Instruction.Create(OpCodes.Call, module.ImportReference(writeEvent));
		}

		#endregion

		private TypeReference GetEnumBaseType(TypeDefinition type)
		{
			if (type.IsEnum)
				return type.Fields.First(f => f.Name == "value__").FieldType;

			if (type.FullName == module.TypeSystem.Boolean.FullName)
				return module.TypeSystem.Int32;

			return type;
		}

		private IEnumerable<Instruction> EmitConvertCode(TypeReference sourceType)
		{
			if (sourceType.FullName == module.TypeSystem.Boolean.FullName)
				return EmitBoolConvertCode();

			return Enumerable.Empty<Instruction>();
		}

		private IEnumerable<Instruction> EmitBoolConvertCode()
		{
			var @true = 1.AsLdc_I();
			var @false = 0.AsLdc_I();
			var nop = Instruction.Create(OpCodes.Nop);

			yield return Instruction.Create(OpCodes.Brtrue, @true);
			yield return @false;
			yield return Instruction.Create(OpCodes.Br, nop);
			yield return @true;
			yield return nop;
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
