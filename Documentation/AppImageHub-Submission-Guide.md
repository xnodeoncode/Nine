# AppImageHub Submission Guide

This guide walks through submitting Nine Property Management to AppImageHub for improved discoverability.

## Prerequisites

- ✅ v1.0.0 release published on GitHub
- ✅ AppImage built and tested (230MB, ~2s startup)
- ✅ Desktop file created (`aquiis.desktop`)
- ✅ AppStream metadata created (`com.aquiis.propertymanagement.appdata.xml`)
- ⏳ Screenshots needed (4 images)
- ⏳ Fork appimage.github.io repository
- ⏳ Submit pull request

## Step 1: Capture Screenshots

Launch the application and capture 4 screenshots (see `Documentation/Screenshots/README.md` for details):

```bash
# Launch application
~/Applications/Nine-1.0.0.AppImage

# Capture dashboard (primary screenshot)
gnome-screenshot -w -d 2
# Save as: Documentation/Screenshots/dashboard.png

# Navigate to Properties and capture
gnome-screenshot -w -d 2
# Save as: Documentation/Screenshots/property-management.png

# Navigate to Leases and capture
gnome-screenshot -w -d 2
# Save as: Documentation/Screenshots/lease-workflow.png

# Navigate to Invoices and capture
gnome-screenshot -w -d 2
# Save as: Documentation/Screenshots/invoice-tracking.png
```

**Requirements:**

- PNG format
- 1920x1080 recommended (minimum 752x423)
- Under 500KB each
- Show actual features, not promotional graphics

## Step 2: Commit Screenshots to Repository

Screenshots must be on GitHub before AppImageHub submission:

```bash
cd ~/Source/Nine

# Add screenshots
git add Documentation/Screenshots/*.png

# Commit
git commit -m "Add AppImageHub screenshots for v1.0.0 submission"

# Follow normal workflow: feature → development → PR to main
```

## Step 3: Fork AppImageHub Repository

1. Visit: https://github.com/AppImage/appimage.github.io
2. Click "Fork" button (top right)
3. Fork to your GitHub account

## Step 4: Clone Your Fork

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/appimage.github.io.git ~/appimage.github.io

cd ~/appimage.github.io

# Add upstream remote
git remote add upstream https://github.com/AppImage/appimage.github.io.git

# Fetch latest
git fetch upstream
git checkout master
git merge upstream/master
```

## Step 5: Run Preparation Script

```bash
cd ~/Source/Nine/4-Nine/Assets
./prepare-appimage-hub-submission.sh
```

This script will:

- Create `database/Nine_Property_Management/` directory
- Copy desktop file, icon, and AppStream metadata
- Copy primary screenshot
- Provide next steps

## Step 6: Verify Files

```bash
cd ~/appimage.github.io/database/Nine_Property_Management
ls -lh

# Should see:
# aquiis.desktop
# aquiis.png (512x512 icon)
# com.aquiis.propertymanagement.appdata.xml
# screenshot.png (dashboard screenshot)
```

**Verify desktop file:**

```bash
cat aquiis.desktop
```

**Validate AppStream metadata:**

```bash
appstreamcli validate com.aquiis.propertymanagement.appdata.xml
```

## Step 7: Create Branch and Commit

```bash
cd ~/appimage.github.io

# Create branch
git checkout -b add-aquiis-property-management

# Add files
git add database/Nine_Property_Management/

# Commit
git commit -m "Add Nine Property Management

Nine is a property management application for small landlords
managing 1-9 residential rental properties.

Features:
- Property, tenant, and lease management
- Automated invoicing and payment tracking
- Maintenance request tracking
- Property inspections with PDF reports
- Security deposit investment tracking
- Multi-user support with role-based access

Homepage: https://github.com/xnodeoncode/nine
License: MIT
AppImage: https://github.com/xnodeoncode/nine/releases/tag/v1.0.0"

