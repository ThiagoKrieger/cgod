using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Core.Metadata.Edm;
using System.Reflection;
using System.Text;
using CodeGenerator.Utils;
using Newtonsoft.Json;

namespace CodeGenerator;

[Serializable]
public class DatabaseDefinition
{
    public ICollection<TableDefinition>? TableDefinitions { get; set; }
}

[Serializable]
public class TableDefinition
{
    public string? ClassName { get; set; }
    public string? DbTable { get; set; }
    public List<UniqueInfo>? Uniques { get; set; }
    public List<NavigationPropertyDefinition>? NavigationProperties { get; set; }
    public ICollection<PropertyDefinition>? PropertyDefinitions { get; set; }
}

[Serializable]
public class PropertyDefinition
{
    public RequiredDefinition? RequiredDefinition { get; set; }
    public bool IsKey { get; set; }
    public bool IsRowVersion { get; set; }
    public DatabaseGeneratedOption? DatabaseGeneratedOption { get; set; }
    public PropertyTypeDefinition? PropTypeDefinition { get; set; }
    public string? PropName { get; set; }
    public string? DbColumn { get; set; }
    public string? DbType { get; set; }
    public MaxLengthDefinition? MaxLengthDefinition { get; set; }
    public bool AllowEmptyString { get; set; }
    public PrecisionScaleDefinition? PrecisionScaleDefinition { get; set; }
    public RangeDefinition? RangeDefinition { get; set; }
    public DecimalRangeDefinition? DecimalRangeDefinition { get; set; }
}

[Serializable]
public class PrecisionScaleDefinition
{
    public byte? MapPrecision { get; set; }
    public byte? MapScale { get; set; }
}

[Serializable]
public class MaxLengthDefinition
{
    public bool? IsMaxLength { get; set; }
    public int? MapMaxLength { get; set; }
    public int? ValidationMaxLength { get; set; }
}

[Serializable]
public class PropertyTypeDefinition
{
    public bool IsNullable { get; set; }
    public string? PropType { get; set; }
}

[Serializable]
public class RequiredDefinition
{
    public bool MapIsRequired { get; set; }
    public bool IsRequiredValidation { get; set; }
}

[Serializable]
public class DecimalRangeDefinition
{
    public string? Minimum { get; set; }
    public string? Maximum { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public RangeMinValue? RangeMinValue { get; set; }
}

[Serializable]
public class RangeDefinition
{
    public object? Min { get; set; }
    public object? Max { get; set; }
    public string? TypeName { get; set; }
}

[Serializable]
public class NavigationPropertyDefinition // Model Quotation
{
    public string? RelationshipPropertyName { get; set; } //Items  (Quotation items)
    public RelationshipMultiplicity? RelationshipMultiplicity { get; set; } //Many? Collection | One/ZeroOrOne Single entity
    public string? RelationshipPropertyType { get; set; } //QuotationItem
    public string? InverseEndKindPropertyName { get; set; } //(model) QutationItem.cs.Quotation
    public RelationshipMultiplicity? InverseEndKind { get; set; } //QuotationItem has ONE Quotation
    public string? InverseEndKindType { get; set; }
    public IList<string>? ForeignKeyNames { get; set; }
}

[Serializable]
public class UniqueInfo
{
    public string? Name { get; set; }
    public string? TableName { get; set; }
    public bool IsClustered { get; set; }
    public string[]? ColumnNames { get; set; }
}

public static class DataBaseDefinitionSerializationExtensions
{
    public static TableDefinition GetTableDefinition(this string entity)
    {
        using var resourceStream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("CodeGenerator.DBD.json");

        if (resourceStream is null)
            throw new InvalidOperationException("Couldn't deserialize the data base definitions, resource file not found.");

        using var reader =
            new StreamReader(resourceStream, Encoding.UTF8);

        var definitionString = reader.ReadToEnd();
        var databaseDefinition = JsonConvert.DeserializeObject<DatabaseDefinition>(definitionString);

        if (databaseDefinition is null)
            throw new InvalidOperationException("Couldn't deserialize the data base definitions, not valid file.");

        var result = databaseDefinition.TableDefinitions?
            .FirstOrDefault(definition =>
                definition.ClassName?.ToLowerInvariant() == entity.ToLowerInvariant() ||
                definition.DbTable?.ToLowerInvariant() == entity.ToLowerInvariant());

        if (result is null)
            throw new InvalidOperationException("Couldn't deserialize the data base definitions, could not find requested entity.");

        return result;
    }
}