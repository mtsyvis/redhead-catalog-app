using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Domain.ClientCatalog;

public interface IClientCatalogAlertEmailSender
{
    Task<bool> SendAsync(ClientCatalogAlert alert, string userEmail, CancellationToken cancellationToken);
    Task<bool> SendAutoBanAsync(ClientCatalogAutoBan autoBan, string userEmail, CancellationToken cancellationToken);
}