# Push to your fork
git push origin add-aquiis-property-management
```

## Step 8: Create Pull Request

1. Visit: https://github.com/AppImage/appimage.github.io/compare
2. Click "compare across forks"
3. **Base fork**: `AppImage/appimage.github.io`, branch: `master`
4. **Head fork**: `YOUR_USERNAME/appimage.github.io`, branch: `add-aquiis-property-management`
5. Click "Create pull request"

**PR Title:**

```
Add Nine Property Management
```

**PR Description:**

```markdown
## Application Details

**Name:** Nine Property Management  
**Category:** Office, Finance  
**License:** MIT  
**Homepage:** https://github.com/xnodeoncode/nine  
**Download:** https://github.com/xnodeoncode/nine/releases/tag/v1.0.0

## Description

Property management software for small landlords managing 1-9 residential rental properties. Provides professional-grade features including:

- Complete tenant lifecycle management (prospect → application → lease)
- Automated rent invoicing and payment tracking
- Maintenance request tracking with vendor management
- Property inspections with comprehensive checklists
- Security deposit investment tracking
- Multi-user support with role-based access control

Built with .NET 10, Blazor Server, and ElectronNET. Standalone desktop application with no server or subscription required.

## Files Included

- ✅ `aquiis.desktop` - Desktop entry file
- ✅ `aquiis.png` - Application icon (512x512)
- ✅ `com.aquiis.propertymanagement.appdata.xml` - AppStream metadata
- ✅ `screenshot.png` - Primary screenshot (dashboard)

## Validation

AppStream metadata validated with `appstreamcli`:
```

appstreamcli validate com.aquiis.propertymanagement.appdata.xml

```

## Testing

AppImage tested on:
- RHEL 10 (development platform)
- Performance: ~2 second startup
- Size: 230MB (normal compression)

## Contact

- **GitHub:** @xnodeoncode
- **Email:** cisguru@outlook.com
```

6. Click "Create pull request"

## Step 9: Respond to Review

AppImageHub maintainers typically review within 1 week. Common feedback:

- Screenshot quality or content
- Desktop file formatting
- AppStream metadata completeness
- Icon resolution

**Be responsive:**

- Check GitHub notifications daily
- Respond within 24-48 hours
- Make requested changes promptly

## Step 10: Post-Approval

Once merged:

1. **Verify listing**: Visit https://appimage.github.io and search for "Nine"
2. **Update README**: Add AppImageHub badge
3. **Monitor feedback**: Watch for user issues on GitHub
4. **Update listing**: Submit new PR for version updates

## AppImageHub Badges (Post-Approval)

Add to README.md:

```markdown
[![Get it on AppImageHub](https://img.shields.io/badge/AppImageHub-Nine%20Property%20Management-blue.svg)](https://appimage.github.io/Nine_Property_Management/)
```

## Maintenance

For future releases:

1. **Update AppStream metadata** with new `<release>` entry
2. **Update screenshots** if UI changes significantly
3. **Submit new PR** to appimage.github.io with updates
4. **Maintainers prefer**: Incremental updates over large changes

## Troubleshooting

### Validation Errors

```bash
# Common issues
appstreamcli validate --explain com.aquiis.propertymanagement.appdata.xml
```

### Screenshot Not Found

Ensure screenshots are:

- Committed to main branch
- Accessible via raw GitHub URLs
- Correct filenames in AppStream metadata

### Desktop File Issues

```bash
# Validate desktop file
desktop-file-validate aquiis.desktop
```

## Resources

- **AppImageHub**: https://github.com/AppImage/appimage.github.io
- **AppStream Spec**: https://www.freedesktop.org/software/appstream/docs/
- **Desktop File Spec**: https://specifications.freedesktop.org/desktop-entry-spec/
- **AppImage Best Practices**: https://docs.appimage.org/

## Timeline

- **Screenshots**: 30 minutes - 1 hour
- **Fork/Clone**: 5 minutes
- **Preparation**: 10 minutes (automated script)
- **PR Creation**: 15 minutes
- **Review Process**: 3-7 days
- **Total**: ~1 week from start to approval

## Status

- [ ] Screenshots captured and committed
- [ ] appimage.github.io forked
- [ ] Preparation script executed
- [ ] Files verified and validated
- [ ] Pull request created
- [ ] Review feedback addressed
- [ ] Approved and merged
- [ ] Listing verified on AppImageHub
