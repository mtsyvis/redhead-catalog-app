import React, { useState } from 'react';
import {
  Box,
  Paper,
  Typography,
  Button,
  Alert,
  List,
  ListItem,
  ListItemText,
  CircularProgress,
} from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { PageShell } from '../components/layout/PageShell';
import {
  importSites,
  type SitesImportResult,
  MAX_IMPORT_FILE_SIZE_BYTES,
  FILE_TOO_LARGE_MESSAGE,
} from '../services/import.service';

const ACCEPT_FILES = '.csv';
const MAX_DUPLICATES_SHOW = 50;
const MAX_ERRORS_SHOW = 50;

export function ImportSites() {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<SitesImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const chosen = e.target.files?.[0];
    setFile(chosen ?? null);
    setResult(null);
    setError(null);
    if (chosen && chosen.size > MAX_IMPORT_FILE_SIZE_BYTES) {
      setError(FILE_TOO_LARGE_MESSAGE);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const data = await importSites(file);
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageShell maxWidth="md">
      <Typography variant="h4" gutterBottom>
        Import Sites
      </Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Upload a CSV file. The first row must be a header with all columns in this order: <strong>Domain, DR, Traffic, Location, PriceUsd, PriceCasino, PriceCrypto, PriceLinkInsert, Niche, Categories</strong>. Values for PriceCasino, PriceCrypto, PriceLinkInsert, Niche, and Categories may be empty. Duplicates are skipped; row errors are reported. Invalid headers return an error below.
      </Typography>

      <Paper sx={{ p: 3, mb: 3 }}>
        <form onSubmit={handleSubmit}>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Button
              variant="outlined"
              component="label"
              startIcon={<UploadFileIcon />}
              disabled={loading}
            >
              Choose file (CSV)
              <input
                type="file"
                hidden
                accept={ACCEPT_FILES}
                onChange={handleFileChange}
              />
            </Button>
            {file && (
              <Typography variant="body2" color="text.secondary">
                Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </Typography>
            )}
            <Button
              type="submit"
              variant="contained"
              disabled={
                !file ||
                loading ||
                (file !== null && file.size > MAX_IMPORT_FILE_SIZE_BYTES)
              }
              startIcon={loading ? <CircularProgress size={20} color="inherit" /> : null}
            >
              {loading ? 'Importing…' : 'Import'}
            </Button>
          </Box>
        </form>
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {result && (
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Import result
          </Typography>
          <Typography>Inserted: <strong>{result.inserted}</strong></Typography>
          <Typography>Duplicates skipped: <strong>{result.duplicatesCount}</strong></Typography>
          <Typography>Errors: <strong>{result.errorsCount}</strong></Typography>

          {result.duplicates.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Duplicate domains (first {MAX_DUPLICATES_SHOW}):</Typography>
              <List dense>
                {result.duplicates.slice(0, MAX_DUPLICATES_SHOW).map((d, i) => (
                  <ListItem key={i}>
                    <ListItemText primary={d} />
                  </ListItem>
                ))}
                {result.duplicates.length > MAX_DUPLICATES_SHOW && (
                  <ListItem>
                    <ListItemText primary={`… and ${result.duplicates.length - MAX_DUPLICATES_SHOW} more`} />
                  </ListItem>
                )}
              </List>
            </Box>
          )}

          {result.errors.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Errors (first {MAX_ERRORS_SHOW}):</Typography>
              <List dense>
                {result.errors.slice(0, MAX_ERRORS_SHOW).map((e, i) => (
                  <ListItem key={i}>
                    <ListItemText primary={`Row ${e.rowNumber}: ${e.message}`} />
                  </ListItem>
                ))}
                {result.errors.length > MAX_ERRORS_SHOW && (
                  <ListItem>
                    <ListItemText primary={`… and ${result.errors.length - MAX_ERRORS_SHOW} more`} />
                  </ListItem>
                )}
              </List>
            </Box>
          )}
        </Paper>
      )}
    </PageShell>
  );
}