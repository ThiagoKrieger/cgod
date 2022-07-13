using CodeGenerator;
using CodeGenerator.Commands;
using Microsoft.Extensions.DependencyInjection;

if (args.Length == 0)
{
    Console.WriteLine("use a valid command.");

    return;
}

var requestedCommand = args[0].Replace("-", "");

var arguments = args.Where(arg => arg.StartsWith("-")).Select(arg => arg.Replace("-", "")).ToList();

var parameters = new Dictionary<string, string>();
for (var i = 1; i < arguments.Count; i++)
{
    var arg = arguments[i];
    var parts = arg.Split(":");

    parameters.Add(
        parts[0],
        parts.Length > 1
            ? parts[1]
            : string.Empty);
}

var application = new ApplicationHost();
var cancellationTokenSource = new CancellationTokenSource();

await application.StartAsync(cancellationTokenSource.Token);

var availableCommands = application.Services.GetServices<ICommand>();
foreach (var command in availableCommands)
{
    if (command.CommandKey == requestedCommand)
        await command.Generate(parameters, cancellationTokenSource.Token);
}

await application.StopAsync(cancellationTokenSource.Token);