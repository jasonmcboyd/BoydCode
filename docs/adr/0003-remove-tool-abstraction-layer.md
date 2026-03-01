# ADR-0003: Remove Tool Abstraction Layer — Shell-Only Execution

**Status:** Implemented

---

## Context

BoydCode originally shipped eight dedicated tools — `ReadTool`, `WriteTool`, `EditTool`, `GlobTool`, `GrepTool`, `PowerShellTool`, `WebFetchTool`, and `WebSearchTool` — each registered in an `IToolRegistry` and guarded by an `IPermissionEngine` and `IHookEngine`. This architecture had four compounding problems.

**1. LLMs bypass dedicated tools.** Over the course of a conversation, models drift toward shell commands (`cat`, `grep`, `find`, `Set-Content`) issued through the general-purpose shell tool rather than the purpose-built tools. The dedicated tools were a design intent, not a reliable LLM behavior constraint.

**2. The platform already enforces permissions.** JEA profiles constrain exactly which PowerShell commands are available to the `ConstrainedRunspaceEngine`. Container volume mounts and network flags constrain filesystem and network access in `ContainerExecutionEngine`. The tool-level `IPermissionEngine` duplicated that enforcement as a separate, weaker layer that the LLM could route around.

**3. Tools created a path translation problem.** `ReadTool`, `GlobTool`, and `GrepTool` executed on the host process and returned host-relative paths. In container mode those paths do not exist inside the container. The fix — translating paths between host and container at the tool layer — was a workaround for an architectural mistake: a tool that runs outside the execution engine has no business performing file operations on behalf of an agent that is executing inside a container.

**4. Tools duplicate shell capabilities.** `GlobTool` reimplemented `Get-ChildItem`/`find`, `ReadTool` reimplemented `Get-Content`/`cat`, and `GrepTool` reimplemented `Select-String`/`grep` — through .NET APIs that bypassed the execution engine's security boundary entirely. The shell the LLM uses natively already provides all of this, constrained by the execution engine.

---

## Decision

Remove all dedicated tool implementations and the supporting permission and hook infrastructure. Replace them with a single Shell tool defined as a static field on `AgentOrchestrator`. The Shell tool delegates all execution to the active `IExecutionEngine`. The JEA profile or container boundary is the sole security enforcement mechanism.

### Removed types and projects

| Removed | Category |
|---|---|
| `ReadTool`, `WriteTool`, `EditTool`, `GlobTool`, `GrepTool`, `PowerShellTool`, `WebFetchTool`, `WebSearchTool` | `ITool` implementations |
| `IToolRegistry`, `ToolRegistry` | Tool lookup |
| `IPermissionEngine`, `PermissionEngine` | Permission evaluation |
| `IHookEngine`, `NoOpHookEngine` | Pre/post-tool hooks |
| `ToolCategory`, `ToolExecutionResult`, `PermissionLevel`, `PermissionMode` | Supporting enums and result types |
| `HookTiming`, `PermissionRule`, `HookDefinition`, `HookResult` | Hook and permission value objects |
| `BoydCode.Infrastructure.Tools` project | Entire project deleted |

### Kept types

The following types survive because they are part of the LLM API protocol, not the tool execution layer:

- `ToolDefinition` and `ToolParameter` — JSON schema descriptors sent to the LLM in `LlmRequest.Tools`
- `ToolUseBlock` and `ToolResultBlock` — agentic loop protocol messages
- `LlmRequest.Tools` property
- `ToolChoiceStrategy` enum

### Shell tool definition

The Shell tool is declared as a static field on `AgentOrchestrator`:

```csharp
private static readonly ToolDefinition ShellTool = new()
{
    Name = "Shell",
    Description = "Execute a shell command via the active execution engine.",
    Parameters =
    [
        new ToolParameter { Name = "command",  Type = "string",  Required = true,  Description = "The command to run." },
        new ToolParameter { Name = "timeout",  Type = "integer", Required = false, Description = "Timeout in seconds. Defaults to 30." },
    ],
};
```

`AgentOrchestrator` passes `[ShellTool]` as `LlmRequest.Tools` each turn. When the LLM emits a `ToolUseBlock` for `Shell`, the orchestrator calls `_executionEngine.ExecuteAsync(command, timeout)` and returns the result as a `ToolResultBlock`.

### Execution environment context

`MetaPrompt.Build()` is extended to include the execution environment so the LLM knows which shell it is talking to:

- **InProcess mode** — lists the JEA-allowed command names from the composed effective profile
- **Container mode** — declares the shell binary (`bash`, `sh`, or the configured shell) and notes that the working directory is the mounted project root

---

## Consequences

### Benefits

- **Simpler architecture.** One tool instead of eight, no permission engine, no hook engine, no tool registry. `AgentOrchestrator` manages the agentic loop without routing through an abstraction whose primary purpose was guarding operations the execution engine already guards.
- **Path translation eliminated.** Shell tool output comes from the execution engine, which operates inside the container and returns container-native paths. There is no host-vs-container path mismatch.
- **Fewer tokens per request.** One tool definition replaces eight in every `LlmRequest.Tools` payload. At the scale of a long session this meaningfully reduces per-turn token count and prefix-cache pressure on the tier-1 fields.
- **Security enforcement is unified.** JEA allow/deny lists and container isolation are the single enforcement layer. There is no second layer that can be bypassed by the LLM choosing a different tool.
- **Full shell expressiveness.** The LLM can pipe commands, write scripts, and chain operations natively. The old tools exposed a tool-shaped subset of shell capabilities; the Shell tool exposes the whole shell, bounded only by the execution engine's constraints.

### Costs and risks

- **LLM must know the shell environment.** The model needs to know which commands are available and which shell syntax applies. This is addressed by `MetaPrompt.Build()` injecting execution environment context, but it is a runtime dependency — if the meta-prompt is incomplete or stale, the LLM may attempt unavailable commands.
- **No per-tool permission granularity.** The old `PermissionEngine` could allow `ReadTool` while blocking `WriteTool`. That distinction is now expressed as JEA allow/deny entries or container volume mount flags. Operators who want read-only access must configure it at the execution engine layer, not through a tool-level policy.
- **Web operations depend on the shell environment.** `WebFetchTool` and `WebSearchTool` are gone. Web access is only available if the execution environment exposes it: `Invoke-WebRequest`/`Invoke-RestMethod` in InProcess mode (subject to JEA), `curl`/`wget` in container mode (subject to network flags). A JEA profile that omits these commands or a container launched with `--network none` silently removes web capability.
