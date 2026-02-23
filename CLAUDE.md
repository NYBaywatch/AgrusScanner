# Agrus Scanner — Project Instructions

## Release Process

When committing and pushing changes:

1. Build the solution (`dotnet build`)
2. Run `.\build-installer.ps1` to rebuild the MSI with the new code
3. Commit all changes (including updated docs/README if applicable)
4. Push to origin
5. If creating a GitHub release, attach the MSI from `Installer\bin\Release\AgrusScanner-Setup.msi`

The installer does NOT auto-rebuild — the publish folder will contain stale binaries until the build script runs.
