import {
  Autocomplete,
  Box,
  MenuItem,
  TextField,
  Typography,
} from '@mui/material';
import type {
  WebmasterOfferEdit,
  WebmasterOfferMailboxOption,
} from '../../types/webmasterOffers.types';
import {
  STATUS_ACTIVE,
  STATUS_INACTIVE,
  mailboxLabel,
  type OfferFieldUpdater,
  type OfferFormState,
  type TermKind,
} from './editWebmasterOfferForm';

interface Props {
  readonly edit: WebmasterOfferEdit;
  readonly form: OfferFormState;
  readonly fieldErrors: Record<string, string[]>;
  readonly selectedMailboxes: WebmasterOfferMailboxOption[];
  readonly updateField: OfferFieldUpdater;
}

export function WebmasterOfferDetailsTab({
  edit,
  form,
  fieldErrors,
  selectedMailboxes,
  updateField,
}: Props) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography variant="subtitle2">Conditions &amp; notes</Typography>
        <TextField select label="Status" size="small" value={form.status} onChange={(event) => updateField('status', Number(event.target.value))}>
          <MenuItem value={STATUS_ACTIVE}>Active</MenuItem>
          <MenuItem value={STATUS_INACTIVE}>Inactive</MenuItem>
        </TextField>
        <Box sx={{ display: 'grid', gridTemplateColumns: form.termKind === 'finite' ? '1fr 1fr' : '1fr', gap: 1 }}>
          <TextField select label="Effective term" size="small" value={form.termKind} onChange={(event) => updateField('termKind', event.target.value as TermKind)} error={Boolean(fieldErrors.term?.length)} helperText={fieldErrors.term?.[0]}>
            <MenuItem value="none">No term</MenuItem>
            <MenuItem value="permanent">Permanent</MenuItem>
            <MenuItem value="finite">Finite years</MenuItem>
          </TextField>
          {form.termKind === 'finite' && (
            <TextField label="Years" type="number" size="small" inputProps={{ min: 1, step: 1 }} value={form.termYears} onChange={(event) => updateField('termYears', event.target.value)} error={Boolean(fieldErrors.term?.length)} />
          )}
        </Box>
        <TextField label="Imported term · read only" size="small" value={edit.offer.termRawText || '—'} disabled />
        <TextField label="Link policy" size="small" value={form.linkPolicyText} onChange={(event) => updateField('linkPolicyText', event.target.value)} multiline minRows={1} maxRows={4} error={Boolean(fieldErrors.linkPolicyText?.length)} helperText={fieldErrors.linkPolicyText?.[0]} />
        <TextField label="DF links" size="small" value={form.dfLinksRawText} onChange={(event) => updateField('dfLinksRawText', event.target.value)} multiline minRows={1} maxRows={4} error={Boolean(fieldErrors.dfLinksRawText?.length)} helperText={fieldErrors.dfLinksRawText?.[0]} />
        <TextField label="Sponsored tag" size="small" value={form.sponsoredTagRawText} onChange={(event) => updateField('sponsoredTagRawText', event.target.value)} multiline minRows={1} maxRows={4} error={Boolean(fieldErrors.sponsoredTagRawText?.length)} helperText={fieldErrors.sponsoredTagRawText?.[0]} />
        <TextField label="Client" size="small" value={form.clientRawText} onChange={(event) => updateField('clientRawText', event.target.value)} multiline minRows={1} maxRows={4} error={Boolean(fieldErrors.clientRawText?.length)} helperText={fieldErrors.clientRawText?.[0]} />
        <TextField label="Comments" size="small" value={form.commentText} onChange={(event) => updateField('commentText', event.target.value)} multiline minRows={3} maxRows={6} error={Boolean(fieldErrors.commentText?.length)} helperText={fieldErrors.commentText?.[0]} />
      </Box>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography variant="subtitle2">Contacts &amp; outreach</Typography>
        <TextField
          label="Contact raw text · read only"
          size="small"
          value={edit.offer.contactRawText || '—'}
          multiline
          minRows={4}
          disabled
        />
        <TextField label="Outreach sender" size="small" value={form.outreachSenderRawText} onChange={(event) => updateField('outreachSenderRawText', event.target.value)} multiline minRows={1} maxRows={3} error={Boolean(fieldErrors.outreachSenderRawText?.length)} helperText={fieldErrors.outreachSenderRawText?.[0]} />
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            gap: 1.25,
            p: 1.5,
            border: 1,
            borderColor: 'divider',
            borderRadius: 1,
          }}
        >
          <Typography variant="subtitle2">Linkbuilder mailboxes</Typography>
          <Autocomplete
            multiple
            options={edit.availableMailboxes}
            value={selectedMailboxes}
            getOptionLabel={mailboxLabel}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            onChange={(_event, values) => updateField('mailboxIds', values.map((mailbox) => mailbox.id))}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Assigned mailboxes"
                size="small"
                error={Boolean(fieldErrors.linkbuilderMailboxIds?.length)}
                helperText={fieldErrors.linkbuilderMailboxIds?.[0]}
              />
            )}
          />
          <TextField
            label="Imported text · read only"
            size="small"
            value={edit.offer.linkbuilderMailboxRawText || '—'}
            multiline
            maxRows={3}
            disabled
          />
        </Box>
      </Box>
    </Box>
  );
}
