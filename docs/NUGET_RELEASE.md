# NuGet Release

`DimonSmart.ExtremaSpectrum` is published automatically when a Git tag matching `v*` is pushed.

## One-time setup

1. Sign in to `nuget.org`.
2. Open `API Keys`.
3. Create a key for GitHub Actions, for example `DimonSmart.ExtremaSpectrum GitHub Actions`.
4. Scope:
   - permission: `Push`
   - package glob: `DimonSmart.ExtremaSpectrum`
5. Copy the key immediately. NuGet shows it only once.
6. Save the key to the GitHub repository secret `NUGET_API_KEY`.

GitHub CLI:

```powershell
gh secret set NUGET_API_KEY --repo DimonSmart/ExtremaSpectrum
```

GitHub UI:

- `Settings` -> `Secrets and variables` -> `Actions` -> `New repository secret`
- name: `NUGET_API_KEY`

## Release flow

1. Make sure the version you want to publish does not already exist on NuGet.
2. Create a version tag with the `v` prefix.
3. Push the tag.

Example:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## What the workflow does

`release.yml` runs on every pushed tag matching `v*` and then:

1. extracts the package version from the tag
2. restores, builds, and tests the solution
3. packs `src/DimonSmart.ExtremaSpectrum/DimonSmart.ExtremaSpectrum.csproj`
4. publishes `.nupkg` and `.snupkg` to `nuget.org`
5. creates a GitHub Release with the produced packages attached

## Version format

Supported examples:

- `v1.0.0`
- `v1.0.1`
- `v1.1.0-beta.1`

The package version is the tag value without the leading `v`.
