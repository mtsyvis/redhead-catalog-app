import { useCallback, useEffect, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Box,
  CircularProgress,
  IconButton,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import SearchIcon from '@mui/icons-material/Search';
import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { WebmasterWorkspaceView } from '../components/webmasters/WebmasterWorkspaceView';
import { useUserRoles } from '../hooks/useUserRoles';
import { webmastersService } from '../services/webmasters.service';
import type {
  WebmasterSearchItem,
  WebmasterSearchResult,
  WebmasterWorkspace,
} from '../types/webmasters.types';

const MINIMUM_CONTACT_LENGTH = 3;
const SEARCH_DEBOUNCE_MS = 400;

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}

export function Webmasters() {
  const { canReadWebmasterOffers } = useUserRoles();
  const [contact, setContact] = useState('');
  const [debouncedContact, setDebouncedContact] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [result, setResult] = useState<WebmasterSearchResult | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [selectedWebmasterId, setSelectedWebmasterId] = useState<string | null>(null);
  const [workspace, setWorkspace] = useState<WebmasterWorkspace | null>(null);
  const [workspaceLoading, setWorkspaceLoading] = useState(false);
  const [workspaceError, setWorkspaceError] = useState<string | null>(null);
  const searchRequestSequence = useRef(0);
  const workspaceRequestSequence = useRef(0);
  const lastSearchKey = useRef('');

  const resetWorkspace = useCallback(() => {
    workspaceRequestSequence.current += 1;
    setSelectedWebmasterId(null);
    setWorkspace(null);
    setWorkspaceLoading(false);
    setWorkspaceError(null);
  }, []);

  const runSearch = useCallback(
    async (query: string, pageIndex: number, requestedPageSize: number, force = false) => {
      const trimmed = query.trim();
      if (trimmed.length < MINIMUM_CONTACT_LENGTH) return;

      const key = `${trimmed.toLowerCase()}|${pageIndex}|${requestedPageSize}`;
      if (!force && lastSearchKey.current === key) return;
      lastSearchKey.current = key;
      const requestSequence = ++searchRequestSequence.current;

      setSearchLoading(true);
      setSearchError(null);
      setResult(null);
      resetWorkspace();

      try {
        const searchResult = await webmastersService.search(
          trimmed,
          pageIndex + 1,
          requestedPageSize
        );
        if (searchRequestSequence.current !== requestSequence) return;
        setResult(searchResult);
      } catch (error) {
        if (searchRequestSequence.current !== requestSequence) return;
        lastSearchKey.current = '';
        setSearchError(error instanceof Error ? error.message : 'Failed to search webmasters');
      } finally {
        if (searchRequestSequence.current === requestSequence) {
          setSearchLoading(false);
        }
      }
    },
    [resetWorkspace]
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedContact(contact.trim());
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [contact]);

  useEffect(() => {
    if (debouncedContact.length >= MINIMUM_CONTACT_LENGTH) {
      void runSearch(debouncedContact, page, pageSize);
    }
  }, [debouncedContact, page, pageSize, runSearch]);

  if (!canReadWebmasterOffers) {
    return <Navigate to="/sites" replace />;
  }

  const handleContactChange = (value: string) => {
    setContact(value);
    setDebouncedContact('');
    setPage(0);
    lastSearchKey.current = '';
    searchRequestSequence.current += 1;
    setSearchLoading(false);
    setSearchError(null);
    setResult(null);
    resetWorkspace();
  };

  const handleClear = () => {
    searchRequestSequence.current += 1;
    lastSearchKey.current = '';
    setContact('');
    setDebouncedContact('');
    setPage(0);
    setResult(null);
    setSearchLoading(false);
    setSearchError(null);
    resetWorkspace();
  };

  const loadWorkspace = async (webmasterId: string) => {
    const requestSequence = ++workspaceRequestSequence.current;
    setSelectedWebmasterId(webmasterId);
    setWorkspace(null);
    setWorkspaceLoading(true);
    setWorkspaceError(null);

    try {
      const loadedWorkspace = await webmastersService.getWorkspace(webmasterId);
      if (workspaceRequestSequence.current !== requestSequence) return;
      setWorkspace(loadedWorkspace);
    } catch (error) {
      if (workspaceRequestSequence.current !== requestSequence) return;
      setWorkspaceError(
        error instanceof Error ? error.message : 'Failed to load the webmaster workspace'
      );
    } finally {
      if (workspaceRequestSequence.current === requestSequence) {
        setWorkspaceLoading(false);
      }
    }
  };

  const handleResultKeyDown = (event: KeyboardEvent, item: WebmasterSearchItem) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      void loadWorkspace(item.webmasterId);
    }
  };

  return (
    <PageShell title="Webmasters" maxWidth="xl">
      <Box sx={{ mb: 1.5 }}>
        <TextField
          name="webmaster-contact-query"
          autoComplete="off"
          value={contact}
          onChange={(event) => handleContactChange(event.target.value)}
          placeholder="Search by email, name, or contact text"
          fullWidth
          inputProps={{
            'aria-label': 'Search webmasters by contact',
          }}
          InputProps={{
            startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
            endAdornment: contact ? (
              <IconButton
                aria-label="Clear search"
                edge="end"
                size="small"
                onMouseDown={(event) => event.preventDefault()}
                onClick={handleClear}
                sx={{ color: 'text.secondary' }}
              >
                <ClearIcon fontSize="small" />
              </IconButton>
            ) : undefined,
          }}
        />
      </Box>

      {searchError && (
        <Alert
          severity="error"
          sx={{ mb: 3 }}
          action={
            <BrandButton
              size="small"
              onClick={() => void runSearch(contact, page, pageSize, true)}
            >
              Retry
            </BrandButton>
          }
        >
          {searchError}
        </Alert>
      )}

      {searchLoading && (
        <Paper variant="outlined" sx={{ p: 4, mb: 3, textAlign: 'center' }}>
          <CircularProgress size={28} />
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
            Searching webmasters…
          </Typography>
        </Paper>
      )}

      {!searchLoading && result && result.total === 0 && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No webmasters matched “{debouncedContact}”.
        </Alert>
      )}

      {!searchLoading && result && result.total > 0 && (
        <Paper variant="outlined" sx={{ mb: 3, overflow: 'hidden' }}>
          <Box sx={{ px: 2, py: 1.5, borderBottom: 1, borderColor: 'divider' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Matching webmasters
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Select a row to load all offers and catalog domains.
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small" aria-label="Webmaster search results">
              <TableHead>
                <TableRow sx={{ bgcolor: 'grey.100' }}>
                  <TableCell>Primary email</TableCell>
                  <TableCell>Contact evidence</TableCell>
                  <TableCell align="right">Offers</TableCell>
                  <TableCell align="right">Active</TableCell>
                  <TableCell align="right">Domains</TableCell>
                  <TableCell>Latest update</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {result.items.map((item) => {
                  const selected = selectedWebmasterId === item.webmasterId;
                  return (
                    <TableRow
                      key={item.webmasterId}
                      hover
                      selected={selected}
                      tabIndex={0}
                      aria-selected={selected}
                      onClick={() => void loadWorkspace(item.webmasterId)}
                      onKeyDown={(event) => handleResultKeyDown(event, item)}
                      sx={{ cursor: 'pointer' }}
                    >
                      <TableCell sx={{ fontWeight: 600 }}>
                        {item.primaryEmail ?? 'Not detected'}
                      </TableCell>
                      <TableCell sx={{ maxWidth: 480 }}>
                        <Typography
                          variant="body2"
                          sx={{
                            whiteSpace: 'pre-wrap',
                            overflowWrap: 'anywhere',
                            display: '-webkit-box',
                            WebkitBoxOrient: 'vertical',
                            WebkitLineClamp: 3,
                            overflow: 'hidden',
                          }}
                        >
                          {item.matchingContactSnippet || item.representativeContactRawText}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">{item.offerCount}</TableCell>
                      <TableCell align="right">{item.activeOfferCount}</TableCell>
                      <TableCell align="right">{item.domainCount}</TableCell>
                      <TableCell>{formatDate(item.latestOfferUpdatedAtUtc)}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            count={result.total}
            page={page}
            rowsPerPage={pageSize}
            rowsPerPageOptions={[10, 20, 50]}
            onPageChange={(_event, nextPage) => setPage(nextPage)}
            onRowsPerPageChange={(event) => {
              setPageSize(Number(event.target.value));
              setPage(0);
            }}
          />
        </Paper>
      )}

      {workspaceLoading && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <CircularProgress size={28} />
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
            Loading webmaster workspace…
          </Typography>
        </Paper>
      )}

      {workspaceError && selectedWebmasterId && (
        <Alert
          severity="error"
          action={
            <BrandButton
              size="small"
              onClick={() => void loadWorkspace(selectedWebmasterId)}
            >
              Retry
            </BrandButton>
          }
        >
          {workspaceError}
        </Alert>
      )}

      {workspace && !workspaceLoading && (
        <WebmasterWorkspaceView workspace={workspace} />
      )}
    </PageShell>
  );
}
