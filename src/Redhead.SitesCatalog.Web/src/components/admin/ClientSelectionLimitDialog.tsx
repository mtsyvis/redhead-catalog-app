import { useEffect, useState } from 'react';
import { Alert, Box, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, TextField, Typography } from '@mui/material';
import { adminUsersService } from '../../services/adminUsers.service';
import type { ClientSelectionLimit } from '../../types/adminUsers.types';
import { BrandButton } from '../common/BrandButton';

export function ClientSelectionLimitDialog({ userId, email, onClose, onSaved }: {
  userId: string;
  email: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [data, setData] = useState<ClientSelectionLimit | null>(null);
  const [mode, setMode] = useState('default');
  const [rows, setRows] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    let active = true;
    adminUsersService.getSelectionLimit(userId).then((value) => {
      if (!active) return;
      setData(value);
      setMode(value.overrideRows == null ? 'default' : 'custom');
      setRows(String(value.overrideRows ?? value.defaultRows));
    }).catch((err: unknown) => {
      if (active) setError(err instanceof Error ? err.message : 'Could not load selection limit.');
    });
    return () => { active = false; };
  }, [userId]);

  const save = async () => {
    if (!data) return;
    const value = Number(rows);
    if (mode === 'custom' && (!/^\d+$/.test(rows) || !Number.isSafeInteger(value) || value < 1 || value > data.maxRows)) {
      setError(`Enter a whole number between 1 and ${data.maxRows.toLocaleString()}.`);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await adminUsersService.updateSelectionLimit(userId, mode === 'default' ? null : value);
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save selection limit.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Edit selection limit</DialogTitle>
      <DialogContent>
        <Typography variant="body2" sx={{ mb: 2 }}>{email} · Client</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {!data && !error && <CircularProgress size={24} />}
        {data && <Box sx={{ display: 'grid', gap: 2, pt: 1 }}>
          <TextField select label="Selection limit" value={mode} onChange={(event) => setMode(event.target.value)} disabled={saving}>
            <MenuItem value="default">Use default ({data.defaultRows} sites)</MenuItem>
            <MenuItem value="custom">Personal limit</MenuItem>
          </TextField>
          {mode === 'custom' && <TextField label="Sites per selection" value={rows} onChange={(event) => setRows(event.target.value)} disabled={saving} slotProps={{ htmlInput: { inputMode: 'numeric' } }} />}
          <Typography variant="body2" color="text.secondary">
            Limits catalog results and unique domains per Multi-search. Exports stay within this selection;
            smaller export limits and daily/weekly quotas still apply. Selections above 100 sites have pages within the selection only.
          </Typography>
          <Typography variant="subtitle2">Catalog activity</Typography>
          {data.activity.map((window) => <Box key={window.period}>
            <Typography variant="body2">{window.period}: {window.uniqueSites.toLocaleString()} unique sites</Typography>
            <Typography variant="caption" color="text.secondary">{window.requests.toLocaleString()} requests · {window.rateLimitedRequests.toLocaleString()} rate-limited</Typography>
          </Box>)}
          <Typography variant="caption" color="text.secondary">Unique sites combine catalog results, Multi-search matches and completed exports. Activity is observed without a daily or weekly viewing quota.</Typography>
        </Box>}
      </DialogContent>
      <DialogActions>
        <BrandButton kind="outline" onClick={onClose} disabled={saving}>Cancel</BrandButton>
        <BrandButton kind="primary" onClick={() => void save()} disabled={saving || !data}>{saving ? 'Saving...' : 'Save'}</BrandButton>
      </DialogActions>
    </Dialog>
  );
}
