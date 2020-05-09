<p>
<img align="right" src="nuget-icon.png">  

# nupkg-validator
</p>

Inspect and validate the contents of your NuGet packages before you push them out in the world.

Available inspections

- Inspects that all dlls are build in `Release` configuration
- Inspects version numbers of dlls inside the nuget package.
- Inspect that dlls have the right public key token applied

The tool will also emit all metadata in a way that its easy unleash your own bash/powershell/scripting skills
against standard out.

## Installation


Distributed as a .NET tool so install using the following

```
dotnet tool install nupkg-validator
```

## Run 

```bat
dotnet nupkg-validator 
```

You can omit `dotnet` if you install this as a global tool

```bat
USAGE: nupkg-validator [--help] [--assemblynametolookfor <string>] [--dllstoskip <string>]
                       [--expectedversion <string>] [--notmajoronly <bool>] [--publickey <string>]
                       [--nodependencies <bool>] <string>

NUGETPACKAGEPATH:

  <string>             Specify the path to the nuget package

OPTIONS:

  --assemblynametolookfor, -a <string>
        Filter for dll(s) with this AssemblyName
  --dllstoskip, -d <string>
        Filter, comma separated list of strings of dlls file names to skip, defaults to none
  --expectedversion, -v <string>
        Assert that this version number was set properly on the dlls
  --notmajoronly, -n <bool>
        Assert AssemblyVersion is the --expectedversion, by default we assert its MAJOR.0.0.0
  --publickey, -k <string>
        Assert this public key token makes it way on the AssemblyName for the dlls
  --nodependencies <bool>
        Assert the package has NO dependencies
  --help                display this list of options.
```

#### Examples:

Print out nuspec and dll metadata information.

By default the tool inspects all dlls for Release mode. There is no toggle to turn this of. Feel free to open an issue
with your usecase if you need this.

```bat
dotnet nupkg-validator  build/output/nupkg-validator*.nupkg  
```

truncated output example:

```
Temp output folder: /tmp/nupkg-validator.0.2.1-canary.0.9

[nuspec] file: /tmp/nupkg-validator.0.2.1-canary.0.9/nupkg-validator.nuspec
[nuspec] namespace: http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd
[metadata] id: nupkg-validator 
[metadata] version: 0.2.1-canary.0.9 
[metadata] title: nupkg-validator: a dotnet tool to validate NuGet packages 
[metadata] authors: nupkg-validator 
[metadata] owners: nupkg-validator 
...
[dll] tools/netcoreapp3.1/any/nupkg-validator.dll
[dll] nupkg-validator, Version=0.0.0.0, Culture=neutral, PublicKeyToken=96c599bbe3e70f5d
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.2.1.0
[version] Informational: 0.2.1-canary.0.9
```

##### Validate version

```bat
dotnet nupkg-validator  build/output/nupkg-validator*.nupkg -v 0.2.1-canary.0.9
```

Asserts best practices are being [followed around open source libraries](https://docs.microsoft.com/en-ca/dotnet/standard/library-guidance/versioning#version-numbers)

```
[version] Assembly: 0.0.0.0
[version] AssemblyFile: 0.2.1.0
[version] Informational: 0.2.1-canary.0.9
```

Noteworthy is that the `AssemblyVersion` is expected to be `Major.0.0.0`, if you don't follow this pattern use `--notmajoronly`

```bat
dotnet nupkg-validator  build/output/nupkg-validator*.nupkg -v 0.2.1-canary.0.9 --notmajoronly true
```

##### Validate strong name

```bat
dotnet nupkg-validator  build/output/nupkg-validator*.nupkg -k 96c599bbe3e
```

Asserts `PublicKeyToken=96c599bbe3e` is part of the full assembly name

##### Validate no nuget dependencies

A flag to fail the tool if the nuspec file declares dependencies to other NuGet packages.

```bat
dotnet nupkg-validator  build/output/nupkg-validator*.nupkg --nodependencies true
```

