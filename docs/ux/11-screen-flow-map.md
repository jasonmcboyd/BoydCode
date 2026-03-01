# Screen Flow Map

A visual map of how screens connect in the BoydCode terminal application. This
document uses Mermaid diagrams to show navigation paths between screen groups
(overview) and individual screen IDs (detail), plus a flat reference table of
every transition trigger.

## How to read this document

- **Overview diagram**: One box per functional group, arrows show the major
  navigation paths. Start here to understand the application's structure at a
  glance.
- **Detail diagrams**: One diagram per group, showing individual screen IDs
  (e.g., STARTUP-01, CHAT-02) and their transitions. Use these when you need
  to trace a specific path through the UI.
- **Transition reference table**: Flat lookup table. Every row is a transition
  from one screen to another with the trigger that causes it.

Screen IDs reference the master catalog in
[03-screen-inventory.md](03-screen-inventory.md). Flows reference the detailed
step-by-step documents in [05-flows/](05-flows/).

---

## 1. Overview Diagram

The high-level structure of the BoydCode application. Each box is a screen
group; arrows show the major navigation paths between them.

```mermaid
graph TD
    LAUNCH["Launch\n(banner, info grid, footer)"]
    CHAT["Chat Loop\n(idle input, slash dispatch)"]
    TURN["Active Turn\n(thinking, streaming, tool execution)"]
    HELP["/help\n(command reference)"]
    PROJECT["/project\n(create, list, show, edit, delete)"]
    PROVIDER["/provider\n(setup, list, show, remove)"]
    JEA["/jea\n(profiles, effective, assign)"]
    CONVERSATIONS["/conversations\n(list, show, rename, delete, clear)"]
    CONTEXT["/context\n(show, summarize, refresh, prune)"]
    EXPAND["/expand\n(full tool output)"]
    AGENT["/agent\n(list, show)"]
    AUTH["Auth\n(boydcode login)"]
    SYSTEM["System\n(crash recovery, exit)"]

    LAUNCH -->|"layout activated"| CHAT
    LAUNCH -->|"--resume invalid"| SYSTEM

    CHAT -->|"user message"| TURN
    CHAT -->|"/quit or /exit"| SYSTEM
    CHAT -->|"/help"| HELP
    CHAT -->|"/project ..."| PROJECT
    CHAT -->|"/provider ..."| PROVIDER
    CHAT -->|"/jea ..."| JEA
    CHAT -->|"/conversations ..."| CONVERSATIONS
    CHAT -->|"/context ..."| CONTEXT
    CHAT -->|"/expand"| EXPAND
    CHAT -->|"/agent ..."| AGENT

    TURN -->|"end_turn"| CHAT
    TURN -->|"tool_use"| TURN
    TURN -->|"error"| CHAT
    TURN -->|"cancel"| CHAT

    HELP -->|"output rendered"| CHAT
    PROJECT -->|"command complete"| CHAT
    PROVIDER -->|"command complete"| CHAT
    JEA -->|"command complete"| CHAT
    CONVERSATIONS -->|"command complete"| CHAT
    CONTEXT -->|"command complete"| CHAT
    EXPAND -->|"output rendered"| CHAT
    AGENT -->|"command complete"| CHAT

    AUTH -->|"success or failure"| SYSTEM

    PROJECT -.->|"stale settings"| CONTEXT
```

---

## 2. Per-Group Detail Diagrams

### 2.1 Launch

From application invocation through reaching the input prompt. Covers both
new sessions and resumed sessions.

```mermaid
graph TD
    subgraph Launch
        INIT["App invocation\n(resolve project, dirs, provider)"]
        S09["STARTUP-09\nUnknown Provider Warning"]
        S10["STARTUP-10\nMissing Directory Warning"]
        S11["STARTUP-11\nProvider Init Failure"]
        S01["STARTUP-01\nFull Banner (height >= 30)"]
        S02["STARTUP-02\nCompact Banner (height < 30)"]
        S03["STARTUP-03\nInfo Grid"]
        S04["STARTUP-04\nReady Footer"]
        S05["STARTUP-05\nNot Configured Footer"]
        S06["STARTUP-06\nStart Hint"]
        S07["STARTUP-07\nSession Resumed Hint"]
        S08["STARTUP-08\nSession Not Found Error"]
        L01["LAYOUT-01\nSplit-Pane Activated"]
        L07["LAYOUT-07\nFallback Prompt"]
        L02["LAYOUT-02\nInput Prompt"]
    end

    INIT -->|"unknown --provider"| S09
    INIT -->|"dirs missing"| S10
    INIT -->|"provider activate throws"| S11
    S09 --> S01
    S10 --> S01
    S11 --> S01

    INIT -->|"height >= 30"| S01
    INIT -->|"height < 30"| S02
    S01 --> S03
    S02 --> S03

    S03 -->|"provider configured"| S04
    S03 -->|"no API key"| S05

    S04 -->|"new session"| S06
    S04 -->|"--resume valid"| S07
    S04 -->|"--resume invalid"| S08

    S05 --> L01
    S06 --> L01
    S07 --> L01

    S08 -->|"exit code 1"| EXIT["EXIT"]

    L01 -->|"interactive, height >= 10"| L02
    INIT -->|"non-interactive or height < 10"| L07
    L07 --> L02
```

### 2.2 Chat Loop

The idle state between agent turns. The user is at the input prompt and can
type messages, slash commands, or exit commands.

```mermaid
graph TD
    subgraph "Chat Loop"
        L02["LAYOUT-02\nEmpty Input"]
        L03["LAYOUT-03\nInput With Text"]
        L04["LAYOUT-04\nQueued Messages"]
        L05["LAYOUT-05\nStatus Line"]
        L06["LAYOUT-06\nSeparator Row"]

        C15["CHAT-15\nUnknown Command (with suggestion)"]
        C16["CHAT-16\nUnknown Command (no suggestion)"]
        C17["CHAT-17\nInput Error"]
        C18["CHAT-18\nSlash Command Error"]
    end

    L02 -->|"user types"| L03
    L03 -->|"Enter (message)"| TURN["Active Turn"]
    L03 -->|"Enter (slash cmd)"| SLASH["Slash Command Dispatch"]
    L03 -->|"/quit or /exit"| EXIT["EXIT"]
    L03 -->|"unknown /cmd, close match"| C15
    L03 -->|"unknown /cmd, no match"| C16
    C15 --> L02
    C16 --> L02
    C17 --> L02
    C18 --> L02

    TURN -->|"agent busy, user types"| L04
    L04 -->|"agent idle"| L02
```

