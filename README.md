# ReRank Studio

> Cross-platform desktop application for evaluating, comparing, and fine-tuning information retrieval reranker models — no Python required.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-12.0-blueviolet)](https://avaloniaui.net)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#installation)

ReRank Studio runs fully on .NET/C# — download HuggingFace Hub models, score them with ONNX Runtime, compute standard IR metrics (NDCG, MRR, MAP), and compare models side-by-side on your own datasets. A future AI Agent layer will orchestrate entire evaluation pipelines from natural language instructions.

---

## Why ReRank Studio?

Most reranker evaluation tooling is Python-only and assumes a Jupyter-notebook workflow. ReRank Studio offers:

- **No Python.** Single self-contained binary. Works offline after the first model download.
- **ONNX-native inference.** CPU out of the box; CUDA and CoreML with no code changes.
- **Graded-relevance metrics.** NDCG@K, MRR@K, MAP@K, Precision@K, Recall@K, Spearman ρ, Kendall τ, calibration curves — all computed locally.
- **Persistent experiment store.** Every run is saved to SQLite; nothing is lost between sessions.
- **Cross-platform.** One codebase runs on Windows, macOS, and Linux.

---

## Screenshots

> _Screenshots will be added once the UI stabilises. Want to contribute one? See [Contributing](#contributing)._

---

## Features

- Search and download models from HuggingFace Hub with HTTP resume support
- Batched ONNX inference — CPU out of the box, CUDA and CoreML with no code changes
- Full IR metric suite: NDCG@K, MRR@K, MAP@K, Precision@K, Recall@K, Spearman ρ, Kendall τ, calibration curves
- JSONL and CSV dataset loading with built-in validation
- Persistent SQLite experiment store — every run is saved, nothing is lost between sessions
- Multi-model comparison in a single evaluation run

---

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- _(Optional)_ CUDA 12.x for GPU inference on Windows / Linux
- _(Optional)_ Apple Silicon Mac for CoreML acceleration

### Build from source

```bash
git clone https://github.com/<your-org>/rerank-studio.git
cd rerank-studio
dotnet build -c Release
dotnet run --project src/ReRankEval.App -c Release
```

On first launch the app creates `~/.rerank_studio/` and applies the database migration automatically.

### Self-contained binaries

```bash
# Windows x64
dotnet publish src/ReRankEval.App -r win-x64 -c Release --self-contained -o ./publish/win-x64

# macOS Apple Silicon
dotnet publish src/ReRankEval.App -r osx-arm64 -c Release --self-contained -o ./publish/osx-arm64

# Linux x64
dotnet publish src/ReRankEval.App -r linux-x64 -c Release --self-contained -o ./publish/linux-x64
chmod +x ./publish/linux-x64/ReRankEval.App
```

Approximate size: **~280 MB** (CPU-only). CUDA build is larger due to native libtorch.

---

## Quick Start

### 1. Download a model

Open the **Models** tab, search for a model on HuggingFace Hub (e.g. `cross-encoder/ms-marco-MiniLM-L-6-v2`), and click **Download**. `config.json`, `tokenizer.json`, and weights are fetched with HTTP range resume support.

### 2. Prepare a dataset

**JSONL** — one object per line:

```json
{"query": "What is BERT?", "docs": ["BERT is a transformer...", "GPT is..."], "labels": [2, 0]}
{"query": "Vector search", "docs": ["FAISS is a library...", "BM25 is sparse..."], "labels": [2, 1]}
```

**CSV** — one row per query–document pair, rows with the same query are grouped automatically:

```
query,document,relevance,domain_tag
"What is BERT?","BERT is a transformer...",2,nlp
"What is BERT?","GPT is a language model",0,nlp
```

### 3. Run an evaluation

Open **Evaluation**, select one or more local models, pick a dataset, set K values (`1,5,10`), and click **Run**. Results are saved to SQLite and appear in the **Metrics** tab immediately.

### 4. Inspect results

The Metrics tab shows a comparison table per evaluation run:

| Metric | Description |
|--------|-------------|
| NDCG@K | Normalized Discounted Cumulative Gain — graded relevance |
| MRR@K | Mean Reciprocal Rank — position of first relevant doc |
| MAP@K | Mean Average Precision |
| P50 / P90 latency | Per-query inference latency percentiles |

---

## Metrics reference

### NDCG@K

```
DCG@K  = Σ (2^rel_i − 1) / log₂(i + 2)   for i = 0 … K−1
NDCG@K = DCG@K / IDCG@K
```

A perfect ranking returns **1.0**; random ranking approaches **0**.

### MRR@K

`1 / rank` of the first relevant document, averaged over all queries, clipped at K.

### MAP@K

Mean of per-query average precision values computed at each relevant document position up to K.

### Rank correlation (Spearman ρ, Kendall τ)

Measure how similarly two models rank documents on the same query set. Useful for model selection without a labelled dataset.

### Calibration

Reliability diagram — buckets of predicted scores (x) vs. actual relevance fraction (y). A well-calibrated model lies close to the diagonal.

---

## Project structure

```
rerank-studio/
├── src/
│   ├── ReRankEval.Domain/           # Entities, interfaces, MetricsCalculator (pure C#)
│   ├── ReRankEval.Infrastructure/   # HF Hub client, ONNX inference, EF Core, dataset parsing
│   ├── ReRankEval.Agent/            # Semantic Kernel agent (Phase 4 stub)
│   └── ReRankEval.App/              # Avalonia startup, DI wiring, XAML views
└── tests/
    ├── ReRankEval.Domain.Tests/        # 21 unit tests covering all metrics
    └── ReRankEval.Infrastructure.Tests/
```

### Data directory

```
~/.rerank_studio/
├── models/{org}/{model-name}/   # downloaded weights + ONNX
├── datasets/{id}/               # imported dataset files
├── checkpoints/                 # fine-tuning checkpoints (Phase 3)
├── exports/                     # CSV / JSON / Markdown reports
├── experiments.db               # SQLite (EF Core)
└── logs/app_YYYYMMDD.log
```

---

## Tech stack

| Layer | Technology |
|-------|------------|
| UI | [Avalonia UI 12](https://avaloniaui.net) (XAML, Fluent theme) |
| MVVM | [CommunityToolkit.Mvvm 8](https://github.com/CommunityToolkit/dotnet) |
| Inference | [Microsoft.ML.OnnxRuntime 1.27](https://onnxruntime.ai) |
| Storage | EF Core 10 + SQLite |
| HTTP / resilience | `System.Net.Http` + Polly |
| Logging | Serilog (rolling file) |
| DI / hosting | `Microsoft.Extensions.Hosting` |
| Analytics (Phase 2) | DuckDB.NET |
| Fine-tuning (Phase 3) | TorchSharp |
| AI Agent (Phase 4) | Microsoft.SemanticKernel |

---

## Running tests

```bash
dotnet test tests/ReRankEval.Domain.Tests
dotnet test tests/ReRankEval.Infrastructure.Tests
```

---

## Roadmap

| Phase | Status | Highlights |
|-------|--------|------------|
| 1 — Foundation | ✅ Complete | Domain model, ONNX inference, metrics, SQLite store, basic UI |
| 2 — Evaluation engine | 🔜 Planned | Comparison charts, BEIR downloader, DuckDB analytics, export |
| 3 — Analysis & fine-tuning | 🔜 Planned | Error analysis, calibration view, TorchSharp LoRA fine-tuning |
| 4 — AI Agent | 🔜 Planned | Semantic Kernel agent, natural-language evaluation pipelines |

---

## Contributing

Contributions are welcome. Please:

1. Fork the repo and create a feature branch from `main`.
2. Add unit tests for any new domain logic (`ReRankEval.Domain.Tests`).
3. Add integration tests for infrastructure changes (`ReRankEval.Infrastructure.Tests`).
4. Ensure `dotnet build -c Release` produces zero warnings.
5. Open a pull request describing what changed and why.

If you have a feature idea or bug report, open an issue first so we can discuss the approach.

---

## License

[MIT](LICENSE)
