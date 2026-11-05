export function parseCatalogPriceAmount(text: string): {
  amount: number | null;
  error: string | null;
} {
  if (!text.trim()) return { amount: null, error: null };

  const amount = Number(text);
  if (!Number.isFinite(amount) || amount <= 0) {
    return { amount: null, error: "Enter a price greater than 0." };
  }
  if (amount >= 10_000_000_000_000_000) {
    return { amount: null, error: "Price amount is too large." };
  }
  if (Math.round(amount * 100) / 100 !== amount) {
    return { amount: null, error: "Use at most 2 decimal places." };
  }
  return { amount, error: null };
}
