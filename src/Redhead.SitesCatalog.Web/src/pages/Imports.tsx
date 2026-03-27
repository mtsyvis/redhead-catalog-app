import { useState } from 'react';
import { Typography, Tabs, Tab } from '@mui/material';
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
  SITES_UPDATE_IMPORT_INSTRUCTIONS,
  SITES_IMPORT_INSTRUCTIONS,
} from '../constants/imports.constants';
import { useImportTab } from '../hooks/useImportTab';
import { ImportUploadSection } from '../components/imports/ImportUploadSection';
import { ImportTabContent } from '../components/imports/ImportTabContent';
import { SitesImportResultCard } from '../components/imports/SitesImportResultCard';
import { UpdateImportTab } from '../components/imports/UpdateImportTab';

function SitesImportTab() {
  const {
    file,
    loading,
    error,
    result,
    setError,
    handleFileChange,
    handleSubmit,
  } = useImportTab<SitesImportResult>(importSites, {
    maxFileSizeBytes: MAX_IMPORT_FILE_SIZE_BYTES,
    fileTooLargeMessage: FILE_TOO_LARGE_MESSAGE,
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
      loading={loading}
      accept={ACCEPT_FILES}
      maxFileSizeBytes={MAX_IMPORT_FILE_SIZE_BYTES}
      onFileChange={handleFileChange}
      onSubmit={handleSubmit}
    />
  );

  const resultContent = result ? <SitesImportResultCard result={result} /> : null;

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

export function Imports() {
  const [tab, setTab] = useState(0);

  return (
    <PageShell maxWidth="md">
      <Typography variant="h4" gutterBottom>
        Imports
      </Typography>
      <Tabs value={tab} onChange={(_e, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Sites Import" />
        <Tab label="Sites Update Import" />
        <Tab label="Quarantine Import" />
        <Tab label="Last Published Import" />
      </Tabs>
      {tab === 0 && <SitesImportTab />}
      {tab === 1 && <UpdateImportTab
        resultTitle="Sites update import result"
        runImport={importSitesUpdate}
        instructions= {{
          description: SITES_UPDATE_IMPORT_INSTRUCTIONS.description,
          requiredColumns: SITES_UPDATE_IMPORT_INSTRUCTIONS.requiredColumns,
          optionalNote: SITES_UPDATE_IMPORT_INSTRUCTIONS.optionalNote,
        }}
      />}
      {tab === 2 && <UpdateImportTab
        resultTitle="Quarantine import result"
        runImport={importQuarantine}
        instructions= {{
          description: QUARANTINE_IMPORT_INSTRUCTIONS.description,
          requiredColumns: QUARANTINE_IMPORT_INSTRUCTIONS.requiredColumns,
          optionalNote: QUARANTINE_IMPORT_INSTRUCTIONS.optionalNote,
        }}
      />}
      {tab === 3 && <UpdateImportTab
        resultTitle="Last published import result"
        runImport={importLastPublished}
        instructions= {{
          description: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.description,
          requiredColumns: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.requiredColumns,
          optionalNote: LAST_PUBLISHED_IMPORT_INSTRUCTIONS.optionalNote,
        }}
      />}
    </PageShell>
  );
}
