# Fixed d9 owned UI review probes

Run from the project root with .NET 10. These diagnostics write only fresh evidence below `temp_docs/owned-ui-d9-review/`. All service-control calls use private BCL stubs; publication uses an in-process loopback listener and a stub portal. Production source has no endpoint override: only the fixture copy of `ReviewedWebProgressRelease.cs` replaces the fixed HTTPS origin with loopback, and its manifest records that transformed copy.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/owned-ui-d9-review/OwnedProbe.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/owned-ui-d9-review/LegacyReadProbe.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/owned-ui-d9-review/FreezeProbe.cs
```

The first prints a private fixture path and performs 22 finite controls. The second performs 20 read-only checks of the existing UI424 release, raw legacy metadata, six real Git tools, complete drop-ins and retained real effective argv, plus three HTTPS GETs. It does not query or change the live service manager. The third uses three controls to show actual freeze/prepare from fixed Git objects despite a deliberately edited private input tool, then refuses a dirty UI checkout.

Pass the first probe's printed path, relative to this script directory, to the exact missing-owned-hook counterexample:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/owned-ui-d9-review/MissingOwnedProbe.cs -- ../../temp_docs/owned-ui-d9-review/<printed-fixture-id>
```

It copies the fixture, validates relocated pins/file sets/producer/portal/unit identities, removes only the previous owned hook, and runs the actual install CLI with all three previous-release options still present. Its private stub retains the old effective command after file unlink, modelling removal without daemon-reload. At fixed d9 the **inner CLI exits0**, creates a new hook, records `ownedUpgrade=false`, stops the stub timer and reloads: the outer diagnostic exits0 to confirm that counterexample, **not to accept the tool**. Inspect `receipt.json`, `install.log` and `stub.json.calls`; do not judge future closure by the outer diagnostic's exit alone.

The queued-writer control in the first probe retains its whole failed release as `next-queued-evidence`. The independent success scenario starts from a separate pristine fixture copy; this is diagnostic setup, not a runbook for bypassing an existing backup or blindly retrying a failed install.

The current fixed review verdict is **blocked** until an explicit owned transition cannot silently become first install when its expected old hook is absent. Live UI424, the frozen 354 tools, existing A+B acceptance and future hosted/proxy migration gates are unaffected.
