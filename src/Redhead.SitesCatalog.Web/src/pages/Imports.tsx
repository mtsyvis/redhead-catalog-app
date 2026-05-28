import { Typography, Tabs, Tab } from '@mui/material';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { PageShell } from '../components/layout/PageShell';
import {
  importSites,
  importSitesUpdate,
  importQuarantine,
  importLastPublished,
  type SitesImportResult,
  MAX_IMPORT_FILE_SIZE_BYTES,
  FILE_TOO_LARGE_MESSAGE,
  ACCEPT_FILES,
} from '../services/import.service';
import { ImportInstructionsCard } from '../components/imports/ImportInstructionsCard';
import {
  LAST_PUBLISHED_IMPORT_INSTRUCTIONS,
  QUARANTINE_IMPORT_INSTRUCTIONS,
  SITES_IMPORT_INSTRUCTIONS,
} from '../constants/imports.constants';
import { useImportTab } from '../hooks/useImportTab';
import { ImportUploadSection } from '../components/imports/ImportUploadSection';
import { ImportTabContent } from '../components/imports/ImportTabContent';
import { SitesImportResultCard } from '../components/imports/SitesImportResultCard';
import { UpdateImportTab } from '../components/imports/UpdateImportTab';
import {
  SitesUpdateImportInstructions,
  SitesUpdateImportUploadNotes,
} from '../components/imports/SitesUpdateImportInstructions';
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
    label: 'Quarantine Import',
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

  const instructions = (
    <ImportInstructionsCard
      description={SITES_IMPORT_INSTRUCTIONS.description}
      requiredColumns={SITES_IMPORT_INSTRUCTIONS.requiredColumns}
      optionalNote={SITES_IMPORT_INSTRUCTIONS.optionalNote}
    />
  );

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
    return (
      <UpdateImportTab
        resultTitle="Quarantine import result"
        runImport={importQuarantine}
        persistedStateKey={persistedStateKey}
        instructions={{
          description: QUARANTINE_IMPORT_INSTRUCTIONS.description,
          requiredColumns: QUARANTINE_IMPORT_INSTRUCTIONS.requiredColumns,
          optionalNote: QUARANTINE_IMPORT_INSTRUCTIONS.optionalNote,
        }}
      />
    );
  }

  return (
    <UpdateImportTab
      resultTitle="Last published import result"
      runImport={importLastPublished}
      persistedStateKey={persistedStateKey}
      instructions={{
        description: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.description,
        requiredColumns: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.requiredColumns,
        optionalNote: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.optionalNote,
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
