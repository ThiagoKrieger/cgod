using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using RelationshipMultiplicity = System.Data.Entity.Core.Metadata.Edm.RelationshipMultiplicity;

namespace CodeGenerator.Commands;

public class ConvertSemiOldToNew : ICommand
{
    public string CommandKey => "semi";

    public async Task Generate(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        await using var resourceStream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("CodeGenerator.DBD.json");

        if (resourceStream is null)
            throw new InvalidOperationException("Couldn't deserialize the data base definitions, resource file not found.");

        using var reader =
            new StreamReader(resourceStream, Encoding.UTF8);

        var definitionString = await reader.ReadToEndAsync();
        var databaseDefinition = JsonConvert.DeserializeObject<DatabaseDefinition>(definitionString);

        if (databaseDefinition?.TableDefinitions == null)
            throw new InvalidOperationException("affe");

        foreach (var tableDefinition in databaseDefinition.TableDefinitions)
        {
            await GenerateInterface(tableDefinition, cancellationToken);
            await GenerateModel(tableDefinition, cancellationToken);
            await GenerateDefinition(databaseDefinition, tableDefinition, cancellationToken);

        }
    }

    private async Task CreateFile(string fileName, string fileContent, CancellationToken cancellationToken)
    {
        if (File.Exists(fileName))
            File.Delete(fileName);

        var dir = Path.GetDirectoryName(fileName);

        if (dir is null)
            throw new InvalidOperationException();

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fileName, fileContent, cancellationToken);
    }

    private async Task GenerateInterface(TableDefinition tableDefinition, CancellationToken cancellationToken)
    {
        var fileContent = GenerateModelInterfaceCommand.GenerateInterface(tableDefinition, true);
        var fileName = $"CP.Domain.Abstractions/Models/I{tableDefinition.ClassName}.cs";

        await CreateFile(fileName, fileContent, cancellationToken);
    }

    private async Task GenerateModel(TableDefinition tableDefinition, CancellationToken cancellationToken)
    {
        var fileContent = GenerateModelCommand.GenerateModel(tableDefinition, true);
        var fileName = $"CP.Domain/Models/{tableDefinition.ClassName}.cs";

        await CreateFile(fileName, fileContent, cancellationToken);
    }

    public static string? FirstCharToLowerCase(string? str)
    {
        if ( !string.IsNullOrEmpty(str) && char.IsUpper(str[0]))
            return str.Length == 1 ? char.ToLower(str[0]).ToString() : char.ToLower(str[0]) + str[1..];

        return str;
    }

    private async Task GenerateDefinition(DatabaseDefinition databaseDefinition, TableDefinition tableDefinition, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = FirstCharToLowerCase(className);
        var interfaceName = $"I{tableDefinition.ClassName}";
        var tableName = tableDefinition.DbTable;

        builder.AppendLine($"using CP.Composition.Abstractions;");
        builder.AppendLine($"using CP.DataModel.Abstractions;");
        builder.AppendLine($"using CP.DataModel.Abstractions.DatabaseDefinition;");
        builder.AppendLine($"using CP.Domain.Abstractions.Models;");
        builder.AppendLine($"");
        builder.AppendLine($"namespace CP.Domain.Abstractions.EntityDefinition;");
        builder.AppendLine($"");
        builder.AppendLine($"[CompositionPart(typeof(IEntityDefinition))]");
        builder.AppendLine($"public class {className}EntityDefinition : AbstractEntityDefinition<{interfaceName}>");
        builder.AppendLine($"{{");
        builder.AppendLine($"    public {className}EntityDefinition(IPluralizationService pluralizationService)");
        builder.AppendLine($"        : base(pluralizationService)");
        builder.AppendLine($"    {{");
        builder.AppendLine($"        ToTable(\"{tableName}\");");
        builder.AppendLine($"");

        if (tableDefinition.PropertyDefinitions is null)
            throw new ArgumentNullException(nameof(tableDefinition.PropertyDefinitions));

        builder.Append($"        HasKey(");
        var keyList = tableDefinition.PropertyDefinitions.Where(p => p.IsKey).ToList();

        for (var i = 0; i < keyList.Count; i++)
        {
            var propertyName = keyList[i].PropName;

            if (i > 0)
                builder.Append($", \r\n                        ");

            builder.Append($"{lowerCaseClassName} => {lowerCaseClassName}.{propertyName}");
        }

        builder.AppendLine($")\r\n                .WithName(\"PK_{tableName}\");");
        builder.AppendLine($"");

        AddProperties(builder, tableDefinition);

        builder.AppendLine($"");

        AddNavigations(builder, databaseDefinition, tableDefinition);

        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");

        var fileName = $"CP.Domain.Abstractions/EntityDefinition/{tableDefinition.ClassName}EntityDefinition.cs";
        var fileContent = builder.ToString();

        await CreateFile(fileName, fileContent, cancellationToken);
    }

    private static void AddNavigations(StringBuilder builder, DatabaseDefinition databaseDefinition, TableDefinition tableDefinition)
    {
        if (tableDefinition.NavigationProperties == null)
            return;

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = FirstCharToLowerCase(className);

        foreach (var sourceNavigation in tableDefinition.NavigationProperties)
        {
            var targetTableDefinition = databaseDefinition.TableDefinitions?
                .FirstOrDefault(definition => definition.ClassName == sourceNavigation.RelationshipPropertyType);

            var targetNavigation = targetTableDefinition?.NavigationProperties?
                .FirstOrDefault(definition => definition.RelationshipPropertyType == tableDefinition.ClassName
                                              && definition.RelationshipPropertyName
                                              == sourceNavigation.InverseEndKindPropertyName);

            if (targetTableDefinition is null || targetNavigation is null)
            {
                builder.AppendLine($"//TODO:({sourceNavigation.RelationshipPropertyName})<->({targetTableDefinition?.ClassName ?? sourceNavigation.InverseEndKindPropertyName}.{targetNavigation?.RelationshipPropertyName ?? "LOST"})");

                continue;
            }

            var sourceMultiplicity = sourceNavigation.RelationshipMultiplicity;
            var targetMultiplicity = targetNavigation.RelationshipMultiplicity;

            var sourceForeignKeys = sourceNavigation.ForeignKeyNames;
            var targetForiengKeys = targetNavigation.ForeignKeyNames;

            var sourceNavigationProperty = sourceNavigation.RelationshipPropertyName;

            var relationshipMethod = targetMultiplicity switch
            {
                RelationshipMultiplicity.One when
                    sourceMultiplicity == RelationshipMultiplicity.One => "RequiredWith",
                RelationshipMultiplicity.One when
                    sourceMultiplicity == RelationshipMultiplicity.Many => "ParentFor",
                RelationshipMultiplicity.One when
                    sourceMultiplicity == RelationshipMultiplicity.ZeroOrOne => "OptionalOf",
                RelationshipMultiplicity.ZeroOrOne when
                    sourceMultiplicity == RelationshipMultiplicity.One => "OptionalFor",
                RelationshipMultiplicity.ZeroOrOne when
                    sourceMultiplicity == RelationshipMultiplicity.Many => "OptionalParentFor",
                RelationshipMultiplicity.Many when
                    sourceMultiplicity == RelationshipMultiplicity.One => "ChildOf",
                _ => $"//TODO:{sourceMultiplicity}_{targetMultiplicity}"
            };

            builder.Append($"        {relationshipMethod}({lowerCaseClassName} => "
                           + $"{lowerCaseClassName}.{sourceNavigationProperty})");

            if (sourceForeignKeys is not null && targetForiengKeys is not null &&
                sourceForeignKeys.Count == targetForiengKeys.Count)
            {
                var targetTableName = FirstCharToLowerCase(targetTableDefinition.ClassName);

                for (var i = 0; i < sourceForeignKeys.Count; i++)
                {
                    var sourceKey = sourceForeignKeys[i];
                    var targetKey = targetForiengKeys[i];

                    builder.Append($".Map("
                                   + $"{lowerCaseClassName} => {lowerCaseClassName}!.{sourceKey}, "
                                   + $"{targetTableName} => {targetTableName}!.{targetKey})");
                }
            }

            builder.AppendLine($";");
        }
    }

    private static RelationshipMultiplicity? TryCaclulateRelationShip(DatabaseDefinition databaseDefinition, TableDefinition tableDefinition, NavigationPropertyDefinition navigationDefinition)
    {
        if (navigationDefinition.InverseEndKind is not null)
            return navigationDefinition.InverseEndKind;

        if (navigationDefinition.RelationshipMultiplicity is not null)
        {
            var relatedTo = navigationDefinition.RelationshipPropertyType;
            var targetDefinition =
                databaseDefinition.TableDefinitions?.FirstOrDefault(definition => definition.ClassName == relatedTo);
            var targetNavigation = targetDefinition?.NavigationProperties?.FirstOrDefault(navigation =>
                navigation.RelationshipPropertyType == tableDefinition.ClassName);
            var targetMultiplicity = targetNavigation?.InverseEndKind;

            if (targetMultiplicity is not null)
                return targetMultiplicity;

            var targetProperty = targetDefinition?.PropertyDefinitions?.FirstOrDefault(
                property => property.PropName == navigationDefinition.InverseEndKindPropertyName);

            targetProperty ??= targetDefinition?.PropertyDefinitions?.FirstOrDefault(
                property => property.PropTypeDefinition?.PropType == tableDefinition.ClassName);

            if (targetProperty is null)
                return null;

            if ((targetProperty?.PropTypeDefinition?.PropType ?? string.Empty).Contains("ICollection"))
                return RelationshipMultiplicity.Many;
            if (targetProperty?.PropTypeDefinition?.IsNullable ?? false)
                return RelationshipMultiplicity.ZeroOrOne;

            return RelationshipMultiplicity.One;
        }

        return null;
    }

    private static void AddProperties(StringBuilder builder, TableDefinition tableDefinition)
    {
        if (tableDefinition.PropertyDefinitions is null)
            return;

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = FirstCharToLowerCase(className);

        foreach (var propertyDefinition in tableDefinition.PropertyDefinitions)
        {
            var propertyName = propertyDefinition.PropName;

            builder.Append($"        Property({lowerCaseClassName} => {lowerCaseClassName}.{propertyName})");

            if (propertyDefinition.RequiredDefinition?.IsRequiredValidation ?? false)
            {
                builder.AppendLine();
                builder.Append($"                .IsRequired(");

                if (propertyDefinition.AllowEmptyString)
                    builder.Append($"allowEmptyStrings: true");

                builder.Append($")");
            }

            if ((propertyDefinition.MaxLengthDefinition?.IsMaxLength ?? false) &&
                propertyDefinition.MaxLengthDefinition.ValidationMaxLength is not null)
            {
                builder.AppendLine();
                builder.Append($"                .WithMaxLength({propertyDefinition.MaxLengthDefinition.ValidationMaxLength})");
            }

            if (propertyDefinition.PrecisionScaleDefinition is not null)
            {
                if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null ||
                    propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                {
                    builder.AppendLine();
                    builder.Append($"                .WithPrecision(");
                }

                if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null)
                {
                    if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                        builder.Append($"{propertyDefinition.PrecisionScaleDefinition.MapPrecision}, "
                                       + $"{propertyDefinition.PrecisionScaleDefinition.MapScale})");
                    else
                        builder.Append($"precision: {propertyDefinition.PrecisionScaleDefinition.MapPrecision})");
                }
                else if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                    builder.Append($"scale: {propertyDefinition.PrecisionScaleDefinition.MapScale})");
            }

            switch (propertyDefinition.PropTypeDefinition?.PropType)
            {
                case "int" when propertyDefinition.RangeDefinition is not null:
                {
                    if (propertyDefinition.RangeDefinition.Min is not null ||
                        propertyDefinition.RangeDefinition.Max is not null)
                    {
                        builder.AppendLine();
                        builder.Append($"                .WithIntRange(");
                    }

                    if (propertyDefinition.RangeDefinition.Min is not null)
                    {
                        if (propertyDefinition.RangeDefinition.Max is not null)
                            builder.Append($"{propertyDefinition.RangeDefinition.Min}, "
                                           + $"{propertyDefinition.RangeDefinition.Max})");
                        else
                            builder.Append($"min: {propertyDefinition.RangeDefinition.Min})");
                    }
                    else if (propertyDefinition.RangeDefinition.Max is not null)
                        builder.Append($"max: {propertyDefinition.RangeDefinition.Max})");

                    break;
                }
                case "decimal" when propertyDefinition.DecimalRangeDefinition is not null:
                {
                    if (propertyDefinition.DecimalRangeDefinition.Minimum is not null ||
                        propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                    {
                        builder.AppendLine();
                        builder.Append($"                .WithDecimalRange(");
                    }

                    if (propertyDefinition.DecimalRangeDefinition.Minimum is not null)
                    {
                        if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                            builder.Append($"{propertyDefinition.DecimalRangeDefinition.Minimum}, "
                                           + $"{propertyDefinition.DecimalRangeDefinition.Maximum})");
                        else
                            builder.Append($"min: {propertyDefinition.DecimalRangeDefinition.Minimum})");
                    }
                    else if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                        builder.Append($"max: {propertyDefinition.DecimalRangeDefinition.Maximum})");

                    break;
                }
            }

            AddColumnDefinition(builder, propertyDefinition);

            builder.AppendLine($";");
        }
    }

    private static void AddColumnDefinition(StringBuilder builder, PropertyDefinition propertyDefinition)
    {
        builder.AppendLine();
        builder.Append($"                .Column(\"{propertyDefinition.DbColumn}\")");

        if (propertyDefinition.RequiredDefinition?.MapIsRequired ?? false)
        {
            builder.AppendLine();
            builder.Append($"                .IsRequired()");
        }

        if ((propertyDefinition.MaxLengthDefinition?.IsMaxLength ?? false) &&
            propertyDefinition.MaxLengthDefinition.MapMaxLength is not null)
        {
            builder.AppendLine();
            builder.Append($"                .WithMaxLength({propertyDefinition.MaxLengthDefinition.MapMaxLength})");
        }

        if (propertyDefinition.PrecisionScaleDefinition is not null)
        {
            if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null ||
                propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
            {
                builder.AppendLine();
                builder.Append($"                .WithPrecision(");
            }

            if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null)
            {
                if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                    builder.Append($"{propertyDefinition.PrecisionScaleDefinition.MapPrecision}, "
                                   + $"{propertyDefinition.PrecisionScaleDefinition.MapScale})");
                else
                    builder.Append($"precision: {propertyDefinition.PrecisionScaleDefinition.MapPrecision})");
            }
            else if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                builder.Append($"scale: {propertyDefinition.PrecisionScaleDefinition.MapScale})");
        }

        switch (propertyDefinition.PropTypeDefinition?.PropType)
        {
            case "int" when propertyDefinition.RangeDefinition is not null:
            {
                if (propertyDefinition.RangeDefinition.Min is not null ||
                    propertyDefinition.RangeDefinition.Max is not null)
                {
                    builder.AppendLine();
                    builder.Append($"                .WithIntRange(");
                }

                if (propertyDefinition.RangeDefinition.Min is not null)
                {
                    if (propertyDefinition.RangeDefinition.Max is not null)
                        builder.Append($"{propertyDefinition.RangeDefinition.Min}, "
                                       + $"{propertyDefinition.RangeDefinition.Max})");
                    else
                        builder.Append($"min: {propertyDefinition.RangeDefinition.Min})");
                }
                else if (propertyDefinition.RangeDefinition.Max is not null)
                    builder.Append($"max: {propertyDefinition.RangeDefinition.Max})");

                break;
            }
            case "decimal" when propertyDefinition.DecimalRangeDefinition is not null:
            {
                if (propertyDefinition.DecimalRangeDefinition.Minimum is not null ||
                    propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                {
                    builder.AppendLine();
                    builder.Append($"                .WithDecimalRange(");
                }

                if (propertyDefinition.DecimalRangeDefinition.Minimum is not null)
                {
                    if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                        builder.Append($"{propertyDefinition.DecimalRangeDefinition.Minimum}, "
                                       + $"{propertyDefinition.DecimalRangeDefinition.Maximum})");
                    else
                        builder.Append($"min: {propertyDefinition.DecimalRangeDefinition.Minimum})");
                }
                else if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                    builder.Append($"max: {propertyDefinition.DecimalRangeDefinition.Maximum})");

                break;
            }
        }
    }
}