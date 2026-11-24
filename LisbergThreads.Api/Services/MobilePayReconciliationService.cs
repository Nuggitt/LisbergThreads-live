using LisbergThreads.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LisbergThreads.Api.Services;

public sealed class MobilePayReconciliationService(IServiceScopeFactory scopes, IOptions<MobilePayOptions> options,
    ILogger<MobilePayReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<Guid> ids;
                using (var scope = scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    ids = await db.MobilePayPayments.Where(p => p.IsActive && p.NextCheckAt <= DateTime.UtcNow)
                        .OrderBy(p => p.NextCheckAt).Select(p => p.Id).Take(20).ToListAsync(stoppingToken);
                }
                foreach (var id in ids)
                {
                    // A fresh context per attempt also discards changes after a concurrency rollback.
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var payment = await db.MobilePayPayments.FindAsync([id], stoppingToken);
                    if (payment is null) continue;
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<MobilePayPaymentService>()
                            .ReconcileAsync(payment, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception)
                    {
                        // Never log credentials, shipping details, response bodies or access tokens.
                        logger.LogWarning("MobilePay payment {PaymentId} needs another reconciliation attempt.", id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("MobilePay reconciliation could not complete this interval."); }
        }
    }
}
