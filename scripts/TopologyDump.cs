#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

unsafe
{
    RestartSession();

    int block;
    Check(KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &block), "BodyCreateSolidBlock");
    var blockDump = DumpBody("block", block);
    Console.WriteLine(blockDump);
    RequireContains(blockDump, "regions: 2");
    RequireContains(blockDump, "region[0] void");
    RequireContains(blockDump, "region[1] solid");
    RequireContains(blockDump, "shells: 2");
    RequireContains(blockDump, "faces: 6");
    RequireContains(blockDump, "face-uses: negative=6 positive=6");

    int cylinder;
    Check(KernelRuntime.BodyCreateSolidCyl(2, 5, null, &cylinder), "BodyCreateSolidCyl");
    var cylinderDump = DumpBody("cylinder", cylinder);
    Console.WriteLine(cylinderDump);
    RequireContains(cylinderDump, "regions: 2");
    RequireContains(cylinderDump, "shells: 2");
    RequireContains(cylinderDump, "faces: 3");
    RequireContains(cylinderDump, "edges: 2");
    RequireContains(cylinderDump, "vertices: 0");
    RequireContains(cylinderDump, "face-uses: negative=3 positive=3");

    Check(KernelRuntime.SessionStop(), "SessionStop");
}

static unsafe string DumpBody(string name, int body)
{
    var sb = new StringBuilder();
    sb.AppendLine($"body {name}: {body}");

    int count;
    int* tags;
    Check(KernelRuntime.BodyAskRegions(body, &count, &tags), "BodyAskRegions");
    sb.AppendLine($"regions: {count}");
    for (int i = 0; i < count; i++)
    {
        byte isSolid;
        Check(KernelRuntime.RegionIsSolid(tags[i], &isSolid), "RegionIsSolid");
        sb.AppendLine($"  region[{i}] {(isSolid != 0 ? "solid" : "void")} tag={tags[i]}");
    }

    int nShells;
    int* shells;
    Check(KernelRuntime.BodyAskShells(body, &nShells, &shells), "BodyAskShells");
    sb.AppendLine($"shells: {nShells}");
    for (int i = 0; i < nShells; i++)
        sb.AppendLine($"  shell[{i}] tag={shells[i]}");

    int nFaces;
    int* faces;
    Check(KernelRuntime.BodyAskFaces(body, &nFaces, &faces), "BodyAskFaces");
    sb.AppendLine($"faces: {nFaces}");
    int* faceShells = stackalloc int[2];
    for (int i = 0; i < nFaces; i++)
    {
        Check(KernelRuntime.FaceAskShells(faces[i], faceShells), "FaceAskShells");
        sb.AppendLine($"  face[{i}] tag={faces[i]} back_shell={faceShells[0]} front_shell={faceShells[1]}");
    }

    Check(KernelRuntime.BodyAskEdges(body, &count, &tags), "BodyAskEdges");
    sb.AppendLine($"edges: {count}");
    Check(KernelRuntime.BodyAskVertices(body, &count, &tags), "BodyAskVertices");
    sb.AppendLine($"vertices: {count}");

    int nTopols;
    nint topolsRaw;
    nint classesRaw;
    int nRelations;
    nint parentsRaw;
    nint childrenRaw;
    nint sensesRaw;
    Check(KernelRuntime.BodyAskTopology(body, null, &nTopols, &topolsRaw, &classesRaw, &nRelations, &parentsRaw, &childrenRaw, &sensesRaw), "BodyAskTopology");
    sb.AppendLine($"topols: {nTopols}");
    sb.AppendLine($"relations: {nRelations}");

    var senses = (int*)sensesRaw;
    var negative = CountSense(senses, nRelations, ParasolidConstants.PK_TOPOL_sense_negative_c);
    var positive = CountSense(senses, nRelations, ParasolidConstants.PK_TOPOL_sense_positive_c);
    sb.AppendLine($"face-uses: negative={negative} positive={positive}");

    return sb.ToString();
}

static unsafe int CountSense(int* senses, int count, int value)
{
    int result = 0;
    for (int i = 0; i < count; i++)
    {
        if (senses[i] == value)
            result++;
    }
    return result;
}

static unsafe void RestartSession()
{
    KernelRuntime.SessionStop();
    var options = new PK_SESSION_start_o_s { o_t_version = 1 };
    Check(KernelRuntime.SessionStart(&options), "SessionStart");
}

static void Check(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void RequireContains(string text, string fragment)
{
    if (!text.Contains(fragment, StringComparison.Ordinal))
        throw new InvalidOperationException($"Topology dump missing '{fragment}'");
}
