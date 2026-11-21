import { useState } from "react";
import {
  Alert,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Typography,
} from "@mui/material";
import { BrandButton } from "../common/BrandButton";
import { WebmasterOffersLink } from "./WebmasterOffersLink";
import { BULK_PRICE_TYPES } from "../../constants/webmasterOffers.constants";
import { BULK_PREVIEW_OUTCOME } from "../../constants/bulkOfferPricePreview.constants";
import { DEFAULT_PAGE_SIZE, PAGE_SIZE_OPTIONS } from "../../constants/pagination.constants";
import { writeTextToClipboard } from "../../utils/clipboard";
import type { BulkOfferPricePreview } from "../../types/bulkOfferPricePreview.types";

export function BulkOfferPriceFollowUp({
  preview,
  saved,
}: {
  preview: BulkOfferPricePreview;
  saved: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [copyState, setCopyState] = useState<{
    target: "domains" | "request";
    status: "copied" | "error";
  } | null>(null);
  const rows = preview.rows.filter(
    (row) =>
      row.outcome !== BULK_PREVIEW_OUTCOME.Exceptions &&
      (row.unsubmittedPriceTypes?.length ?? 0) > 0
  );
  const services = (types: number[]) =>
    types
      .map((type) => BULK_PRICE_TYPES.find((item) => item.value === type)?.label ?? String(type))
      .join(", ");

  if (!rows.length) return null;

  const copy = async (target: "domains" | "request") => {
    try {
      await writeTextToClipboard(
        target === "domains"
          ? rows.map((row) => row.domain).join("\n")
          : [
              "Please confirm current prices for the following services:",
              ...rows.map(
                (row) =>
                  `${row.domain} — ${row.proposedTerm} — ${services(row.unsubmittedPriceTypes!)}`
              ),
            ].join("\n")
      );
      setCopyState({ target, status: "copied" });
    } catch {
      setCopyState({ target, status: "error" });
    }
  };

  const followUpButton = (
    <BrandButton
      size="small"
      sx={{ alignSelf: "flex-start" }}
      onClick={() => {
        setPage(0);
        setCopyState(null);
        setOpen(true);
      }}
    >
      Follow up · {rows.length} {rows.length === 1 ? "domain" : "domains"}
    </BrandButton>
  );

  return (
    <>
      {saved ? (
        followUpButton
      ) : (
        <Alert
          severity="warning"
          role="status"
          action={followUpButton}
          sx={{ alignItems: "center", flexWrap: "wrap", "& .MuiAlert-action": { py: 0.5 } }}
        >
          {rows.length} {rows.length === 1 ? "domain has" : "domains have"} saved prices for
          services not included in this update. Existing prices will be kept.
        </Alert>
      )}
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        fullWidth
        maxWidth="md"
        aria-labelledby="price-follow-up-title"
      >
        <DialogTitle id="price-follow-up-title">Follow up with webmaster</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {saved
              ? "These services kept their previous prices and were not included in this update. This list was saved at the time of the operation."
              : "These services are not included in this update. Their previous prices will be kept."}
          </Typography>
          {copyState?.status === "error" && (
            <Alert severity="error" sx={{ mb: 2 }}>
              Could not copy {copyState.target === "domains" ? "domains" : "the request"}. Please
              try again.
            </Alert>
          )}
          <TableContainer>
            <Table size="small" aria-label="Prices to clarify with webmaster">
              <TableHead>
                <TableRow>
                  <TableCell>Domain</TableCell>
                  <TableCell>Term</TableCell>
                  <TableCell>Services to clarify</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.slice(page * pageSize, (page + 1) * pageSize).map((row) => (
                  <TableRow key={row.domain}>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} alignItems="center">
                        <Typography variant="body2" sx={{ overflowWrap: "anywhere", minWidth: 0 }}>
                          {row.domain}
                        </Typography>
                        <WebmasterOffersLink domain={row.domain} />
                      </Stack>
                    </TableCell>
                    <TableCell>{row.proposedTerm}</TableCell>
                    <TableCell>{services(row.unsubmittedPriceTypes!)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            count={rows.length}
            page={page}
            rowsPerPage={pageSize}
            rowsPerPageOptions={PAGE_SIZE_OPTIONS}
            onPageChange={(_, value) => setPage(value)}
            onRowsPerPageChange={(event) => {
              setPageSize(Number(event.target.value));
              setPage(0);
            }}
          />
        </DialogContent>
        <DialogActions disableSpacing sx={{ flexWrap: "wrap", gap: 1 }}>
          <BrandButton onClick={() => setOpen(false)}>Close</BrandButton>
          <BrandButton onClick={() => void copy("domains")}>
            {copyState?.status === "copied" && copyState.target === "domains"
              ? "Domains copied"
              : "Copy domains"}
          </BrandButton>
          <BrandButton onClick={() => void copy("request")}>
            {copyState?.status === "copied" && copyState.target === "request"
              ? "Request copied"
              : "Copy request to webmaster"}
          </BrandButton>
        </DialogActions>
      </Dialog>
    </>
  );
}