### 2.3 Active Turn

A single agent turn from user message through LLM response, potentially
including multiple tool execution rounds. This is the core interaction loop.

```mermaid
graph TD
    subgraph "Active Turn"
        C08["CHAT-08\nNo Provider Error"]
        C06["CHAT-06\nContext Compaction Warning"]
        C01["CHAT-01\nThinking Indicator"]
        C02["CHAT-02\nStreaming Response"]
        C03["CHAT-03\nStreaming Complete"]
        C04["CHAT-04\nAssistant Text (static)"]
        C05["CHAT-05\nToken Usage"]
        C07["CHAT-07\nMax Rounds Error"]
    end

    subgraph "Provider Errors"
        C09["CHAT-09\nAuth Error"]
        C10["CHAT-10\nRate Limit"]
        C11["CHAT-11\nContext Overflow"]
        C12["CHAT-12\nNetwork Error"]
        C13["CHAT-13\nServer Error"]
        C14["CHAT-14\nGeneric Error"]
        C19["CHAT-19\nFatal Error"]
    end

    INPUT["User submits message"] -->|"provider not configured"| C08
    C08 --> IDLE["LAYOUT-02\nBack to Input"]

    INPUT -->|"provider configured"| COMPACT{"Compact\nneeded?"}
    COMPACT -->|"yes"| C06
    COMPACT -->|"no"| C01
    C06 --> C01

    C01 -->|"streaming"| C02
    C01 -->|"non-streaming"| C04

    C02 --> C03
    C03 --> C05
    C04 --> C05

    C05 -->|"has tool_use"| EXEC["Tool Execution\n(see 2.4)"]
    C05 -->|"end_turn"| IDLE

    EXEC -->|"tool results added"| ROUND{"Round\n< 50?"}
    ROUND -->|"yes"| COMPACT
    ROUND -->|"no"| C07
    C07 --> IDLE

    C01 -->|"provider error"| C09
    C01 -->|"provider error"| C10
    C01 -->|"provider error"| C11
    C01 -->|"provider error"| C12
    C01 -->|"provider error"| C13
    C01 -->|"provider error"| C14
    C01 -->|"unhandled exception"| C19

    C09 --> IDLE
    C10 --> IDLE
    C11 --> IDLE
    C12 --> IDLE
    C13 --> IDLE
    C14 --> IDLE
    C19 --> EXIT["EXIT (code 1)"]
```

### 2.4 Tool Execution

The execution sub-flow within an active turn. Each tool call in a response
is processed sequentially.

```mermaid
graph TD
    subgraph "Tool Execution"
        E01["EXEC-01\nTool Call Preview Panel"]
        E02["EXEC-02\nWaiting Spinner"]
        E03["EXEC-03\nStreaming Output (layout)"]
        E04["EXEC-04\nStreaming Output (filling, <= 5 lines)"]
        E05["EXEC-05\nStreaming Output (scrolling, > 5 lines)"]
        E06["EXEC-06\nSuccess, Collapsed (> 5 lines)"]
        E07["EXEC-07\nSuccess, Short (1-5 lines)"]
        E08["EXEC-08\nSuccess, No Output"]
        E09["EXEC-09\nError, Collapsed (> 5 lines)"]
        E10["EXEC-10\nError, Short (1-5 lines)"]
        E11["EXEC-11\nError, No Output"]
        E14["EXEC-14\nCommand Cancelled"]
        E15["EXEC-15\nExecution Exception"]
        E16["EXEC-16\nUnknown Tool (silent)"]
        E17["EXEC-17\nEngine Not Initialized (silent)"]
    end

    subgraph "Cancellation"
        CA01["CANCEL-01\nCancel Hint"]
        CA02["CANCEL-02\nHint Cleared"]
    end

    TOOL_USE["LLM emits tool_use"] -->|"unknown tool name"| E16
    TOOL_USE -->|"engine not init"| E17
    TOOL_USE -->|"valid Shell call"| E01

    E16 --> NEXT["Next tool call\nor agent loop"]
    E17 --> NEXT

    E01 --> E02

    E02 -->|"output arrives, layout"| E03
    E02 -->|"output arrives, non-layout"| E04
    E02 -->|"no output, completes"| E08
    E02 -->|"exception"| E15
    E02 -->|"Esc / Ctrl+C"| CA01

    E04 -->|"> 5 lines"| E05

    CA01 -->|"2nd press within 1s"| E14
    CA01 -->|"timer expires (1s)"| CA02
    CA02 -->|"spinner resumes"| E02

    E03 -->|"success"| E06
    E03 -->|"error"| E09
    E04 -->|"success, 1-5 lines"| E07
    E04 -->|"error, 1-5 lines"| E10
    E05 -->|"success"| E06
    E05 -->|"error"| E09

    E06 --> NEXT
    E07 --> NEXT
    E08 --> NEXT
    E09 --> NEXT
    E10 --> NEXT
    E11 --> NEXT
    E14 --> NEXT
    E15 --> NEXT
```

### 2.5 /help

Single screen. Renders a command reference table and returns to the chat loop.

```mermaid
graph LR
    INPUT["/help typed"] --> H01["HELP-01\nHelp Table"]
    H01 --> IDLE["LAYOUT-02\nBack to Input"]
```

### 2.6 /project

Project management CRUD flows. Create and edit are interactive multi-step
wizards; list, show, and delete are simpler flows.

