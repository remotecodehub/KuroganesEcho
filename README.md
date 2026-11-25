# Kuroganes Echo

## 🛠️ CI/CD

The GitHub Actions workflow builds the DesktopGL project for Windows, Linux, and macOS, builds the Android APK and iOS simulator app, and packages native DesktopGL installers. Each build and installer is published as a downloadable workflow artifact.

DesktopGL installers include the published content, raw game assets, Array font files, the font license, and `src/KuroganesEcho.Core/Content/Fonts/FontshareKit-2611002845.zip`. The iOS artifact is an unsigned simulator build; signing and App Store distribution are not configured.
