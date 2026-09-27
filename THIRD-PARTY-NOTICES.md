# Third-party notices

WinDrop PC is an unofficial community adaptation developed independently by AlbinoPriest. It is not affiliated with, sponsored by, or endorsed by Apple, Microsoft, Uvejs Gjelaj, or the OWL maintainers. Its AirDrop protocol implementation incorporates and modifies Uvejs Gjelaj's WinDrop; the Windows frontend, installer, and hardware integration are this project's additions. These additions do not replace upstream authorship or license terms.

The app embeds MIT-licensed WinDrop assemblies. Starting with 0.3, the setup EXE additionally embeds OWL, matching GPL Linux driver modules, minimal redistributable firmware, component notices, and their corresponding source archive. Setup installs the archive under `Sources` so copied installers carry the source materials.

| Component | Upstream | License | Local notice |
| --- | --- | --- | --- |
| WinDrop | https://github.com/UvejsGj/WinDrop | MIT, copyright 2026 Uvejs Gjelaj | `licenses/WinDrop-LICENSE.txt` |
| OWL | https://github.com/seemoo-lab/owl | GPL-3.0 | `licenses/OWL-LICENSE.txt` |
| rtl8188eus driver | https://github.com/aircrack-ng/rtl8188eus | GPL-2.0-only; source headers credit Realtek Corporation | `licenses/RTL8188EU-LICENSE.txt` |
| Matching WSL kernel source | https://github.com/microsoft/WSL2-Linux-Kernel | Linux kernel licensing, including GPL-2.0 | See upstream `COPYING` and `LICENSES/` |

Exact revisions are in `source-versions.json`. `prototype-changes.patch` modifies MIT-licensed WinDrop. `driver-compatibility.patch` modifies GPL-2.0-only driver files. Original code in this repository does not change those licenses.

The driver repository has license declarations in its source headers rather than a top-level LICENSE file. The included GPL-2.0 text was copied from the matching Linux kernel's `LICENSES/preferred/GPL-2.0`, including its SPDX usage metadata.

usbipd-win and WSL prerequisites are downloaded from official signed releases, not embedded. Bundled driver binaries use the pinned kernel/vendor source. Their corresponding source archive includes kernel configurations, symbol versions, build instructions, vendor changes, OWL, and its submodules. Firmware and regulatory database copyright notices are retained in the runtime's `notices` directory. Source-tree licenses remain in the corresponding source archive.

Preview codecs are installed separately from Ubuntu packages: Pillow (HPND license), libheif (LGPL-3.0-or-later library, with tool-specific licenses), and libde265 (LGPL-3.0-or-later). Their package copyright notices are installed under `/usr/share/doc/`. The Windows EXE does not bundle those codecs. `render-preview.py` invokes these installed tools to create separate thumbnails and leaves the received media untouched.

Firmware notices and WHENCE were retrieved from the official kernel.org Google mirror at revision `3b128b60`, matching the base revision of Ubuntu's installed `linux-firmware` package `20240318.git3b128b60.0ubuntu3.1`. Realtek firmware uses `LICENCE.rtlwifi_firmware.txt`; MediaTek firmware uses `LICENCE.mediatek` or `LICENCE.ralink_a_mediatek_company_firmware` as identified by WHENCE. These separate binary-firmware redistribution terms do not relicense firmware under the app's MIT license or the kernel's GPL. The corresponding license texts and WHENCE are retained under `licenses` and copied into the runtime notices.
