using System;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class InterfaceBasedEventSourceBuilder
	{
		private readonly ModuleDefinition module;

		public InterfaceBasedEventSourceBuilder(ModuleDefinition module)
		{
			this.module = module;
		}

		internal ImplementedEventSource Implement(string name, string guid, TypeDefinition template)
		{
			var typeDefs = new InterfaceBasedTypeDefs(module);
			// create new public sealed type
			var newType = module.NewType(template.Namespace, template.Name.Substring(1), typeDefs.BaseTypeRef);
			newType.Attributes |= TypeAttributes.Sealed;
			// mark it as EventSource
			newType.CustomAttributes.Add(CreateEventSourceAttribute(typeDefs, name, guid));

			TryNestClass(template, newType, "Keywords");
			TryNestClass(template, newType, "Tasks");
			TryNestClass(template, newType, "Opcodes");

			return new ImplementedEventSource
			{
				Old = template,
				New = newType,
				Methods = new Implementer(module, new EventSourceTemplate(template, typeDefs), newType).Implement()
			};
		}

		private void TryNestClass(TypeDefinition template, TypeDefinition implementation, string name)
		{
			var childName = template.Name.Substring(1) + name;
			var childRef = template.Module.FindType(template.Namespace + "." + childName);

			// move the "child" class inside the logger implementation
			if (childRef != null)
			{
				var kw = childRef.Resolve();
				kw.Name = name;
				kw.Attributes = TypeAttributes.NestedPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;

				module.Types.Remove(kw);
				implementation.NestedTypes.Add(kw);
			}
		}

		private CustomAttribute CreateEventSourceAttribute(IEventSourceTypeDefs typeDefs, string name, string guid)
		{
			var ca = module.NewAttr(typeDefs.EventSourceAttribute);

			if (!String.IsNullOrEmpty(name))
				ca.SetPropertyValue("Name", module.TypeSystem.String, name);

			if (!String.IsNullOrEmpty(guid))
				ca.SetPropertyValue("Guid", module.TypeSystem.String, guid);

			return ca;
		}

		#region [ Implementer                  ]

		internal class Implementer : EventSourceImplementerBase
		{
			private readonly TypeDefinition target;

			public Implementer(ModuleDefinition module, EventSourceTemplate template, TypeDefinition target)
					: base(module, template)
			{
				this.target = target;
			}

			protected override MethodDefinition ImplementGuardMethod(GuardMethod metadata)
			{
				var oldMethod = metadata.Template;
				var newMethod = new MethodDefinition(oldMethod.Name, MethodAttributes.Public, module.TypeSystem.Boolean);

				oldMethod.CopyAttrsTo(newMethod);
				target.Methods.Add(newMethod);

				SetGuardMethodBody(newMethod, metadata.LoggerTemplate.Level, metadata.LoggerTemplate.Keywords);

				return newMethod;
			}

			protected override MethodDefinition ImplementLogMethod(LogMethod metadata)
			{
				var oldMethod = metadata.Method;
				// duplicate the interface method
				var newMethod = new MethodDefinition(oldMethod.Name, MethodAttributes.Public, module.ImportReference(oldMethod.ReturnType));
				target.Methods.Add(newMethod);

				// implement all source parameters
				foreach (var p in oldMethod.Parameters)
					newMethod.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, module.ImportReference(p.ParameterType)));

				SetLogMethodBody(newMethod, metadata);
				UpdateEventAttribute(newMethod, metadata);

				return newMethod;
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
