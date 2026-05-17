using System.Globalization;
using System.Text;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static class XtText
{
    private const int CurrentTransmitModelerVersion = 3800150;

    public static string Encode(IReadOnlyList<XtNode> nodes)
    {
        var sb = new StringBuilder(nodes.Count * 256);
        var versionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}";
        sb.Append('T')
          .Append(versionText.Length)
          .Append(' ')
          .Append(versionText)
          .Append(XtSchema.SchemaName.Length)
          .Append(' ')
          .Append(XtSchema.SchemaName)
          .Append('0')
          .Append(' ');

        foreach (var node in nodes)
        {
            var descriptor = XtSchema.GetNode(node.Type);
            if (descriptor.Type == 0)
                throw new InvalidOperationException("Unknown XT node type.");

            sb.Append(node.Type).Append(' ');
            if (descriptor.Variable)
                sb.Append(VariableLength(node)).Append(' ');
            sb.Append(node.Index).Append(' ');
            var fields = XtSchema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
            var valueIndex = 0;
            for (var i = 0; i < fields.Length; i++)
            {
                if (!fields[i].Transmit)
                    continue;

                var count = descriptor.Variable && fields[i].ElementCount == 1 ? VariableLength(node) : 1;
                for (var j = 0; j < count; j++)
                    WriteField(sb, fields[i].Type, node.Fields[valueIndex++]);
            }
        }

        sb.Append((int)XtNodeTypes.Terminator).Append(' ').Append('0').Append(' ');
        return Wrap(sb.ToString());
    }

    public static XtNode[] Decode(string text)
    {
        text = RemoveLineBreaks(text);
        var tokenizer = new XtTokenizer(text);
        var flag = tokenizer.NextRequired();
        if (flag != "T")
            throw new FormatException("XT text flag is missing.");

        var versionLength = tokenizer.NextInt();
        var versionText = tokenizer.NextRaw(versionLength);
        var schemaLength = tokenizer.NextInt();
        var schema = tokenizer.NextRaw(schemaLength);
        var userFieldSize = tokenizer.NextInt();
        if (!IsSupportedSchema(schema))
            throw new FormatException("Unsupported XT schema.");
        if (userFieldSize != 0)
            throw new FormatException("XT user fields are not supported.");
        _ = versionText;

        var nodes = new List<XtNode>();
        while (true)
        {
            var type = tokenizer.NextInt();
            if (type == (int)XtNodeTypes.Terminator)
            {
                var terminatorIndex = tokenizer.NextInt();
                if (terminatorIndex != 0)
                    throw new FormatException("Invalid XT terminator.");
                break;
            }

            var descriptor = XtSchema.GetNode(type);
            if (descriptor.Type == 0)
                throw new FormatException($"Unsupported XT node type {type} at token position {tokenizer.Position}.");
            var variableLength = descriptor.Variable ? tokenizer.NextInt() : 0;

            var node = new XtNode { Type = type, Index = tokenizer.NextInt() };
            var fields = XtSchema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
            var transmitted = CountTransmitted(fields, variableLength);
            node.Fields = new XtFieldValue[transmitted];
            var valueIndex = 0;
            for (var i = 0; i < fields.Length; i++)
            {
                if (!fields[i].Transmit)
                    continue;

                var count = fields[i].ElementCount == 1 ? Math.Max(1, variableLength) : 1;
                for (var j = 0; j < count; j++)
                    node.Fields[valueIndex++] = ReadField(ref tokenizer, fields[i].Type);
            }
            nodes.Add(node);
        }

        return nodes.ToArray();
    }

    private static int CountTransmitted(ReadOnlySpan<XtFieldDescriptor> fields, int variableLength)
    {
        var count = 0;
        foreach (var field in fields)
        {
            if (field.Transmit)
                count += field.ElementCount == 1 ? Math.Max(1, variableLength) : 1;
        }

        return count;
    }

    private static int VariableLength(XtNode node)
    {
        return node.Type == (int)XtNodeTypes.PartTransmitBlock
            ? node.Fields.Length - 5
            : node.Fields.Length;
    }

    private static bool IsSupportedSchema(string schema)
    {
        if (!schema.StartsWith("SCH_", StringComparison.Ordinal))
            return false;

        var last = schema.LastIndexOf('_');
        return last >= 0
            && int.TryParse(schema.AsSpan(last + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var schemaNumber)
            && schemaNumber == XtSchema.SchemaNumber;
    }

    private static string RemoveLineBreaks(string text)
    {
        if (text.IndexOfAny(['\n', '\r']) < 0)
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is not '\n' and not '\r')
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static void WriteField(StringBuilder sb, char type, XtFieldValue value)
    {
        if (value.Kind == XtFieldKind.Empty)
        {
            sb.Append('?');
            return;
        }

        switch (type)
        {
            case 'p':
                sb.Append(value.Pointer).Append(' ');
                break;
            case 'd':
            case 'u':
            case 'i':
            case 'n':
            case 'b':
            case 'w':
            case 't':
            case 'q':
                sb.Append(value.Integer).Append(' ');
                break;
            case 'f':
                sb.Append(FormatReal(value.Real)).Append(' ');
                break;
            case 'c':
                sb.Append(value.Character);
                break;
            case 'l':
                sb.Append(value.Integer != 0 ? 'T' : 'F');
                break;
            case 'v':
            case 'h':
                sb.Append(FormatReal(value.Vector.X)).Append(' ')
                  .Append(FormatReal(value.Vector.Y)).Append(' ')
                  .Append(FormatReal(value.Vector.Z)).Append(' ');
                break;
            default:
                throw new NotSupportedException($"Unsupported XT field type '{type}'.");
        }
    }

    private static string FormatReal(double value)
    {
        if (value == 1000.0)
            return "1e3";

        var text = value.ToString("G17", CultureInfo.InvariantCulture).Replace('E', 'e');
        var exponent = text.IndexOf('e', StringComparison.Ordinal);
        if (exponent < 0)
            return text;

        var mantissa = text[..exponent];
        var sign = "";
        var digits = text[(exponent + 1)..];
        if (digits.StartsWith("+", StringComparison.Ordinal) || digits.StartsWith("-", StringComparison.Ordinal))
        {
            sign = digits[..1] == "+" ? "" : "-";
            digits = digits[1..];
        }

        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            digits = "0";
        return mantissa + "e" + sign + digits;
    }

    private static XtFieldValue ReadField(ref XtTokenizer tokenizer, char type)
    {
        return type switch
        {
            'p' => XtFieldValue.Ptr(tokenizer.NextInt()),
            'd' or 'i' or 'n' or 'b' or 'w' or 't' or 'q' => XtFieldValue.Int(tokenizer.NextInt()),
            'u' => XtFieldValue.Unsigned(tokenizer.NextInt()),
            'f' => tokenizer.NextDoubleValue(),
            'c' => XtFieldValue.Char(tokenizer.NextChar()),
            'l' => XtFieldValue.Logical(tokenizer.NextChar() == 'T'),
            'v' or 'h' => tokenizer.NextVectorValue(),
            _ => throw new NotSupportedException($"Unsupported XT field type '{type}'."),
        };
    }

    private static string Wrap(string text)
    {
        var sb = new StringBuilder(text.Length + text.Length / 80 + 1);
        var column = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (column == 79 && ch == ' ')
            {
                sb.Append('\n');
                column = 0;
            }

            sb.Append(ch);
            column++;
            if (column == 80)
            {
                sb.Append('\n');
                column = 0;
            }
        }

        if (column != 0)
            sb.Append('\n');
        return sb.ToString();
    }

    private ref struct XtTokenizer
    {
        private readonly string text;
        private int position;
        private string? pending;
        public readonly int Position => position;

        public XtTokenizer(string text)
        {
            this.text = text;
            position = 0;
            pending = null;
        }

        public string NextRequired()
        {
            if (pending is not null)
            {
                var value = pending;
                pending = null;
                return value;
            }

            SkipIgnored();
            if (position >= text.Length)
                throw new FormatException("Unexpected end of XT text.");

            if (text[position] == 'T')
            {
                position++;
                return "T";
            }

            var start = position;
            while (position < text.Length && !IsSeparator(text[position]))
                position++;
            return text[start..position];
        }

        public int NextInt()
        {
            var token = NextRequired();
            if (token == "?")
                return 0;
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return 0;
            }
            return int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public double NextDouble()
        {
            var token = NextRequired();
            if (token == "?")
                return 0;
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return 0;
            }
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public XtFieldValue NextDoubleValue()
        {
            var token = NextRequired();
            if (token == "?")
                return XtFieldValue.Null();
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return XtFieldValue.Null();
            }

            return XtFieldValue.RealValue(double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        public XtFieldValue NextVectorValue()
        {
            var first = NextRequired();
            if (first == "?")
                return XtFieldValue.Null();
            if (first.Length > 1 && first[0] == '?')
            {
                pending = first[1..];
                return XtFieldValue.Null();
            }

            return XtFieldValue.Vec(
                double.Parse(first, NumberStyles.Float, CultureInfo.InvariantCulture),
                NextDouble(),
                NextDouble());
        }

        public char NextChar()
        {
            if (pending is not null)
            {
                var token = pending;
                var ch = token[0];
                pending = token.Length > 1 ? token[1..] : null;
                return ch;
            }

            SkipIgnored();
            if (position >= text.Length)
                throw new FormatException("Unexpected end of XT text.");

            var start = position;
            while (position < text.Length && !IsSeparator(text[position]))
                position++;
            var value = text[start..position];
            if (value.Length > 1)
                pending = value[1..];
            return value[0];
        }

        public string NextRaw(int length)
        {
            SkipIgnored();
            if (position + length > text.Length)
                throw new FormatException("Unexpected end of XT raw string.");

            var value = text.Substring(position, length);
            position += length;
            return value;
        }

        private void SkipIgnored()
        {
            while (position < text.Length && IsSeparator(text[position]))
                position++;
        }

        private static bool IsSeparator(char ch) => ch is ' ' or '\n' or '\r' or '\t';
    }
}
