# ImageContentRetrieval v3

A **Content-Based Image Retrieval (CBIR)** desktop application built with WPF on **.NET 10**.

Given a query image, the tool searches a pre-built feature database and returns the most visually similar images ranked by Euclidean distance.

## Features

| Feature | Description |
|---|---|
| **Build** | Scan a folder (recursively) to extract feature vectors and persist them to a local vector database. |
| **Retrieve** | Select a query image and get the top-N most similar images from the database. |
| **Cleanup** | Remove stale entries whose source files no longer exist on disk. |
| **Locate** | Double-click a result row to reveal the file in Windows Explorer via Shell API. |

## Tech Stack

| Component | Technology |
|---|---|
| UI Framework | WPF with [Vorcyc.RoundUI](https://www.nuget.org/packages/Vorcyc.RoundUI/) |
| Deep Learning | [TensorFlow.NET](https://github.com/SciSharp/TensorFlow.NET) + [SciSharp.TensorFlow.Redist](https://www.nuget.org/packages/SciSharp.TensorFlow.Redist/) |
| Model | Google **Inception V3** (`inception_v3_2016_08_28_frozen.pb`) |
| Vector Database | [Vorcyc.Quiver](https://www.nuget.org/packages/Vorcyc.Quiver/) |
| Folder Dialog | [WindowsAPICodePack](https://www.nuget.org/packages/WindowsAPICodePack/) |
| File Locator | Win32 Shell API (`SHOpenFolderAndSelectItems`) |

## How It Works

```
Image  ──►  Decode & Resize (299×299)  ──►  Inception V3  ──►  1001-d Embedding
                                                                      │
                                                                      ▼
                                                              Vorcyc.Quiver DB
                                                            (filename ↔ vector)
                                                                      │
Query Image  ──►  Extract Embedding  ──►  Euclidean Distance Search  ─┘  ──►  Top-N Results
```

1. **Feature Extraction** — Each image is decoded, resized to 299 × 299, and fed through the frozen Inception V3 graph. The output of the `InceptionV3/Predictions/Reshape` layer produces a **1001-dimensional float32** embedding vector.
2. **Storage** — Filename–vector pairs are stored in a local **Vorcyc.Quiver** vector database (`features.vdb`) with WAL support.
3. **Retrieval** — The query image's embedding is compared against all stored vectors using **Euclidean distance**; the closest N results are returned.

## Supported Image Formats

- JPEG (`.jpg`, `.jpeg`, `.jfif`)
- PNG (`.png`)

## Prerequisites

- **Windows 7** or later
- **.NET 10 SDK** (`net10.0-windows7.0`)
- The frozen model file `inception_v3_2016_08_28_frozen.pb` placed in the output directory (copied automatically on build)

## Getting Started

```bash
# Clone the repository
git clone https://github.com/cyclonedll/ImageContentRetrieval_v3.git

# Build and run
cd ImageContentRetrieval_v3
dotnet run --project ImageContentRetrieval_v3
```

## Project Structure

```
ImageContentRetrieval_v3/
├── FeatureExtractor.cs        # TensorFlow.NET inference & batch extraction
├── Euclidean.cs               # Euclidean distance / similarity helpers
├── MainWindow.xaml.cs         # WPF main window logic (build, retrieve, cleanup)
├── QuiverDb/
│   ├── ImageDb.cs             # Entity: filename + 1001-d feature vector
│   └── ImageDbContext.cs      # Vorcyc.Quiver database context
├── IOHelper.cs                # Path utilities & database cleanup
├── ShellFolderSelector.cs     # Win32 Shell API file locator
├── NumericUpDown.cs           # Custom numeric input control
└── inception_v3_2016_08_28_frozen.pb  # Frozen Inception V3 model
```

## License

See [LICENSE](LICENSE) for details.

## Author

**cyclone_dll** · [Vorcyc](https://github.com/cyclonedll)