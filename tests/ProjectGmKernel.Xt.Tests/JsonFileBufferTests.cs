using System.Text.Encodings.Web;
using System.Text.Json;
using ProjectGmKernel.Xt;
using ProjectGmKernel.Xt.JsonTool;
using Schema37102 = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

namespace ProjectGmKernel.Xt.Tests;

public sealed class JsonFileBufferTests
{
    [Fact]
    public void ChunkedJsonMatchesStreamWriterAndReachesTheFileBeforeDispose()
    {
        var options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using var expected = new MemoryStream();
        using (var writer = new Utf8JsonWriter(expected, options))
            WriteSample(writer);

        using var actual = new MemoryStream();
        long lengthDuringWrite;
        using (var buffer = new JsonFileBuffer(actual, chunkSize: 512))
        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            WriteSample(writer);
            lengthDuringWrite = actual.Length;
        }

        Assert.True(lengthDuringWrite > 0);
        Assert.Equal(expected.ToArray(), actual.ToArray());
    }

    [Fact]
    public void ChunkedModelJsonMatchesStreamWriter()
    {
        var builder = new Schema37102.MODEL_BUILDER(new Schema37102.COUNTS { BODY = 1, CHAR_VALUES = 1, CHAR_VALUES__values = 80 });
        builder.BODY[0]._xt_index = 1;
        builder.BODY[0]._xt_order = 0;
        builder.BODY[0].res_size = XtSchemaField.NullReal;
        builder.BODY[0].next = new Schema37102.BODYRef { Index = XtSchemaField.NullPointer };
        builder.CHAR_VALUES[0]._xt_index = 2;
        builder.CHAR_VALUES[0]._xt_order = 1;
        builder.CHAR_VALUES[0]._xt_variable_length = 80;
        builder.CHAR_VALUES[0].values = new XtRange { Offset = 0, Count = 80 };
        var text = "Part1.2\\line\r\n"u8;
        text.CopyTo(builder.CHAR_VALUES__values);
        for (var i = text.Length; i < 80; i++)
            builder.CHAR_VALUES__values[i] = (byte)('a' + (i % 26));
        var model = builder.FinalizeModel();

        var options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using var expected = new MemoryStream();
        using (var writer = new Utf8JsonWriter(expected, options))
            XtGeneratedModelJson.Write(model, writer);

        using var actual = new MemoryStream();
        using (var buffer = new JsonFileBuffer(actual, chunkSize: 128))
        using (var writer = new Utf8JsonWriter(buffer, options))
            XtGeneratedModelJson.Write(model, writer);

        Assert.Equal(expected.ToArray(), actual.ToArray());
    }

    private static void WriteSample(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("rows");
        for (var i = 0; i < 40; i++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", i);
            writer.WriteString("name", new string((char)('A' + (i % 26)), 80));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

}

