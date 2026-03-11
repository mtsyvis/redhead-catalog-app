import { Box, List, ListItem, ListItemText, Paper, Typography } from '@mui/material';
import { type UpdateImportResult } from '../../services/import.service';

const MAX_LIST_SHOW = 50;

export interface UpdateImportResultCardProps {
  readonly title: string;
  readonly unmatchedTitle: string;
  readonly result: UpdateImportResult;
}

export function UpdateImportResultCard({
  title,
  unmatchedTitle,
  result,
}: UpdateImportResultCardProps) {
  return (
    <Paper sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>
        {title}
      </Typography>

      <Typography>
        Matched: <strong>{result.matched}</strong>
      </Typography>
      <Typography>
        Unmatched: <strong>{result.unmatched.length}</strong>
      </Typography>
      <Typography>
        Duplicates: <strong>{result.duplicatesCount}</strong>
      </Typography>
      <Typography>
        Errors: <strong>{result.errorsCount}</strong>
      </Typography>

      {result.unmatched.length > 0 && (
        <Box sx={{ mt: 2 }}>
          <Typography variant="subtitle2">
            {unmatchedTitle} (first {MAX_LIST_SHOW}):
          </Typography>
          <List dense>
            {result.unmatched.slice(0, MAX_LIST_SHOW).map((domain) => (
              <ListItem key={domain}>
                <ListItemText primary={domain} />
              </ListItem>
            ))}
            {result.unmatched.length > MAX_LIST_SHOW && (
              <ListItem>
                <ListItemText
                  primary={`… and ${result.unmatched.length - MAX_LIST_SHOW} more`}
                />
              </ListItem>
            )}
          </List>
        </Box>
      )}

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
