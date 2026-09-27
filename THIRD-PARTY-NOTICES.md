# Third-party notices

This repository stores integration code and patches rather than vendoring dependency source or binaries. Built Windows releases embed MIT-licensed WinDrop assemblies; its license is included with distributed builds. OWL and driver binaries are not embedded.

| Component | Upstream | License | Local notice |
| --- | --- | --- | --- |
| WinDrop | https://github.com/UvejsGj/WinDrop | MIT, copyright 2026 Uvejs Gjelaj | `licenses/WinDrop-LICENSE.txt` |
| OWL | https://github.com/seemoo-lab/owl | GPL-3.0 | `licenses/OWL-LICENSE.txt` |
| rtl8188eus driver | https://github.com/aircrack-ng/rtl8188eus | GPL-2.0-only; source headers credit Realtek Corporation | `licenses/RTL8188EU-LICENSE.txt` |
| Matching WSL kernel source | https://github.com/microsoft/WSL2-Linux-Kernel | Linux kernel licensing, including GPL-2.0 | See upstream `COPYING` and `LICENSES/` |

Exact revisions are in `source-versions.json`. `prototype-changes.patch` modifies MIT-licensed WinDrop. `driver-compatibility.patch` modifies GPL-2.0-only driver files. Original code in this repository does not change those licenses.

The driver repository has license declarations in its source headers rather than a top-level LICENSE file. The included GPL-2.0 text was copied from the matching Linux kernel's `LICENSES/preferred/GPL-2.0`, including its SPDX usage metadata.

usbipd-win is installed separately from its official signed release. This repository does not redistribute that installer or the built kernel/driver binaries.

Preview codecs are installed separately from Ubuntu packages: Pillow (HPND license), libheif (LGPL-3.0-or-later library, with tool-specific licenses), and libde265 (LGPL-3.0-or-later). Their package copyright notices are installed under `/usr/share/doc/`. The Windows EXE does not bundle those codecs. `render-preview.py` invokes these installed tools to create separate thumbnails and leaves the received media untouched.
