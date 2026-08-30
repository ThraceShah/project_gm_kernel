using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using System.Text;

namespace KernelTests;

public sealed unsafe class XtSchemaRegistryTests
{
    [Fact]
    public void Registry_LoadsEveryBundledSchemaWithConsistentFieldRanges()
    {
        Assert.Equal(101, XtSchemaRegistry.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (var schemaIndex = 0; schemaIndex < XtSchemaRegistry.Count; schemaIndex++)
        {
            var schema = XtSchemaRegistry.GetByIndex(schemaIndex);
            Assert.True(identities.Add(schema.Identity), schema.Identity);
            Assert.NotEmpty(schema.Nodes.ToArray());
            Assert.NotEmpty(schema.Fields.ToArray());
            foreach (var node in schema.Nodes)
            {
                Assert.Equal(node, schema.GetNode(node.Type));
                Assert.InRange(node.FieldOffset, 0, schema.Fields.Length);
                Assert.InRange(node.ParsedFieldCount, 0, schema.Fields.Length - node.FieldOffset);
            }
        }
    }

    [Fact]
    public void Registry_ResolvesNormalizedCurrentSchemaIdentity()
    {
        var schema = XtSchemaRegistry.Resolve("SCH_3701000_37102");
        Assert.Equal(37102, schema.SchemaNumber);
        Assert.Equal("SCH_3701097_37102", schema.Identity);
        Assert.Equal(20000, XtSchemaRegistry.Resolve("SCH_2300000_20000").SchemaNumber);
        Assert.Equal(37102, XtSchemaRegistry.Resolve("SCH_3800150_37102").SchemaNumber);
        Assert.Throws<FormatException>(() => XtSchemaRegistry.Resolve("SCH_9999999_37102"));
    }

    [Fact]
    public void Registry_ReportsIdenticalDescriptorsForSchemas5030And5031()
    {
        var schemas = XtCorpusInspection.GetSupportedSchemas();
        var schema5030 = Assert.Single(schemas, static schema => schema.Identity == "SCH_5030");
        var schema5031 = Assert.Single(schemas, static schema => schema.Identity == "SCH_5031");
        Assert.Equal(schema5030.DescriptorSha256, schema5031.DescriptorSha256);
    }

    [Theory]
    [InlineData(140, 14)]
    [InlineData(210, 21)]
    [InlineData(401019, 40)]
    [InlineData(502233, 50)]
    [InlineData(701081, 70)]
    [InlineData(900080, 90)]
    [InlineData(901101, 91)]
    [InlineData(1001026, 101)]
    public void Registry_ResolvesNearestPublicTransmitVersionForProducer(int modelerVersion, int expected)
        => Assert.Equal(expected, XtCorpusInspection.GetCompatibleTransmitVersion(modelerVersion));

    [Fact]
    public void Codec_RoundTripsEveryGoldenFixtureWithoutLosingFieldValues()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var paths = Directory.EnumerateFiles(fixtureRoot, "model.x_t", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var original = XtText.DecodeDocument(File.ReadAllText(path));
            var roundTrip = XtText.DecodeDocument(XtText.Encode(original));
            AssertDocumentsEqual(original, roundTrip);
        }
    }

    [Fact]
    public void SemanticProjection_IndexesEveryGoldenPartRootAndCommonEntityFamily()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var paths = Directory.EnumerateFiles(fixtureRoot, "model.x_t", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        foreach (var path in paths)
        {
            var document = XtText.DecodeDocument(File.ReadAllText(path));
            var semantic = document.SemanticModel;
            Assert.Equal(document.Nodes.Length, semantic.Entities.Length);
            Assert.NotEmpty(semantic.PartRoots);
            foreach (var root in semantic.PartRoots)
            {
                Assert.InRange(root, 0, document.Nodes.Length - 1);
                Assert.Contains(semantic.Entities[root].Kind, new[] { XtSemanticKind.Body, XtSemanticKind.Assembly });
            }
            Assert.Equal(
                document.Nodes.Count(node => document.Schema.GetNode(node.Type).Name == "BODY"),
                semantic.Bodies.Length);
        }
    }

