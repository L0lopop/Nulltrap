# Third-party notices

Nulltrap is written from scratch and is not a fork. It does, however, stand on
work published by others, and this file records that plainly.

## Bloxstrap

**Repository:** <https://github.com/bloxstraplabs/bloxstrap>
**License:** MIT — Copyright (c) 2022 pizzaboxer

Bloxstrap established how a third-party Roblox bootstrapper works on Windows,
and Nulltrap's understanding of the Roblox deployment protocol is derived from
reading its source: the setup CDN mirror list and the `versionStudio`
connectivity probe, the `clientsettingscdn` client-version endpoint and its
channel suffix, the `rbxPkgManifest` package format and the package-to-directory
mapping, the shape of the file-modification overlay and its deletion manifest,
and the `FLog` log entries a session can be tracked by.

Where Nulltrap reproduces that behaviour it reproduces protocol knowledge —
facts about how Roblox distributes its client — rather than copying source
files. Any code that is adapted rather than reimplemented carries a comment
naming this notice at the point of use.

## Fishstrap

**Repository:** <https://github.com/fishstrap/fishstrap>
**License:** MIT — Copyright (c) 2025 returnrqt

Fishstrap is the fork that first documented the practical consequences of the
Roblox FastFlag allowlist for bootstrappers, which is why Nulltrap validates
flags against the allowlist instead of pretending arbitrary flags still apply.

## Froststrap and Voidstrap

**Repositories:** <https://github.com/Froststrap/Froststrap>,
<https://github.com/KloBraticc/Voidstrap>
**License:** MIT

Both were studied for feature scope and project layout. Voidstrap's split into
separate core, platform-abstraction and platform-implementation projects is a
good idea, and Nulltrap's solution structure follows the same principle.

## Fonts and glyphs

Nulltrap draws its text in Segoe UI and its icons from Segoe MDL2 Assets, and
shows flag names in Consolas. All three ship with Windows and are used through
the system, not redistributed, so no font licence travels with this repository.

## Build-time tools

`scripts/make-icon.py` imports NumPy (BSD 3-Clause), Pillow (MIT-CMU), OpenCV
(Apache 2.0) and SciPy (BSD 3-Clause). It runs on a developer's machine to turn
the artwork in `assets/` into the icon files; none of it is redistributed, and
nothing it produces contains their code. The test suite uses xUnit (Apache 2.0),
which likewise does not ship with the launcher.

## Roblox

Nulltrap is an unofficial, independent project. It is not affiliated with,
endorsed by, or sponsored by Roblox Corporation. "Roblox" is a trademark of
Roblox Corporation.

Nulltrap does not modify, inject into, or read the memory of the Roblox client
process. It downloads the official client from Roblox's own distribution
servers and launches it.
