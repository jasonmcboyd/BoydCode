namespace BoydCode.Domain.Enums;

// Maps to Claude Code modes: Default requires approval for writes, AcceptEdits auto-approves file edits,
// Plan mode is read-only, DontAsk auto-approves everything except deny-list, Bypass skips permission engine entirely (JEA is the boundary)
public enum PermissionMode { Default, AcceptEdits, Plan, DontAsk, BypassPermissions }
