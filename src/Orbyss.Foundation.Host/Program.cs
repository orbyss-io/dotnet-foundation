using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using Nuplane;
using Nuplane.Loading.Hosting.Builder;
using Nuplane.Sources.Directory.Configuration;
using Orbyss.Foundation.Host.Feed;
using Orbyss.Foundation.Host.Shells;
using Orbyss.Foundation.Host.Transport;
using Orbyss.Foundation.Host.Diagnostics;
using Orbyss.Foundation.Web.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("hostsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile(".orbyss-foundation/web-profile.shells.json", optional: true, reloadOnChange: false)
    .AddJsonFile("shells.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var nuplaneConfiguration = configuration.GetSection("Nuplane");
var transport = configuration.GetSection("Foundation:Transport").Get<FoundationTransportOptions>() ?? new();
builder.WebHost.ConfigureKestrel(options => transport.Apply(options));
builder.Services.AddFoundationRequestDiagnosticRedaction();
builder.Services.AddFoundationProblemDetails();

builder.Services.AddNuplane(nuplaneConfiguration, nuplane =>
{
    nuplane.AddDirectoryFeedsFromConfiguration(nuplaneConfiguration);
    nuplane.AutoloadPackages(nuplaneConfiguration.GetSection("Loading"));
});

builder.Services.AddSingleton<NuplaneAssemblyProvider>();
builder.Services.AddCShellsAspNetCore(shells => shells
    .WithAssemblyProvider<NuplaneAssemblyProvider>()
    .WithConfigurationProvider(configuration)
    .WithWebRouting(options =>
    {
        options.EnablePathRouting = true;
    }));

builder.Services.AddHostedService<EagerShellActivationHostedService>();

var app = builder.Build();
app.UseFoundationProblemStatusCodePages();
app.MapShells();
app.Run();
