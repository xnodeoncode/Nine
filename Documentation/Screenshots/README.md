# AppImageHub Screenshots

This directory contains screenshots for the AppImageHub submission.

## Requirements

- **Format**: PNG
- **Minimum resolution**: 752x423 (16:9 aspect ratio)
- **Recommended resolution**: 1920x1080
- **File size**: Keep under 500KB per image (optimize if needed)
- **Content**: Show actual application features, not promotional graphics

## Required Screenshots

### 1. dashboard.png (Primary - Required)

- **Content**: Main dashboard showing property overview, metrics, and navigation
- **Caption**: "Dashboard with property overview and metrics"
- **Status**: ⏳ Needs to be captured

### 2. property-management.png

- **Content**: Property management interface showing property list or details
- **Caption**: "Property management interface"
- **Status**: ⏳ Needs to be captured

### 3. lease-workflow.png

- **Content**: Lease creation or tenant management screen
- **Caption**: "Lease workflow and tenant management"
- **Status**: ⏳ Needs to be captured

### 4. invoice-tracking.png

- **Content**: Invoice list or payment tracking screen
- **Caption**: "Invoice and payment tracking"
- **Status**: ⏳ Needs to be captured

## How to Capture Screenshots

### Option 1: Using GNOME Screenshot (Linux)

```bash
# Launch application
~/Applications/Nine-1.0.0.AppImage

# Open GNOME Screenshot
gnome-screenshot -w -d 2

# Click on application window within 2 seconds
# Screenshot will be saved to ~/Pictures/
```

### Option 2: Using Spectacle (KDE)

```bash
# Launch Spectacle
spectacle

# Select "Active Window" mode
# Click application window
# Save to this directory
```

### Option 3: Using Flameshot

```bash
# Install flameshot
sudo dnf install flameshot  # RHEL/Fedora
sudo apt install flameshot  # Ubuntu/Debian

# Launch application
flameshot gui

# Select window and save
```

## Post-Processing (If Needed)

If screenshots are too large, optimize with:

```bash
# Install imagemagick if needed
sudo dnf install ImageMagick

# Resize to 1920x1080 (if larger)
mogrify -resize 1920x1080 *.png

# Reduce file size (if over 500KB)
mogrify -quality 85 -strip *.png
```

## Verification

Before submitting, verify all screenshots:

```bash
# Check resolution and size
identify *.png

# Preview
eog *.png  # or xdg-open
```

## AppStream Metadata

Screenshots are referenced in `com.aquiis.propertymanagement.appdata.xml`:

```xml
<screenshots>
  <screenshot type="default">
    <caption>Dashboard with property overview and metrics</caption>
    <image>https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/Screenshots/dashboard.png</image>
  </screenshot>
  <!-- ... additional screenshots ... -->
</screenshots>
```

**Important**: Screenshots must be committed to the repository and merged to `main` branch before AppImageHub submission, as the XML references the GitHub raw URLs.
