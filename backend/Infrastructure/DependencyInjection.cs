using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Import;
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
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ISearchPresetRepository, SearchPresetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
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
        var importOptions = configuration.GetSection(ImportOptions.SectionName).Get<ImportOptions>() ?? new();
        importOptions.Validate();
        services.AddSingleton(storageOptions);
        services.AddSingleton(importOptions);
        services.AddSingleton(scannerOptions);
        services.AddSingleton(workerOptions);
        services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddSingleton<IDocumentStorageInventory>(provider =>
            (FileSystemDocumentStorage)provider.GetRequiredService<IDocumentStorage>());
        services.AddSingleton<IDocumentContentInspector, DocumentContentInspector>();
        services.AddSingleton<IDocumentStorageKeyFactory, DocumentStorageKeyFactory>();
        services.AddSingleton<IMalwareScanner, ClamAvScanner>();
        services.AddScoped<IDocumentDownloadService, DocumentDownloadService>();
        services.AddScoped<ScanOperationHandler>();
        services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<ScanOperationHandler>());
        services.AddSingleton<IImportFileStorage, ImportFileStorage>();
        services.AddSingleton<IImportFileInspector, ImportFileInspector>();
        services.AddSingleton<IImportRowReader, CsvImportRowReader>();
        services.AddScoped<ImportRunSupport>();
        services.AddScoped<ImportScanHandler>();
        services.AddScoped<ImportValidationHandler>();
        services.AddScoped<ImportCommitHandler>();
        services.AddScoped<ImportPurgeHandler>();
        services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<ImportScanHandler>());
        services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<ImportValidationHandler>());
        services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<ImportCommitHandler>());
        services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<ImportPurgeHandler>());
        services.AddScoped<DocumentStorageReconciler>();
        services.AddScoped<IOperationRepository, PostgreSqlOperationRepository>();
        services.AddHostedService<DurableOperationWorker>();
        services.AddHostedService<ImportMaintenanceService>();
        return services;
    }
}
