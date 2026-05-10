using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Studywise.Cli;

/// <summary>
/// Service locator for CLI commands.
/// Allows static commands to access DI services.
/// </summary>
public static class CommandServices
{
    private static IServiceProvider? _provider;
    
    public static void Initialize(IServiceProvider provider)
    {
        _provider = provider;
    }
    
    public static IServiceProvider Provider
    {
        get
        {
            if (_provider == null)
                throw new InvalidOperationException("CommandServices not initialized. Call Initialize() in Program.cs first.");
            return _provider;
        }
    }
    
    public static T GetRequiredService<T>() where T : notnull
        => Provider.GetRequiredService<T>();
        
    public static HttpClient GetHttpClient(string name = "Studywise")
        => Provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
}