```mermaid
graph TD
    subgraph "/project create"
        P02["PROJ-02\nName Prompt"]
        P03["PROJ-03\nAlready Exists Error"]
        P04["PROJ-04\nCreate Success"]
        P05["PROJ-05\nConfigure Prompt"]
        P06["PROJ-06\nSection Picker"]
        P07["PROJ-07\nDirectory Loop"]
        P08["PROJ-08\nSystem Prompt"]
        P09["PROJ-09\nContainer Settings"]
        P10["PROJ-10\nSaved"]
        P11["PROJ-11\nTip (No Configure)"]
        P12["PROJ-12\nUsage (Non-Interactive)"]
    end

    subgraph "/project list"
        P13["PROJ-13\nProject Table"]
        P14["PROJ-14\nEmpty State"]
    end

    subgraph "/project show"
        P15["PROJ-15\nDetail View"]
        P16["PROJ-16\nMinimal Tip"]
        P17["PROJ-17\nNot Found"]
    end

    subgraph "/project edit"
        P19["PROJ-19\nEdit Menu Loop"]
        P20["PROJ-20\nDirectories"]
        P21["PROJ-21\nSystem Prompt"]
        P22["PROJ-22\nDocker Image"]
        P23["PROJ-23\nRequire Container"]
        P24["PROJ-24\nSaved"]
        P25["PROJ-25\nStale Warning"]
    end

    subgraph "/project delete"
        P28["PROJ-28\nConfirmation"]
        P29["PROJ-29\nDelete Success"]
        P30["PROJ-30\nCancelled"]
        P31["PROJ-31\nAmbient Error"]
        P32["PROJ-32\nNot Found"]
    end

    IDLE["LAYOUT-02\nInput"] -->|"/project create"| P02
    P02 -->|"name exists"| P03
    P03 --> IDLE
    P02 -->|"name valid"| P04
    P04 --> P05
    P05 -->|"yes"| P06
    P05 -->|"no"| P11
    P06 --> P07
    P06 --> P08
    P06 --> P09
    P07 --> P10
    P08 --> P10
    P09 --> P10
    P10 --> IDLE
    P11 --> IDLE
    P12 --> IDLE

    IDLE -->|"/project list"| P13
    IDLE -->|"/project list (empty)"| P14
    P13 --> IDLE
    P14 --> IDLE

    IDLE -->|"/project show"| P15
    P15 --> IDLE
    P16 --> IDLE
    P17 --> IDLE

    IDLE -->|"/project edit"| P19
    P19 -->|"Directories"| P20
    P19 -->|"System prompt"| P21
    P19 -->|"Docker image"| P22
    P19 -->|"Require container"| P23
    P20 --> P24
    P21 --> P24
    P22 --> P24
    P23 --> P24
    P24 --> P19
    P19 -->|"Done"| IDLE
    P24 -->|"active project changed"| P25

    IDLE -->|"/project delete"| P28
    IDLE -->|"/project delete _default"| P31
    P28 -->|"confirm"| P29
    P28 -->|"decline"| P30
    P29 --> IDLE
    P30 --> IDLE
    P31 --> IDLE
    P32 --> IDLE
```

### 2.7 /provider

Provider setup, listing, showing, and removal.

```mermaid
graph TD
    subgraph "/provider setup"
        PV03["PROV-03\nProvider Selection"]
        PV04["PROV-04\nAPI Key Prompt"]
        PV05["PROV-05\nModel Prompt"]
        PV06["PROV-06\nSetup Success"]
        PV07["PROV-07\nNon-Interactive Error"]
    end

    subgraph "/provider list"
        PV02["PROV-02\nProvider Table"]
    end

    subgraph "/provider show"
        PV09["PROV-09\nDetail Panel"]
        PV10["PROV-10\nNo Provider"]
    end

    subgraph "/provider remove"
        PV11["PROV-11\nProvider Selection"]
        PV12["PROV-12\nRemove Success"]
        PV13["PROV-13\nActive Warning"]
    end

    IDLE["LAYOUT-02\nInput"] -->|"/provider setup"| PV03
    PV03 --> PV04
    PV04 --> PV05
    PV05 --> PV06
    PV06 --> IDLE
    PV07 --> IDLE

    IDLE -->|"/provider list"| PV02
    PV02 --> IDLE

    IDLE -->|"/provider show"| PV09
    IDLE -->|"/provider show (none)"| PV10
    PV09 --> IDLE
    PV10 --> IDLE

    IDLE -->|"/provider remove"| PV11
    PV11 -->|"not active"| PV12
    PV11 -->|"is active"| PV13
    PV12 --> IDLE
    PV13 --> IDLE
```

### 2.8 /jea

JEA profile management: CRUD, effective view, and project assignment.

```mermaid
graph TD
    subgraph "/jea create"
        J08["JEA-08\nName Prompt"]
        J09["JEA-09\nName Validation Error"]
        J10["JEA-10\nAlready Exists"]
        J11["JEA-11\nLanguage Mode Selection"]
        J12["JEA-12\nAdd Command/Module Loop"]
        J13["JEA-13\nCreate Success"]
    end

    subgraph "/jea list & show"
        J02["JEA-02\nList Table"]
        J03["JEA-03\nList Empty"]
        J04["JEA-04\nShow Detail Panel"]
        J05["JEA-05\nShow Not Found"]
        J06["JEA-06\nProfile Selection"]
    end

    subgraph "/jea edit"
        J14["JEA-14\nEdit Menu Loop"]
        J15["JEA-15\nChange Language Mode"]
        J16["JEA-16\nAdd Command"]
        J17["JEA-17\nRemove Command"]
        J18["JEA-18\nToggle Deny"]
        J19["JEA-19\nAdd Module"]
        J20["JEA-20\nRemove Module"]
        J21["JEA-21\nEdit Saved"]
    end

    subgraph "/jea effective"
        J30["JEA-30\nEffective View"]
    end

    subgraph "/jea delete"
        J23["JEA-23\nDelete Selection"]
        J25["JEA-25\nDelete Confirmation"]
        J26["JEA-26\nDelete Success"]
        J27["JEA-27\nDelete Cancelled"]
        J28["JEA-28\nGlobal Error"]
    end

    subgraph "/jea assign & unassign"
        J31["JEA-31\nAssign Selection"]
        J32["JEA-32\nAssign Success"]
        J38["JEA-38\nUnassign Selection"]
        J39["JEA-39\nUnassign Success"]
    end

    IDLE["LAYOUT-02\nInput"] -->|"/jea create"| J08
    J08 -->|"invalid"| J09
    J08 -->|"exists"| J10
    J08 -->|"valid"| J11
    J09 --> IDLE
    J10 --> IDLE
    J11 --> J12
    J12 -->|"Done"| J13
    J13 --> IDLE

    IDLE -->|"/jea list"| J02
    IDLE -->|"/jea list (empty)"| J03
    J02 --> IDLE
    J03 --> IDLE

    IDLE -->|"/jea show"| J06
    J06 --> J04
    J04 --> IDLE
    J05 --> IDLE

    IDLE -->|"/jea edit"| J14
    J14 -->|"Language mode"| J15
    J14 -->|"Add command"| J16
    J14 -->|"Remove command"| J17
    J14 -->|"Toggle deny"| J18
    J14 -->|"Add module"| J19
    J14 -->|"Remove module"| J20
    J15 --> J21
    J16 --> J21
    J17 --> J21
    J18 --> J21
    J19 --> J21
    J20 --> J21
    J21 --> J14
    J14 -->|"Done"| IDLE

    IDLE -->|"/jea effective"| J30
    J30 --> IDLE

    IDLE -->|"/jea delete"| J23
    J23 --> J25
    J25 -->|"confirm"| J26
    J25 -->|"decline"| J27
    J26 --> IDLE
    J27 --> IDLE
    J28 --> IDLE

    IDLE -->|"/jea assign"| J31
    J31 --> J32
    J32 --> IDLE

    IDLE -->|"/jea unassign"| J38
    J38 --> J39
    J39 --> IDLE
```

