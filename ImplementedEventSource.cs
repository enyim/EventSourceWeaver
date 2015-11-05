using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace Weavers.Internal
{
	internal class ImplementedEventSource: Implemented<TypeDefinition>
	{
		public Implemented<MethodDefinition>[] Methods;
	}

	internal class Implemented<T>
	{
		public T Old;
		public T New;

		public static Implemented<T> Create(T old, T @new)
		{
			return new Implemented<T>
			{
				New = @new,
				Old = old
			};
		}
	}

	internal static class Implemented
	{
		public static Implemented<T> Create<T>(T old, T @new)
		{
			return new Implemented<T>
			{
				New = @new,
				Old = old
			};
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
