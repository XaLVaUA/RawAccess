# RawAccess

**RawAccess** is a C# source generator that automatically produces static helper classes for accessing and updating public fields and properties of your types.

It generates:

- `Get` methods for reading public fields and properties
- `With` methods for creating copies with modified values
- Factory methods for constructing instances without manual boilerplate

Add the package to your project:

```bash
dotnet add package RawAccess.SourceGeneration
```

The generator will run automatically during build.

## Example

Given a type:

```csharp
[GenerateRawAccess]
public class MyClass<TA, TB, TC>
    where TA : TB
    where TB : IEnumerable<TC>
    where TC : class
{
    public TA A { get; }
    public TB B { get; set; }
    public TC C { set { } }

    public MyClass(string myString, TA a, TB b, TC c)
    {
        A = a;
        B = b; 
        C = c;
    }
}
```

RawAccess will generate a static helper class:

```csharp
public static class MyClassRawAccess
{
    public static MyClass<TA, TB, TC> GetMyClass<TA, TB, TC>(string myString, TA a, TB b, TC c) { ... }

    public static TA GetA<TA, TB, TC>(MyClass<TA, TB, TC> instance) => instance.A;
    public static TB GetB<TA, TB, TC>(MyClass<TA, TB, TC> instance) => instance.B;
    public static MyClass<TA, TB, TC> WithB<TA, TB, TC>(MyClass<TA, TB, TC> instance, TB value) { ... }
    public static MyClass<TA, TB, TC> WithC<TA, TB, TC>(MyClass<TA, TB, TC> instance, TC value) { ... }
}
```

## Usage

1. Add the `[GenerateRawAccess]` attribute to your class, struct, or record.
2. Build your project.
3. Use the generated `*RawAccess` static class to construct, get, and modify instances.

Example test:

```csharp
var myStruct = MyStructRawAccess.GetMyStruct("str");
var updated = MyStructRawAccess.WithStr(myStruct, "newStr");
Assert.AreEqual("newStr", MyStructRawAccess.GetStr(updated));
```

## License

This project is dedicated to the public domain under the **Unlicense**.  
See the [LICENSE](LICENSE) file for details.
