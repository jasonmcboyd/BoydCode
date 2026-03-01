# BoydCode UX Documentation

Formal UX design documentation for the BoydCode terminal application. Every visual state, interaction pattern, and design decision is documented here using Markdown with ASCII mockups — version-controlled, diffable, and readable anywhere.

## Design Philosophy

BoydCode is a **TUI** (text user interface), not a simple CLI. It has a persistent split-pane layout, streaming AI responses, collapsible execution windows, and menu-driven configuration. The UX design follows a **human-first** principle: we borrow freely from GUI, TUI, and CLI traditions based on what serves the user best, not what fits a paradigm.

See the [spectre-ux-expert agent](../../.claude/agents/spectre-ux-expert.md) for the full set of design principles, and the [Terminal UX Knowledge Base](../terminal-ux-knowledge-base.md) for deep reference material on terminal UX patterns.

## Reading Order

The documents are numbered by recommended reading order, not creation order.

### Foundation

| # | Document | What It Contains |
|---|---|---|
| 01 | [Personas](01-personas.md) | 3 user archetypes (Daily Driver, First-Timer, Automator) with goals, frustrations, and mental models |
| 02 | [Journey Maps](02-journey-maps.md) | 5-phase user experience arc from discovery through advanced configuration |
| 03 | [Screen Inventory](03-screen-inventory.md) | Master catalog of all 202 visual states with IDs, triggers, and navigation map |

### Design System

| # | Document | What It Contains |
|---|---|---|
| 06 | [Style Tokens](06-style-tokens.md) | Color semantics, typography weight, spacing conventions, status indicators, consistency audit |
| 07 | [Component Patterns](07-component-patterns.md) | 20 reusable UI patterns with ASCII mockups, code examples, and usage rules |

### Screen Specifications

Individual specs for every screen in the application. Each includes ASCII mockups at 80 and 120 columns, visual states, markup tokens, interactive elements, behavior, and edge cases.

#### Core Screens

| Screen | File |
|---|---|
| Startup Banner | [04-screens/startup-banner.md](04-screens/startup-banner.md) |
| Chat Loop (Split-Pane Layout) | [04-screens/chat-loop.md](04-screens/chat-loop.md) |
| Execution Window | [04-screens/execution-window.md](04-screens/execution-window.md) |
| Streaming Response | [04-screens/streaming-response.md](04-screens/streaming-response.md) |
| Not Configured State | [04-screens/not-configured.md](04-screens/not-configured.md) |
| Crash Screen | [04-screens/crash-screen.md](04-screens/crash-screen.md) |
| Login (OAuth) | [04-screens/login-flow.md](04-screens/login-flow.md) |

#### Slash Command Screens

| Command | Screens |
|---|---|
| `/help` | [slash-help.md](04-screens/slash-help.md) |
| `/project` | [create](04-screens/slash-project-create.md), [list](04-screens/slash-project-list.md), [show](04-screens/slash-project-show.md), [edit](04-screens/slash-project-edit.md), [delete](04-screens/slash-project-delete.md) |
| `/provider` | [list](04-screens/slash-provider-list.md), [setup](04-screens/slash-provider-setup.md), [show](04-screens/slash-provider-show.md), [remove](04-screens/slash-provider-remove.md) |
| `/jea` | [list](04-screens/slash-jea-list.md), [create](04-screens/slash-jea-create.md), [edit](04-screens/slash-jea-edit.md), [show](04-screens/slash-jea-show.md), [effective](04-screens/slash-jea-effective.md), [assign/unassign](04-screens/slash-jea-assign.md) |
| `/conversations` | [list](04-screens/slash-conversations-list.md), [show](04-screens/slash-conversations-show.md), [rename](04-screens/slash-conversations-rename.md), [delete](04-screens/slash-conversations-delete.md), [clear](04-screens/slash-conversations-clear.md) |
| `/context` | [show](04-screens/slash-context-show.md), [summarize](04-screens/slash-context-summarize.md), [refresh](04-screens/slash-context-refresh.md) |
| `/expand` | [slash-expand.md](04-screens/slash-expand.md) |

### User Flows

Step-by-step task flows with decision points, error branches, and screen references.

| Flow | File |
|---|---|
| First Run | [05-flows/first-run.md](05-flows/first-run.md) |
| New Session | [05-flows/new-session.md](05-flows/new-session.md) |
| Chat Turn | [05-flows/chat-turn.md](05-flows/chat-turn.md) |
| Tool Execution | [05-flows/tool-execution.md](05-flows/tool-execution.md) |
| Cancellation | [05-flows/cancellation.md](05-flows/cancellation.md) |

### Technical Specifications

| # | Document | What It Contains |
|---|---|---|
| 08 | [Interaction Specs](08-interaction-specs.md) | Keyboard shortcuts, animation timing, state machines, thread safety, resize handling |
| 09 | [Error Catalog](09-error-catalog.md) | 50+ error messages with IDs, triggers, severity, recovery, and consistency analysis |
| 10 | [Accessibility](10-accessibility.md) | Screen reader audit, NO_COLOR support, non-interactive mode, narrow terminal behavior, platform matrix |

## How to Use This Documentation

### When modifying or adding UI

1. Check the [Screen Inventory](03-screen-inventory.md) — does a spec exist for the screen you're changing?
2. Read the existing spec to understand the current design
3. Check [Style Tokens](06-style-tokens.md) and [Component Patterns](07-component-patterns.md) for consistency
4. Update the spec first, then implement
5. If adding a new screen, create a new spec file and add it to the inventory
6. Update the [Error Catalog](09-error-catalog.md) if adding new error states

### When reviewing UI changes

1. Compare the implementation against the screen spec
2. Verify style tokens are used consistently
3. Check the [Interaction Specs](08-interaction-specs.md) for keyboard and timing requirements
4. Run through the UX Review Checklist (in the spectre-ux-expert agent file)

### When designing new features

1. Review [Personas](01-personas.md) to understand who you're designing for
2. Create a flow diagram in `05-flows/`
3. Create screen specs in `04-screens/`
4. Reference existing [Component Patterns](07-component-patterns.md) for consistency
5. Check [Accessibility](10-accessibility.md) for requirements

## File Counts

| Category | Count |
|---|---|
| Foundation documents | 3 |
| Design system documents | 2 |
| Screen specifications | 35 |
| User flow diagrams | 5 |
| Technical specifications | 3 |
| **Total** | **48** |

## Maintenance

- **Spec-then-implement**: Update specs before changing code. PRs that touch `Presentation.Console` should reference relevant screen specs.
- **Keep the inventory current**: Add new screens to `03-screen-inventory.md` when they're created.
- **Style tokens are canonical**: If you need a new color or typography pattern, add it to `06-style-tokens.md` first.
- **Error catalog is comprehensive**: Every user-visible error message should be in `09-error-catalog.md`.
