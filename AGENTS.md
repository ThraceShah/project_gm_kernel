# AGENTS

- 持久性文档统一放入 `docs/`。
- 临时文档统一放入 `temp_docs/`。
- 禁止使用绝对路径。
- 所有文档中的路径都必须相对于项目根目录。
- 所有脚本中的路径都必须相对于脚本文件自身。
- 所有需要编译的代码中的路径都必须相对于其最终编译产物所在位置。
- 所有 C# 代码必须基于 `.NET 10`。
- 所有 C# 代码都必须优先考虑 AOT 兼容性。
- 所有 C# 代码都必须优先考虑零分配。
- 所有脚本统一使用 C# 编写，并使用 `.NET 10` 的 `dotnet run file.cs` 方式运行。
- 所有 `int` 语义类型必须通过 `global using` 创建类型别名（如 `BodySlot`、`CurveTag`），禁止在 record/struct 中使用裸 `int` 表示不同含义的实体索引或句柄。定义见 `src/ProjectGmKernel.Native/Runtime/KernelTypes.cs`。

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. dotnet Process Management

**dotnet CLI 的 MSBuild worker 和 testhost 进程会在后台驻留，导致 CPU 占用 100% 或资源泄漏。必须遵守以下规则：**

- 所有 `dotnet build` / `dotnet test` 命令必须加 `MSBUILDDISABLENODEREUSE=1` 前缀，防止 MSBuild worker 进程驻留后台。
- `dotnet test` 执行完毕后，必须用 `pkill -f testhost.dll 2>/dev/null; true` 清理残留 testhost 进程。
- 推荐写法：
  ```
  MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
  ```
- 禁止不带 `MSBUILDDISABLENODEREUSE=1` 直接运行 `dotnet build` 或 `dotnet test`。

## 5. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.