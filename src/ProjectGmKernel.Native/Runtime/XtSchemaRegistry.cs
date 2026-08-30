using System.Globalization;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal readonly record struct XtSchemaRegistration(
    string Identity,
    string ResourceFileName,
    int ModelerVersion,
    int SchemaNumber,
    int NodeCount,
    int FieldCount);

internal sealed class XtSchemaDefinition
{
    private readonly XtNodeDescriptor[] nodes;
    private readonly XtFieldDescriptor[] fields;

    public XtSchemaDefinition(
        string identity,
        int modelerVersion,
        int schemaNumber,
        XtNodeDescriptor[] nodes,
        XtFieldDescriptor[] fields)
    {
        Identity = identity;
        ModelerVersion = modelerVersion;
        SchemaNumber = schemaNumber;
        this.nodes = nodes;
        this.fields = fields;
    }

    public string Identity { get; }
    public int ModelerVersion { get; }
    public int SchemaNumber { get; }
    public ReadOnlySpan<XtNodeDescriptor> Nodes => nodes;
    public ReadOnlySpan<XtFieldDescriptor> Fields => fields;

    public XtNodeDescriptor GetNode(XtNodeType type)
    {
        foreach (var node in nodes)
        {
            if (node.Type == type)
                return node;
        }

        return default;
    }
}

internal static partial class XtSchemaRegistry
{
    private const string ResourcePrefix = "ProjectGmKernel.Native.Schemas.";
    private const int MaximumKnownProducerModelerVersion = 3800150;
    private static readonly XtSchemaDefinition?[] Cache = new XtSchemaDefinition?[RegistrationData.Length];
    private static readonly object Sync = new();

    internal static int Count => RegistrationData.Length;

    internal static bool TryResolve(string identity, out XtSchemaDefinition schema)
    {
        var registrationIndex = FindRegistration(identity);
        if (registrationIndex < 0)
        {
            schema = null!;
            return false;
        }

        schema = GetOrLoad(registrationIndex);
        return true;
    }

    internal static XtSchemaDefinition Resolve(string identity)
    {
        if (!TryResolve(identity, out var schema))
            throw new FormatException($"Unsupported XT schema {identity}.");
        return schema;
    }

    internal static XtSchemaDefinition ResolveBySchemaNumber(int schemaNumber)
    {
        var match = -1;
        for (var i = 0; i < RegistrationData.Length; i++)
        {
            if (RegistrationData[i].SchemaNumber != schemaNumber)
                continue;
            if (match >= 0)
                throw new FormatException($"XT schema number {schemaNumber} is ambiguous without a full identity.");
            match = i;
        }
        if (match < 0)
            throw new FormatException($"Unsupported XT schema number {schemaNumber}.");
        return GetOrLoad(match);
    }

    internal static bool TryResolveTransmitVersion(int transmitVersion, out XtSchemaDefinition schema)
    {
        var targetModelerVersion = -1;
        var targetSchemaNumber = transmitVersion switch
        {
            10 => 1000,
            11 => SetModeler(110, 0, ref targetModelerVersion),
            12 => SetModeler(120, 0, ref targetModelerVersion),
            13 => SetModeler(130, 0, ref targetModelerVersion),
            14 => SetModeler(140, 0, ref targetModelerVersion),
            20 => 1012,
            21 => SetModeler(210, 1012, ref targetModelerVersion),
            30 => 3000,
            40 => 4039,
            50 => 5059,
            60 => 6021,
            70 => 7007,
            80 => 8008,
            90 or 91 => 9008,
            100 or 101 => 10004,
            110 or 111 => 11004,
            120 => 12006,
            121 => 12103,
            130 or 132 => 13006,
            140 or 141 => 14000,
            150 => 15003,
            151 => 15102,
            160 => 16004,
            161 or 170 => 16100,
            171 => 17106,
            180 => 18007,
            181 => 18106,
            190 or 191 => 19008,
            200 or 210 or 220 or 221 or 230 or 231 or 240 or 241 => 20000,
            250 or 251 or 260 => 25001,
            261 or 270 or 271 => 26105,
            280 => 28002,
            281 or 290 or 291 => 28101,
            300 => 30000,
            301 => 30100,
            310 => 31001,
            311 => 31100,
            320 or 321 or 330 => 32001,
            331 => 33103,
            340 => 34001,
            341 => 34101,
            350 => 35001,
            351 => 35102,
            360 or 361 or 370 => 36001,
            371 or 380 => 37102,
            _ => -1,
        };
        if (targetSchemaNumber < 0)
        {
            schema = null!;
            return false;
        }

        var match = -1;
        for (var index = 0; index < RegistrationData.Length; index++)
        {
            var registration = RegistrationData[index];
            if (registration.SchemaNumber != targetSchemaNumber ||
                targetModelerVersion >= 0 && registration.ModelerVersion != targetModelerVersion)
                continue;
            if (match < 0 || registration.SchemaNumber > RegistrationData[match].SchemaNumber ||
                registration.SchemaNumber == RegistrationData[match].SchemaNumber &&
                registration.ModelerVersion > RegistrationData[match].ModelerVersion)
                match = index;
        }
        if (match < 0)
        {
            schema = null!;
            return false;
        }
        schema = GetOrLoad(match);
        return true;
    }

