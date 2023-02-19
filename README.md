# Fixed-Width Ordered Binary (FWOB) Format Library

This repository contains the library implementation of the FWOB file format.

## Features

* Enfores increasing order of data frames by key
* Binary format storage
* User-defined flat data structure
* Supports all primitive field types including string
* High-efficient (de)serializer (40% performance vs the best C++ implementation)
* Built-in support for a string table
* Supports on-disk file and in-memory storage

## How to Use

### Define your frame data structure

The data structure can be any class, structure or record that implements `Mozo.Fwob.Models.IFrame<TKey>`, where `TKey` is a struct type and implements `IComparable<TKey>`.

The data structure can have fields, methods, indexers, properties, custom parameterized constructors and non-public members. But only public fields and the `Key` property getter will be used and accessed by the FWOB library.

Since the frame must be fixed-width, a string type must define a fixed length. This can be achieved by using the `Mozo.Fwob.LengthAttribute`.

Here is an example,

```csharp
public class StockTick : IFrame<uint>
{
    public uint Time;

    public uint Price;

    public double RealPrice
    {
        get => Price / 10000.0;
        set => Price = (uint)Math.Round(value * 10000);
    }

    public int Size;

    [Length(4)]
    public string SpecCond;

    public uint Key => Time;
}
```

Only the four public fields `Time`, `Price`, `Size` and `SpecCond` will hold data in a FWOB file. The `RealPrice` and `Key` property won't hold any data, though the `Key` property will be used by the FWOB library to compare and order the frames in the file.

### Create an on-disk FWOB file

```csharp
var fwobFile = FwobFile<StockTick, uint>.CreateNewFile(path, "FileTitle");
```

### Open an existing on-disk FWOB file

```csharp
var fwobFile = new FwobFile<StockTick, uint>(path);
```

### Create an in-memory FWOB file

```csharp
var fwobFile = new InMemoryFwobFile<StockTick, uint>(title);
```

### Read frames

```csharp
// Get the first frame in the file
var firstFrame = fwobFile.FirstFrame;

// Get the last frame in the file
var lastFrame = fwobFile.LastFrame;

// Get the first frame of a given key in the file
var frame = fwobFile.GetFrame(key);

// Get an enumerator for iterating the frames of a given key in the file, in case the key is not unique
var frames = fwobFile.GetFrames(key);

// Get an enumerator for iterating the frames of a given key range [firstKey, lastKey) in the file
var frames = fwobFile.GetFrames(firstKey, lastKey);

// Get an enumerator for iterating the frames of a given lower bound (inclusive) in the file
var frames = fwobFile.GetFramesAfter(firstKey);

// Get an enumerator for iterating the frames of a given higher bound (inclusive) in the file
var frames = fwobFile.GetFramesBefore(lastKey);
```

### Write frames

```csharp
// Append frames to the end of the file, only the ascending prefixing frames will be taken
long appendedCount = fwobFile.AppendFrames(frames);

// Append frames to the end of the file while enforcing the ascending order by key and no data will be appended if the ordering rule is violated
long appendedCount = fwobFile.AppendFramesTx(frames);
```

### Delete frames

```csharp
// Deletes all frames whose key is greater than or equal to the given key
long deletedCount = fwobFile.DeleteFramesAfter(firstKey);

// Deletes all frames whose key is less than or equal to the given key
long deletedCount = fwobFile.DeleteFramesBefore(lastKey);

// Deletes all frames from the storage
fwobFile.ClearFrames();
```

### String table

```csharp
// Load and unload the string table into memory, not needed in the in-memory storage
fwobFile.LoadStringTable();
fwobFile.UnloadStringTable();

// The string table is accessible via a list
var list = fwobFile.Strings;

// Get a specific string at a given index
var str = fwobFile.GetString(index);

// Get a specific index of a given string
int index = fwobFile.GetString(str);

// Append a string to the string table if not exist
int index = fwobFile.AppendString(str);

// Check if a string is in the string table
bool exist = fwobFile.ContainsString(str);

// Remove all strings from the string table
fwobFile.ClearStrings();
```
