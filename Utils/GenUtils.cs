using System.Text;

namespace CodeGenerator.Utils;

public static class GenUtils
{
    public static string ChangeToNonPrimitiveType(string primitiveType)
    {
        var typeDictionary = new Dictionary<string, string>() {
            {"Boolean", "bool"}, {"Byte", "byte"}, {"SByte", "sbyte"}, {"Char", "char"}, {"Decimal", "decimal"}, {"Double", "double"},
            // ReSharper disable once StringLiteralTypo
            {"Single", "float"}, {"Int32", "int"}, {"UInt32", "uint"}, {"IntPtr", "nint"},
            // ReSharper disable once StringLiteralTypo
            {"UIntPtr", "nuint"}, {"Int64", "long"},
            {"UInt64", "ulong"}, {"Int16", "short"}, {"UInt16", "ushort"}, {"Object", "object"}, {"String", "string"}};

        var nonPrimitiveType = typeDictionary.FirstOrDefault(p => p.Key == primitiveType).Value;
        return string.IsNullOrEmpty(nonPrimitiveType)
            ? primitiveType
            : nonPrimitiveType;
    }

    public static StringBuilder DoubleAppendLine(this StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine();
        return builder;
    }

    public static StringBuilder AppendLineWithTabs(this StringBuilder builder, string str, int tabs)
    {
        for (var i = 0; i < tabs; i++)
            builder.Append('\t');

        builder.AppendLine(str);

        return builder;
    }
}