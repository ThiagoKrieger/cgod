using System.Data.Entity.Core.Metadata.Edm;
using CodeGenerator.Utils;

namespace CodeGenerator.Commands;

public class GenerateModelCommand : ICommand
{
    public string CommandKey => "m";

    private const string EntityParameter = "e";
    private const string WithNavigationParameter = "n";

    public async Task Generate(Dictionary<string,string> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue(EntityParameter, out var entity))
            return;

        var tableDefinition = entity.GetTableDefinition();
        var withNavigations = parameters.ContainsKey(WithNavigationParameter);

        var fileContent = GenerateModel(tableDefinition, withNavigations);
        var fileName = $"{tableDefinition.ClassName}.cs";

        await File.WriteAllTextAsync(fileName, fileContent, cancellationToken);
    }

    public static string GenerateModel(TableDefinition tableDefinition, bool withNavigations)
    {
        try
        {
            var namespaceString = $"using System;\n"
                                  + $"using System.Collections.Generic;\n"
                                  + $"using CP.Domain.Abstractions.Enums;\n"
                                  + $"using CP.Domain.Abstractions.Models;\n"
                                  + $"\n"
                                  + $"namespace CP.Domain.Models;\n";

            var tableSummary = $"\n/// <summary>" +
                               $"\n/// Table {tableDefinition.DbTable}" +
                               $"\n/// </summary>";
            var header = $"public class {tableDefinition.ClassName} : I{tableDefinition.ClassName}";

            var modelProperties = tableDefinition.PropertyDefinitions?.ToList();
            var body = "";

            if (modelProperties is null)
                throw new InvalidOperationException();

            for (var i = 0; i < modelProperties.Count; i++)
            {
                var prop = modelProperties[i];

                if (prop.PropTypeDefinition?.PropType == null)
                {
                    body += $"##INVALID PropType {prop.PropName}";

                    continue;
                }

                var propType = GenUtils.ChangeToNonPrimitiveType(prop.PropTypeDefinition.PropType);
                var singleProp =
                    prop.PropTypeDefinition is { IsNullable: true }
                        ? $"\n    public {propType}? {prop.PropName} {{ get; set; }}"
                        : $"\n    public {propType} {prop.PropName} {{ get; set; }}";

                if (prop.PropTypeDefinition is not null &&
                    !prop.PropTypeDefinition.IsNullable &&
                    propType == "string")
                    singleProp += " = string.Empty;";

                if (prop.PropTypeDefinition is not null &&
                    !prop.PropTypeDefinition.IsNullable &&
                    propType == "Byte[]")
                    singleProp += " = Array.Empty<byte>();";

                var summary = $"\n    /// <summary>" +
                              $"\n    /// Column {prop.DbColumn}" +
                              $"\n    /// </summary>";

                body += summary + singleProp;

                if (i != modelProperties.Count - 1)
                    body += "\n";
            }

            var navigations = string.Empty;

            if (withNavigations)
                navigations = WriteModelNavigations(tableDefinition);

            return $"{namespaceString}\n{tableSummary}\n{header}\n{{{body}\n{navigations}\n}}";
        }
        catch (Exception e)
        {
            return $"A exception occured while generating the file: \n{e.Message}";
        }
    }

    private static string WriteModelNavigations(TableDefinition tableDefinition)
    {
        var modelNavigations = tableDefinition.NavigationProperties?.ToList();
        var navigations = "";

        if (modelNavigations == null)
            return navigations;


        for (var i = 0; i < modelNavigations.Count; i++)
        {
            var navigationProperty = modelNavigations[i];

            var navigationType = navigationProperty.RelationshipMultiplicity;

            var singleNavigation = navigationType switch
            {
                RelationshipMultiplicity.ZeroOrOne =>
                    $"\n    public I{navigationProperty.RelationshipPropertyType}? {navigationProperty.RelationshipPropertyName} {{ get ; set; }}",
                RelationshipMultiplicity.One =>
                    $"\n    public I{navigationProperty.RelationshipPropertyType}? {navigationProperty.RelationshipPropertyName} {{ get ; set; }}",
                RelationshipMultiplicity.Many =>
                    $"\n    public ICollection<I{navigationProperty.RelationshipPropertyType}> {navigationProperty.RelationshipPropertyName} {{ get; set; }} = new HashSet<I{navigationProperty.RelationshipPropertyType}>();",
                _ => throw new ArgumentOutOfRangeException()
            };

            navigations += singleNavigation;

            if (i != modelNavigations.Count - 1)
                navigations += "\n";
        }

        return navigations;
    }
}