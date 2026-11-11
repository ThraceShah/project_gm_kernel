# Same-input Boolean oracle result gate

The independent review of `3344c53` identified a future false-pass path in
`scripts/RollingBooleanConsumerOracle.cs`: two API calls returning error 0 with
equal zero body counts would skip every output comparison. Error 0 also does
not replace checking the Boolean result status. This does not alter the recorded
GM error-157 experiments or the accepted scope of the source-root repair.

The oracle now requires `PK_boolean_result_success_c`, exactly one result body,
and a non-null body array independently for each successful GM and native API
call, before reading output arrays or entering full-check / strict-compare /
mass loops. The status name is from the official local SDK header and generated
binding; it is not inferred from an integer alone. One body is the expectation
of this particular overlapping-tools subtraction fixture, as confirmed by its
same-input native baseline. No general empty-Boolean behavior is changed.

`--check-result-gate` runs the same guard with one positive and five negatives:
failed status with a body, success with zero bodies and either array state,
success with a missing array, and success with two bodies. The check performs
no modeling API calls and returns exit 0 only when all five invalid outcomes
are rejected. Actual log: `temp_docs/contract-sol/fixture-result-gate.log`,
process exit **0**. This is qualification of the harness guard, not evidence
that the unfinished GM Boolean succeeds.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingBooleanConsumerOracle.cs -c Release \
  -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -p:GmInProcessLinkTasks=true -- --check-result-gate
```
