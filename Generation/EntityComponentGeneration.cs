using CodeGenerator.Utils;

namespace CodeGenerator.Generation;

public static class EntityComponentGeneration
{
    public static Tuple<string, string> GenerateEntityComponent(TableDefinition tableDefinition)
    {
        var properties = tableDefinition.PropertyDefinitions;
        var className = tableDefinition.ClassName;
        var header = $"public class {className}Component : EntityComponent<{className}>\n{{";
        var body = "";

        if (properties != null)
            foreach (var prop in properties)
            {
                var primitiveType = prop.PropTypeDefinition?.PropType;
                if (primitiveType == null) 
                    continue;
                
                var propType = prop.PropTypeDefinition is { IsNullable: true }
                    ? $"{GenUtils.ChangeToNonPrimitiveType(primitiveType)}?"
                    : $"{GenUtils.ChangeToNonPrimitiveType(primitiveType)}";
                body +=
                    $"\n\tpublic {propType} {prop.PropName}" +
                    $"\n\t{{" +
                    $"\n\t\tget => Entity.{prop.PropName};" +
                    $"\n\t\tset => SetProperty(Entity, value);" +
                    $"\n\t}}\n";
            }

        body += "}";
        var file = $"{header}{body}";
        var filename = $"{className}EntityComponent.cs";
        return new Tuple<string, string>(file, filename);
    }
}