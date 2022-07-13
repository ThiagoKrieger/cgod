using CodeGenerator.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeGenerator;

public class ApplicationHost : IHost
{
    private readonly IHost _genericHost;

    public ApplicationHost()
    {
        _genericHost = new HostBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddTransient(typeof(ICommand), typeof(GenerateModelCommand));
        services.AddTransient(typeof(ICommand), typeof(GenerateModelInterfaceCommand));
        services.AddTransient(typeof(ICommand), typeof(GenerateResourcesForEnumCommand));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _genericHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _genericHost.StopAsync(cancellationToken);
    }

    public IServiceProvider Services => _genericHost.Services;

    public void Dispose()
    {
        _genericHost.Dispose();
    }
}