### 2.9 /conversations

Conversation management: list, show, rename, delete, and clear.

```mermaid
graph TD
    subgraph "/conversations list"
        S02["SESS-02\nSession Table"]
        S03["SESS-03\nEmpty State"]
    end

    subgraph "/conversations show"
        S04["SESS-04\nDetail View"]
        S05["SESS-05\nNot Found"]
        S06["SESS-06\nUsage"]
    end

    subgraph "/conversations rename"
        S13["SESS-13\nName Prompt"]
        S14["SESS-14\nRename Success"]
        S15["SESS-15\nNot Found"]
    end

    subgraph "/conversations delete"
        S07["SESS-07\nDelete Confirmation"]
        S08["SESS-08\nDelete Success"]
        S09["SESS-09\nDelete Cancelled"]
        S10["SESS-10\nActive Session Error"]
        S11["SESS-11\nNot Found"]
    end

    subgraph "/conversations clear"
        CL01["CLEAR-01\nClear Success"]
        CL02["CLEAR-02\nNo Session Error"]
    end

    IDLE["LAYOUT-02\nInput"] -->|"/conversations list"| S02
    IDLE -->|"/conversations list (empty)"| S03
    S02 --> IDLE
    S03 --> IDLE

    IDLE -->|"/conversations show ID"| S04
    IDLE -->|"/conversations show (bad ID)"| S05
    IDLE -->|"/conversations show (no ID)"| S06
    S04 --> IDLE
    S05 --> IDLE
    S06 --> IDLE

    IDLE -->|"/conversations rename ID"| S13
    S13 --> S14
    S14 --> IDLE
    S15 --> IDLE

    IDLE -->|"/conversations delete ID"| S07
    S07 -->|"confirm"| S08
    S07 -->|"decline"| S09
    S08 --> IDLE
    S09 --> IDLE
    IDLE -->|"/conversations delete (active)"| S10
    S10 --> IDLE
    S11 --> IDLE

    IDLE -->|"/conversations clear"| CL01
    IDLE -->|"/conversations clear (no session)"| CL02
    CL01 --> IDLE
    CL02 --> IDLE
```

### 2.10 /context

Context management: show, summarize, refresh, and prune.

```mermaid
graph TD
    subgraph "/context show"
        CT02["CTX-02\nDashboard"]
        CT03["CTX-03\nNo Session Error"]
    end

    subgraph "/context summarize"
        CT04["CTX-04\nSummarize Success"]
        CT05["CTX-05\nToo Few Messages"]
        CT06["CTX-06\nNo Session Error"]
        CT07["CTX-07\nNo Provider Error"]
        CT08["CTX-08\nSummarize Failure"]
    end

    subgraph "/context refresh"
        RF01["REFRESH-01\nRefresh Summary"]
        RF02["REFRESH-02\nNo Session Error"]
        RF03["REFRESH-03\nProject Not Found"]
        RF04["REFRESH-04\nMissing Dir Warning"]
        RF05["REFRESH-05\nEngine Refresh Failure"]
    end

    IDLE["LAYOUT-02\nInput"] -->|"/context show"| CT02
    IDLE -->|"/context show (no session)"| CT03
    CT02 --> IDLE
    CT03 --> IDLE

    IDLE -->|"/context summarize"| CT_CHECK{"Preconditions\nmet?"}
    CT_CHECK -->|"< 4 messages"| CT05
    CT_CHECK -->|"no session"| CT06
    CT_CHECK -->|"no provider"| CT07
    CT_CHECK -->|"LLM fails"| CT08
    CT_CHECK -->|"success"| CT04
    CT04 --> IDLE
    CT05 --> IDLE
    CT06 --> IDLE
    CT07 --> IDLE
    CT08 --> IDLE

    IDLE -->|"/context refresh"| RF_CHECK{"Session\nexists?"}
    RF_CHECK -->|"no"| RF02
    RF_CHECK -->|"project deleted"| RF03
    RF_CHECK -->|"yes"| RF01
    RF01 -->|"dirs missing"| RF04
    RF01 -->|"engine fails"| RF05
    RF01 --> IDLE
    RF02 --> IDLE
    RF03 --> IDLE
    RF04 --> IDLE
    RF05 --> IDLE
```

### 2.11 /expand

Expands collapsed tool output from the most recent execution.

```mermaid
graph LR
    IDLE["LAYOUT-02\nInput"] -->|"/expand (output buffered)"| EX01["EXPAND-01\nExpanded Output"]
    IDLE -->|"/expand (no output)"| EX02["EXPAND-02\nNo Output"]
    IDLE -->|"/expand (already expanded)"| EX03["EXPAND-03\nAlready Expanded"]
    EX01 --> IDLE
    EX02 --> IDLE
    EX03 --> IDLE
```

### 2.12 /agent

