# How to use

You will need Fody.

Also, you have to add the repo as a submodule to your project as `Solution_Root`\Weavers. Nuget is not supported yet.

1. Projects that need automatic EventSources must have a reference to the Weavers assembly.
2. Define your abstract/interface template
3. Every time you need to instantiate the Event Source, use the `Enyim.EventSourceFactory.Get<TemplateType>()`. These calls will be rewritten by the weaver to `new ImplementedTemplateType()`

_Limitations: you cannot use the auto-generated Event Sources accross assemblies, as in it's defined in one assembly, but instantiated in another._

## Implement an abstract template

The weaver will implement all abstract classes inheriting from the `EventSource` class. (Does not matter if it's the built-oni or the one from Nuget.)

```csharp
using System.Diagnostic.Tracing;
// or use the nuget package
//using Microsoft.Diagnostic.Tracing;

[EventSource(Name = "Enyim-Test-Adder")]
public abstract class AdderEventSource : EventSource
{
	[Event(1)]
	public abstract void AddStart(string a, string b);

	[Event(2)]
	public abstract void AddStop(string a, string b);

	[Event(3)]
	public unsafe void AddProgress(string a, string a2, bool b, int c, long d)
	{
		fixed (char* ptr_a = a)
		fixed (char* ptr_a2 = a2)
		{
			var data = stackalloc EventData[5];

			data[0].Size = (a.Length + 1) * sizeof(char);
			data[0].DataPointer = (IntPtr)ptr_a;

			data[1].Size = (a2.Length + 1) * sizeof(char);
			data[1].DataPointer = (IntPtr)ptr_a2;

			var tmp = b ? 1 : 0;
			data[2].Size = sizeof(int);
			data[2].DataPointer = (IntPtr)(&tmp);

			data[3].Size = sizeof(int);
			data[3].DataPointer = (IntPtr)(&c);

			data[4].Size = sizeof(long);
			data[4].DataPointer = (IntPtr)(&d);

			WriteEventCore(3, 5, data);
		}
	}
}
```

Abstract methods will be implemented using the pattern:

	if (IsEnabled()) WriteEvent(eventId, arg1, arg2, argN);

The code will try to use a matching overload of WriteEvent, but eventually it will fall back to the slow `object[] args` version. You can avoid this by implementing your own "fast" WriteEvent (see the above example and the MSDN documentation). These methods wil not be touched by the weaver.

## Implement an interface

Just define your logger as an interface (as opposed to the abstract class). There is no real advantage to it, it's just personal taste. Only thing to look out for is the `EventSource` attribute, as it cannot be applied to interfaces. Also, since you are using an interfacem you have to tell the weaver whch ones to implement.

You can use the `AsEventSource` attribute for both purposes.

```csharp
[AsEventSource(Name = "Event-Source-Test")]
public interface ICalculatorEventSource
{
	[Event(1)]
	void Add(string a, string b);

	[Event(2)]
	void Replace(string a, float b);

	void Clear(int a, string b);
}
```

The implemented class will be called the same as the interface without the `I` prefix. (=> `CalculatorEventSource`)

## Event Ids

Methods that does not have the `Event` attribute applied will get one with an auto-generated ID, starting from the largest defined event id  +1.

## Keywords, Tasks and Opcodes

### Abstract templates

Just use the standard way of defining them. (Nested static classes, see MSDN for more info.)

### Interface templates

Since interfaces cannot have nested types, you have to use special naming conventions:

```csharp
[AsEventSource(Name = "Event-Source-Test")]
public interface ICalculatorEventSource
{
	[Event(1, Keywords = CalculatorEventSourceKeywords.Add)]
	void Add(string a, string b);

	[Event(2, Keywords = CalculatorEventSourceKeywords.Replace)]
	void Replace(string a, float b);

	void Clear(int a, string b);
}

public static class CalculatorEventSourceKeywords
{
	public const EventKeywords Add = (EventKeywords)1;
	public const EventKeywords Replace = (EventKeywords)2;
}

public static class CalculatorEventSourceTasks { }
public static class CalculatorEventSourceOpcodes { }
```

### Automatic Keywords and Opcodes

If method
 
- named using the `SomethingHappening` pattern
- the method does not have Task and Opcode defined,

then the weaver will automatically generate both and apply the appropriate `Event` attribute. This works with both implemented and existing (in case of abstract templates) methods.

In the above example `Something` will become the Task and `Happening` the Opcode.


Currently there is no way of opting out of this behavior.
