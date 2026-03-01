# BoydCode Backlog

## Context Window Management

### 1. Implement real ContextCompactor (eviction-based)
**Priority:** High | **Status:** Pending

Replace `NoOpContextCompactor` with a working implementation. When triggered at ~80% of context limit, evict oldest messages down to ~50% capacity (not just one message — evict in blocks to avoid re-triggering immediately). Preserve system prompt and recent turns. This is the foundation that other context features build on.

### 2. Add /compact and /summarize slash commands
**Priority:** High | **Status:** Pending | **Blocked by:** #1

- `/compact` — manually trigger context compaction (eviction)
- `/summarize` — use the LLM to summarize the conversation so far into a condensed form before evicting
- Both should report how many tokens were freed
- `/compact` could optionally accept a focus topic: `/compact focus on authentication`

### 3. Add provider rate/token limits to ProviderCapabilities
**Priority:** Medium | **Status:** Pending

Extend `ProviderCapabilities` (or provider config) to support additional constraints beyond `MaxContextWindowTokens` — specifically per-minute token rate limits (like the 30K input tokens/min limit). The orchestrator should be aware of these limits and either pre-check before sending or handle 429 responses with appropriate backoff. Consider making these user-configurable per provider profile since limits vary by org/plan.

### 4. Add tool output caps to prevent single-round overflow
**Priority:** Medium | **Status:** Pending

Add configurable output size limits to tools that can return large results — Glob (cap file count), Read (cap line count / total size), Grep (cap match count). Truncate with a footer like `"... 47 more files"`. This prevents a single tool round from exceeding the context window, which is a gap that compaction alone can't fix since compaction runs before the send, not after tool execution.

### 5. Design subagent architecture for bulk reads
**Priority:** Medium | **Status:** Pending | **Type:** Architecture/ADR

Plan how the orchestrator can route exploratory/bulk tool calls (reading many files, large globs) through a subagent that runs in its own context window and returns only a concise summary to the main conversation. Key design questions:
- How does the orchestrator decide what's "bulk"? Automatic (heuristic) or tool-specific?
- How does the subagent communicate results back?
- What's the interface between orchestrator and subagent?

Produce a design doc or ADR, not implementation.

### 6. Add truncation points and interactive context management
**Priority:** Low | **Status:** Pending | **Blocked by:** #2

After summarization, insert a truncation point marker in the conversation. Users can then interactively view truncation points (e.g., `/context` showing markers with timestamps and summaries) and select which point to truncate up to. This gives users explicit control over what stays in context. Design questions:
- How are truncation points displayed?
- How does the user select one?
- What happens to messages before the selected point?

---

*Items 1, 3, 4, and 5 can be worked independently. Item 2 depends on 1. Item 6 depends on 2.*

---

## UI Polish

### 7. Fix /help subcommand display format
**Priority:** Low | **Status:** Done

Subcommand rows in `/help` showed the full prefix redundantly (e.g., `/project create [name]`). Changed to show just the subcommand usage (e.g., `create [name]`) and indented the description column to match visual hierarchy.

### 8. Indent /help description column for subcommands
**Priority:** Low | **Status:** Done

Description text for subcommands now indented to create clearer visual hierarchy between parent commands and their subcommands.
