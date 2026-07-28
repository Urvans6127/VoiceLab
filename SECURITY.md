# VoiceLab security

## Local trust boundary

The application opens selected local preset files, writes settings/presets/logs/recordings locally, and talks to Windows audio endpoints. It does not listen on a network port, send HTTP requests, execute downloaded code, modify the registry, create startup entries, request elevation, or install drivers.

Preset JSON is validated and bounded before use. Corrupt settings or presets are quarantined or replaced with safe defaults. Recording queues are bounded. Folder opening is limited to an existing or explicitly created local directory and invokes Windows Explorer directly.

## Reporting and operational safety

Do not include private recordings or personal settings in a report. Only process audio you are authorized to use.

Dependency versions are pinned in project files and documented in THIRD-PARTY-NOTICES.md. This source repository does not include an updater, installer, package, or signed binary.
