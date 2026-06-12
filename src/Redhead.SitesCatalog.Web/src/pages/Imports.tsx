import { Typography, Tabs, Tab } from '@mui/material';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { PageShell } from '../components/layout/PageShell';
import {
  importSites,
  importSitesUpdate,
  importLastPublished,
  type SitesImportResult,
  MAX_IMPORT_FILE_SIZE_BYTES,
  FILE_TOO_LARGE_MESSAGE,
  ACCEPT_FILES,
} from '../services/import.service';
import {
  LAST_PUBLISHED_IMPORT_INSTRUCTIONS,
} from '../constants/imports.constants';
import { useImportTab } from '../hooks/useImportTab';
import { ImportUploadSection } from '../components/imports/ImportUploadSection';
import { ImportTabContent } from '../components/imports/ImportTabContent';
import { SitesImportResultCard } from '../components/imports/SitesImportResultCard';
import { UpdateImportTab } from '../components/imports/UpdateImportTab';
import { AvailabilityImportTab } from '../components/imports/AvailabilityImportTab';
import {
  SitesUpdateImportInstructions,
  SitesUpdateImportUploadNotes,
} from '../components/imports/SitesUpdateImportInstructions';
import { SitesImportInstructions } from '../components/imports/SitesImportInstructions';
import { useAuth } from '../contexts/AuthContext';

const IMPORT_RESULT_STORAGE_PREFIX = 'redhead.importResults.v1';

const IMPORT_ROUTES = [
  {
    key: 'sites-import',
    path: '/imports/sites-import',
    label: 'Sites Import',
  },
  {
    key: 'sites-update-import',
    path: '/imports/sites-update-import',
    label: 'Sites Update Import',
  },
  {
    key: 'quarantine-import',
    path: '/imports/quarantine-import',
    label: 'Availability Import',
  },
  {
    key: 'last-published-import',
    path: '/imports/last-published-import',
    label: 'Last Published Import',
  },
] as const;

type ImportRoute = (typeof IMPORT_ROUTES)[number];

function buildPersistedStateKey(userId: string, importKey: string) {
  return `${IMPORT_RESULT_STORAGE_PREFIX}.${userId}.${importKey}`;
}

function SitesImportTab({ persistedStateKey }: { readonly persistedStateKey: string }) {
  const {
    file,
    fileInputKey,
    loading,
    error,
    result,
    persistedResult,
    setError,
    clearImportState,
    handleFileChange,
    handleSubmit,
  } = useImportTab<SitesImportResult>(importSites, {
    maxFileSizeBytes: MAX_IMPORT_FILE_SIZE_BYTES,
    fileTooLargeMessage: FILE_TOO_LARGE_MESSAGE,
    persistedStateKey,
  });

  const instructions = <SitesImportInstructions />;

  const uploadSection = (
    <ImportUploadSection
      file={file}
      fileInputKey={fileInputKey}
      loading={loading}
      accept={ACCEPT_FILES}
      maxFileSizeBytes={MAX_IMPORT_FILE_SIZE_BYTES}
      onFileChange={handleFileChange}
      onSubmit={handleSubmit}
    />
  );

  const resultContent = result ? (
    <SitesImportResultCard
      result={result}
      fileName={persistedResult?.fileName}
      fileSize={persistedResult?.fileSize}
      completedAtUtc={persistedResult?.completedAtUtc}
      onStartNewImport={clearImportState}
    />
  ) : null;

  return (
    <ImportTabContent
      instructions={instructions}
      uploadSection={uploadSection}
      error={error}
      onClearError={() => setError(null)}
      result={resultContent}
    />
  );
}

function ImportRouteContent({
  activeImport,
  persistedStateKey,
}: {
  readonly activeImport: ImportRoute;
  readonly persistedStateKey: string;
}) {
  if (activeImport.key === 'sites-import') {
    return <SitesImportTab persistedStateKey={persistedStateKey} />;
  }

  if (activeImport.key === 'sites-update-import') {
    return (
      <UpdateImportTab
        resultTitle="Sites update import result"
        runImport={importSitesUpdate}
        persistedStateKey={persistedStateKey}
        instructionsContent={<SitesUpdateImportInstructions />}
        uploadHelper={<SitesUpdateImportUploadNotes />}
      />
    );
  }

  if (activeImport.key === 'quarantine-import') {
    return <AvailabilityImportTab persistedStateKey={persistedStateKey} />;
  }

  return (
    <UpdateImportTab
      resultTitle="Last published import result"
      runImport={importLastPublished}
      persistedStateKey={persistedStateKey}
      instructions={{
        title: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.title,
        description: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.description,
        requiredColumns: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.requiredColumns,
        requiredColumnsNote: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.optionalNote,
        rules: [
          'Columns must match exactly: Domain, LastPublishedDate.',
          'LastPublishedDate is required for each updated row.',
          'Domains are matched by normalized domain.',
          'Duplicate domains: last valid row wins.',
        ],
        examples: [
          {
            title: 'Full dates',
            csv: 'Domain,LastPublishedDate\nexample.com,15.01.2026\nanother-site.com,03.02.2026',
            note: 'Use DD.MM.YYYY for exact publication dates.',
          },
          {
            title: 'Month-only dates',
            csv: 'Domain,LastPublishedDate\nexample.com,January 2026\nanother-site.com,Feb 2026',
            note: 'Month and year values are saved as month-only dates.',
          },
        ],
      }}
    />
  );
}

export function Imports() {
  const { user } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const activeImport = IMPORT_ROUTES.find((route) => route.path === location.pathname);

  if (!activeImport || !user) {
    return <Navigate to="/imports/sites-import" replace />;
  }

  const persistedStateKey = buildPersistedStateKey(user.id, activeImport.key);

  return (
    <PageShell maxWidth="md">
      <Typography variant="h4" gutterBottom>
        Imports
      </Typography>
      <Tabs
        value={activeImport.path}
        onChange={(_event, nextPath) => navigate(nextPath)}
        sx={{ mb: 3 }}
      >
        {IMPORT_ROUTES.map((route) => (
          <Tab key={route.path} label={route.label} value={route.path} />
        ))}
      </Tabs>
      <ImportRouteContent
        key={activeImport.key}
        activeImport={activeImport}
        persistedStateKey={persistedStateKey}
      />
    </PageShell>
  );
}
