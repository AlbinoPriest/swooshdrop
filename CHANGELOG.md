# Changelog

## 0.3.7 beta — 2026-10-23

### Contributions from KuiDaXElaina

Integrated [KuiDaXElaina's six original commits](https://github.com/KuiDaXElaina/SwooshDrop/compare/e5b09e697a47579c8db8a70d63f8ba80d7471537...97d732a8eee57378772fd6fa10dd28cf7c2500c3) with their authorship preserved:

- English / Traditional Chinese interface localization.
- Configurable AirDrop receiver name and save folder, with restart-now/later prompts.
- Live incoming-file progress, including filename, file position, percentage and bytes.
- Interrupted-transfer feedback and UI recovery.
- Smaller installer with corresponding sources distributed separately.

### Integration fixes and retained work

- Scope receive consent, progress, completion and failures to their connection. Unrelated TLS/discovery failures cannot dismiss another transfer's approval.
- Build the backend from a fresh pinned source archive and ordered patch series on every run.
- Include all protocol patches and application instructions in corresponding-source archives.
- Preserve existing experimental sending, Bluetooth wake-up and update-check work.
- Keep official download links on AlbinoPriest/swooshdrop and credit upstream contributors explicitly.

PC-to-iPhone sending remains experimental: discovery works in hardware tests, but delivery to the test iPhone has not been validated. Fresh receiving tests on the combined build and second-PC installation tests remain unverified in this beta.
