# IHC Project Download/Upload Utility

Command-line utility to download or upload IHC project files (.vis) from/to an IHC controller.

## Usage

```bash
# Download project from controller
dotnet run --project ihc_ProjectDownloadUpload.csproj DOWNLOAD <destination-file>

# Upload project to controller
dotnet run --project ihc_ProjectDownloadUpload.csproj UPLOAD <source-file>
```

## Configuration

Requires `ihcsettings.json` with IHC controller endpoint and credentials (see `ihcsettings_template.json` in repository root).
