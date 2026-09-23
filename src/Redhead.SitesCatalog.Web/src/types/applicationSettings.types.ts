export interface ApplicationSettingsLimits {
  defaultClientSelectionLimit: number;
  maxClientSelectionLimit: number;
  clientRequestsPerMinute: number;
  clientUniqueSitesPerFiveMinutes: number;
  clientAlertUniqueSitesPerHour: number;
  globalMultiSearchMaxInputs: number;
  stopListMaxDomains: number;
  liteMultiSearchMaxDomainsPerRequest: number;
  liteMonthlyDomainLimit: number;
  customTableViewsPerUserTable: number;
  savedFilterSetsPerUserTable: number;
}

export interface ClientCatalogProtectionSettings {
  autoBanEnabled: boolean;
  autoBanUniqueSitesPer24Hours: number;
  updatedAtUtc: string | null;
  updatedByUserId: string | null;
  updatedByDisplayName: string | null;
}

export interface ClientCatalogProtectionSettingsUpdate {
  autoBanEnabled: boolean;
  autoBanUniqueSitesPer24Hours: number;
}