    [Theory]
    [InlineData('i')]
    [InlineData('b')]
    [InlineData('h')]
    public void Codec_RoundTripsCompositePhysicalFieldTypes(char requiredType)
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var descriptor = FindNodeWithType(schema, requiredType);
        var node = CreateNode(schema, descriptor, 2);
        var original = CreateDocument(schema, 0, node);
        var roundTrip = XtText.DecodeDocument(XtText.Encode(original));
        AssertDocumentsEqual(original, roundTrip);
    }

    [Fact]
    public void Codec_RoundTripsEntityUserFields()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var descriptor = schema.GetNode((int)XtNodeTypes.Body);
        var node = CreateNode(schema, descriptor, 0);
        node.UserFields = [31415, -2718];
        var original = CreateDocument(schema, node.UserFields.Length, node);
        var roundTrip = XtText.DecodeDocument(XtText.Encode(original));
        AssertDocumentsEqual(original, roundTrip);
    }

    [Fact]
    public void Codec_RoundTripsRawCharacterArraysContainingSpaces()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var descriptor = schema.GetNode(79);
        var text = "Kg/Cu M ";
        var node = new XtNode
        {
            Type = descriptor.Type,
            Index = 17,
            VariableLength = text.Length,
            Fields = text.Select(XtFieldValue.Char).ToArray(),
        };
        var original = CreateDocument(schema, 0, node);
        var roundTrip = XtText.DecodeDocument(XtText.Encode(original));
        AssertDocumentsEqual(original, roundTrip);
    }

    [Fact]
    public void Codec_ParsesHistoricalBareSignedZeroComponents()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var descriptor = FindNodeWithType(schema, 'v');
        var node = CreateNode(schema, descriptor, 0);
        var vectorIndex = Array.FindIndex(node.Fields, static value => value.Kind == XtFieldKind.Vector);
        Assert.True(vectorIndex >= 0);
        node.Fields[vectorIndex] = XtFieldValue.Vec(1, BitConverter.Int64BitsToDouble(long.MinValue), 0);
        var original = CreateDocument(schema, 0, node);
        var encoded = XtText.Encode(original).Replace("-0 ", "- ", StringComparison.Ordinal);
        var decoded = XtText.DecodeDocument(encoded);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(decoded.Nodes[0].Fields[vectorIndex].Vector.Y));
    }

    [Fact]
    public void Codec_RoundTripsPhysicalFileHeaderWithoutRewrappingIt()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var descriptor = schema.GetNode((int)XtNodeTypes.Body);
        var original = new XtDocument
        {
            PhysicalHeader = "**PART1;\n**PART2;\n**END_OF_HEADER*****************************************************************\n",
            VersionText = ": TRANSMIT FILE created by modeller version 3800150",
            HeaderSchemaIdentity = XtSchemaRegistry.ResolveCurrent().Identity,
            Schema = schema,
            UserFieldSize = 0,
            Nodes = [CreateNode(schema, descriptor, 0)],
        };
        var encoded = XtText.Encode(original);
        Assert.StartsWith(original.PhysicalHeader, encoded, StringComparison.Ordinal);
        AssertDocumentsEqual(original, XtText.DecodeDocument(encoded));
    }

    [Fact]
    public void Codec_RejectsPhysicalHeaderSchemaAndUserFieldMismatches()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var original = CreateDocument(schema, 0, CreateNode(schema, schema.GetNode((int)XtNodeTypes.Body), 0));
        var payload = XtText.Encode(original);
        var malformedSchema = "**PART2;SCH=NOT_A_SCHEMA;USFLD_SIZE=0;\n**END_OF_HEADER*****************************************************************\n" + payload;
        var differentKnownSchema = "**PART2;SCH=SCH_1000102_10002;USFLD_SIZE=0;\n**END_OF_HEADER*****************************************************************\n" + payload;
        var wrongUserFields = $"**PART2;SCH={XtSchemaRegistry.ResolveCurrent().Identity};USFLD_SIZE=4;\n**END_OF_HEADER*****************************************************************\n" + payload;
        Assert.Throws<FormatException>(() => XtText.DecodeDocument(malformedSchema));
        Assert.NotNull(XtText.DecodeDocument(differentKnownSchema));
        Assert.Throws<FormatException>(() => XtText.DecodeDocument(wrongUserFields));
    }

    [Fact]
    public void Codec_RoundTripsEveryTransmittedFieldInEveryBundledSchema()
    {
        for (var schemaIndex = 0; schemaIndex < XtSchemaRegistry.Count; schemaIndex++)
        {
            var schema = XtSchemaRegistry.GetByIndex(schemaIndex);
            var nodes = new List<XtNode>();
            var nodeIndex = 2;
            foreach (var descriptor in schema.Nodes)
            {
                if (!descriptor.Transmit || descriptor.Type == 1)
                    continue;
                nodes.Add(CreateNode(schema, descriptor, descriptor.Variable ? 0 : 0, nodeIndex++));
                if (descriptor.Variable)
                    nodes.Add(CreateNode(schema, descriptor, 3, nodeIndex++));
            }

            var original = CreateDocument(schema, schema.Identity, 0, nodes.ToArray());
            var roundTrip = XtText.DecodeDocument(XtText.Encode(original));
            AssertDocumentsEqual(original, roundTrip);
        }
    }

    [Theory]
    [InlineData("assembly.single-block-identity-instance/model.x_t", true)]
    [InlineData("blend.fxf.bsurf-plane.rolling-ball/model.x_t", false)]
    public void ManagedReceiveTransmit_PreservesOpaquePartDocuments(string fixture, bool assembly)
    {
        KernelRuntime.SessionStop();
        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&startOptions));
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
            var bytes = File.ReadAllBytes(path);
            fixed (byte* pointer = bytes)
            {
                var block = new PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
                var receiveOptions = new PK_PART_receive_o_s
                {
                    o_t_version = 1,
                    transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                };
                int count;
                int* parts;
                Assert.Equal(0, KernelRuntime.PartReceiveB(block, &receiveOptions, &count, &parts));
                Assert.Equal(1, count);
                try
                {
                    int entityClass;
                    Assert.Equal(0, KernelRuntime.EntityAskClass(parts[0], &entityClass));
                    Assert.Equal(assembly ? ParasolidConstants.PK_CLASS_assembly : ParasolidConstants.PK_CLASS_body, entityClass);

                    var transmitOptions = new PK_PART_transmit_o_s
                    {
                        o_t_version = 4,
                        transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                        transmit_version = 371,
                        transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
                    };
                    var output = new PK_MEMORY_block_s();
                    Assert.Equal(0, KernelRuntime.PartTransmitB(1, parts, &transmitOptions, &output));
                    try
                    {
                        var expected = XtText.DecodeDocument(File.ReadAllText(path));
                        var actual = XtText.DecodeDocument(Encoding.ASCII.GetString(output.bytes, checked((int)output.n_bytes)));
                        AssertDocumentsEqual(expected, actual);
                    }
                    finally
                    {
                        Assert.Equal(0, KernelRuntime.MemoryBlockFree(&output));
                    }
                }
                finally
                {
                    Assert.Equal(0, KernelRuntime.MemoryFree(parts));
                }
            }
        }
        finally
        {
            Assert.Equal(0, KernelRuntime.SessionStop());
        }
    }

    [Fact]
    public void SchemaTranscoder_ProducesStructurallyRoundTrippableDocumentsForEverySchema()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "body.solid.block.typical", "model.x_t");
        var source = XtText.DecodeDocument(File.ReadAllText(sourcePath));
        for (var schemaIndex = 0; schemaIndex < XtSchemaRegistry.Count; schemaIndex++)
        {
            var target = XtSchemaRegistry.GetByIndex(schemaIndex);
            if (!XtSchemaTranscoder.CanTranscode(source, target))
                continue;
            var transcoded = XtSchemaTranscoder.Transcode(source, target);
            var decoded = XtText.DecodeDocument(XtText.Encode(transcoded));
            Assert.Equal(target.SchemaNumber, decoded.Schema.SchemaNumber);
            AssertDocumentsEqual(transcoded, decoded);
        }
    }

    [Fact]
    public void SchemaTranscoder_RepresentsBasicBlockInEveryParasolid6AndLaterSchema()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "body.solid.block.typical", "model.x_t");
        var source = XtText.DecodeDocument(File.ReadAllText(sourcePath));
        for (var schemaIndex = 0; schemaIndex < XtSchemaRegistry.Count; schemaIndex++)
        {
            var target = XtSchemaRegistry.GetByIndex(schemaIndex);
            if (target.ModelerVersion < 600000)
                continue;
            Assert.True(XtSchemaTranscoder.CanTranscode(source, target), target.Identity + ": " + XtSchemaTranscoder.GetIncompatibility(source, target));
        }
    }

    [Theory]
    [InlineData(10, 1000)]
    [InlineData(20, 1012)]
    [InlineData(30, 3000)]
    [InlineData(40, 4039)]
    [InlineData(50, 5059)]
    [InlineData(60, 6021)]
    [InlineData(91, 9008)]
    [InlineData(100, 10004)]
    [InlineData(101, 10004)]
    [InlineData(111, 11004)]
    [InlineData(132, 13006)]
    [InlineData(210, 20000)]
    [InlineData(241, 20000)]
    [InlineData(250, 25001)]
    [InlineData(330, 32001)]
    [InlineData(360, 36001)]
    public void PartTransmit_HonorsHistoricalTransmitVersion(int transmitVersion, int expectedSchemaNumber)
    {
        KernelRuntime.SessionStop();
        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&startOptions));
        try
        {
            int body;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            var options = new PK_PART_transmit_o_s
            {
                o_t_version = 4,
                transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                transmit_version = transmitVersion,
                transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
            };
            var block = new PK_MEMORY_block_s();
            Assert.Equal(0, KernelRuntime.PartTransmitB(1, &body, &options, &block));
            try
            {
                var document = XtText.DecodeDocument(Encoding.ASCII.GetString(block.bytes, checked((int)block.n_bytes)));
                Assert.Equal(expectedSchemaNumber, document.Schema.SchemaNumber);
                var receiveOptions = new PK_PART_receive_o_s
                {
                    o_t_version = 8,
                    transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                };
                int receivedCount;
                int* receivedParts;
                Assert.Equal(0, KernelRuntime.PartReceiveB(block, &receiveOptions, &receivedCount, &receivedParts));
                try
                {
                    Assert.Equal(1, receivedCount);
                    var retransmitted = new PK_MEMORY_block_s();
                    Assert.Equal(0, KernelRuntime.PartTransmitB(receivedCount, receivedParts, &options, &retransmitted));
                    try
                    {
                        var second = XtText.DecodeDocument(Encoding.ASCII.GetString(retransmitted.bytes, checked((int)retransmitted.n_bytes)));
                        AssertDocumentsEqual(document, second);
                    }
                    finally
                    {
                        Assert.Equal(0, KernelRuntime.MemoryBlockFree(&retransmitted));
                    }
                }
                finally
                {
                    Assert.Equal(0, KernelRuntime.MemoryFree(receivedParts));
                }
            }
            finally
            {
                Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block));
            }
        }
        finally
        {
            Assert.Equal(0, KernelRuntime.SessionStop());
        }
    }

    [Fact]
    public void Codec_DefaultTransmitVersionNormalizesHistoricalInputToCurrentEmbeddedSchema()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "body.solid.block.typical", "model.x_t");
        var source = XtText.DecodeDocument(File.ReadAllText(sourcePath));
        var historical = XtSchemaTranscoder.Transcode(source, XtSchemaRegistry.ResolveBySchemaNumber(25001));
        Assert.True(XtText.TryEncodeForTransmitVersion(historical, 0, out var text));
        var normalized = XtText.DecodeDocument(text);
        Assert.Equal(XtSchemaRegistry.ResolveCurrent().SchemaNumber, normalized.Schema.SchemaNumber);
        Assert.NotNull(normalized.BaseSchema);
        Assert.Equal(13006, normalized.BaseSchema.SchemaNumber);
    }

    [Fact]
    public void Codec_SelectsAndReordersReceivedMultiPartRoots()
    {
        KernelRuntime.SessionStop();
        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&startOptions));
        try
        {
            int blockBody;
            int sphereBody;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &blockBody));
            Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(2, null, &sphereBody));
            var parts = stackalloc int[2] { blockBody, sphereBody };
            var options = new PK_PART_transmit_o_s
            {
                o_t_version = 4,
                transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                transmit_version = 371,
                transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
            };
            var output = new PK_MEMORY_block_s();
            Assert.Equal(0, KernelRuntime.PartTransmitB(2, parts, &options, &output));
            try
            {
                var document = XtText.DecodeDocument(Encoding.ASCII.GetString(output.bytes, checked((int)output.n_bytes)));
                var partBlock = document.Nodes[0];
                Assert.Equal((int)XtNodeTypes.PartTransmitBlock, partBlock.Type);
                var roots = new[] { partBlock.Fields[6].Pointer, partBlock.Fields[5].Pointer };
                Assert.True(XtText.TrySelectPartRoots(document, roots, out var selected));
                var decoded = XtText.DecodeDocument(XtText.Encode(selected));
                Assert.Equal(2, decoded.Nodes[0].VariableLength);
                Assert.Equal(2, decoded.Nodes[0].Fields[0].Integer);
                Assert.Equal(roots[0], decoded.Nodes[0].Fields[5].Pointer);
                Assert.Equal(roots[1], decoded.Nodes[0].Fields[6].Pointer);

                Assert.True(XtText.TrySelectPartRoots(document, roots.AsSpan(0, 1), out var subset));
                decoded = XtText.DecodeDocument(XtText.Encode(subset));
                Assert.Equal(1, decoded.Nodes[0].VariableLength);
                Assert.Equal(1, decoded.Nodes[0].Fields[0].Integer);
                Assert.Equal(roots[0], decoded.Nodes[0].Fields[5].Pointer);
            }
            finally
            {
                Assert.Equal(0, KernelRuntime.MemoryBlockFree(&output));
            }
        }
        finally
        {
            Assert.Equal(0, KernelRuntime.SessionStop());
        }
    }

    [Fact]
    public void SemanticProjection_RecognizesLegacyPointerListPartContainer()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var listDescriptor = schema.GetNode(74);
        var bodyDescriptor = schema.GetNode((int)XtNodeTypes.Body);
        var list = CreateNode(schema, listDescriptor, 2, 1);
        list.Fields[0] = XtFieldValue.Int(2);
        list.Fields[1] = XtFieldValue.Int(0);
        list.Fields[2] = XtFieldValue.Ptr(0);
        list.Fields[3] = XtFieldValue.Ptr(10);
        list.Fields[4] = XtFieldValue.Ptr(11);
        var document = CreateDocument(
            schema,
            0,
            list,
            CreateNode(schema, bodyDescriptor, 0, 10),
            CreateNode(schema, bodyDescriptor, 0, 11));
        Assert.Equal(new[] { 10, 11 }, XtPartGraph.GetRootIndexes(document));
        Assert.Equal(2, document.SemanticModel.PartRoots.Length);
    }

    [Fact]
    public void PartReceive_ImplementsCompoundSplitKeepAndFailContracts()
    {
        var schema = XtSchemaRegistry.ResolveCurrent();
        var bodyDescriptor = schema.GetNode((int)XtNodeTypes.Body);
        var parent = CreateNode(schema, bodyDescriptor, 0, 1);
        var firstChild = CreateNode(schema, bodyDescriptor, 0, 10);
        var secondChild = CreateNode(schema, bodyDescriptor, 0, 11);
        SetField(schema, bodyDescriptor, parent, "child", XtFieldValue.Ptr(firstChild.Index));
        SetField(schema, bodyDescriptor, firstChild, "owner", XtFieldValue.Ptr(parent.Index));
        SetField(schema, bodyDescriptor, firstChild, "next", XtFieldValue.Ptr(secondChild.Index));
        SetField(schema, bodyDescriptor, secondChild, "owner", XtFieldValue.Ptr(parent.Index));
        SetField(schema, bodyDescriptor, secondChild, "previous", XtFieldValue.Ptr(firstChild.Index));
        var bytes = Encoding.ASCII.GetBytes(XtText.Encode(CreateDocument(schema, 0, parent, firstChild, secondChild)));

        KernelRuntime.SessionStop();
        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&startOptions));
        try
        {
            fixed (byte* pointer = bytes)
            {
                var input = new PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
                var options = new PK_PART_receive_o_s
                {
                    o_t_version = 8,
                    transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                    attdef_mismatch = ParasolidConstants.PK_ATTDEF_mismatch_fail_c,
                    receive_compound = ParasolidConstants.PK_receive_compound_split_c,
                    receive_using_seek = ParasolidConstants.PK_receive_using_seek_no_c,
                    receive_mixed = ParasolidConstants.PK_receive_mixed_fail_c,
                };
                int count;
                int* parts;
                Assert.Equal(0, KernelRuntime.PartReceiveB(input, &options, &count, &parts));
                try
                {
                    Assert.Equal(2, count);
                    var transmitOptions = new PK_PART_transmit_o_s
                    {
                        o_t_version = 4,
                        transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                        transmit_version = 371,
                        transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
                    };
                    var output = new PK_MEMORY_block_s();
                    Assert.Equal(0, KernelRuntime.PartTransmitB(count, parts, &transmitOptions, &output));
                    try
                    {
                        var splitDocument = XtText.DecodeDocument(Encoding.ASCII.GetString(output.bytes, checked((int)output.n_bytes)));
                        Assert.Equal(new[] { 10, 11 }, XtPartGraph.GetRootIndexes(splitDocument));
                    }
                    finally
                    {
                        Assert.Equal(0, KernelRuntime.MemoryBlockFree(&output));
                    }
                }
                finally
                {
                    Assert.Equal(0, KernelRuntime.MemoryFree(parts));
                }

                options.receive_compound = ParasolidConstants.PK_receive_compound_keep_c;
                Assert.Equal(0, KernelRuntime.PartReceiveB(input, &options, &count, &parts));
                try { Assert.Equal(1, count); }
                finally { Assert.Equal(0, KernelRuntime.MemoryFree(parts)); }

                options.receive_compound = ParasolidConstants.PK_receive_compound_fail_c;
                Assert.Equal(ParasolidConstants.PK_ERROR_compound_body, KernelRuntime.PartReceiveB(input, &options, &count, &parts));
            }
        }
        finally
        {
            Assert.Equal(0, KernelRuntime.SessionStop());
        }
    }

    private static XtDocument CreateDocument(XtSchemaDefinition schema, int userFieldSize, params XtNode[] nodes)
        => CreateDocument(schema, XtSchemaRegistry.ResolveCurrent().Identity, userFieldSize, nodes);

    private static XtDocument CreateDocument(XtSchemaDefinition schema, string headerIdentity, int userFieldSize, params XtNode[] nodes)
        => new()
        {
            VersionText = ": TRANSMIT FILE created by modeller version 3800150",
            HeaderSchemaIdentity = headerIdentity,
            Schema = schema,
            UserFieldSize = userFieldSize,
            Nodes = nodes,
        };

    private static XtNodeDescriptor FindNodeWithType(XtSchemaDefinition schema, char type)
    {
        foreach (var node in schema.Nodes)
        {
            if (node.Transmit && node.Type != 1 && schema.Fields.Slice(node.FieldOffset, node.ParsedFieldCount).ContainsType(type))
                return node;
        }
        throw new InvalidOperationException($"Schema has no transmitted field of type {type}.");
    }

    private static XtNode CreateNode(XtSchemaDefinition schema, XtNodeDescriptor descriptor, int variableLength, int nodeIndex = 7)
    {
        var values = new List<XtFieldValue>();
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = field.ElementCount > 1 ? field.ElementCount : descriptor.Variable && field.ElementCount == 1 ? variableLength : 1;
            for (var index = 0; index < count; index++)
                values.Add(CreateValue(field.Type, index));
        }

        return new XtNode
        {
            Type = descriptor.Type,
            Index = nodeIndex,
            VariableLength = descriptor.Variable ? variableLength : 0,
            Fields = values.ToArray(),
        };
    }

    private static XtFieldValue CreateValue(char type, int index) => type switch
    {
        'p' => XtFieldValue.Ptr(0),
        'u' => XtFieldValue.Unsigned(index + 1),
        'd' or 'n' or 'w' or 't' or 'q' => XtFieldValue.Int(index + 1),
        'f' => XtFieldValue.RealValue(index + 0.25),
        'c' => XtFieldValue.Char('+'),
        'l' => XtFieldValue.Logical((index & 1) == 0),
        'v' or 'h' => XtFieldValue.Vec(index + 0.1, index + 0.2, index + 0.3),
        'i' => XtFieldValue.IntervalValue(index - 0.5, index + 0.5),
        'b' => XtFieldValue.BoxValue(index, index + 1, index + 2, index + 3, index + 4, index + 5),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static void SetField(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        string fieldName,
        XtFieldValue value)
    {
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = field.ElementCount > 1
                ? field.ElementCount
                : descriptor.Variable && field.ElementCount == 1 ? Math.Max(0, node.VariableLength) : 1;
            if (field.Name == fieldName)
            {
                Assert.Equal(1, count);
                node.Fields[valueOffset] = value;
                return;
            }
            valueOffset += count;
        }
        throw new InvalidOperationException($"Field {descriptor.Name}.{fieldName} is missing.");
    }

    private static void AssertDocumentsEqual(XtDocument expected, XtDocument actual)
    {
        Assert.Equal(expected.PhysicalHeader, actual.PhysicalHeader);
        Assert.Equal(expected.HeaderSchemaIdentity, actual.HeaderSchemaIdentity);
        Assert.Equal(expected.Schema.SchemaNumber, actual.Schema.SchemaNumber);
        Assert.Equal(expected.UserFieldSize, actual.UserFieldSize);
        Assert.Equal(expected.Nodes.Length, actual.Nodes.Length);
        for (var nodeIndex = 0; nodeIndex < expected.Nodes.Length; nodeIndex++)
        {
            var expectedNode = expected.Nodes[nodeIndex];
            var actualNode = actual.Nodes[nodeIndex];
            Assert.Equal(expectedNode.Type, actualNode.Type);
            Assert.Equal(expectedNode.Index, actualNode.Index);
            Assert.Equal(expectedNode.VariableLength, actualNode.VariableLength);
            Assert.Equal(expectedNode.UserFields, actualNode.UserFields);
            Assert.Equal(expectedNode.Fields.Length, actualNode.Fields.Length);
            for (var fieldIndex = 0; fieldIndex < expectedNode.Fields.Length; fieldIndex++)
                AssertFieldEqual(expectedNode.Fields[fieldIndex], actualNode.Fields[fieldIndex]);
        }
    }

    private static void AssertFieldEqual(XtFieldValue expected, XtFieldValue actual)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Integer, actual.Integer);
        Assert.Equal(expected.Pointer, actual.Pointer);
        Assert.Equal(expected.Character, actual.Character);
        Assert.Equal(expected.Real, actual.Real);
        Assert.Equal(expected.Vector.X, actual.Vector.X);
        Assert.Equal(expected.Vector.Y, actual.Vector.Y);
        Assert.Equal(expected.Vector.Z, actual.Vector.Z);
        Assert.Equal(expected.Fourth, actual.Fourth);
        Assert.Equal(expected.Fifth, actual.Fifth);
        Assert.Equal(expected.Sixth, actual.Sixth);
    }
}

internal static class XtSchemaTestExtensions
{
    public static bool ContainsType(this ReadOnlySpan<XtFieldDescriptor> fields, char type)
    {
        foreach (var field in fields)
        {
            if (field.Transmit && field.Type == type)
                return true;
        }
        return false;
    }
}
