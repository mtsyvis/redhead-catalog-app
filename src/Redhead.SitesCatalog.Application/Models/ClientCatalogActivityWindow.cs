namespace Redhead.SitesCatalog.Application.Models;

public sealed record ClientCatalogActivityWindow(string Period, int Requests, int RateLimitedRequests, int UniqueSites);
