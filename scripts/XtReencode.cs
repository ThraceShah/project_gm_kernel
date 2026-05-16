#!/usr/bin/env dotnet run
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: dotnet run scripts/XtReencode.cs -- INPUT.x_t OUTPUT.x_t");
    return 2;
}

var nodes = XtText.Decode(File.ReadAllText(args[0]));
File.WriteAllText(args[1], XtText.Encode(nodes));
return 0;
