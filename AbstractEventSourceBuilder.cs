using System;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class AbstractEventSourceBuilder
	{
		private readonly ModuleDefinition module;

		public AbstractEventSourceBuilder(ModuleDefinition module)
		{
			this.module = module;
		}

		internal ImplementedEventSource Implement(TypeDefinition type)
		{
			type.FindConstructor().Resolve().Attributes |= MethodAttributes.Public;
			type.Attributes = type.Attributes
									.Remove(TypeAttributes.Abstract)
									.Add(TypeAttributes.Sealed);

			var methods = new Implementer(module, new EventSourceTemplate(type, new AbstractClassBasedTypeDefs(type))).Implement();

			return new ImplementedEventSource
			{
				Methods = methods,
				New = type,
				Old = type
			};
		}

		#region [ Implementer                  ]

		internal class Implementer : EventSourceImplementerBase
		{
			public Implementer(ModuleDefinition module, EventSourceTemplate template) : base(module, template) { }

			public override Implemented<MethodDefinition>[] Implement()
			{
				var retval = base.Implement();
				var template = this.template;

				foreach (var m in template.Loggers)
				{
					if (m.Opcode == null || m.Opcode.Exists) continue;

					Console.WriteLine(m.Opcode.Name);
				}

				return retval;
			}

			protected override MethodDefinition ImplementGuardMethod(GuardMethod metadata)
			{
				var method = metadata.Template;

				if (metadata.IsTemplate)
				{
					method.Attributes = MethodAttributes.Public | MethodAttributes.HideBySig;
					SetGuardMethodBody(method, metadata.LoggerTemplate.Level, metadata.LoggerTemplate.Keywords);
				}

				return method;
			}

			protected override MethodDefinition ImplementLogMethod(LogMethod metadata)
			{
				var method = metadata.Method;

				if (metadata.IsTemplate)
				{
					method.Attributes = MethodAttributes.Public | MethodAttributes.HideBySig;
					SetLogMethodBody(method, metadata);
				}

				UpdateEventAttribute(method, metadata);

				return method;
			}
		}

		#endregion
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
