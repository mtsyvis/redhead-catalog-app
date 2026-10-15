import { StatusBadge } from '../common/StatusBadge';
import type { WebmasterOfferStatus } from '../../types/webmasterOffers.types';

export function OfferStatusChip({ status }: { readonly status: WebmasterOfferStatus }) {
  const inactive = status === 'Inactive' || status === 2;
  return (
    <StatusBadge label={inactive ? 'Inactive' : 'Active'} tone={inactive ? 'error' : 'success'} />
  );
}
