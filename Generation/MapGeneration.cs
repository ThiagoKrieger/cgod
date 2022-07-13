using System.Data.Entity.Core.Metadata.Edm;

namespace CodeGenerator.Generation;

public static class MapGeneration
{
    public static Tuple<string, string> GenerateMap(TableDefinition tableDefinition)
    {
        var uniques = tableDefinition.Uniques;
        var tableName = tableDefinition.DbTable;
        var className = tableDefinition.ClassName;

        var header = $"public class {className}Map : EntityTypeConfiguration<{className}>\n{{";
        var ctor = $"\n\tpublic {className}Map()\n\t{{";
        var toTable = $"\n\t\tToTable(\"{tableName}\");";

        //HasKey()
        var keyLine = "";

        if (tableDefinition.PropertyDefinitions != null)
        {
            var keyProps = tableDefinition.PropertyDefinitions
                .Where(p => p.IsKey)
                .ToList();

            var keyPropsStr = new List<string>();
            foreach (var propName in keyProps)
            {
                keyPropsStr.Add($"x.{propName.PropName}");
            }
            var joinedKeys = String.Join(", ", keyPropsStr);
            keyLine = keyPropsStr.Count == 1 
                ? $"\n\t\tHasKey(x => {keyPropsStr.First()})" 
                : $"\n\t\tHasKey(x => new {{ {joinedKeys} }})";
            keyLine += ";";
        }

        //HasIndex()
        var indexLine = "";
        var hasIndex = false;
        if (uniques != null)
            hasIndex = uniques.Any();

        if (hasIndex && uniques != null)
        {
            foreach (var index in uniques)
            {
                var indexPropsNames = new List<string>();
                var columnNames = index.ColumnNames;
                
                if (columnNames != null)
                    foreach (var columnName in columnNames)
                    {
                        var indexProp =
                            tableDefinition.PropertyDefinitions?.FirstOrDefault(p => p.DbColumn == columnName);
                        if (indexProp != null)
                            indexPropsNames.Add($"x.{indexProp.PropName}");
                    }

                var joinedIndexes = String.Join(", ", indexPropsNames);
                indexLine += indexPropsNames.Count == 1
                    ? $"\n\t\tHasIndex(x => {indexPropsNames.First()}).HasName(\"UK_{tableName}\").IsUnique()"
                    : $"\n\t\tHasIndex(x => new {{ {joinedIndexes} }}).HasName(\"UK_{tableName}\").IsUnique()";
                
                if (index.IsClustered)
                    indexLine += $".IsClustered()";
                
                indexLine += ";";
            }
        }
        
        //Properties()
        var properties = tableDefinition.PropertyDefinitions ?? new List<PropertyDefinition>();
        var propertiesLines = "";
        foreach (var property in properties)
        {
            propertiesLines += $"\n\t\tProperty(x => x.{property.PropName}).HasColumnName(\"{property.DbColumn}\")";
            if (property.IsKey)
            {
                propertiesLines += $".HasDatabaseGeneratedOption(DatabaseGeneratedOption.{property.DatabaseGeneratedOption});";
                continue;
            }

            propertiesLines += $".HasColumnType(\"{property.DbType}\")";
            
            if (property.IsRowVersion)
            {
                propertiesLines += ".IsRowVersion();";
                continue;
            }
            
            switch (property.PropTypeDefinition?.PropType)
            {
                case "Decimal":
                    propertiesLines += $".HasPrecision({property.PrecisionScaleDefinition?.MapPrecision}, {property.PrecisionScaleDefinition?.MapScale})";
                    break;
                case "String":
                    if (property.DbType != null && !property.DbType.Contains("(max)"))
                    {
                        propertiesLines += $".HasMaxLength({property.MaxLengthDefinition?.MapMaxLength})";
                        break;
                    }
                    propertiesLines += $".IsMaxLength()";
                    break;
            }
            if (property.RequiredDefinition is { MapIsRequired: true })
                propertiesLines += ".IsRequired()";
            propertiesLines += ";";
        }
        
        //Navigation Properties()
        var navigationProperties = tableDefinition.NavigationProperties;
        var navigationLine = "";

        if (navigationProperties != null)
            foreach (var navigationProperty in navigationProperties)
            {
                //one to one
                if (navigationProperty.RelationshipMultiplicity == RelationshipMultiplicity.One &&
                    navigationProperty.InverseEndKind == RelationshipMultiplicity.One)
                {
                    navigationLine +=
                        $"\n\t\tHasRequired(x => x.{navigationProperty.RelationshipPropertyName}).WithRequired(x => x.{navigationProperty.InverseEndKindPropertyName})";
                }

                //one to many
                if (navigationProperty.RelationshipMultiplicity == RelationshipMultiplicity.Many &&
                    navigationProperty.InverseEndKind == RelationshipMultiplicity.One)
                {
                    navigationLine +=
                        $"\n\t\tHasMany(x => x.{navigationProperty.RelationshipPropertyName}).WithRequired(x => x.{navigationProperty.InverseEndKindPropertyName})";
                }

                //many to many
                if (navigationProperty.RelationshipMultiplicity == RelationshipMultiplicity.Many &&
                    navigationProperty.InverseEndKind == RelationshipMultiplicity.Many)
                {
                    navigationLine +=
                        $"\n\t\tHasMany(x => x.{navigationProperty.RelationshipPropertyName}).WithMany(x => x.{navigationProperty.InverseEndKindPropertyName})";
                }

                if (navigationProperty.ForeignKeyNames == null)
                {
                    navigationLine += ";";
                    continue;
                }


                if (navigationProperty.ForeignKeyNames.Count() != 0)
                {
                    var listFormattedKey = new List<string>();
                    foreach (var keyName in navigationProperty.ForeignKeyNames)
                    {
                        listFormattedKey.Add($"x.{keyName}");
                    }

                    navigationLine +=
                        navigationProperty.ForeignKeyNames.Count == 1
                            ? $".HasForeignKey(x => {listFormattedKey.First()})"
                            : $".HasForeignKey(x => new {{ {String.Join(", ", listFormattedKey)} }})";
                }

                if (navigationLine == String.Empty)
                    continue;
                navigationLine += ";";
            }

        var map = $"{header}{ctor}{toTable}{keyLine}{indexLine}{propertiesLines}{navigationLine}\n\t}}\n}}";
        var filename = $"{tableDefinition.ClassName}Map.cs";
        return new Tuple<string, string>(map, filename);
    }
}