Agent listing and detail view. Note: the `/agent` slash command exists in code
but does not yet have screen IDs in the screen inventory. IDs below are
provisional.

```mermaid
graph LR
    IDLE["LAYOUT-02\nInput"] -->|"/agent list"| AL["Agent List\n(table of agents)"]
    IDLE -->|"/agent show NAME"| AS["Agent Detail\n(panel with description, model, scope)"]
    AL --> IDLE
    AS --> IDLE
```

### 2.13 Auth (Login)

The `boydcode login` command is a separate CLI command (not a slash command).
It runs outside the TUI session.

```mermaid
graph TD
    subgraph "boydcode login"
        A01["AUTH-01\nNon-Interactive Error"]
        A02["AUTH-02\nNo OAuth Support"]
        A03["AUTH-03\nLogin Start"]
        A04["AUTH-04\nClient Credentials Prompt"]
        A05["AUTH-05\nClient ID Missing"]
        A06["AUTH-06\nBrowser Opening"]
        A07["AUTH-07\nWaiting for Auth"]
        A08["AUTH-08\nToken Exchange"]
        A09["AUTH-09\nLogin Success"]
        A10["AUTH-10\nLogin Timeout"]
        A11["AUTH-11\nAuth Error"]
        A12["AUTH-12\nToken Exchange Failure"]
        A13["AUTH-13\nToken Exchange Null"]
    end

    START["boydcode login"] -->|"non-interactive"| A01
    START -->|"no OAuth for provider"| A02
    START -->|"interactive"| A03
    A01 --> EXIT["EXIT"]
    A02 --> EXIT

    A03 -->|"needs client creds"| A04
    A03 -->|"has client creds"| A06
    A04 -->|"empty client ID"| A05
    A04 --> A06
    A05 --> EXIT

    A06 --> A07
    A07 --> A08
    A08 --> A09
    A09 --> EXIT

    A07 -->|"5 min timeout"| A10
    A07 -->|"callback error"| A11
    A08 -->|"HTTP error"| A12
    A08 -->|"null response"| A13
    A10 --> EXIT
    A11 --> EXIT
    A12 --> EXIT
    A13 --> EXIT
```

### 2.14 System (Crash / Exit)

Fatal error handling and application exit.

```mermaid
graph TD
    SYS01["SYS-01\nCrash Panel"]
    SYS02["SYS-02\nCrash Fallback (stderr)"]

    CRASH["Unhandled exception\nin Program.cs"] -->|"render succeeds"| SYS01
    CRASH -->|"render fails"| SYS02

    SYS01 --> EXIT["EXIT (code 1)"]
    SYS02 --> EXIT

    QUIT["/quit or /exit"] -->|"session auto-saved"| EXIT_CLEAN["EXIT (code 0)"]
    CTRLC["Ctrl+C during startup"] --> EXIT_CANCEL["EXIT (code 2)"]
```

---

## 3. Transition Reference Table

Every screen-to-screen transition in the application. Sorted by From screen.

### Launch

| From | To | Trigger |
|---|---|---|
| App invocation | STARTUP-01 | Terminal height >= 30 |
| App invocation | STARTUP-02 | Terminal height < 30 |
| App invocation | STARTUP-09 | `--provider` flag with unrecognized name |
| App invocation | STARTUP-10 | Project directory does not exist on disk |
| App invocation | STARTUP-11 | `ActiveProvider.Activate` throws exception |
| STARTUP-01 | STARTUP-03 | Banner rendering completes |
| STARTUP-02 | STARTUP-03 | Banner rendering completes |
| STARTUP-03 | STARTUP-04 | Provider is configured (API key found or Ollama) |
| STARTUP-03 | STARTUP-05 | No API key for non-Ollama provider |
| STARTUP-04 | STARTUP-06 | New session (no `--resume` flag) |
| STARTUP-04 | STARTUP-07 | `--resume` flag with valid session ID |
| STARTUP-04 | STARTUP-08 | `--resume` flag with invalid session ID |
| STARTUP-05 | LAYOUT-01 | Layout activation (interactive, height >= 10) |
| STARTUP-06 | LAYOUT-01 | Layout activation (interactive, height >= 10) |
| STARTUP-07 | LAYOUT-01 | Layout activation (interactive, height >= 10) |
| STARTUP-08 | EXIT | Application exits with code 1 |
| STARTUP-09 | STARTUP-01/02 | Warning rendered, continue startup |
| STARTUP-10 | STARTUP-01/02 | Warning rendered, continue startup |
| STARTUP-11 | STARTUP-01/02 | Error rendered, continue startup with isConfigured=false |
| LAYOUT-01 | LAYOUT-02 | Layout established, input prompt shown |
| App invocation | LAYOUT-07 | Non-interactive terminal or height < 10 |

### Chat Loop

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | LAYOUT-03 | User types any character |
| LAYOUT-03 | LAYOUT-02 | Enter pressed (empty buffer ignored) |
| LAYOUT-03 | Active Turn | Enter pressed with chat message |
| LAYOUT-03 | Slash Command | Enter pressed with `/` prefix |
| LAYOUT-03 | EXIT | `/quit`, `/exit`, `quit`, or `exit` typed |
| LAYOUT-03 | CHAT-15 | Unknown slash command, close Levenshtein match found |
| LAYOUT-03 | CHAT-16 | Unknown slash command, no close match |
| LAYOUT-02 | LAYOUT-04 | Agent is busy and user submits queued message |
| LAYOUT-04 | LAYOUT-02 | Agent turn completes, queue processed |
| CHAT-15 | LAYOUT-02 | Error rendered |
| CHAT-16 | LAYOUT-02 | Error rendered |
| CHAT-17 | LAYOUT-02 | Input error rendered |
| CHAT-18 | LAYOUT-02 | Slash command exception rendered |

### Active Turn

