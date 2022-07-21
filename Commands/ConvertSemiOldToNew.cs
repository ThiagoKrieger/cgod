﻿using System.Reflection;
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
        var oldDefinition = JsonConvert.DeserializeObject<DatabaseDefinition>(definitionString);

        if (oldDefinition?.TableDefinitions == null)
            throw new InvalidOperationException("affe");



        foreach (var tableDefinition in oldDefinition.TableDefinitions)
        {
            await GenerateInterface(tableDefinition, cancellationToken);
            await GenerateModel(tableDefinition, cancellationToken);
            await GenerateDefinition(tableDefinition, cancellationToken);

        }
    }

    private async Task CreateFile(string fileName, string fileContent, CancellationToken cancellationToken)
    {
        if (!File.Exists(fileName))
        {
            var dir = Path.GetDirectoryName(fileName);

            if (dir is null)
                throw new InvalidOperationException();

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

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

    private async Task GenerateDefinition(TableDefinition tableDefinition, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = className.ToLowerInvariant();
        var interfaceName = $"I{tableDefinition.ClassName}";
        var tableName = tableDefinition.DbTable;

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
                builder.Append($", ");

            builder.Append($"{lowerCaseClassName} => {lowerCaseClassName}.{propertyName}");
        }

        builder.AppendLine($").WithName(\"PK_{tableName}\");");
        builder.AppendLine($"");

        AddProperties(builder, tableDefinition);

        builder.AppendLine($"");

        AddNavigations(builder, tableDefinition);

        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");

        var fileName = $"CP.Domain.Abstractions/EntityDefinition/{tableDefinition.ClassName}EntityDefinition.cs";
        var fileContent = builder.ToString();

        await CreateFile(fileName, fileContent, cancellationToken);
    }

    private static void AddNavigations(StringBuilder builder, TableDefinition tableDefinition)
    {
        if (tableDefinition.NavigationProperties == null)
            return;

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = className.ToLowerInvariant();

        foreach (var navigationDefinition in tableDefinition.NavigationProperties)
        {
            var relationshipPropertyname = navigationDefinition.RelationshipPropertyName
                                           ?? navigationDefinition.RelationshipPropertyType
                                           ?? "INVALID CONFIGURATION";

            var methodName = navigationDefinition.InverseEndKind switch
            {
                RelationshipMultiplicity.One when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.One => "RequiredWith",
                RelationshipMultiplicity.One when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.Many => "ParentFor",
                RelationshipMultiplicity.One when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.ZeroOrOne => "OptionalOf",
                RelationshipMultiplicity.ZeroOrOne when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.One => "OptionalFor",
                RelationshipMultiplicity.ZeroOrOne when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.Many => "OptionalParentFor",
                RelationshipMultiplicity.Many when
                    navigationDefinition.RelationshipMultiplicity == RelationshipMultiplicity.One => "ChildOf",
                _ => $"Unexpected_"
                     + $"{navigationDefinition.InverseEndKind}_"
                     + $"{navigationDefinition.RelationshipMultiplicity}"
            };

            builder.Append($"        {methodName}({lowerCaseClassName} => "
                           + $"{lowerCaseClassName}.{relationshipPropertyname})");

            if (navigationDefinition.ForeignKeyNames != null)
                foreach (var foreignKeyName in navigationDefinition.ForeignKeyNames)
                {
                    builder.Append($".Map({lowerCaseClassName} => {lowerCaseClassName}.{foreignKeyName})");
                }

            builder.AppendLine($";");
        }
    }

    private static void AddProperties(StringBuilder builder, TableDefinition tableDefinition)
    {
        if (tableDefinition.PropertyDefinitions is null)
            return;

        var className = tableDefinition.ClassName ?? throw new ArgumentNullException(nameof(tableDefinition.ClassName));
        var lowerCaseClassName = className.ToLowerInvariant();

        foreach (var propertyDefinition in tableDefinition.PropertyDefinitions)
        {
            var propertyName = propertyDefinition.PropName;

            builder.Append($"        Property({lowerCaseClassName} => {lowerCaseClassName}.{propertyName})");

            if (propertyDefinition.RequiredDefinition?.IsRequiredValidation ?? false)
            {
                builder.Append($".IsRequired(");

                if (propertyDefinition.AllowEmptyString)
                    builder.Append($"allowEmptyStrings = true");

                builder.Append($")");
            }

            if (propertyDefinition.MaxLengthDefinition?.IsMaxLength ?? false)
                builder.Append($".WithMaxLength({propertyDefinition.MaxLengthDefinition.ValidationMaxLength})");

            if (propertyDefinition.PrecisionScaleDefinition is not null)
            {
                if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null ||
                    propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                    builder.Append($".WithPrecision(");

                if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null)
                {
                    if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                        builder.Append($"{propertyDefinition.PrecisionScaleDefinition.MapPrecision}, "
                                       + $"{propertyDefinition.PrecisionScaleDefinition.MapScale})");
                    else
                        builder.Append($"precision = {propertyDefinition.PrecisionScaleDefinition.MapPrecision})");
                }
                else if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                    builder.Append($"scale = {propertyDefinition.PrecisionScaleDefinition.MapScale})");
            }

            switch (propertyDefinition.PropTypeDefinition?.PropType)
            {
                case "int" when propertyDefinition.RangeDefinition is not null:
                {
                    if (propertyDefinition.RangeDefinition.Min is not null ||
                        propertyDefinition.RangeDefinition.Max is not null)
                        builder.Append($".WithIntRange(");

                    if (propertyDefinition.RangeDefinition.Min is not null)
                    {
                        if (propertyDefinition.RangeDefinition.Max is not null)
                            builder.Append($"{propertyDefinition.RangeDefinition.Min}, "
                                           + $"{propertyDefinition.RangeDefinition.Max})");
                        else
                            builder.Append($"min = {propertyDefinition.RangeDefinition.Min})");
                    }
                    else
                        builder.Append($"max = {propertyDefinition.RangeDefinition.Max})");

                    break;
                }
                case "decimal" when propertyDefinition.DecimalRangeDefinition is not null:
                {
                    if (propertyDefinition.DecimalRangeDefinition.Minimum is not null ||
                        propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                        builder.Append($".WithDecimalRange(");

                    if (propertyDefinition.DecimalRangeDefinition.Minimum is not null)
                    {
                        if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                            builder.Append($"{propertyDefinition.DecimalRangeDefinition.Minimum}, "
                                           + $"{propertyDefinition.DecimalRangeDefinition.Maximum})");
                        else
                            builder.Append($"min = {propertyDefinition.DecimalRangeDefinition.Minimum})");
                    }
                    else
                        builder.Append($"max = {propertyDefinition.DecimalRangeDefinition.Maximum})");

                    break;
                }
            }

            AddColumnDefinition(builder, propertyDefinition);

            builder.AppendLine($";");
        }
    }

    private static void AddColumnDefinition(StringBuilder builder, PropertyDefinition propertyDefinition)
    {
        builder.Append($".Column(\"{propertyDefinition.DbColumn}\")");

        if (propertyDefinition.RequiredDefinition?.MapIsRequired ?? false)
            builder.Append($".IsRequired()");

        if (propertyDefinition.MaxLengthDefinition?.IsMaxLength ?? false)
            builder.Append($".WithMaxLength({propertyDefinition.MaxLengthDefinition.MapMaxLength})");

        if (propertyDefinition.PrecisionScaleDefinition is not null)
        {
            if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null ||
                propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                builder.Append($".WithPrecision(");

            if (propertyDefinition.PrecisionScaleDefinition.MapPrecision is not null)
            {
                if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                    builder.Append($"{propertyDefinition.PrecisionScaleDefinition.MapPrecision}, "
                                   + $"{propertyDefinition.PrecisionScaleDefinition.MapScale})");
                else
                    builder.Append($"precision = {propertyDefinition.PrecisionScaleDefinition.MapPrecision})");
            }
            else if (propertyDefinition.PrecisionScaleDefinition.MapScale is not null)
                builder.Append($"scale = {propertyDefinition.PrecisionScaleDefinition.MapScale})");
        }

        switch (propertyDefinition.PropTypeDefinition?.PropType)
        {
            case "int" when propertyDefinition.RangeDefinition is not null:
            {
                if (propertyDefinition.RangeDefinition.Min is not null ||
                    propertyDefinition.RangeDefinition.Max is not null)
                    builder.Append($".WithIntRange(");

                if (propertyDefinition.RangeDefinition.Min is not null)
                {
                    if (propertyDefinition.RangeDefinition.Max is not null)
                        builder.Append($"{propertyDefinition.RangeDefinition.Min}, "
                                       + $"{propertyDefinition.RangeDefinition.Max})");
                    else
                        builder.Append($"min = {propertyDefinition.RangeDefinition.Min})");
                }
                else
                    builder.Append($"max = {propertyDefinition.RangeDefinition.Max})");

                break;
            }
            case "decimal" when propertyDefinition.DecimalRangeDefinition is not null:
            {
                if (propertyDefinition.DecimalRangeDefinition.Minimum is not null ||
                    propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                    builder.Append($".WithDecimalRange(");

                if (propertyDefinition.DecimalRangeDefinition.Minimum is not null)
                {
                    if (propertyDefinition.DecimalRangeDefinition.Maximum is not null)
                        builder.Append($"{propertyDefinition.DecimalRangeDefinition.Minimum}, "
                                       + $"{propertyDefinition.DecimalRangeDefinition.Maximum})");
                    else
                        builder.Append($"min = {propertyDefinition.DecimalRangeDefinition.Minimum})");
                }
                else
                    builder.Append($"max = {propertyDefinition.DecimalRangeDefinition.Maximum})");

                break;
            }
        }
    }
}