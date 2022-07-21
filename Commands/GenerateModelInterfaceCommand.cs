using System.Data.Entity.Core.Metadata.Edm;
using System.Net;
using CodeGenerator.Utils;

namespace CodeGenerator.Commands;

public class GenerateModelInterfaceCommand : ICommand
{
    public string CommandKey => "mi";

    private const string EntityParameter = "e";
    private const string WithNavigationParameter = "n";

    public async Task Generate(Dictionary<string,string> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue(EntityParameter, out var entity))
            return;

        var tableDefinition = entity.GetTableDefinition();
        var withNavigations = parameters.ContainsKey(WithNavigationParameter);

        var fileContent = GenerateInterface(tableDefinition, withNavigations);
        var fileName = $"I{tableDefinition.ClassName}.cs";

        await File.WriteAllTextAsync(fileName, fileContent, cancellationToken);
    }

    public static string GenerateInterface(TableDefinition tableDefinition, bool withNavigations)
    {
        try
        {
            var namespaceString = $"using System;\n"
                                  + $"using System.Collections.Generic;\n"
                                  + $"using CP.DataModel.Abstractions;\n"
                                  + $"using CP.Domain.Abstractions.Enums;\n"
                                  + $"\n"
                                  + $"namespace CP.Domain.Abstractions.Models;\n";

            var tableSummary = $"\n/// <summary>" +
                               $"\n/// Table {tableDefinition.DbTable}" +
                               $"\n/// </summary>";
            var header = $"public interface I{tableDefinition.ClassName} : IEntity";

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

            return $"{namespaceString}\n{tableSummary}\n{header}\n{{{body}{navigations}\n}}";
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
                    $"\n    public I{navigationProperty.RelationshipPropertyType} {navigationProperty.RelationshipPropertyName} {{ get ; set; }}",
                RelationshipMultiplicity.One =>
                    $"\n    public I{navigationProperty.RelationshipPropertyType} {navigationProperty.RelationshipPropertyName} {{ get ; set; }}",
                RelationshipMultiplicity.Many =>
                    $"\n    public ICollection<I{navigationProperty.RelationshipPropertyType}> {navigationProperty.RelationshipPropertyName} {{ get; set; }}",
                _ => throw new ArgumentOutOfRangeException(nameof(navigationType), navigationType, "Invalid Navigation Type")
            };

            navigations += singleNavigation;

            if (i != modelNavigations.Count - 1)
                navigations += "\n";
        }

        return navigations;
    }
}