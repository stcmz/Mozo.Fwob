# Fixed-Width Ordered Binary (FWOB) Format Library

[![NuGet version (Mozo.Fwob)](https://img.shields.io/nuget/v/Mozo.Fwob.svg)](https://www.nuget.org/packages/Mozo.Fwob/)
![Build workflow](https://github.com/stcmz/Mozo.Fwob/actions/workflows/build.yml/badge.svg)
![Release workflow](https://github.com/stcmz/Mozo.Fwob/actions/workflows/release.yml/badge.svg)

This repository contains the library implementation of the FWOB file format.

## Features

* Enfores increasing order of data frames by key
* Binary format storage
* User-defined flat data structure
* High-efficient (de)serializer (40% performance vs the best C++ implementation)
* Built-in support for a string table
* Supports on-disk file and in-memory storage

## Capacity

* File length: up to 2^63 - 1 bytes (8,192 PiB)
* String table length: up to 2^31 - 1 bytes (2 GiB)
* Fwob title: up to 16 bytes
* Frame name: up to 16 bytes
* Frame type: up to 16 fields
* Field name: up to 8 bytes
* Field type: of any primitive types (non-nullable) and string type
* String field length: up to 255 bytes

## Build from source

```bash
# Restore any necessary imported packages
dotnet restore

# Build the entire solution
dotnet build --no-restore

# Run the test cases in the test directory and generate coverage report
dotnet test --no-build --verbosity normal /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Visualize coverage report in HTML (requires the dotnet tool dotnet-reportgenerator-globaltool to be installed)
reportgenerator -reports:test/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html -historydir:CoverageHistory

# Publish a portable Mozo.Fwob.dll for any supported system that have the .NET 7.0 installed
dotnet publish src -c Release -f net7.0 --no-self-contained

# Pack the built manifests into a .nupkg package
dotnet pack -c Release
```

## How to Use

### Define your frame data structure

The data structure can be any POCO class or record class with a key field (annotated or by convention the first type-matched field) defined as a struct type that implements `IComparable<TKey>`.

The data structure can have fields, methods, indexers, properties, custom parameterized constructors and non-public members. But only public fields and the parameterless constructor will be accessed by the FWOB library.

Since the frame must be fixed-width, a field of string type must define a fixed length. This can be achieved by annotating with the `Mozo.Fwob.LengthAttribute`.

Here is an example,

```csharp
public class StockTick
{
    public uint Price;

    [Key]
    public uint Time;

    public double RealPrice
    {
        get => Price / 10000.0;
        set => Price = (uint)Math.Round(value * 10000);
    }

    public int Size;

    [Ignore]
    public int Extra;

    [Length(4)]
    public string SpecCond;
}
```

Only the public fields `Price`, `Time`, `Size` and `SpecCond` will hold data in a FWOB file. The `RealPrice` property, which is not a field, and the `Extra` field, which is ignored with the `Mozo.Fwob.IgnoreAttribute` annotation, won't hold any data in the file. By convention, the library use the first field defined of `TKey` type as the key. To specify a custom key, use the `Mozo.Fwob.KeyAttribute` to annotate a field of `TKey` type. The key field will be used by the library to compare and order the frames in the file.

The library checks the schema in the initialization of either an `FwobFile<TFrame, TKey>` or an `InMemoryFwobFile<TFrame, TKey>` file object. Exceptions will be thrown if conflicts is detected or rules are violated.

### Create an on-disk FWOB file

```csharp
var fwobFile = FwobFile<StockTick, uint>.CreateNew(fileName, "FileTitle");
```

### Open an existing on-disk FWOB file

```csharp
var fwobFile = new FwobFile<StockTick, uint>(fileName);
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
// Load and unload the string table into memory, not needed for the in-memory storage
fwobFile.LoadStringTable();
fwobFile.UnloadStringTable();

// The string table is accessible via a list
var list = fwobFile.Strings;

// Get a specific string at a given index
var str = fwobFile.GetString(index);

// Get a specific index of a given string
int index = fwobFile.GetString(str);

// Append a string to the back of the string table (duplicate allowed)
int index = fwobFile.AppendString(str);

// Check if a string is in the string table
bool exist = fwobFile.ContainsString(str);

// Remove all strings from the string table
fwobFile.ClearStrings();
```
