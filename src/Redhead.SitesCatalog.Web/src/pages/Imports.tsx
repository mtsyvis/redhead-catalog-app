import React, { useState } from 'react';
import {
  Box,
  Paper,
  Typography,
  Tabs,
  Tab,
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
  importQuarantine,
  importLastPublished,
  type SitesImportResult,
  type QuarantineImportResult,
  type LastPublishedImportResult,
  MAX_IMPORT_FILE_SIZE_BYTES,
  FILE_TOO_LARGE_MESSAGE,
} from '../services/import.service';

const ACCEPT_FILES = '.csv';
const MAX_LIST_SHOW = 50;

function SitesImportTab() {
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

  const handleSubmit = async (e: React.SubmitEvent<HTMLFormElement>) => {
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
    <Box>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Upload a CSV file. The first row must be a header with all columns in this order: <strong>Domain, DR, Traffic, Location, PriceUsd, PriceCasino, PriceCrypto, PriceLinkInsert, Niche, Categories</strong>. Values for PriceCasino, PriceCrypto, PriceLinkInsert, Niche, and Categories may be empty. Duplicates are skipped; row errors are reported.
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
              <input type="file" hidden accept={ACCEPT_FILES} onChange={handleFileChange} />
            </Button>
            {file && (
              <Typography variant="body2" color="text.secondary">
                Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </Typography>
            )}
            <Button
              type="submit"
              variant="contained"
              disabled={!file || loading || (file !== null && file.size > MAX_IMPORT_FILE_SIZE_BYTES)}
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
          <Typography variant="h6" gutterBottom>Import result</Typography>
          <Typography>Inserted: <strong>{result.inserted}</strong></Typography>
          <Typography>Duplicates skipped: <strong>{result.duplicatesCount}</strong></Typography>
          <Typography>Errors: <strong>{result.errorsCount}</strong></Typography>
          {result.duplicates.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Duplicate domains (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.duplicates.slice(0, MAX_LIST_SHOW).map((d, i) => (
                  <ListItem key={i}><ListItemText primary={d} /></ListItem>
                ))}
                {result.duplicates.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.duplicates.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
          {result.errors.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Errors (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.errors.slice(0, MAX_LIST_SHOW).map((e, i) => (
                  <ListItem key={i}><ListItemText primary={`Row ${e.rowNumber}: ${e.message}`} /></ListItem>
                ))}
                {result.errors.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.errors.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}

function QuarantineImportTab() {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<QuarantineImportResult | null>(null);
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

  const handleSubmit = async (e: React.SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!file) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const data = await importQuarantine(file);
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Upload a CSV with headers <strong>Domain</strong> and <strong>Reason</strong> (Reason optional). Existing sites matched by normalized domain will be marked Unavailable with the given reason. Unmatched domains are listed below.
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
              <input type="file" hidden accept={ACCEPT_FILES} onChange={handleFileChange} />
            </Button>
            {file && (
              <Typography variant="body2" color="text.secondary">
                Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </Typography>
            )}
            <Button
              type="submit"
              variant="contained"
              disabled={!file || loading || (file !== null && file.size > MAX_IMPORT_FILE_SIZE_BYTES)}
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
          <Typography variant="h6" gutterBottom>Quarantine import result</Typography>
          <Typography>Matched: <strong>{result.matched}</strong></Typography>
          <Typography>Unmatched: <strong>{result.unmatched.length}</strong></Typography>
          <Typography>Errors: <strong>{result.errorsCount}</strong></Typography>
          {result.unmatched.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Unmatched domains (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.unmatched.slice(0, MAX_LIST_SHOW).map((d, i) => (
                  <ListItem key={i}><ListItemText primary={d} /></ListItem>
                ))}
                {result.unmatched.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.unmatched.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
          {result.errors.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Errors (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.errors.slice(0, MAX_LIST_SHOW).map((e, i) => (
                  <ListItem key={i}><ListItemText primary={`Row ${e.rowNumber}: ${e.message}`} /></ListItem>
                ))}
                {result.errors.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.errors.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}

function LastPublishedImportTab() {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<LastPublishedImportResult | null>(null);
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

  const handleSubmit = async (e: React.SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!file) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const data = await importLastPublished(file);
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Upload a CSV with headers <strong>Domain</strong> and <strong>LastPublishedDate</strong>. Dates: DD.MM.YYYY (day precision) or month+year (e.g. January 2026, Jan 2026, 01.2026). Existing sites matched by normalized domain will be updated. Unmatched domains and errors are listed below.
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
              <input type="file" hidden accept={ACCEPT_FILES} onChange={handleFileChange} />
            </Button>
            {file && (
              <Typography variant="body2" color="text.secondary">
                Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </Typography>
            )}
            <Button
              type="submit"
              variant="contained"
              disabled={!file || loading || (file !== null && file.size > MAX_IMPORT_FILE_SIZE_BYTES)}
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
          <Typography variant="h6" gutterBottom>Last Published Import result</Typography>
          <Typography>Matched: <strong>{result.matched}</strong></Typography>
          <Typography>Unmatched: <strong>{result.unmatched.length}</strong></Typography>
          <Typography>Errors: <strong>{result.errorsCount}</strong></Typography>
          {result.unmatched.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Unmatched domains (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.unmatched.slice(0, MAX_LIST_SHOW).map((d, i) => (
                  <ListItem key={i}><ListItemText primary={d} /></ListItem>
                ))}
                {result.unmatched.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.unmatched.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
          {result.errors.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2">Errors (first {MAX_LIST_SHOW}):</Typography>
              <List dense>
                {result.errors.slice(0, MAX_LIST_SHOW).map((e, i) => (
                  <ListItem key={i}><ListItemText primary={`Row ${e.rowNumber}: ${e.message}`} /></ListItem>
                ))}
                {result.errors.length > MAX_LIST_SHOW && (
                  <ListItem><ListItemText primary={`… and ${result.errors.length - MAX_LIST_SHOW} more`} /></ListItem>
                )}
              </List>
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}

export function Imports() {
  const [tab, setTab] = useState(0);

  return (
    <PageShell maxWidth="md">
      <Typography variant="h4" gutterBottom>
        Imports
      </Typography>
      <Tabs value={tab} onChange={(_e, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Sites Import" />
        <Tab label="Quarantine Import" />
        <Tab label="Last Published Import" />
      </Tabs>
      {tab === 0 && <SitesImportTab />}
      {tab === 1 && <QuarantineImportTab />}
      {tab === 2 && <LastPublishedImportTab />}
    </PageShell>
  );
}
