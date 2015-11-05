using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace Weavers.Internal
{
	internal static class CecilExtensions
	{
		public static TypeAttributes Add(this TypeAttributes target, TypeAttributes value)
		{
			return target | value;
		}

		public static TypeAttributes Remove(this TypeAttributes target, TypeAttributes value)
		{
			return target & (~value);
		}

		public static void Append(this MethodDefinition method, IEnumerable<Instruction> code)
		{
			var list = method.Body.Instructions;

			foreach (var i in code)
				list.Add(i);
		}

		public static TypeReference FindType(this ModuleDefinition module, string fullName)
		{
			return module.Types.FirstOrDefault(t => t.FullName == fullName);
		}

		public static MethodReference FindConstructor(this TypeDefinition type, params TypeReference[] args)
		{
			return FindMethod(type, ".ctor", args);
		}

		public static MethodReference FindConstructor(this TypeDefinition type, IEnumerable<TypeReference> args)
		{
			return FindMethod(type, ".ctor", args);
		}

		public static MethodReference FindMethod(this TypeDefinition type, string name, params TypeReference[] args)
		{
			return FindMethod(type, name, args.AsEnumerable());
		}

		public static MethodReference FindMethod(this TypeDefinition type, string name, IEnumerable<TypeReference> args)
		{
			var expected = args.Select(t => t.FullName).ToArray();

			return type.Methods
							.FirstOrDefault(m => m.Name == name
													&& m.Parameters
																.Select(p => p.ParameterType.FullName)
																.SequenceEqual(expected));
		}

		public static CustomAttribute Clone(this CustomAttribute what)
		{
			var retval = new CustomAttribute(what.Constructor);

			foreach (var ca in what.ConstructorArguments) retval.ConstructorArguments.Add(new CustomAttributeArgument(ca.Type, ca.Value));
			foreach (var ca in what.Properties) retval.Properties.Add(new CustomAttributeNamedArgument(ca.Name, new CustomAttributeArgument(ca.Argument.Type, ca.Argument.Value)));
			foreach (var ca in what.Fields) retval.Fields.Add(new CustomAttributeNamedArgument(ca.Name, new CustomAttributeArgument(ca.Argument.Type, ca.Argument.Value)));

			return retval;
		}

		public static void CopyAttrsTo(this ICustomAttributeProvider source, ICustomAttributeProvider target)
		{
			foreach (var ca in source.CustomAttributes)
				target.CustomAttributes.Add(ca.Clone());
		}

		public static bool IsAttrDefined<T>(this ICustomAttributeProvider source)
		{
			return source.GetAttr<T>() != null;
		}

		public static CustomAttribute GetAttr<T>(this ICustomAttributeProvider source)
		{
			var expected = typeof(T).FullName;
			var retval = source.CustomAttributes
								.FirstOrDefault(a => a.AttributeType.FullName == expected);

			return retval;
		}

		public static CustomAttribute Named(this Collection<CustomAttribute> source, string name)
		{
			return source.FirstOrDefault(a => a.AttributeType.Name == name);
		}

		public static T GetPropertyValue<T>(this CustomAttribute source, string name)
		{
			var a = source.Properties.FirstOrDefault(cana => cana.Name == name);

			return a.Name == null ? default(T) : (T)a.Argument.Value;
		}

		public static void SetPropertyValue(this CustomAttribute source, string name, TypeReference type, object value)
		{
			var a = source.Properties.FirstOrDefault(cana => cana.Name == name);
			if (a.Name != null)
				source.Properties.Remove(a);

			source.Properties.Add(new CustomAttributeNamedArgument(name, new CustomAttributeArgument(type, value)));
		}

		public static bool TryGetPropertyValue<T>(this CustomAttribute source, string name, out T value)
		{
			var a = source.Properties.FirstOrDefault(cana => cana.Name == name);

			if (a.Name == null)
			{
				value = default(T);
				return false;
			}

			value = (T)a.Argument.Value;
			return true;
		}

		public static Instruction AsLdc_I(this int i)
		{
			return Instruction.Create(OpCodes.Ldc_I4, i);
		}

		public static IEnumerable<T> Once<T>(this T item)
		{
			return new[] { item };
		}

		public static TypeDefinition NewType(this ModuleDefinition module, string @namespace, string name, TypeReference baseType)
		{
			const MethodAttributes CtorAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
			const TypeAttributes TypeAttributes = TypeAttributes.Public | TypeAttributes.BeforeFieldInit;

			var retval = new TypeDefinition(@namespace, name, TypeAttributes, baseType);
			var ctor = new MethodDefinition(".ctor", CtorAttributes, module.TypeSystem.Void);

			var instructions = ctor.Body.Instructions;

			instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
			instructions.Add(Instruction.Create(OpCodes.Call, module.ImportReference(baseType.Resolve().FindConstructor())));
			instructions.Add(Instruction.Create(OpCodes.Ret));

			retval.Methods.Add(ctor);

			return retval;
		}

		public static Instruction[] GetOpsOf(this MethodDefinition method, OpCode op)
		{
			return method.Body.Instructions.Where(i => i.OpCode == op).ToArray();
		}

		public static void Add<T>(this Collection<T> source, IEnumerable<T> what)
		{
			foreach (var w in what)
				source.Add(w);
		}

		public static void Add<T>(this Collection<T> source, params T[] what)
		{
			foreach (var w in what)
				source.Add(w);
		}

		public static void Remove<T>(this Collection<T> source, IEnumerable<T> what)
		{
			foreach (var w in what)
				source.Remove(w);
		}

		public static MethodReference ImportInto(this MethodReference method, ModuleDefinition module)
		{
			return module.ImportReference(method);
		}

		public static TypeReference ImportInto(this TypeReference type, ModuleDefinition module)
		{
			return module.ImportReference(type);
		}

		public static CustomAttribute NewAttr(this ModuleDefinition module, Type attrType, params object[] ctorArgs)
		{
			return module.NewAttr(module.ImportReference(attrType), ctorArgs);
		}

		public static CustomAttribute NewAttr(this ModuleDefinition module, TypeReference attrRef, params object[] ctorArgs)
		{
			var cat = ctorArgs.Select(o => o == null ? module.TypeSystem.Object : module.ImportReference(o.GetType())).ToArray();
			var attrImpl = attrRef.Resolve();
			var ctor = module.ImportReference(attrImpl.FindConstructor(cat));
			var retval = new CustomAttribute(ctor);

			for (var i = 0; i < ctorArgs.Length; i++)
			{
				retval.ConstructorArguments.Add(new CustomAttributeArgument(cat[i], ctorArgs[i]));
			}

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
