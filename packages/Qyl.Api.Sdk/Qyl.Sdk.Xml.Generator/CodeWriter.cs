using System.Text;

namespace Qyl.Sdk.Xml.Generator;

/// <summary>A line-oriented builder with block indentation; writes "\n" so output is identical on every platform.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _depth;

    public void Line()
    {
        _builder.Append('\n');
    }

    public void Line(string text)
    {
        _builder.Append(' ', _depth * 4).Append(text).Append('\n');
    }

    public void Open()
    {
        Line("{");
        _depth++;
    }

    public void Close()
    {
        _depth--;
        Line("}");
    }

    public override string ToString() => _builder.ToString();
}
