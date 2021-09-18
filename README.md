# QuakePakSharp

A .NET Core library to handle Quake .pak files.

Note that this library loads the whole pak into memory.

## Quickstart
**Nuget**:  
[![NuGet](https://img.shields.io/nuget/dt/QuakePakSharp.svg)](https://www.nuget.org/packages/QuakePakSharp/)  
`Install-Package QuakePakSharp`

## Usage example

```csharp
// Loading a pak file
var pak = await PakFile.FromFileAsync("pak0.pak");

// Fetching a file from the pak
var map = pak.FindEntryByName("maps/start.bsp");
var data = map.Data;

// Adding new file to pak
pak.Entries.Add(new PakFile.Entry("maps/start2.bsp",data));

// Saving pak
await pak.SaveAsync("pak0.pak");
```