| From | To | Trigger |
|---|---|---|
| User message | CHAT-08 | Provider not configured (`_activeProvider.IsConfigured == false`) |
| CHAT-08 | LAYOUT-02 | Error rendered, user message removed from conversation |
| User message | CHAT-06 | Token estimate exceeds compaction threshold |
| CHAT-06 | CHAT-01 | Compaction complete, LLM request sent |
| User message | CHAT-01 | LLM request sent (no compaction needed) |
| CHAT-01 | CHAT-02 | First streaming token arrives |
| CHAT-01 | CHAT-04 | Non-streaming response received |
| CHAT-02 | CHAT-03 | Stream completes (CompletionChunk received) |
| CHAT-03 | CHAT-05 | Token usage counters updated |
| CHAT-04 | CHAT-05 | Token usage counters updated |
| CHAT-05 | LAYOUT-02 | `stop_reason == "end_turn"` (no tool calls) |
| CHAT-05 | EXEC-01 | `stop_reason == "tool_use"` (tool calls in response) |
| CHAT-05 | CHAT-01 | Next round after tool results added (round < 50) |
| CHAT-05 | CHAT-07 | Round count reaches 50 |
| CHAT-07 | LAYOUT-02 | Error rendered, session auto-saved |
| CHAT-01 | CHAT-09 | Provider returns 401/403 (auth error) |
| CHAT-01 | CHAT-10 | Provider returns 429 (rate limit) |
| CHAT-01 | CHAT-11 | Token limit exceeded |
| CHAT-01 | CHAT-12 | Connection or timeout failure |
| CHAT-01 | CHAT-13 | Provider returns 500/503 |
| CHAT-01 | CHAT-14 | Unclassified provider error |
| CHAT-09 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-10 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-11 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-12 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-13 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-14 | LAYOUT-02 | Error rendered, user message removed |
| CHAT-19 | EXIT | Fatal error, application exits with code 1 |

### Tool Execution

| From | To | Trigger |
|---|---|---|
| CHAT-05 | EXEC-01 | Response contains `ToolUseBlock` |
| EXEC-01 | EXEC-02 | Preview rendered, execution starts |
| EXEC-02 | EXEC-03 | First output line arrives (layout mode) |
| EXEC-02 | EXEC-04 | First output line arrives (non-layout mode) |
| EXEC-02 | EXEC-08 | Execution completes with 0 output lines (success) |
| EXEC-02 | EXEC-11 | Execution completes with 0 output lines (error) |
| EXEC-02 | EXEC-15 | `ExecuteAsync` throws exception |
| EXEC-02 | CANCEL-01 | User presses Esc or Ctrl+C (first press) |
| EXEC-04 | EXEC-05 | Output line count exceeds 5 (non-layout) |
| EXEC-04 | EXEC-07 | Execution completes, 1-5 lines (success) |
| EXEC-04 | EXEC-10 | Execution completes, 1-5 lines (error) |
| EXEC-03 | EXEC-06 | Execution completes, > 5 lines (success, layout) |
| EXEC-03 | EXEC-09 | Execution completes, > 5 lines (error, layout) |
| EXEC-05 | EXEC-06 | Execution completes, > 5 lines (success, non-layout) |
| EXEC-05 | EXEC-09 | Execution completes, > 5 lines (error, non-layout) |
| EXEC-06 | EXEC-01 | Next tool call in batch |
| EXEC-06 | CHAT-01 | Last tool call, next LLM round |
| EXEC-07 | EXEC-01 | Next tool call in batch |
| EXEC-07 | CHAT-01 | Last tool call, next LLM round |
| EXEC-08 | EXEC-01 | Next tool call in batch |
| EXEC-08 | CHAT-01 | Last tool call, next LLM round |
| EXEC-09 | EXEC-01 | Next tool call in batch |
| EXEC-09 | CHAT-01 | Last tool call, next LLM round |
| EXEC-10 | EXEC-01 | Next tool call in batch |
| EXEC-10 | CHAT-01 | Last tool call, next LLM round |
| EXEC-11 | EXEC-01 | Next tool call in batch |
| EXEC-11 | CHAT-01 | Last tool call, next LLM round |
| EXEC-14 | EXEC-01 | Cancelled, next tool call in batch |
| EXEC-14 | CHAT-01 | Cancelled, last tool call, next LLM round |
| EXEC-15 | EXEC-01 | Exception, next tool call in batch |
| EXEC-15 | CHAT-01 | Exception, last tool call, next LLM round |
| EXEC-16 | EXEC-01 | Unknown tool, next tool call in batch |
| EXEC-16 | CHAT-01 | Unknown tool, last tool call, next LLM round |
| EXEC-17 | EXEC-01 | Engine not init, next tool call in batch |
| EXEC-17 | CHAT-01 | Engine not init, last tool call, next LLM round |

### Cancellation

| From | To | Trigger |
|---|---|---|
| EXEC-02 | CANCEL-01 | First press of Esc or Ctrl+C during execution |
| EXEC-03 | CANCEL-01 | First press of Esc or Ctrl+C during output streaming |
| EXEC-04/05 | CANCEL-01 | First press of Esc or Ctrl+C during output streaming |
| CANCEL-01 | EXEC-14 | Second Esc/Ctrl+C press within 1 second |
| CANCEL-01 | CANCEL-02 | Timer expires (1 second, no second press) |
| CANCEL-02 | EXEC-02 | Hint cleared, spinner resumes (if in Waiting state) |

### /help

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | HELP-01 | User types `/help` |
| HELP-01 | LAYOUT-02 | Table rendered |

