using System.Globalization;
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Xt;

public sealed class XtSchemaCatalog
{
    private readonly string _directory;
    private readonly Registration[] _registrations;
    private readonly XtSchemaDefinition?[] _cache;
    private readonly object _sync = new();

    private XtSchemaCatalog(string directory, Registration[] registrations)
    {
        _directory = directory;
        _registrations = registrations;
        _cache = new XtSchemaDefinition?[registrations.Length];
        Schemas = registrations.Select(static registration => registration.Info).ToArray();
    }

    public string SchemaDirectory => _directory;
    public IReadOnlyList<XtSchemaInfo> Schemas { get; }

    public static XtSchemaCatalog OpenBuiltIn()
    {
        var registrations = XtBuiltInSchemas.Identities
            .Order(StringComparer.Ordinal)
            .Select(static identity =>
            {
                var definition = XtBuiltInSchemas.Resolve(identity);
                return new Registration(null, definition.Info, definition);
            })
            .ToArray();
        return new XtSchemaCatalog(string.Empty, registrations);
    }

    public static XtSchemaCatalog OpenDirectory(string schemaDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaDirectory);
        var directory = Path.GetFullPath(schemaDirectory);
        if (!Directory.Exists(directory))
            throw new XtFormatException(XtErrorCode.SchemaDirectoryNotFound, $"XT schema directory does not exist: {directory}");

