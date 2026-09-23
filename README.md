# Ooor — Model Dock for Local LLMs

> A featherweight Windows desktop app to install, manage, and chat with local LLMs powered by **llama.cpp**. Ship the whole stack — runtime, model library, downloader, chat console, and a tool-calling agent layer — in an installer under **1.5 MB**.

🌐 **Homepage:** [ooor.cc](https://ooor.cc)  ·  📦 **License:** MIT  ·  🛠 **Author:** oshine

---

## Table of Contents

- [Screenshots](#screenshots)
- [What is Ooor](#what-is-ooor)
- [Why Ooor](#why-ooor)
- [Key Features](#key-features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Feature Walkthrough](#feature-walkthrough)
- [Built-in Tools (Agent Layer)](#built-in-tools-agent-layer)
- [System Requirements](#system-requirements)
- [Build from Source](#build-from-source)
- [Project Layout](#project-layout)
- [Configuration & Paths](#configuration--paths)
- [FAQ](#faq)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgements](#acknowledgements)

---

## Screenshots

| Main window — configure engine & model, watch the live console | Model Manager — your local GGUF library |
|---|---|
| ![Main window](doc/img/main-form.png) | ![Model Manager](doc/img/model-manager.png) |

| Model Market — search & download from Hugging Face mirrors | Download Manager — chunked, resumable, proxy-aware |
|---|---|
| ![Model Market](doc/img/model-market.png) | ![Download Manager](doc/img/download-manager.png) |

---

## What is Ooor

**Ooor** (read as "O-or", Chinese: 欧尔模型坞) is a native Windows desktop application that turns the **llama.cpp** command-line ecosystem into a single, opinionated, click-to-run workspace. It is built for people who want to run large language models on their own hardware — offline, private, and free of any cloud billing — without spending an afternoon reading `llama-server --help`.

Concretely, Ooor bundles four roles into one tiny executable:

1. **Llama runtime manager** — discover, install, and switch between `llama-server` builds (CPU, CUDA, Vulkan, SYCL, …) with one click.
2. **Model library** — catalog every `.gguf` file on your machine, annotate it, soft-delete stale ones, and locate multimodal projector files.
3. **Downloader** — a chunked, resumable download manager with Hugging Face mirror support and a built-in model market browser.
4. **Chat console & agent host** — talk to the running model in a built-in console, and let it call out to a curated set of local tools (web fetch, file read/write, shell, …) through an MCP-compatible tool layer.

All of this ships in an installer **under 1.5 MB**. No Electron, no Node runtime bundled, no 200 MB framework download — just a hand-rolled WinForms binary talking directly to `llama-server.exe` over a local HTTP API.

---

## Why Ooor

The local-LLM tooling space is crowded. Here is the case for Ooor:

### Tiny by design
Most "local AI" GUIs are Electron apps that drag a whole Chromium instance with them. Ooor is a single native binary — the installer is **~1.5 MB**, cold start is instant, and idle RAM usage is measured in single-digit megabytes. If you have ever closed a "lightweight" LLM GUI because it was eating 800 MB just to sit there, Ooor is the rebuttal.

### Privacy by default
Models run on `127.0.0.1`. Nothing leaves your machine unless you explicitly ask the agent to fetch a URL. There is no telemetry, no "anonymous usage statistics", no account. Your prompts, your files, your business.

### Opinionated, not a blank canvas
Tools like `llama.cpp`'s CLI, `text-generation-webui`, or `LM Studio` all expose every knob. That freedom is great — and exhausting. Ooor picks sensible defaults (Q4_K_M, localhost binding, sensible context length) and hides the knobs you almost never touch, while still exposing the ones you do (GPU layers, port, multimodal projector, extra server flags) behind a **More** button.

### One app, not five
The typical local-LLM workflow hops between four tools: a model browser (Hugging Face website), a downloader (`wget` / `aria2` / a browser that chokes on 5 GB files), a model organizer (Explorer + hope), and a runner (`llama-server` + curl). Ooor collapses all four into one window. The download manager even understands `llama-bXXXX-bin-win-*.zip` archives and extracts them into the runtime library automatically.

### Agent-friendly
Beyond chat, Ooor gives the running model a small toolbox — fetch a URL, read a file, write a file, list a directory, run a shell command (with confirmation). This turns the local model into something closer to a **local coding & research assistant** than a single-turn Q&A box.

---

## Key Features

- 🚀 **One-click model startup** — pick engine + model, hit **Start Service**, get an OpenAI-compatible HTTP endpoint on `127.0.0.1:6080`.
- 📚 **Local model library** — scans built-in and external folders, shows size / type / projector / notes, supports soft-delete so you can re-import without re-downloading.
- 🛒 **Built-in model market** — browse Hugging Face (via `hf-mirror` or direct), see download counts / likes / task tags, right-click to open the model page in your browser.
- ⬇️ **Resumable chunked downloader** — multi-part downloads, automatic retry, GitHub proxy mirror list (great for users behind the GFW), automatic extraction of `llama-*.zip` runtimes.
- 💬 **Console chat** — streaming chat against the running model, with token-sparkline and chunk timing visualization.
- 🤖 **Agent host** — bind tools to an agent, let the model decide when to call them, with human-in-the-loop confirmation for destructive operations.
- 🔌 **MCP-friendly** — scan and bind Model Context Protocol servers alongside the built-in tool catalog.
- 🌐 **Multilingual UI** — English and Simplified Chinese, switchable at runtime.
- 🧩 **Profile system** — save engine + model + params + agent bindings as a named profile, switch contexts in one click.
- 🪶 **~1.5 MB installer** — signed, with a proper uninstaller and optional user-data cleanup.

---

## Installation

1. Download the latest `Ooor-Setup-x64-v*.exe` from the [releases page on ooor.cc](https://ooor.cc).
2. Run it. Windows may show an "unknown publisher" prompt the first time — this is expected for a self-signed build; click **Run anyway** (a commercial code-signing certificate is on the roadmap).
3. The installer picks a sensible default directory (`D:\Ooor` if you have a D: drive, otherwise `%LOCALAPPDATA%\Ooor`) and creates the data folders for you:
   - `bin\`        — program files
   - `config\`     — user data (llama-bin, models, configs, chat history)
4. Launch **Ooor** from the Start Menu or desktop shortcut.

No reboot, no runtime install, no admin rights required for normal use (the installer itself requests admin only to write to the chosen install location).

---

## Quick Start

1. **Get a llama engine.** Open **Llama → Downloads** (or the toolbar **Downloads** button), grab a `llama-bXXXX-bin-win-cpu-x64.zip` (or CUDA/Vulkan/SYCL variant matching your hardware). Ooor extracts it into `config\llama-bin\` and picks it automatically.
2. **Get a model.** Open **Models → Download Models** (or the model market). Search for something small to start with — e.g. `Qwen2.5-Coder-1.5B-Instruct-Q4_K_M` — and click **Download**.
3. **Start the service.** Back on the main window, your engine and model are preselected. Hit **Start Service**. The console logs `llama-server` startup; the status bar shows `llama-server detected`.
4. **Chat.** Click **Open Console AI Assistant**. Type a message. The model streams a reply.
5. **Go agentic.** Open **Agent Manager**, bind a couple of tools (e.g. *Fetch URL*, *Read File*), save as a profile, and ask the model to do something that requires the tools — e.g. *"Summarize the README at https://github.com/.../README.md"*.

That is the whole loop. Everything else is refinement.

---

## Feature Walkthrough

### 1. Llama engine management

Ooor does not ship its own inference engine — it manages official `llama.cpp` release builds. The **llama** field on the main window shows the currently selected build (`llama-b11050-bin-win-cpu-x64` in the screenshot); **Choose** lets you switch between every build living in `config\llama-bin\`. New builds land on `llama.cpp`'s GitHub Releases frequently; Ooor's downloader knows how to find them, download the right zip for your architecture, and drop it straight into the runtime folder.

Why this matters: `llama.cpp` moves fast. Builds from a few months apart can differ dramatically in performance and supported model formats. Decoupling the engine from the GUI means you upgrade the engine without upgrading Ooor, and vice versa.

### 2. Local model library

The **Model Manager** window (`Models` menu) is a spreadsheet for your `.gguf` files. Columns:

- **Model Name** — filename
- **Projection File** — the `mmproj-*.gguf` for multimodal (vision) models, if any
- **Folder** — where it lives on disk
- **Type** — *Built-in* (under `config\models`) or *External* (e.g. an LM Studio folder you referenced)
- **Size** — on-disk size
- **Note** — your free-form annotation (e.g. *"smart gemma model"*, *"good at code"*)
- **D** — soft-delete flag

Right-click a row for the context menu: edit note, locate file, locate projector, add a model folder, hard delete, or soft delete. Soft delete drops the row from the list but keeps the file on disk — useful when you are experimenting and want to re-import later without re-downloading 7 GB.

### 3. Model market

The **Model Market** window (`Models → Download Models`) is a Hugging Face browser tuned for GGUF. Pick a mirror (default `hf-mirror`), and you get a sortable table of repositories with download counts, likes, task category, gated flag, and last update. Click a repo to see every `.gguf` file it publishes, with estimated hardware requirements (e.g. *"16 GB GPU"*, *"High-end GPU + high RAM"*) next to each quantization variant. Right-click → *Open model page in browser* if you need the full HF page.

This is especially useful in regions where direct Hugging Face access is flaky — the mirror selector and the proxy-aware downloader work together to make 10 GB model pulls survivable.

### 4. Download manager

The **Download Manager** is a real download manager, not a progress bar pretending to be one. Per file:

- Save path, total size, completed size
- Number of chunks (multi-part parallel download)
- Per-chunk status dots
- Start / Redownload / Delete
- Timestamp

The status bar aggregates tasks, active count, current speed, and total bytes. Downloads are resumable — kill the app mid-download, reopen, hit **Start**, and the chunks pick up where they left off. The downloader also auto-detects `llama-b*-bin-win-*.zip` archives and extracts them into `config\llama-bin\` so the engine is ready to use the moment the download completes.

### 5. Chat console

**Open Console AI Assistant** brings up a streaming chat view against the running model. Every assistant turn shows a token-sparkline (chunks per second over time) and per-chunk timing — useful for comparing engine builds or quantizations without running a separate benchmark. Chat history is persisted locally per profile.

### 6. Agent host & tool calling

The **Agent Manager** (`Llama` menu or the toolbar) lets you define an agent: a system prompt, a bound set of tools, and optional MCP servers. When the model decides a tool call is needed, the console shows the call and (for destructive tools) asks for your confirmation before executing. This is the layer that turns Ooor from a chat box into a local assistant.

### 7. Profiles

A **Profile** is a snapshot of (engine, model, runtime params, agent bindings). Save the current setup via the toolbar **Save Profile**, and switch between profiles from the dropdown. Typical use: one profile for a fast small model for quick questions, another for a large coder model for refactoring sessions.

---

## Built-in Tools (Agent Layer)

The built-in tool catalog gives the model safe, scoped access to the local machine and the network. Highlights:

- **Fetch URL** — HTTP GET a URL, return cleaned text/markdown to the model. Respects the GitHub proxy mirror list when the host is `github.com` / `raw.githubusercontent.com`.
- **Read File** — read a text file from an allowed root.
- **Write File** — write text to an allowed root (asks for confirmation).
- **List Directory** — enumerate files in a directory.
- **Shell** — run a shell command (asks for confirmation; output streamed back to the model).
- **Memory** — short key/value store for cross-turn notes.

Tools are mapped to the model via its native function-calling format; for engines that do not support native function calling, Ooor falls back to a parsing-based call format. MCP servers you bind through the Agent Manager are exposed to the model on equal footing with the built-in tools.

---

## System Requirements

- **OS:** Windows 10 1809+ / Windows 11 (x64). No macOS/Linux build today.
- **Runtime:** .NET Framework 4.8 (preinstalled on Win10 1903+ and Win11).
- **RAM:** 4 GB free for the smallest models; 16 GB+ for 7B-class Q4 models; 32 GB+ for 13B+.
- **GPU:** optional. CPU inference works on anything; for GPU offload, pick a `llama-b*-win-cuda` / `-vulkan` / `-sycl` build matching your hardware and bump **GPU layers** on the main window.
- **Disk:** the app itself is ~5 MB installed; budget separately for models (1–15 GB each) and runtimes (~100 MB each).

---

## Build from Source

You need Windows + Visual Studio 2022 (or Build Tools) with the **.NET desktop workload**, plus **NSIS 3.x** on `%PATH%` for the installer, and optionally **signtool** (Windows SDK) for code signing.

```bash
git clone https://github.com/<your-org>/ooor.git
cd ooor
```

The build is driven by a Node script that handles version bumping, MSBuild invocation, NSIS packaging, and code signing:

```bash
node ooor-utils/desktop-app/build-all.js
```

What the script does, in order:

1. Reads `Core/DEF.cs` `ver`, increments it by `0.0001`, writes it back.
2. Syncs the new version to `Properties/AssemblyInfo.cs` and `app/installer.nsi`.
3. Cleans `bin/Release/` and runs MSBuild (`/p:Configuration=Release`).
4. Copies the runtime `github-proxy.txt` into the config tree.
5. Signs `ooor.exe` with `signtool` (auto-generates a self-signed cert if no `.pfx` is provided).
6. Runs `makensis` to produce `app/out/ooor-Setup-x64-v<ver>.exe`.
7. Signs the installer.

The C# project location is configurable via `ooor-utils/desktop-app/.env`:

```
RELATIVE_MAIN_DIR = ./csharp-desktop-app/OOOR
```

This path is resolved relative to the repository root (two levels above `desktop-app/`). Leave it unset to fall back to the legacy layout.

For end-to-end release automation (build → upload → version list → update check), use:

```bash
node ooor-utils/desktop-app/publish.js build_and_upload --notes "your release notes"
```

See `publish.js --help` for the full command surface (upload, list, latest, download, delete, proxy management, health check).

---

## Project Layout

```
csharp-desktop-app/
├── OOOR/                       # the desktop application (this README's project)
│   ├── Core/                   # domain logic: engine runtime, model store, agents, tools
│   ├── Controls/               # custom WinForms controls (sparkline, no-autoscroll panel, …)
│   ├── Properties/             # AssemblyInfo, Resources.resx
│   ├── html/                   # embedded web assets for the chat console (index.html, vue.js, …)
│   ├── Lang/                   # i18n strings (en.json, zh.json)
│   ├── Ooor.csproj
│   └── Program.cs
├── Ooor-cli/                   # optional command-line front-end
├── OoorFunc/                   # shared agent/tool function library
├── ooor-sqlite-mcp/            # example SQLite MCP server
├── doc/img/                    # screenshots used by this README
└── Ooor.slnx                   # solution listing all C# projects
```

---

## Configuration & Paths

Ooor is data-driven and stores everything user-writable under one config tree so that backups and migrations are trivial.

| Path | Purpose |
|---|---|
| `<install>\bin\Ooor.exe` | the application |
| `<install>\config\llama-bin\` | extracted `llama-server` builds |
| `<install>\config\models\` | built-in model folder (scanned automatically) |
| `<install>\config\github-proxy.txt` | GitHub acceleration mirror list used by the downloader |
| `<install>\config\ref_models.conf` | references to external model folders (e.g. an LM Studio library) |
| `<install>\config\` (chat/temp/remark) | chat history, scratch, annotations |

By default `<install>` is `D:\Ooor` if you have a D: drive, otherwise `%LOCALAPPDATA%\Ooor`. The installer remembers the previous install location via the registry and reuses it on upgrade.

---

## FAQ

**Is my data sent anywhere?**  
No. The model server binds to `127.0.0.1`. The only outbound traffic happens when *you* ask the agent to fetch a URL, or when *you* download a model/engine. No telemetry.

**Can I use Ooor with models not from Hugging Face?**  
Yes. Drop any `.gguf` into `config\models\` or reference an external folder via *Add Model Folder* in the Model Manager — Ooor will catalog it.

**Does it support GPUs?**  
Yes — pick a GPU build of `llama-server` (`cuda`, `vulkan`, `sycl`) in the engine selector, then bump **GPU layers** on the main window. Vulkan is the most portable across vendors.

**Why WinForms and not Electron/WPF/MAUI?**  
WinForms starts instantly, ships tiny, and talks to native Win32 without ceremony. The whole point of Ooor is "tiny and native"; an Electron port would betray that.

**Why is the installer signed with a self-signed certificate?**  
A commercial EV code-signing certificate is expensive and currently self-funded. The build script supports dropping in a real `.pfx` via `app/.env` (`OOOR_SIGN_PFX` / `OOOR_SIGN_PASSWORD`) — contributions toward a cert are welcome.

**Will there be macOS/Linux?**  
Not soon. `llama.cpp` is cross-platform, but the GUI is WinForms. A future web-based console is on the roadmap.

---

## Roadmap

- ✅ Llama engine manager
- ✅ Local model library
- ✅ Hugging Face market browser
- ✅ Resumable downloader
- ✅ Streaming chat console
- ✅ Built-in tool catalog + agent host
- ✅ MCP server binding
- 🚧 Commercial code-signing certificate
- 🚧 Cross-engine support (beyond `llama.cpp`)
- 🚧 RAG / local knowledge base
- 🚧 Plugin SDK for custom tools
- 🚧 Remote (non-localhost) mode with auth

---

## Contributing

Contributions are welcome — bug reports, feature ideas, UI polish, translation fixes, and documentation all count. Before opening a large PR, please open an issue to discuss the direction.

A few conventions worth knowing:

- The project favours **standalone WinForms windows** over stock dialogs for customisability.
- Business logic lives in **`Core/` domain classes**, not in the UI layer.
- New tool integrations should be **HTTP-based** first; WebSocket only after an HTTP version has stabilised.
- When optimising, prefer the **smallest possible diff** — the project values a small binary and a readable codebase.

---

## License

Released under the **MIT License**. See [LICENSE](LICENSE) for the full text.

```
MIT License

Copyright (c) 2026 oshine

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND...
```

---

## Acknowledgements

- The **[llama.cpp](https://github.com/ggerganov/llama.cpp)** project — without it, none of this exists.
- The **Hugging Face** community and the maintainers of **`hf-mirror`** for keeping model access equitable.
- Every model author whose weights end up in someone's `config\models\` folder.
- The WinForms and .NET Framework teams at Microsoft, for a runtime that still runs a 1.5 MB binary on a stock Windows install in 2026.

---

<p align="center">Made by <a href="https://ooor.cc">oshine</a> · <a href="https://ooor.cc">ooor.cc</a></p>
