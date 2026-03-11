import { Box, List, ListItem, ListItemText, Paper, Typography } from '@mui/material';
import { type SitesImportResult } from '../../services/import.service';

const MAX_LIST_SHOW = 50;

export interface SitesImportResultCardProps {
  result: SitesImportResult;
}

export function SitesImportResultCard({ result }: SitesImportResultCardProps) {
  return (
    <Paper sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>
        Import result
      </Typography>

      <Typography>
        Inserted: <strong>{result.inserted}</strong>
      </Typography>
      <Typography>
        Duplicates skipped: <strong>{result.duplicatesCount}</strong>
      </Typography>
      <Typography>
        Errors: <strong>{result.errorsCount}</strong>
      </Typography>

      {result.duplicates.length > 0 && (
        <Box sx={{ mt: 2 }}>
          <Typography variant="subtitle2">
            Duplicate domains (first {MAX_LIST_SHOW}):
          </Typography>
          <List dense>
            {result.duplicates.slice(0, MAX_LIST_SHOW).map((domain) => (
              <ListItem key={domain}>
                <ListItemText primary={domain} />
              </ListItem>
            ))}
            {result.duplicates.length > MAX_LIST_SHOW && (
              <ListItem>
                <ListItemText
                  primary={`… and ${result.duplicates.length - MAX_LIST_SHOW} more`}
                />
              </ListItem>
            )}
          </List>
        </Box>
      )}

      {result.errors.length > 0 && (
        <Box sx={{ mt: 2 }}>
          <Typography variant="subtitle2">
            Errors (first {MAX_LIST_SHOW}):
          </Typography>
          <List dense>
            {result.errors.slice(0, MAX_LIST_SHOW).map((error) => (
              <ListItem key={error.rowNumber}>
                <ListItemText
                  primary={`Row ${error.rowNumber}: ${error.message}`}
                />
              </ListItem>
            ))}
            {result.errors.length > MAX_LIST_SHOW && (
              <ListItem>
                <ListItemText
                  primary={`… and ${result.errors.length - MAX_LIST_SHOW} more`}
                />
              </ListItem>
            )}
          </List>
        </Box>
      )}
    </Paper>
  );
}

