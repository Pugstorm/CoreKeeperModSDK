# Reinterpret a dynamic buffer

You can reinterpret a `DynamicBuffer<T>` to get another `DynamicBuffer<U>`, where `T` and `U` have the same size. This can be useful if you want to reinterpret a dynamic buffer of components as a dynamic buffer of the entities that the components are attached to. This reinterpretation aliases the same memory, so changing the value at index `i` of one changes the value at index `i` of the other.

> [!NOTE]
> The `Reinterpret` method only enforces that the original type and new type have the same size. For example, you can reinterpret a `uint` to a `float` because both types are 32-bit. It's your responsibility to decide whether the reinterpretation makes sense for your purposes.

The following code sample shows how to interpret a dynamic buffer. It assumes a dynamic buffer called `MyElement` exists and contains a single `int` field called `Value`.

```csharp
public class ExampleSystem : SystemBase
{
    private void ReinterpretEntitysChunk(Entity e)
    {
        DynamicBuffer<MyElement> myBuff = EntityManager.GetBuffer<MyElement>(e);

        // Valid as long as each MyElement struct is four bytes. 
        DynamicBuffer<int> intBuffer = myBuff.Reinterpret<int>();

        intBuffer[2] = 6;  // same effect as: myBuff[2] = new MyElement { Value = 6 };

        // The MyElement value has the same four bytes as int value 6. 
        MyElement myElement = myBuff[2];
        Debug.Log(myElement.Value);    // 6
    }
}
```

> [!NOTE]
> Reinterpreted buffers share the safety handle of the original buffer and so are subject to all the same safety restrictions.