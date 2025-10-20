# Winget Submission Guide

This guide explains how jaybird is submitted to the Windows Package Manager (winget) repository.

## Overview

jaybird uses the package identifier `uncleLukie.jaybird` in the winget repository. The submission process is automated through GitHub Actions, but can also be done manually.

**Important**: Winget only supports Windows installations. macOS and Linux builds are available from [GitHub releases](https://github.com/uncleLukie/jaybird/releases) but cannot be installed via winget.

## Automated Submission

### Prerequisites

1. **GitHub Personal Access Token**: Create a token with `public_repo` scope
2. **Repository Secret**: Add the token as `WINGET_GITHUB_TOKEN` in your repository secrets

### Process

1. **Create a new release tag** (e.g., `v1.0.0`)
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Release workflow automatically**:
   - Builds cross-platform executables (Windows, macOS, Linux)
   - Generates SHA256 hashes for all builds
   - Creates GitHub release with all assets

3. **Winget submission workflow triggers** on release publication and:
   - Downloads Windows x64 release assets and hash
   - Validates the SHA256 hash format
   - Installs WingetCreate tool
   - Generates winget manifests using `wingetcreate new`
   - Validates manifests
   - Submits to winget-pkgs repository (if `WINGET_GITHUB_TOKEN` is set)
   - Uploads manifests as artifacts for review

### Manual Trigger

You can also trigger the winget submission manually using GitHub Actions:

1. Go to **Actions** → **Submit to Winget** → **Run workflow**
2. Enter the version number (without 'v' prefix, e.g., `1.0.0`)
3. Click **Run workflow**

Or use the PowerShell script locally:

```powershell
.\scripts\prepare-winget.ps1 -Version 1.0.0 -Submit -Token "your_github_token"
```

## Manual Submission

If you prefer to submit manually:

### 1. Install WingetCreate

```bash
winget install wingetcreate
```

### 2. Run the Preparation Script

```powershell
# Without submission (just creates manifests)
.\scripts\prepare-winget.ps1 -Version 1.0.0

# With submission (requires GitHub token)
.\scripts\prepare-winget.ps1 -Version 1.0.0 -Submit -Token "ghp_your_token_here"
```

The script will:
- Download the Windows x64 release asset and SHA256 hash
- Validate the hash format
- Create winget manifests using `wingetcreate new`
- Validate the manifests
- Optionally submit to winget-pkgs repository

### 3. Review Generated Manifests

The manifests will be created in the `manifests/` directory:

```
manifests/
├── u/
│   ├── uncleLukie/
│   │   ├── jaybird/
│   │   │   ├── 1.0.0/
│   │   │   │   ├── uncleLukie.jaybird.yaml
│   │   │   │   ├── uncleLukie.jaybird.locale.en-US.yaml
│   │   │   │   └── uncleLukie.jaybird.installer.yaml
```

### 4. Submit Manually (if not using -Submit flag)

```bash
wingetcreate submit --token "ghp_your_token_here" manifests
```

This will create a pull request to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).

## Manifest Structure

### Version Manifest (`uncleLukie.jaybird.yaml`)
Specifies the package version and default locale.

### Installer Manifest (`uncleLukie.jaybird.installer.yaml`)
Contains installer details:
- **InstallerType**: `zip` (the release asset is a zip file)
- **NestedInstallerType**: `portable` (jaybird.exe is portable)
- **NestedInstallerFiles**: Specifies `jaybird.exe` with command alias `jaybird`
- **Platform**: Windows.Desktop only
- **Architecture**: x64
- **InstallerUrl**: Points to GitHub release asset
- **InstallerSha256**: SHA256 hash of the zip file

### Locale Manifest (`uncleLukie.jaybird.locale.en-US.yaml`)
Contains package metadata:
- Publisher, package name, description
- License information
- Tags and moniker
- Installation notes
- Release notes

## Package Information

- **Package Identifier**: `uncleLukie.jaybird`
- **Publisher**: `uncleLukie`
- **Moniker**: `jaybird`
- **Installer Type**: `zip` with `portable` nested installer
- **Supported Platform**: Windows x64 only
- **License**: MIT

