# Dedicated publisher installation handoff

This handoff is for the deployment owner on qingyu2, using its dedicated `code/project_gm_kernel-progress-publisher` checkout under that user's home. Do not run the job in the UI/editor/other workers' working trees. The data worker does not install a remote service, start an agent or publish the portal. The coordinator assigns installation and portal publication to the existing deployment worker.

## Required checkout and runtime

Enable the timer only after the data and UI changes have been integrated into `origin/main`. The dedicated checkout must track main without local tracked or staged edits; `git merge --ff-only origin/main` fails rather than replacing local work. Initialize the existing PKToy submodule at the pinned gitlink. .NET 10, the private PK runtime/schema and the UI lockfile's package manager must already be available under the systemd user's PATH. No SDK/runtime files enter static output.

ParasolidScriptHost automatically discovers `third_party/parasolid/schema` and `third_party/parasolid/lib/linux-x64/libpskernel.so` (or `linux-arm64`). If the deployment owner keeps PK outside the dedicated checkout, set `PARASOLID_LIBRARY` and `PARASOLID_SCHEMA_DIR` to script-relative paths into that private installation; the Host resolves them against `scripts/`. Do not place library/schema overrides in the published data or pass tokens. The exporter records the actual PK version and binding commit.

## Exact data command and limits

Run from the dedicated checkout root:

```sh
MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_TieredCompilation=0 dotnet run --file scripts/WatchWebProgress.cs -- --once --state ../temp_docs/web-progress-publish/watch-state.json --registry web-progress-registry.json --case representatives --configuration Release --samples 1 --seconds 1200 --case-seconds 300 --output ../temp_docs/web-progress-publish/generated --publish-data-to ../temp_docs/web-progress-publish/data
```

The v5 default selects 17 recipes: regular solid blend; default short-edge blend; base chain and support chain; self-authored base trivalent corner; planar, sphere, mixed-analytic, NURBS and blend/plane Booleans; exterior contact; open surface blend; local block fillet; standard strict short lifecycle; transverse native-witness lifecycle; actual modified-support bore/cut; separate support identity. They remain a declared representative subset of 463 registrations, not full-project acceptance. Unsupported adapter registrations are not kernel capability claims. Fixed-probe replay, dependent nested fixtures and other adapter gaps stay visible.

One PK oracle process runs at a time. The service template caps aggregate CPU to two cores and memory to 12 GiB, with a 25-minute service timeout. Each case has a 300-second process budget including build; total collection has a 1200-second wall budget. Warmup is zero and samples=1: operation-only cold wall timing, not a stable performance certification. API intervals exclude input construction, export, tessellation, comparison and I/O. Resource quotas are part of the deployment configuration and must be recorded with the run.

`--case`, `--suite`, `--family`, budgets and samples can be overridden explicitly. The watcher checks the checkout SHA plus relevant source/fixture diff and the registry. Generated web/data is excluded from the dirty fingerprint, avoiding self-triggering loops. The supplied service fetches and fast-forwards only its dedicated checkout; the watcher itself never changes branches or pulls. The timer polls every ten minutes. Historical source commits remain in each run; no previously produced result is relabeled as execution at a new SHA.

## Units and static build integration

Install `scripts/systemd/gm-kernel-progress-refresh.service` and `.timer` into the systemd user's unit directory. They use `%h` rather than repository-specific absolute paths. The owner may change the dedicated checkout suffix and the CPU/memory limits in a drop-in. Verify the unit syntax with the target systemd version, reload, and enable the timer:

```sh
systemd-analyze --user verify scripts/systemd/gm-kernel-progress-refresh.service scripts/systemd/gm-kernel-progress-refresh.timer
systemctl --user daemon-reload
systemctl --user enable --now gm-kernel-progress-refresh.timer
systemctl --user start gm-kernel-progress-refresh.service
systemctl --user is-enabled gm-kernel-progress-refresh.timer
systemctl --user status gm-kernel-progress-refresh.timer gm-kernel-progress-refresh.service
journalctl --user -u gm-kernel-progress-refresh.service --no-pager
```

The service writes only ignored `temp_docs/web-progress-publish/` artifacts and ignored frontend build output. Its generated cache is immutable per run; the validated static cache retains prior runs and atomically switches index.json after successful collection. Kernel acceptance failures and timeouts are legitimate result data: they retain original exit codes, faults, null missing snapshots and unqualified ratios. The exporter returns 0 for well-formed results, 2 for its own compilation/contract/export failures; failure leaves watcher state unchanged and the previously published static index intact. A later timer retries. KillMode=control-group terminates owned children on service termination; interrupted run IDs and logs remain as evidence.

The first ExecStartPost prepares `web/dist` using the existing UI build and places the safe cache under `web/dist/data`; tracked `web/data` is never overwritten by this job, so subsequent fast-forward stays clean. It skips unchanged checkout/run pairs and validates the resulting dist/data. It does not invoke portalctl:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/PrepareWebProgressSite.cs -- --data ../temp_docs/web-progress-publish/data --web ../web --state ../temp_docs/web-progress-publish/site-state.json --builder pnpm
```

After confirming a successful UI build and real visible geometry, the deployment owner adds its existing portalctl static command as a final service ExecStartPost/drop-in, using slug `gm-kernel-progress` and this dedicated checkout's `web/dist`. This preserves portalctl's existing atomic publication. Credentials stay on the host. Do not add that hook until the coordinator accepts the UI and enables main tracking.

## Acceptance and restart evidence to return

Return the installed unit names, effective dedicated WorkingDirectory, timer enabled/active status, resource limits (`systemctl --user show gm-kernel-progress-refresh.service -p CPUQuotaPerSecUSec -p MemoryMax -p ControlGroup`) and journal/service exit code. A real new-SHA test must show: clean dedicated checkout fast-forwards to that SHA, index.latestRunId references a complete manifest at that exact source SHA, pkVersion/host/configuration/source hashes exist, actual case process exits and failure/null/timeout statuses are retained, validator errors is empty, dist/data resolves every mesh/evidence reference, real browser checks show triangles, and the portal's served index has the new run ID.

Immediately repeat the same one-shot/service before changing SHA. Its watcher should print `Source and registry unchanged`, retain latestRunId and skip oracles; the site step prints SITE_UNCHANGED. Restart the timer/user service manager and confirm persisted watcher/site state and old history survive. To test interruption recovery, terminate only this service; the next invocation must retain the partial run, use a new run ID and publish only a complete validated replacement. Reboot persistence requires the deployment owner's existing user-session/linger policy; return the actual policy rather than assuming it is enabled. No Windows wake or new agent is involved.
