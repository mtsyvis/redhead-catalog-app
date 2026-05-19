namespace Redhead.SitesCatalog.Api.Services;

public sealed record GoogleDriveAccessToken(string AccessToken);

public sealed record GoogleDriveUser(string? EmailAddress);

public sealed record GoogleDriveFolder(string Id, string? Name);

public sealed record GoogleDriveUploadedFile(string Id, string Name, string? WebViewLink);

