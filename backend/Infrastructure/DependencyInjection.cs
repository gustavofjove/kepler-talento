using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KeplerTalento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:ApplicationDatabase is required.");
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICandidateReader, CandidateReader>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        var storageOptions = configuration.GetSection(DocumentStorageOptions.SectionName).Get<DocumentStorageOptions>()
            ?? throw new InvalidOperationException("DocumentStorage configuration is required.");
        FileSystemDocumentStorage.ValidateAndPrepare(storageOptions);
        var scannerOptions = configuration.GetSection(ClamAvOptions.SectionName).Get<ClamAvOptions>() ?? new();
        var workerOptions = configuration.GetSection(OperationWorkerOptions.SectionName).Get<OperationWorkerOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(scannerOptions.Host) || scannerOptions.Port is < 1 or > 65535 || scannerOptions.TimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException("ClamAV configuration is unsafe.");
        }
        if (workerOptions.PollSeconds is < 1 or > 300 || workerOptions.LeaseSeconds is < 5 or > 3600)
        {
            throw new InvalidOperationException("Operation worker configuration is unsafe.");
        }
        services.AddSingleton(storageOptions);
        services.AddSingleton(scannerOptions);
        services.AddSingleton(workerOptions);
        services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddSingleton<IDocumentStorageInventory>(provider =>
            (FileSystemDocumentStorage)provider.GetRequiredService<IDocumentStorage>());
        services.AddSingleton<IDocumentContentInspector, DocumentContentInspector>();
        services.AddSingleton<IMalwareScanner, ClamAvScanner>();
        services.AddScoped<IDocumentDownloadService, DocumentDownloadService>();
        services.AddScoped<ScanOperationHandler>();
        services.AddScoped<DocumentStorageReconciler>();
        services.AddScoped<IOperationRepository, PostgreSqlOperationRepository>();
        services.AddHostedService<DurableOperationWorker>();
        return services;
    }
}