## Installation Commands

Once published to winget, users can install jaybird with:

```bash
# Using package ID
winget install uncleLukie.jaybird

# Using moniker
winget install jaybird

# Update existing installation
winget upgrade uncleLukie.jaybird

# Uninstall
winget uninstall uncleLukie.jaybird
```

## Version Handling

- **Git tags**: Use `v` prefix (e.g., `v1.0.0`)
- **Release file names**: Include `v` prefix (e.g., `jaybird-v1.0.0-win-x64.zip`)
- **Winget manifests**: Use version without `v` prefix (e.g., `1.0.0`)

The workflows and scripts handle this conversion automatically.

## Validation

Always validate manifests before submission:

```bash
wingetcreate validate manifests
```

The automated workflow includes validation as a mandatory step.

## Troubleshooting

### Common Issues

1. **SHA256 Mismatch**
   - Ensure the hash file matches the release asset exactly
   - Regenerate hashes if needed: `.\scripts\generate-hashes.ps1`

2. **Invalid Installer Type**
   - Use `zip` with `portable` nested installer for jaybird
   - Specify `jaybird.exe` as the relative file path

3. **Version Format Error**
   - Use semantic versioning: `1.0.0`, `1.0.1`, `2.0.0`, etc.
   - Don't include `v` prefix in the version field

4. **Download Failure**
   - Verify the GitHub release exists
   - Check that the release tag matches (e.g., `v1.0.0`)
   - Ensure release assets are publicly accessible

5. **WingetCreate Not Found**
   - Install manually: `winget install wingetcreate`
   - Restart your terminal after installation

### Debugging

1. **Check workflow logs** in GitHub Actions for detailed error messages
2. **Download manifest artifacts** from the workflow run
3. **Validate locally** before submission
4. **Check similar packages** in winget-pkgs for reference

### Example Workflow Artifact Download

If the automated submission fails:
1. Go to the failed workflow run
2. Download the `winget-manifest-{version}` artifact
3. Review the generated manifests
4. Fix any issues and re-run the workflow

## Resources

- [WingetCreate Documentation](https://github.com/microsoft/winget-create)
- [Winget Manifest Specification](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest)
- [Winget Repository](https://github.com/microsoft/winget-pkgs)
- [Submission Guidelines](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [Winget Documentation](https://learn.microsoft.com/en-us/windows/package-manager/)

## Maintenance

### Updating to a New Version

1. **Create new release tag**:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

2. **Automated workflow handles the rest**:
   - Builds and publishes release assets
   - Generates and validates manifests
   - Submits to winget-pkgs (if token is configured)

3. **Monitor the pull request**:
   - Microsoft team reviews submissions (typically 1-2 weeks)
   - Address any feedback from reviewers
   - Once merged, the package is available to all winget users

### Handling Review Feedback

If reviewers request changes:
1. Download the manifest artifact from the workflow
2. Make necessary edits to the manifests
3. Push updates to the pull request
4. Respond to reviewer comments

## Frequently Asked Questions

**Q: Can I install jaybird on macOS or Linux via winget?**
A: No, winget only supports Windows. macOS and Linux users should download from [GitHub releases](https://github.com/uncleLukie/jaybird/releases).

**Q: How long does the review process take?**
A: Typically 1-2 weeks, but can vary based on Microsoft's review queue.

**Q: Can I update an existing submission?**
A: Yes, for updates to existing packages, use `wingetcreate update` instead of `wingetcreate new`. The workflows will need to be adjusted for updates.

**Q: What if my submission is rejected?**
A: Address the reviewer's feedback, update your manifests, and resubmit. Common issues include hash mismatches, incorrect installer types, or missing metadata.

**Q: Do I need a GitHub token?**
A: Only if you want automatic submission. Without a token, you can download the generated manifests and submit manually via a fork and pull request.

---

The automated workflow makes winget submission straightforward. For most releases, simply create a git tag and the rest is handled automatically!
