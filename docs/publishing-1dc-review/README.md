# Fixed 1dc review reproducers

Candidate: `1dc0afc4a644867b49204bfbc76834b910413177`. These are independent BCL diagnostics, not production scripts or geometry acceptance tests. Each creates private evidence under `temp_docs/publishing-1dc-review/`; host processes use dynamic loopback ports and are cleaned up through their own process handles. Run from the project root with .NET 10:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/publishing-1dc-review/InterfaceProbe.cs
```

Expected: 44 checks, including real Watch/Refresh, public Publish, Prepare/Activate and serialized hook success/failure/recovery. It prints an evidence directory; pass that directory, relative to this script directory, to the supplementary retained-generation check:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/publishing-1dc-review/RetainedProbe.cs -- ../../temp_docs/publishing-1dc-review/<printed-fixture-id>
```

Expected: 5 checks and four exact HTTP byte comparisons. The active mapping explicitly excludes the old aggregate, so its successful GET actually exercises retained generation addressing. This changes only the first probe's private fixture, after its original evidence was captured.

The following probe reads the current user's reviewed UI drop-in and copies it into a fresh isolated filesystem root. It invokes only `systemd-analyze --root=<fixture> --man=no verify` with debug output, which parses configuration offline; no service manager, user-unit install, reload, start/stop or portal command is invoked. The supplied executables are only verification inputs and are never executed.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/publishing-1dc-review/HookPrecedenceProbe.cs
```

It reproduces the later deployment conflict: `zz-hosted-activation.conf` sorts before `zz-reviewed-ui.conf`, whose empty `ExecStartPost=` removes the hosted activation. The actual offline parser's final command remains the frozen reviewed static UI hook. This is a future migration blocker, separate from the isolated A+B code interface verdict; no cutover mode is exercised.
