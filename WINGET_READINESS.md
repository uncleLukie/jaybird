# Winget Readiness Summary

This document summarizes the winget preparation work completed for jaybird.

## ✅ Completed Tasks

### 1. Updated GitHub Release Workflow
- **File**: `.github/workflows/release.yml`
- **Changes**:
  - Added SHA256 hash generation for all release assets
  - Updated artifact uploads to include hash files
  - Modified release creation to upload hash files
  - Enhanced release notes with installation instructions

### 2. Created Winget Submission Workflow
- **File**: `.github/workflows/winget-submit.yml`
- **Features**:
  - Triggers on release publication or manual dispatch
  - Downloads Windows release assets and hashes
  - Installs WingetCreate tool
  - Generates winget manifests automatically using `wingetcreate new`
  - Validates manifests before submission
  - Optional automatic submission with GitHub token
  - Uploads manifests as artifacts for review
  - Comprehensive error handling and validation

### 3. Created Winget Manifest Templates
- **Directory**: `winget-manifests/`
- **Files**:
  - `uncleLukie.jaybird.yaml` - Version manifest template
  - `uncleLukie.jaybird.locale.en-US.yaml` - English localization
  - `uncleLukie.jaybird.installer.yaml` - Installer configuration (Windows x64 only)

### 4. Helper Scripts
- **`scripts/prepare-winget.ps1`**: Comprehensive script for manual winget submission
- **`scripts/generate-hashes.ps1`**: Utility to generate SHA256 hashes locally

### 5. Documentation
- **`docs/WINGET_SUBMISSION.md`**: Detailed submission guide
- **Updated `README.md`**: Added winget installation instructions
- **Updated `.gitignore`**: Excluded temporary manifest files

## 📋 Package Information

- **Package Identifier**: `uncleLukie.jaybird`
- **Publisher**: `uncleLukie`
- **Moniker**: `jaybird`
- **Installer Type**: `zip` with `portable` nested installer
- **Supported Platform**: Windows x64 only
- **License**: MIT

**Important Note**: Winget only supports Windows installations. macOS and Linux builds are available from [GitHub releases](https://github.com/uncleLukie/jaybird/releases) but cannot be installed via winget.

## 🚀 Next Steps for Submission

### Option 1: Automated Submission (Recommended)

1. **Create GitHub Personal Access Token**:
   - Go to GitHub Settings → Developer settings → Personal access tokens
   - Create token with `public_repo` scope
   - Add as repository secret: `WINGET_GITHUB_TOKEN`

2. **Create Release**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. **Monitor Workflow**:
   - Release workflow builds and publishes Windows assets with SHA256 hashes
   - Winget submission workflow generates and submits manifests
   - Check winget-pkgs pull request for review status

### Option 2: Manual Submission

1. **Install WingetCreate**:
   ```bash
   winget install wingetcreate
   ```

2. **Run Preparation Script**:
   ```powershell
   .\scripts\prepare-winget.ps1 -Version 1.0.0
   ```

3. **Manual Submission**:
   ```bash
   wingetcreate submit manifests
   ```

## 🔧 Configuration Details

### Release Assets Structure
```
release-v1.0.0/
├── jaybird-v1.0.0-win-x64.zip
├── jaybird-v1.0.0-win-x64.zip.sha256
├── jaybird-v1.0.0-osx-x64.zip          # Not included in winget
├── jaybird-v1.0.0-osx-x64.zip.sha256   # Not included in winget
├── jaybird-v1.0.0-linux-x64.zip        # Not included in winget
└── jaybird-v1.0.0-linux-x64.zip.sha256 # Not included in winget
```

**Note**: Only the Windows x64 build is submitted to winget. Other platforms remain available via GitHub releases.

### Winget Manifest Structure
```
microsoft/winget-pkgs/
├── manifests/
│   ├── u/
│   │   ├── uncleLukie/
│   │   │   ├── jaybird/
│   │   │   │   ├── 1.0.0/
│   │   │   │   │   ├── uncleLukie.jaybird.yaml
│   │   │   │   │   ├── uncleLukie.jaybird.locale.en-US.yaml
│   │   │   │   │   └── uncleLukie.jaybird.installer.yaml
```

## 📊 Installation Commands

Once published, Windows users can install jaybird with:

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

**For macOS and Linux users**: Download builds from [GitHub releases](https://github.com/uncleLukie/jaybird/releases)

## ⚠️ Important Notes

1. **Version Format**: Use semantic versioning (e.g., 1.0.0, not v1.0.0) for winget manifests
2. **SHA256 Hashes**: Must match release assets exactly
3. **Installer Type**: Using zip with portable nested installer for easy installation
4. **Platform**: Winget manifests only include Windows x64 builds
5. **Review Process**: Microsoft team reviews all submissions (typically 1-2 weeks)
6. **Updates**: New versions will automatically update existing winget installations

## 🐛 Troubleshooting

### Common Issues
- **Hash Mismatch**: Ensure SHA256 hashes are generated correctly and match exactly
- **Invalid Installer**: Check that nested installer paths are correct (`jaybird.exe`)
- **Version Conflicts**: Use unique version numbers for each release
- **Download Failure**: Verify the GitHub release exists before running workflow

### Debug Commands
```bash
# Validate manifests locally
wingetcreate validate manifests

# Test installation locally (if you have the manifests)
winget install --manifest ./manifests
```

## 📚 Resources

- [WingetCreate Documentation](https://github.com/microsoft/winget-create)
- [Winget Manifest Specification](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest)
- [Submission Guidelines](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [Winget Documentation](https://learn.microsoft.com/en-us/windows/package-manager/)

---

**Status**: ✅ Ready for winget submission

The repository is fully prepared for winget submission. The automated workflow will handle manifest generation and submission when you create a new release tag.
