# Releasing

This repo is set up for three release surfaces:
- GitHub source and release artifacts
- NuGet tool package
- Homebrew tap formula maintained in a separate tap repository

## Pre-release Checks

Run:

```bash
dotnet test PdfFormFiller.slnx
dotnet build PdfFormFiller.slnx -c Release
dotnet test PdfFormFiller.slnx -c Release --no-build
```

Optional but recommended before a major release:

```bash
dotnet test PdfFormFiller.slnx --filter FullyQualifiedName~CliEquivalenceTests
```

That live equivalence pass requires `SYNCFUSION_LICENSE_KEY`.

## Build Release Artifacts

Pack the tool package:

```bash
dotnet pack src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj -c Release -o ./artifacts/packages
```

Create single-file publish artifacts:

```bash
scripts/package-release.sh osx-arm64
scripts/package-release.sh osx-x64
```

Each run writes:
- a single-file publish directory under `artifacts/release/publish/<rid>/`
- a tarball under `artifacts/release/dist/`
- a `.sha256` file for the tarball

## GitHub Release Checklist

1. Update `PackageVersion` in [PdfFormFiller.Cli.csproj](../src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj).
2. Update [CHANGELOG.md](../CHANGELOG.md).
3. Run the pre-release checks.
4. Build package and tarball artifacts.
5. Create a Git tag matching the release version.
6. Upload the generated tarballs and checksum files to the GitHub release.

## Homebrew Tap Checklist

The actual formula should live in a separate tap repository.

1. Build the release tarballs.
2. Copy [packaging/homebrew/pdf-form-filler.rb.template](../packaging/homebrew/pdf-form-filler.rb.template) into the tap repo as `Formula/pdf-form-filler.rb`.
3. Replace:
   - `__VERSION__`
   - `__HOMEPAGE__`
   - `__URL_OSX_ARM64__`
   - `__SHA256_OSX_ARM64__`
   - `__URL_OSX_X64__`
   - `__SHA256_OSX_X64__`
4. Commit the formula change in the tap repo.
5. Test install from the tap on both Apple Silicon and Intel macOS if you publish both artifacts.

## Notes

- `pdf-form-filler` is already configured as a .NET tool package.
- The release tarballs are designed for binary installation and Homebrew tap consumption.
- The repo includes vendored reference material and exploratory fixtures; those are not required for end-user runtime use.
