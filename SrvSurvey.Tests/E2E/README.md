# Journal replay E2E tests

Each test boots the real `Game` (as `Main.newGame` does) on an STA/WinForms thread and feeds it journal lines,
`Status.json` and `NavRoute.json` through the same `FileSystemWatcher` pipeline the app uses with a running game.
No game, no network data needed: `codexRef.json`, `nebulae.json` and `bio-criteria.zip` come from the repo.

## Premise

- A test passes only if the app raised **no** error a player would see or that gets silently swallowed
  (`ReplayHost.AppErrors`: journal event handler exceptions, `crashGuard`, UI thread exceptions, `FormErrorSubmit`,
  `parseStatusFile` and faulted fire-and-forget tasks), **and** the state the issue lost is present afterwards.
- Every test also asserts its own premise (lock was hit, plotter was created, folder was missing, ...) so a test
  that no longer reaches the broken code fails instead of passing vacuously.
- Every fix-test except `StatusRaceReplayTests` failed against the unfixed code with the stack from the upstream issue.
  `StatusRaceReplayTests` (#1037) never reproduced (0/4 runs, up to 36k Status.json writes x 4000 mode changes):
  it guards the fix against deadlock/regression only.

## Isolation

- `ReplayIsolation` (a module initializer) points `Program.dataFolder` at `%TEMP%\SrvSurveyE2E\<run>` before any
  SrvSurvey type initializes, and boot aborts if the log path escapes it. A real `%appdata%\SrvSurvey` is never touched.
- `Main.isClosing = true` makes `FormErrorSubmit.Show` log instead of opening the Oops dialog.
- Tests run sequentially (SrvSurvey is full of static state). Each test uses its own commander FID and
  `SystemData`'s address-only cache is cleared per session.

## Fixture

`Fixtures/issue1044-Journal.2026-08-19T100112.01.log` is the journal attached to upstream #1044 with the commander
name, FID, squadron, group and ship ident replaced.

## Run

    pwsh SrvSurvey.Tests/E2E/run-e2e.ps1 [-Artifacts <folder>]

Artifacts: `<folder>/<test>.log` (the SrvSurvey log for that session, first line = asserted state) and `run-e2e.txt`.
Must be Release: in DEBUG `Game`'s static constructor skips init unless the process is `SrvSurvey.exe`.

| Test | Upstream issues | Fault injected |
|---|---|---|
| ScanOrganicReplayTests.Issue1044 | #1050 #1044 #1030 #1018 #1015 #1012 #1011 #1009 #1007 #993 #1046 #678 | real journal with a 2s `Music: MainMenu` mid-session |
| ScanOrganicReplayTests.ScanOrganicOnBodyOnlyStatusKnows | #1050 #1015 (residual throw sites) | ScanOrganic on a body only Status.json names |
| ScanOrganicReplayTests.Issue902 | #902 #935 #993 | journal starts landed on a body never scanned on this PC |
| StatusRaceReplayTests | #1037 #1034 | Status.json churn during 400 mode changes |
| JumpInfoReplayTests.Issue985 | #985 | NavRoute.json without `Route`, FSD charging |
| JumpInfoReplayTests.Issue986 | #986 | followed route hops without `id64`, FSD charging |
| SphericalSearchReplayTests | #994 #988 #1014 | active boxel search with unparseable boxel, galaxy map + NavRoute |
| BioCriteriaReplayTests | #1043 | `pub\bio-criteria` missing while scanning |
| DataFileLockReplayTests | #978 #940 #938 | system file locked exclusively for 300ms during FSDJump |
| OfflineReplayTests.Issue1045 | #1045 #1038 #1005 | codexRef.init a week after the last download, every request fails like a DNS failure |
| OfflineReplayTests.Issue963 | #963 #847 | Git.refreshPublishedData with every request failing like a DNS failure |
| UiCrashTests.Issue1075 | #1075 | real ListView sorted by FormBoxelSearch.ColumnSorter with a null distance Tag |
| UiCrashTests.Issue948 | #948 | real ViewJourneySystem.setSystem for a system with no data file |
| ThreadingReplayTests | (P2 scan, no issue) | Touchdown + deferAfter from a background thread still work after moving to the UI thread - regression guard only, the races were not reproducible |
| UiCrashTests.Issue1070 | #1070 #1064 #962 | PlotFSS.screenGrab with 0 width / 0 height rects |

## Failure modes (written before the UI-level tests)

UiCrashTests drive single WinForms controls rather than a journal replay, because no journal event reaches them:

- ColumnSorter: Tag null on either side; both null; ascending/descending flips left/right; unknown distance must sort as 0 (first).
- ViewJourneySystem: no system file at all; unreadable file; the stub must carry the StarRef name so the header is right.
- screenGrab: width 0, height 0 (GetClientRect on a window being created/closed); a real small rect must still return a bitmap of that size.
- Offline: DNS failure has no HTTP status code; codex init must not delete the files it falls back on; Git refresh must not open the Oops dialog.
