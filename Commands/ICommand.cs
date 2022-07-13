namespace CodeGenerator.Commands;

public interface ICommand
{
    public string CommandKey { get; }

    public Task Generate(Dictionary<string, string> parameters, CancellationToken cancellationToken);
}