// Keep numeric values aligned with the corresponding domain enums.
export const MAIN_PRICE_TYPE = 0;
export const STATUS_ACTIVE = 1;
export const STATUS_INACTIVE = 2;
export const TERM_PERMANENT = 1;
export const TERM_FINITE = 2;
export const TERM_YEAR = 1;

// Keep aligned with WebmasterOfferFieldLimits and the API's Int32 term value.
export const PRIMARY_EMAIL_MAX_LENGTH = 20_000;
export const MAX_TERM_YEARS = 2_147_483_647;

export const PRICE_TYPES = [
  { value: MAIN_PRICE_TYPE, label: "Price USD", enumName: "Main" },
  { value: 1, label: "Casino", enumName: "Casino" },
  { value: 2, label: "Crypto", enumName: "Crypto" },
  { value: 3, label: "Dating", enumName: "Dating" },
  { value: 4, label: "Link Insert", enumName: "LinkInsertion" },
  { value: 5, label: "Link Insert 18+", enumName: "LinkInsertion18Plus" },
  { value: 6, label: "Banner", enumName: "Banner" },
  { value: 7, label: "Banner 18+", enumName: "Banner18Plus" },
  { value: 8, label: "Homepage Text Link", enumName: "HomepageTextLink" },
  { value: 9, label: "Homepage Text Link 18+", enumName: "HomepageTextLink18Plus" },
] as const;

export const BULK_PRICE_TYPES = PRICE_TYPES.map((type) => ({
  ...type,
  label: type.value === MAIN_PRICE_TYPE ? "Main" : type.label,
}));
