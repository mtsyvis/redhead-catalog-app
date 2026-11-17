# Backend code review

This is the repository procedure for backend reviews requested through `AGENTS.md`.
It supplies the context for a new session; review conclusions must come from the current
requirements, code, and evidence, rather than earlier chats. It does not change product rules.

## Quick requests

Open a new session in the intended checkout and select its feature branch before requesting a review.
These are ordinary prompts, not slash commands:

| Request | Focus |
| --- | --- |
| `Сделай code review` | General backend review of the current branch and local changes. |
| `Сделай code review на баги` | Correctness, regressions, boundaries, and failure handling. |
| `Сделай performance review` | Database queries, memory, I/O, concurrency, and external-service usage. |
| `Сделай review на рефакторинг` | Readability, responsibilities, reuse, duplication, and unnecessary abstractions. |
| `Сделай security review` | Authorization, authentication, sensitive data, and trust boundaries. |
| `Сделай review тестов` | Useful coverage, realism, duplication, setup, and test organization. |

An explicit focus changes emphasis; always report a confirmed serious correctness or security
defect encountered in the reviewed code. Add a base, commit range, or module when needed, for example:

* `Сделай code review относительно origin/main, только закоммиченные изменения.`
* `Сделай review на рефакторинг модуля Users целиком, включая существующий код.`

Review requests produce findings and recommendations. A request to fix them is a separate
instruction. Do not edit source, tests, documentation, or the Git index during review unless
the user also requests changes. Builds and tests may create their normal local artifacts.

## Determine the scope

1. Read `AGENTS.md` and inspect the current branch, `git status --short`, and available refs.
   An explicit PR target, base ref, commit range, file list, or module takes precedence.
2. For the default branch review, resolve the target from the locally available `origin/HEAD`
   symbolic ref, then `origin/main`, then `main`. Use the first resolvable target different
   from the current feature branch. Do not use a feature branch's upstream as the target
   merely because it is configured. For a stacked branch, use its stated parent target.
   If already on the target branch, including its local counterpart such as `main` for
   `origin/main`, apply step 5 instead of choosing another fallback.
3. Find the merge base with `git merge-base HEAD <target>`. Review the branch changes since
   that commit, not unrelated changes subsequently made on the target branch.
4. Include staged, unstaged, and non-ignored untracked files by default. Inspect their final
   combined state so the same change is not reported multiple times. Useful commands are
   `git diff --find-renames <merge-base>` for tracked files and
   `git ls-files --others --exclude-standard` for untracked paths, whose contents must be read
   separately. If the user requests committed changes only, use
   `git diff --find-renames <merge-base> HEAD` instead.
5. Do not fetch, switch branches, or alter the working tree just to start a review. State
   that the base uses local refs and may lag the remote. If no trustworthy target or merge
   base exists, explain what can be inspected and ask only for the missing scope. On the
   target branch itself, review local changes against `HEAD`; if none exist, request a range
   or module rather than silently reviewing the whole application.
6. Default scope covers the .NET projects under `src/Redhead.SitesCatalog.{Api,Application,Domain,Infrastructure}`,
   backend tests, migrations, and related configuration and documentation. Follow frontend
   callers when necessary to verify an API contract; a full React review requires explicit scope.
7. State the branch, target and merge-base SHA, focus, and treatment of local changes before
   investigating. Record the reviewed HEAD and status; if the checkout changes during review,
   identify the changed scope and recheck affected conclusions rather than claiming a fixed snapshot.

For a branch review, findings should concern introduced or worsened problems. Existing code
is context; identify pre-existing debt separately only when it directly affects the change.
For an explicit module review, existing problems are in scope. Never substitute an entire
repository audit for an empty or ambiguous diff.

## Establish the expected behavior

* Read only the relevant sections of `docs/business-requirements.md`; use `README.md` for
  structure and commands. Consult deployment or backup instructions when those areas change.
* Inspect changed methods in full, their callers, dependency registrations, related entities,
  database mappings, and nearby tests. Do not assess a fragment in isolation.
* Before recommending a new component, search the repository for equivalent behavior and
  inspect whether it has the same semantics. Name an existing reusable implementation when found.
* Follow `AGENTS.md` for business-critical areas and data-migration history. Read relevant
  entries in `docs/technical-debt.md` before presenting an already recorded limitation as new.
* If a requirement is missing, distinguish a question or assumption from a confirmed defect.
  Do not infer new business rules from a test name or a preferred architecture.

## Review criteria

A general review covers all criteria, prioritizing concrete risk. A focused review spends
most effort on its selected area and does not imply full coverage of the others.

