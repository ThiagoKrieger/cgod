using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeGenerator.Commands;

public class GenerateResourcesForEnumCommand : ICommand
{
    public string CommandKey => "er";
    private const string EnumNameParameter = "e";

    public async Task Generate(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue(EnumNameParameter, out var enumName))
            return;

        await using var resourceStream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("CodeGenerator.GenerationResources.BaseResources.file");

        if (resourceStream is null)
            throw new InvalidOperationException("Resource file not found.");

        using var reader =
            new StreamReader(resourceStream, Encoding.UTF8);

        var resourcesBaseFileContent = await reader.ReadToEndAsync();

        await using var designerStream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("CodeGenerator.GenerationResources.BaseResources.Designer.file");

        if (designerStream is null)
            throw new InvalidOperationException("Designer file not found.");

        using var designerReader =
            new StreamReader(resourceStream, Encoding.UTF8);

        var designerBaseFileContent = await designerReader.ReadToEndAsync();

        var enumFile = await File.ReadAllTextAsync($"{enumName}.cs", cancellationToken);

        const string regEx = "(?<=\\    )(.[A-Za-z]+)(?=\\s*\\ \\=)";

        var resourceBody = string.Empty;
        var designerBody = string.Empty;

        foreach (var match in Regex.Matches(enumFile, regEx))
        {
            resourceBody += $"\n    <data name=\"{match}\" xml:space=\"preserve\">" +
                            $"\n        <value></value>" +
                            $"\n    </data>";

            designerBody += $"\n        /// <summary>" +
                            $"\n        ///   Looks up a localized string similar to {match}." +
                            $"\n        /// </summary>" +
                            $"\n        public static string {match} {{" +
                            $"\n            get {{" +
                            $"\n                return ResourceManager.GetString(\"{match}\", resourceCulture);" +
                            $"\n            }}" +
                            $"\n        }};";
        }

        var resourcesContent = string.Format(resourcesBaseFileContent, resourceBody);
        var designerContent = string.Format(designerBaseFileContent, designerBody);

        var resourcesName = $"{enumName}Resources.resx";
        var designerName = $"{enumName}Resources.Designer.cs";

        await File.WriteAllTextAsync(resourcesName, resourcesContent, cancellationToken);
        await File.WriteAllTextAsync(designerName, designerContent, cancellationToken);
    }
}