        var paths = Directory.EnumerateFiles(directory, "sch_*.sch_txt", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registrations = new List<Registration>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identity in XtBuiltInSchemas.Identities.Order(StringComparer.Ordinal))
        {
            var definition = XtBuiltInSchemas.Resolve(identity);
            registrations.Add(new Registration(null, definition.Info, definition));
            identities.Add(identity);
        }
        foreach (var path in paths)
        {
            var info = Inspect(path);
            if (!identities.Add(info.Identity))
            {
                var external = Parse(new Registration(path, info, null));
                XtGeneratedSchemaRuntime.RequireShape(external, XtBuiltInSchemas.Resolve(info.Identity));
                continue;
            }
            registrations.Add(new Registration(path, info, null));
        }
        return new XtSchemaCatalog(directory, registrations.ToArray());
    }

    public void LoadAll()
    {
        for (var index = 0; index < _registrations.Length; index++)
            _ = GetOrLoad(index);
    }

    public bool TryResolve(string schemaIdentity, out XtSchemaDefinition schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaIdentity);
        var index = FindRegistration(schemaIdentity);
        if (index < 0)
        {
            schema = null!;
            return false;
        }
        schema = GetOrLoad(index);
        return true;
    }

    public XtSchemaDefinition Resolve(string schemaIdentity)
        => TryResolve(schemaIdentity, out var schema)
            ? schema
            : throw new XtFormatException(XtErrorCode.SchemaNotFound, $"XT schema {schemaIdentity} is not present in {_directory}.");

    public XtSchemaDefinition ResolveBySchemaNumber(XtSchemaNumber schemaNumber, XtModelerVersion producerModelerVersion = int.MaxValue)
    {
        var match = -1;
        for (var index = 0; index < _registrations.Length; index++)
        {
            var info = _registrations[index].Info;
            if (info.SchemaNumber != schemaNumber || info.ModelerVersion > producerModelerVersion &&
                info.ModelerVersion / 100000 != producerModelerVersion / 100000)
                continue;
            if (match < 0 || info.ModelerVersion > _registrations[match].Info.ModelerVersion)
                match = index;
        }
        if (match < 0)
            throw new XtFormatException(XtErrorCode.SchemaNotFound, $"XT schema number {schemaNumber} is not present in {_directory}.");
        return GetOrLoad(match);
    }

    private int FindRegistration(string identity)
    {
        for (var index = 0; index < _registrations.Length; index++)
        {
            if (string.Equals(_registrations[index].Info.Identity, identity, StringComparison.Ordinal))
                return index;
        }
        if (!TryParsePlainIdentity(identity, out var producerVersion, out var schemaNumber))
            return -1;
        var maximumModelerVersion = 0;
        for (var index = 0; index < _registrations.Length; index++)
            maximumModelerVersion = Math.Max(maximumModelerVersion, _registrations[index].Info.ModelerVersion);
        if (producerVersion / 100000 > maximumModelerVersion / 100000 + 1)
            return -1;

        var match = -1;
        for (var index = 0; index < _registrations.Length; index++)
        {
            var info = _registrations[index].Info;
            if (info.SchemaNumber != schemaNumber || info.ModelerVersion > producerVersion &&
                info.ModelerVersion / 100000 != producerVersion / 100000)
                continue;
            if (match < 0 || info.ModelerVersion > _registrations[match].Info.ModelerVersion)
                match = index;
        }
        return match;
    }

    private XtSchemaDefinition GetOrLoad(int index)
    {
        var cached = _cache[index];
        if (cached is not null)
            return cached;
        lock (_sync)
        {
            cached = _cache[index];
            if (cached is not null)
                return cached;
            cached = _registrations[index].Definition ?? Parse(_registrations[index]);
            _cache[index] = cached;
            return cached;
        }
    }

    private static XtSchemaInfo Inspect(string path)
    {
        var modelerVersion = -1;
        var nodeCount = -1;
        var fieldCount = -1;
        var identity = "";
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (modelerVersion < 0 && TryReadModelerVersion(line, out var version))
                modelerVersion = version;
            if (nodeCount < 0)
            {
                var words = SplitWords(line);
                if (words.Length == 4 && words.All(static word => IsUnsignedInteger(word)))
                {
                    nodeCount = ParseInt(words[1], path, 0);
                    fieldCount = ParseInt(words[2], path, 0);
                }
            }
            if (line.Contains("end of schema", StringComparison.OrdinalIgnoreCase))
                identity = ReadIdentity(line);
        }
        if (modelerVersion < 0 || nodeCount < 0 || fieldCount < 0 || identity.Length == 0)
            throw new XtFormatException(XtErrorCode.SchemaMalformed, $"XT schema header, statistics, or terminator is missing: {path}");
        var schemaNumber = ParseSchemaNumber(identity, path);
        return new XtSchemaInfo(identity, Path.GetFileName(path), modelerVersion, schemaNumber, nodeCount, fieldCount);
    }

    private static XtSchemaDefinition Parse(Registration registration)
    {
        var nodes = new List<XtNodeDescriptor>(registration.Info.NodeCount);
        var fields = new List<XtFieldDescriptor>(registration.Info.FieldCount);
        var nodeIds = new HashSet<XtNodeType>();
        var foundStatistics = false;
        var foundTerminator = false;
        var currentNode = -1;
        var currentNodeName = "";
        var currentNodeDescription = "";
        var currentNodeTransmit = false;
        var currentNodeVariable = false;
        var currentDeclaredFields = 0;
        var currentFieldOffset = 0;
        var lineNumber = 0;

        var path = registration.Path ?? throw new InvalidOperationException("Built-in schema has no source path.");
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (!foundStatistics)
            {
                var words = SplitWords(line);
                if (words.Length == 4 && words.All(static word => IsUnsignedInteger(word)))
                    foundStatistics = true;
                continue;
            }
            if (line.Contains("end of schema", StringComparison.OrdinalIgnoreCase))
            {
                FlushNode();
                if (!string.Equals(ReadIdentity(line), registration.Info.Identity, StringComparison.Ordinal))
                    throw Invalid("Schema terminator identity does not match its catalog entry.");
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
            if (first.Length != 0 && char.IsAsciiDigit(first[0]))
            {
                FlushNode();
                var header = SplitWords(first);
                if (header.Length != 2 || tail.Length != 3)
                    throw Invalid("Invalid schema node declaration.");
                currentNode = ParseInt(header[0], registration.Path, lineNumber);
                if (!nodeIds.Add(currentNode))
                    throw Invalid($"Duplicate schema node {currentNode}.");
                currentNodeName = header[1];
                currentNodeDescription = segments[1].Trim();
                currentNodeTransmit = ParseBit(tail[0]);
                currentDeclaredFields = ParseInt(tail[1], registration.Path, lineNumber);
                currentNodeVariable = ParseBit(tail[2]);
                currentFieldOffset = fields.Count;
                continue;
            }

            if (currentNode < 0 || first.Length == 0 || tail.Length != 3)
                throw Invalid("Invalid schema field declaration.");
            var typeText = segments[1].Trim();
            if (typeText.Length != 1 || !"bcdfhilnpqtuvw".Contains(typeText[0]))
                throw Invalid("Invalid schema field type.");
            var nodeClass = ParseInt(tail[1], registration.Path, lineNumber);
            var elementCount = ParseInt(tail[2], registration.Path, lineNumber);
            if (typeText[0] != 'p' && nodeClass != 0)
                throw Invalid($"Non-pointer field {currentNodeName}.{first} has pointer class {nodeClass}.");
            fields.Add(new XtFieldDescriptor(currentNode, first, typeText[0], ParseBit(tail[0]), nodeClass, elementCount));
        }

        FlushNode();
        if (!foundStatistics || !foundTerminator)
            throw Invalid("Schema header or terminator is missing.");
        if (nodes.Count != registration.Info.NodeCount || fields.Count != registration.Info.FieldCount)
            throw Invalid($"Schema statistics mismatch: expected {registration.Info.NodeCount}/{registration.Info.FieldCount}, parsed {nodes.Count}/{fields.Count}.");
        foreach (var node in nodes)
        {
            var variable = false;
            foreach (var field in CollectionsMarshal.AsSpan(fields).Slice(node.FieldOffset, node.ParsedFieldCount))
            {
                if (field.Type == 'p' && field.NodeClass is > 0 and < 1000 && !nodeIds.Contains(field.NodeClass))
                    throw Invalid($"Pointer field {node.Name}.{field.Name} references unknown class {field.NodeClass}.");
                variable |= field.ElementCount == 1;
            }
            if (variable != node.Variable)
                throw Invalid($"Schema node {node.Name} variable flag does not match its field declarations.");
        }
        return new XtSchemaDefinition(registration.Info, nodes.ToArray(), fields.ToArray());

        void FlushNode()
        {
            if (currentNode < 0)
                return;
            var parsedFields = fields.Count - currentFieldOffset;
            if (parsedFields != currentDeclaredFields)
                throw Invalid($"Schema node {currentNodeName} declares {currentDeclaredFields} fields but contains {parsedFields}.");
            nodes.Add(new XtNodeDescriptor(currentNode, currentNodeName, currentNodeDescription, currentNodeTransmit,
                currentDeclaredFields, currentNodeVariable, currentFieldOffset, parsedFields));
            currentNode = -1;
        }

        XtFormatException Invalid(string message)
            => new(XtErrorCode.SchemaMalformed, $"{message} File {path}, line {lineNumber}.");
    }

    private static bool TryReadModelerVersion(string line, out XtModelerVersion version)
    {
        const string marker = "modeller version";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            version = 0;
            return false;
        }
        start += marker.Length;
        while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
        var end = start;
        while (end < line.Length && char.IsAsciiDigit(line[end])) end++;
        return int.TryParse(line.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    private static string ReadIdentity(string line)
    {
        var start = line.IndexOf("SCH_", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "";
        var end = start + 4;
        while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] == '_')) end++;
        return line[start..end];
    }

    private static XtSchemaNumber ParseSchemaNumber(string identity, string path)
    {
        var separator = identity.LastIndexOf('_');
        var number = separator > 3 ? identity.AsSpan(separator + 1) : identity.AsSpan(4);
        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
            throw new XtFormatException(XtErrorCode.SchemaMalformed, $"Invalid XT schema identity {identity} in {path}.");
        return result;
    }

    private static bool TryParsePlainIdentity(string identity, out XtModelerVersion modelerVersion, out XtSchemaNumber schemaNumber)
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

    private static string[] SplitWords(string value)
        => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsUnsignedInteger(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private static int ParseInt(string value, string path, int line)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
            throw new XtFormatException(XtErrorCode.SchemaMalformed, $"Invalid integer {value} in {path}:{line}.");
        return result;
    }

    private static bool ParseBit(string value) => value switch
    {
        "0" => false,
        "1" => true,
        _ => throw new XtFormatException(XtErrorCode.SchemaMalformed, $"Invalid schema bit {value}."),
    };

    private readonly record struct Registration(string? Path, XtSchemaInfo Info, XtSchemaDefinition? Definition);
}