### /project

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | PROJ-01 | `/project` with invalid subcommand |
| LAYOUT-02 | PROJ-02 | `/project create` (no name, interactive) |
| LAYOUT-02 | PROJ-12 | `/project create` (no name, non-interactive) |
| PROJ-02 | PROJ-03 | Name matches existing project |
| PROJ-02 | PROJ-04 | Name is valid and unique |
| PROJ-04 | PROJ-05 | Create success, prompt to configure |
| PROJ-05 | PROJ-06 | User confirms "yes" |
| PROJ-05 | PROJ-11 | User confirms "no" |
| PROJ-06 | PROJ-07 | "Directories" selected |
| PROJ-06 | PROJ-08 | "System prompt" selected |
| PROJ-06 | PROJ-09 | "Container settings" selected |
| PROJ-07 | PROJ-10 | Directory loop completed |
| PROJ-08 | PROJ-10 | System prompt entered |
| PROJ-09 | PROJ-10 | Container settings entered |
| PROJ-10 | LAYOUT-02 | Project saved |
| PROJ-11 | LAYOUT-02 | Tip rendered |
| PROJ-12 | LAYOUT-02 | Usage hint rendered |
| LAYOUT-02 | PROJ-13 | `/project list` (projects exist) |
| LAYOUT-02 | PROJ-14 | `/project list` (no projects) |
| PROJ-13 | LAYOUT-02 | Table rendered |
| PROJ-14 | LAYOUT-02 | Empty state rendered |
| LAYOUT-02 | PROJ-15 | `/project show NAME` |
| LAYOUT-02 | PROJ-17 | `/project show NAME` (not found) |
| PROJ-15 | LAYOUT-02 | Detail view rendered |
| LAYOUT-02 | PROJ-19 | `/project edit NAME` |
| PROJ-19 | PROJ-20 | "Directories" selected from edit menu |
| PROJ-19 | PROJ-21 | "System prompt" selected from edit menu |
| PROJ-19 | PROJ-22 | "Docker image" selected from edit menu |
| PROJ-19 | PROJ-23 | "Require container" selected from edit menu |
| PROJ-20 | PROJ-24 | Directory edit action completed |
| PROJ-21 | PROJ-24 | System prompt edit completed |
| PROJ-22 | PROJ-24 | Docker image edit completed |
| PROJ-23 | PROJ-24 | Require container toggle completed |
| PROJ-24 | PROJ-19 | Saved, return to edit menu |
| PROJ-24 | PROJ-25 | Active project settings changed (stale warning set) |
| PROJ-19 | LAYOUT-02 | "Done" selected from edit menu |
| LAYOUT-02 | PROJ-28 | `/project delete NAME` |
| LAYOUT-02 | PROJ-31 | `/project delete _default` |
| PROJ-28 | PROJ-29 | User confirms deletion |
| PROJ-28 | PROJ-30 | User declines deletion |
| PROJ-29 | LAYOUT-02 | Success rendered |
| PROJ-30 | LAYOUT-02 | Cancelled rendered |
| PROJ-31 | LAYOUT-02 | Error rendered |

### /provider

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | PROV-01 | `/provider` with invalid subcommand |
| LAYOUT-02 | PROV-02 | `/provider list` |
| LAYOUT-02 | PROV-03 | `/provider setup` (interactive) |
| LAYOUT-02 | PROV-07 | `/provider setup` (non-interactive) |
| PROV-03 | PROV-04 | Provider selected from list |
| PROV-04 | PROV-05 | API key entered (or skipped for Ollama) |
| PROV-05 | PROV-06 | Model entered or default accepted |
| PROV-06 | LAYOUT-02 | Provider activated, status line updated |
| LAYOUT-02 | PROV-09 | `/provider show` (provider active) |
| LAYOUT-02 | PROV-10 | `/provider show` (no provider active) |
| LAYOUT-02 | PROV-11 | `/provider remove` |
| PROV-11 | PROV-12 | Provider removed (was not active) |
| PROV-11 | PROV-13 | Provider removed (was active, warning shown) |
| PROV-02 | LAYOUT-02 | Table rendered |
| PROV-09 | LAYOUT-02 | Panel rendered |
| PROV-10 | LAYOUT-02 | Message rendered |
| PROV-12 | LAYOUT-02 | Success rendered |
| PROV-13 | LAYOUT-02 | Warning + success rendered |

### /jea

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | JEA-01 | `/jea` with invalid subcommand |
| LAYOUT-02 | JEA-02 | `/jea list` (profiles exist) |
| LAYOUT-02 | JEA-03 | `/jea list` (no profiles) |
| LAYOUT-02 | JEA-06 | `/jea show` (no name, interactive) |
| JEA-06 | JEA-04 | Profile selected from list |
| LAYOUT-02 | JEA-04 | `/jea show NAME` |
| LAYOUT-02 | JEA-05 | `/jea show NAME` (not found) |
| LAYOUT-02 | JEA-08 | `/jea create` |
| JEA-08 | JEA-09 | Name validation fails |
| JEA-08 | JEA-10 | Name already exists |
| JEA-08 | JEA-11 | Name is valid |
| JEA-11 | JEA-12 | Language mode selected |
| JEA-12 | JEA-13 | "Done" selected from add loop |
| JEA-13 | LAYOUT-02 | Profile created |
| LAYOUT-02 | JEA-14 | `/jea edit NAME` |
| JEA-14 | JEA-15 | "Change language mode" selected |
| JEA-14 | JEA-16 | "Add command" selected |
| JEA-14 | JEA-17 | "Remove command" selected |
| JEA-14 | JEA-18 | "Toggle command deny" selected |
| JEA-14 | JEA-19 | "Add module" selected |
| JEA-14 | JEA-20 | "Remove module" selected |
| JEA-15 | JEA-21 | Language mode changed |
| JEA-16 | JEA-21 | Command added |
| JEA-17 | JEA-21 | Command removed |
| JEA-18 | JEA-21 | Command deny toggled |
| JEA-19 | JEA-21 | Module added |
| JEA-20 | JEA-21 | Module removed |
| JEA-21 | JEA-14 | Saved, return to edit menu |
| JEA-14 | LAYOUT-02 | "Done" selected |
| LAYOUT-02 | JEA-30 | `/jea effective` |
| JEA-30 | LAYOUT-02 | Effective view rendered |
| LAYOUT-02 | JEA-23 | `/jea delete` (no name, interactive) |
| JEA-23 | JEA-25 | Profile selected for deletion |
| JEA-25 | JEA-26 | User confirms deletion |
| JEA-25 | JEA-27 | User declines deletion |
| JEA-26 | LAYOUT-02 | Success rendered |
| JEA-27 | LAYOUT-02 | Cancelled rendered |
| LAYOUT-02 | JEA-28 | `/jea delete _global` |
| JEA-28 | LAYOUT-02 | Error rendered |
| LAYOUT-02 | JEA-31 | `/jea assign` |
| JEA-31 | JEA-32 | Profile assigned to project |
| JEA-32 | LAYOUT-02 | Success rendered |
| LAYOUT-02 | JEA-38 | `/jea unassign` |
| JEA-38 | JEA-39 | Profile unassigned from project |
| JEA-39 | LAYOUT-02 | Success rendered |

