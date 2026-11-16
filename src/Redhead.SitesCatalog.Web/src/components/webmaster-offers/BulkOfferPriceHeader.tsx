import type { ReactNode } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Link, Stack, Step, StepLabel, Stepper, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";

interface Props {
  title?: string;
  step?: number;
  applied?: boolean;
  busy?: boolean;
  actions: ReactNode;
}

export function BulkOfferPriceHeader({
  title = "Update webmaster prices",
  step,
  applied = false,
  busy = false,
  actions,
}: Props) {
  return (
    <Stack spacing={2}>
      <Link
        component={RouterLink}
        to="/webmaster-offers"
        underline="hover"
        color="text.secondary"
        aria-disabled={busy}
        onClick={(event) => {
          if (busy) event.preventDefault();
        }}
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 0.5,
          alignSelf: "flex-start",
          typography: "body2",
          pointerEvents: busy ? "none" : undefined,
        }}
      >
        <ArrowBackIcon sx={{ fontSize: 16 }} /> Webmaster Offers
      </Link>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="center"
        useFlexGap
        flexWrap="wrap"
        spacing={2}
      >
        <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
          {title}
        </Typography>
        <Stack direction="row" spacing={1} alignItems="center">
          {actions}
        </Stack>
      </Stack>
      {step !== undefined && (
        <Stepper activeStep={step} sx={{ width: "100%" }}>
          <Step>
            <StepLabel>Enter updates</StepLabel>
          </Step>
          <Step>
            <StepLabel>Preview changes</StepLabel>
          </Step>
          <Step completed={step === 2 && applied ? true : undefined}>
            <StepLabel>Result</StepLabel>
          </Step>
        </Stepper>
      )}
    </Stack>
  );
}