### Correctness and security

Trace successful, rejected, empty, boundary, and failure paths. Check server-side permissions,
validation, disabled-account access, sensitive-field exposure, persistence, audit history,
transaction boundaries, concurrency, retries, idempotency, and cancellation where relevant.
For changed business-critical behavior, compare the implementation with its canonical rule.
Check backward compatibility of changed API contracts and background-job behavior.

### Readability and responsibilities

Check whether a reader can follow the operation without reconstructing deeply nested logic,
dense LINQ, nested ternaries, or unrelated state changes. Prefer meaningful names and coherent
operations. Identify independently changing responsibilities mixed in a class; explain the
smallest useful extraction, which may be a private method rather than another service.

File length, method length, dependency count, and nesting are investigation signals, not
standalone findings or universal limits. Moving code into partial files or tiny forwarding
classes is not sufficient evidence of improved design.

### Reuse, constants, and abstractions

Look for repeated business rules, transformations, queries, validation, and artifact formatting.
Compare meaning and reasons for change before proposing shared code. A second occurrence
prompts investigation; it does not automatically require a generic abstraction.

Recommend merging services only when they represent one coherent responsibility. Otherwise,
consider extracting the shared operation. Prefer a small local solution over a new layer,
base-class hierarchy, catch-all helper, or framework. Check non-obvious limits and identifiers
for clear names and ownership; distinguish shared rules from operational configuration.

### Performance and resource usage

Inspect query translation, filtering and pagination before materialization, projection size,
tracking, N+1 queries, repeated enumeration, per-row database or network calls, unbounded
collections, large import/export buffers, cancellation, and external-provider quotas.
Consider cache invalidation and bounded concurrency when the change introduces them.

Explain the concrete execution path and expected data volume behind a concern. Use tests,
query inspection, or isolated measurements when needed; do not run production benchmarks or
claim a measured slowdown without measurements. Separate hypotheses needing an execution
plan or benchmark from confirmed findings. Do not recommend indexes without checking the
existing mappings and migrations and identifying the relevant query.

### Test value and organization

Map tests to distinct business behaviors, boundaries, failure modes, and regressions.
Look for missing important cases, repeated assertions of the same behavior, excessive mocks,
large unrelated fixtures, and tests coupled to private implementation details.
Prefer a theory for a single rule with multiple inputs. Recommend folders under the existing
layer split and feature grouping for larger areas; split large classes by behavior when useful.

Use unit tests for suitable isolated rules. EF InMemory does not establish PostgreSQL query,
constraint, transaction, or concurrency behavior: inspect relevant PostgreSQL integration
coverage for those claims. Flag new production branches added solely to accommodate a fake
provider for examination; explain the actual coverage gap rather than requiring a wholesale
test-suite rewrite. Do not recommend test quotas, tests for every extracted method, or deleting
coverage merely because the suite is large. Follow the existing frontend-test restriction.

### Documentation and verification

Check whether changed behavior, operations, or commands update their canonical documentation;
avoid demanding duplicated feature descriptions. For review-only work, do not update the debt
register automatically or run unrelated frontend checks.

Run relevant backend checks when executable changes are in scope:

```text
dotnet build redhead-catalog-app.sln
dotnet test redhead-catalog-app.sln
```

Use focused reproduction or PostgreSQL integration checks when needed for a finding. Inspect
their prerequisites and use an isolated test database. Record skipped tests and unavailable
dependencies; a successful default run does not prove opt-in PostgreSQL checks ran.
Do not change tests to make a review pass. If a command cannot run, report the reason and the
exact command needed. Documentation-only reviews need link and diff checks, not application tests.

## Report

Lead with actionable findings, ordered by impact. For each finding provide:

* Type: defect, security, performance, maintainability, or tests; priority appropriate to its impact.
* A precise file/line and the triggering input, execution path, or maintenance scenario.
* The consequence and the evidence supporting it.
* The smallest useful correction, referencing existing reusable code when applicable.
* A way to verify the correction without changing intended business behavior.

Keep defects distinct from refactoring recommendations. Put directly relevant pre-existing
debt and unverified hypotheses after findings, clearly labelled. Do not present taste,
formatting handled by tooling, or speculative future reuse as defects. In the concise report,
prioritize up to five maintainability recommendations; do not omit confirmed serious defects
just to meet that limit. Report no actionable findings when that is the evidence-based result.

Finish with the reviewed scope, commands and results, checks not performed, remaining
uncertainties, and whether documentation changed. Use the user's language for the report.
Do not claim approval or absence of bugs from static inspection or passing tests alone.
