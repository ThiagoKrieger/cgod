using System.Text;
using CodeGenerator.Utils;

namespace CodeGenerator.Generation;

public static class I18NGeneration
{
    private const string I18NSuffix = "I18NConfiguration";
    public static Tuple<string, string> GenerateI18N(TableDefinition tableDefinition)
    {
        var fullClassName = $"{tableDefinition.ClassName}{I18NSuffix}";
        var entityAbbrev = tableDefinition.ClassName?.ToLower()[..2];
        var i18NStringBuilder = new StringBuilder();

        i18NStringBuilder.Append("//TODO Add usings")
            .DoubleAppendLine();
        
        i18NStringBuilder.AppendLine("namespace Add.Your.NameSpace");
        i18NStringBuilder.AppendLine("{");
        
        i18NStringBuilder.AppendLineWithTabs("//TODO Check need to add CompositionPartAttribute", 1);

        i18NStringBuilder.AppendLineWithTabs($"public class {fullClassName}", 1);
        i18NStringBuilder.AppendLineWithTabs("{", 1);
        
        i18NStringBuilder.AppendLineWithTabs($"public {fullClassName}()", 2);
        i18NStringBuilder.AppendLineWithTabs("{", 2);
        
        if (tableDefinition.PropertyDefinitions != null && tableDefinition.ClassName != null)
            GenerateForProperties(i18NStringBuilder, tableDefinition.PropertyDefinitions, entityAbbrev);

        i18NStringBuilder.AppendLine()
            .AppendLineWithTabs($"UseIdentification({entityAbbrev} => \"\")", 3);

        i18NStringBuilder.AppendLineWithTabs("}", 2);
        i18NStringBuilder.AppendLineWithTabs("}", 1);
        i18NStringBuilder.Append('}');


        var filename = $"{fullClassName}.cs";

        return new Tuple<string, string>(i18NStringBuilder.ToString(), filename);
    }

    private const string ForPropertyOpen = "ForProperty("; //prop => prop.";
    private const string UseDisplayTextOpen = "UseDisplayText();";

    private static void GenerateForProperties(StringBuilder stringBuilder, 
        IEnumerable<PropertyDefinition> propertyDefinitions,
        string? entityName)
    {
        foreach (var propertyDefinition in propertyDefinitions)
        {
            var propName = propertyDefinition.PropName ?? string.Empty;
            
            stringBuilder.AppendLineWithTabs($"{ForPropertyOpen}{entityName} => {entityName}.{propName})", 3);
            stringBuilder.AppendLineWithTabs($".{UseDisplayTextOpen}", 4);
        }
    }
}