### /context

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | CTX-01 | `/context` with invalid subcommand |
| LAYOUT-02 | CTX-02 | `/context show` (session active) |
| LAYOUT-02 | CTX-03 | `/context show` (no session) |
| CTX-02 | LAYOUT-02 | Dashboard rendered |
| LAYOUT-02 | CTX-04 | `/context summarize` (success) |
| LAYOUT-02 | CTX-05 | `/context summarize` (< 4 messages) |
| LAYOUT-02 | CTX-06 | `/context summarize` (no session) |
| LAYOUT-02 | CTX-07 | `/context summarize` (no provider) |
| LAYOUT-02 | CTX-08 | `/context summarize` (LLM failure) |
| CTX-04 | LAYOUT-02 | Success rendered |
| CTX-05 | LAYOUT-02 | Message rendered |
| LAYOUT-02 | REFRESH-01 | `/context refresh` (success) |
| LAYOUT-02 | REFRESH-02 | `/context refresh` (no session) |
| LAYOUT-02 | REFRESH-03 | `/context refresh` (project deleted) |
| REFRESH-01 | LAYOUT-02 | Summary rendered |
| REFRESH-01 | REFRESH-04 | Missing directories during refresh |
| REFRESH-01 | REFRESH-05 | Engine factory throws during refresh |

### /conversations

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | SESS-01 | `/conversations` with invalid subcommand |
| LAYOUT-02 | SESS-02 | `/conversations list` (sessions exist) |
| LAYOUT-02 | SESS-03 | `/conversations list` (no sessions) |
| SESS-02 | LAYOUT-02 | Table rendered |
| LAYOUT-02 | SESS-04 | `/conversations show ID` (found) |
| LAYOUT-02 | SESS-05 | `/conversations show ID` (not found) |
| LAYOUT-02 | SESS-06 | `/conversations show` (no ID) |
| SESS-04 | LAYOUT-02 | Detail view rendered |
| LAYOUT-02 | SESS-07 | `/conversations delete ID` (interactive) |
| SESS-07 | SESS-08 | User confirms deletion |
| SESS-07 | SESS-09 | User declines deletion |
| SESS-08 | LAYOUT-02 | Success rendered |
| SESS-09 | LAYOUT-02 | Cancelled rendered |
| LAYOUT-02 | SESS-10 | `/conversations delete` (active session) |
| LAYOUT-02 | SESS-13 | `/conversations rename ID` |
| SESS-13 | SESS-14 | Name entered |
| SESS-14 | LAYOUT-02 | Success rendered |
| LAYOUT-02 | CLEAR-01 | `/conversations clear` (session active) |
| LAYOUT-02 | CLEAR-02 | `/conversations clear` (no session) |
| CLEAR-01 | LAYOUT-02 | Success rendered |

### /expand

| From | To | Trigger |
|---|---|---|
| LAYOUT-02 | EXPAND-01 | `/expand` with buffered, unexpanded output |
| LAYOUT-02 | EXPAND-02 | `/expand` with no buffered output |
| LAYOUT-02 | EXPAND-03 | `/expand` after already expanding |
| EXPAND-01 | LAYOUT-02 | Output rendered |
| EXPAND-02 | LAYOUT-02 | Message rendered |
| EXPAND-03 | LAYOUT-02 | Message rendered |

### Auth

| From | To | Trigger |
|---|---|---|
| `boydcode login` | AUTH-01 | Non-interactive terminal |
| `boydcode login` | AUTH-02 | Provider does not support OAuth |
| `boydcode login` | AUTH-03 | Interactive terminal, provider supports OAuth |
| AUTH-03 | AUTH-04 | Provider requires user-supplied OAuth credentials |
| AUTH-03 | AUTH-06 | Client credentials already available |
| AUTH-04 | AUTH-05 | Empty client ID after resolution |
| AUTH-04 | AUTH-06 | Client credentials provided |
| AUTH-05 | EXIT | Error rendered |
| AUTH-06 | AUTH-07 | Browser opened (or URL shown) |
| AUTH-07 | AUTH-08 | Authorization code received from callback |
| AUTH-07 | AUTH-10 | 5-minute timeout expires |
| AUTH-07 | AUTH-11 | OAuth callback returns error |
| AUTH-08 | AUTH-09 | Token exchange succeeds |
| AUTH-08 | AUTH-12 | Token exchange HTTP error |
| AUTH-08 | AUTH-13 | Token exchange returns null |
| AUTH-09 | EXIT | Login complete |
| AUTH-10 | EXIT | Timeout, exit |
| AUTH-11 | EXIT | Error, exit |
| AUTH-12 | EXIT | Error, exit |
| AUTH-13 | EXIT | Error, exit |

### System

| From | To | Trigger |
|---|---|---|
| Any screen | SYS-01 | Unhandled exception in `Program.cs` |
| SYS-01 | EXIT | Crash panel rendered, exit code 1 |
| SYS-01 | SYS-02 | Crash panel rendering itself fails |
| SYS-02 | EXIT | Fallback error written to stderr, exit code 1 |
| LAYOUT-02 | EXIT | `/quit` or `/exit` typed, session auto-saved |
| Any screen | EXIT | Ctrl+C during startup, exit code 2 |

---

## Gaps and Notes

- **`/agent` screens**: The `/agent list` and `/agent show` slash commands
  exist in code (`AgentSlashCommand`) but do not have screen IDs in the
  [screen inventory](03-screen-inventory.md). IDs should be assigned (e.g.,
  AGENT-01 through AGENT-04) and specs created.
- **`/context prune`**: Referenced in the `/context` subcommand list but does
  not have screen IDs in the inventory. The prune flow uses `SmartPruneCompactor`
  with interactive confirmation.
- **`/context summarize` interactive menu**: The four-option menu (Apply, Fork,
  Revise, Cancel) and fork flow are implemented but the screen inventory still
  lists the simpler CTX-04/05/06/07/08 IDs. Additional screen IDs may be needed
  for the preview panel, action menu, fork confirmation, and revision loop.
- **Non-ANSI tool results**: EXEC-12 and EXEC-13 are listed in the inventory
  but omitted from the tool execution detail diagram for clarity (they follow
  the same flow as EXEC-06/09 but with truncated text instead of collapse).