    private static int SetModeler(int modelerVersion, int schemaNumber, ref int targetModelerVersion)
    {
        targetModelerVersion = modelerVersion;
        return schemaNumber;
    }

    internal static XtSchemaDefinition GetByIndex(int index)
    {
        if ((uint)index >= (uint)RegistrationData.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return GetOrLoad(index);
    }

    private static int FindRegistration(string identity)
    {
        for (var i = 0; i < RegistrationData.Length; i++)
        {
            if (string.Equals(RegistrationData[i].Identity, identity, StringComparison.Ordinal))
                return i;
        }

        if (!TryParsePlainIdentity(identity, out var producerModelerVersion, out var schemaNumber) ||
            producerModelerVersion > MaximumKnownProducerModelerVersion)
            return -1;

        var uniqueMatch = -1;
        var matchCount = 0;
        for (var i = 0; i < RegistrationData.Length; i++)
        {
            if (RegistrationData[i].SchemaNumber != schemaNumber)
                continue;
            uniqueMatch = i;
            matchCount++;
        }
        if (matchCount == 1)
            return uniqueMatch;

        // The first numeric component identifies the producer modeller, not a
        // unique schema revision.  A producer may continue using an older
        // external schema, so select the newest bundled definition with the
        // requested schema number that is not newer than the producer.  A
        // maintenance build in the same major release is also compatible (the
        // locked 37102 file is 3701097 while V37 emits 3701000).
        var match = -1;
        for (var i = 0; i < RegistrationData.Length; i++)
        {
            var registration = RegistrationData[i];
            if (registration.SchemaNumber != schemaNumber ||
                registration.ModelerVersion > producerModelerVersion &&
                registration.ModelerVersion / 100000 != producerModelerVersion / 100000)
                continue;
            if (match < 0 || registration.ModelerVersion > RegistrationData[match].ModelerVersion)
                match = i;
        }
        return match;
    }

    private static XtSchemaDefinition GetOrLoad(int index)
    {
        var cached = Cache[index];
        if (cached is not null)
            return cached;

        lock (Sync)
        {
            cached = Cache[index];
            if (cached is not null)
                return cached;
            cached = Load(RegistrationData[index]);
            Cache[index] = cached;
            return cached;
        }
    }

    private static XtSchemaDefinition Load(XtSchemaRegistration registration)
    {
        var assembly = typeof(XtSchemaRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + registration.ResourceFileName)
            ?? throw new InvalidOperationException($"Embedded XT schema resource {registration.ResourceFileName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader, registration);
    }

    private static XtSchemaDefinition Parse(TextReader reader, XtSchemaRegistration registration)
    {
        var nodes = new List<XtNodeDescriptor>(registration.NodeCount);
        var fields = new List<XtFieldDescriptor>(registration.FieldCount);
        var nodeIds = new HashSet<int>();
        var foundStatistics = false;
        var foundTerminator = false;
        var declaredNodes = -1;
        var declaredFields = -1;
        var currentNode = -1;
        var currentNodeName = "";
        var currentNodeDescription = "";
        var currentNodeTransmit = false;
        var currentNodeVariable = false;
        var currentDeclaredFields = 0;
        var currentFieldOffset = 0;
        var lineNumber = 0;

        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (!foundStatistics)
            {
                var statistics = SplitWords(line);
                if (statistics.Length == 4 && statistics.All(static word => IsUnsignedInteger(word)))
                {
                    declaredNodes = ParseInt(statistics[1], registration.ResourceFileName, lineNumber);
                    declaredFields = ParseInt(statistics[2], registration.ResourceFileName, lineNumber);
                    foundStatistics = true;
                }
                continue;
            }

            if (line.StartsWith("**************** end of schema ", StringComparison.OrdinalIgnoreCase))
            {
                FlushNode();
                if (!line.Contains(registration.Identity, StringComparison.Ordinal))
                    throw Invalid("Schema terminator identity does not match its registration.");
                foundTerminator = true;
                continue;
            }

            if (line.Length == 0)
                continue;
            if (foundTerminator)
                throw Invalid("Unexpected content after schema terminator.");

            var segments = line.Split(';');
            if (segments.Length != 3)
                throw Invalid("Unrecognized schema line.");

            var first = segments[0].Trim();
            var tail = SplitWords(segments[2]);
            if (first.Length != 0 && char.IsDigit(first[0]))
            {
                FlushNode();
                var header = SplitWords(first);
                if (header.Length != 2 || tail.Length != 3)
                    throw Invalid("Invalid schema node declaration.");
                currentNode = ParseInt(header[0], registration.ResourceFileName, lineNumber);
                if (!nodeIds.Add(currentNode))
                    throw Invalid($"Duplicate schema node {currentNode}.");
                currentNodeName = header[1];
                currentNodeDescription = segments[1].Trim();
                currentNodeTransmit = ParseBit(tail[0]);
                currentDeclaredFields = ParseInt(tail[1], registration.ResourceFileName, lineNumber);
                currentNodeVariable = ParseBit(tail[2]);
                currentFieldOffset = fields.Count;
                continue;
            }

            if (currentNode < 0 || first.Length == 0 || tail.Length != 3)
                throw Invalid("Invalid schema field declaration.");
            var typeText = segments[1].Trim();
            if (typeText.Length != 1 || !"bcdfhilnpqtuvw".Contains(typeText[0]))
                throw Invalid("Invalid schema field type.");
            var elementCount = ParseInt(tail[2], registration.ResourceFileName, lineNumber);
            fields.Add(new XtFieldDescriptor(
                currentNode,
                first,
                typeText[0],
                ParseBit(tail[0]),
                ParseInt(tail[1], registration.ResourceFileName, lineNumber),
                elementCount));
        }

        FlushNode();
        if (!foundStatistics || !foundTerminator)
            throw Invalid("Schema header or terminator is missing.");
        if (declaredNodes != nodes.Count || declaredFields != fields.Count ||
            registration.NodeCount != nodes.Count || registration.FieldCount != fields.Count)
            throw Invalid($"Schema statistics mismatch: expected {registration.NodeCount}/{registration.FieldCount}, parsed {nodes.Count}/{fields.Count}.");

        return new XtSchemaDefinition(
            registration.Identity,
            registration.ModelerVersion,
            registration.SchemaNumber,
            nodes.ToArray(),
            fields.ToArray());

        void FlushNode()
        {
            if (currentNode < 0)
                return;
            var parsedFields = fields.Count - currentFieldOffset;
            if (parsedFields != currentDeclaredFields)
                throw Invalid($"Schema node {currentNodeName} declares {currentDeclaredFields} fields but contains {parsedFields}.");
            nodes.Add(new XtNodeDescriptor(
                currentNode,
                currentNodeName,
                currentNodeDescription,
                currentNodeTransmit,
                currentDeclaredFields,
                currentNodeVariable,
                currentFieldOffset,
                parsedFields));
            currentNode = -1;
        }

        FormatException Invalid(string message) => new($"{message} Resource {registration.ResourceFileName}, line {lineNumber}.");
    }

    private static string[] SplitWords(string value)
        => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsUnsignedInteger(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private static int ParseInt(string value, string resource, int line)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
            throw new FormatException($"Invalid integer {value} in {resource}:{line}.");
        return result;
    }

    private static bool ParseBit(string value) => value switch
    {
        "0" => false,
        "1" => true,
        _ => throw new FormatException($"Invalid schema bit {value}."),
    };

    private static bool TryParsePlainIdentity(string identity, out int modelerVersion, out int schemaNumber)
    {
        modelerVersion = 0;
        schemaNumber = 0;
        if (!identity.StartsWith("SCH_", StringComparison.Ordinal))
            return false;
        var separator = identity.IndexOf('_', 4);
        if (separator < 0 || identity.IndexOf('_', separator + 1) >= 0)
            return false;
        return int.TryParse(identity.AsSpan(4, separator - 4), NumberStyles.None, CultureInfo.InvariantCulture, out modelerVersion) &&
            int.TryParse(identity.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out schemaNumber);
    }

}
