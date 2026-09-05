<div align="center">

<img src="assets/nulltrap-icon.png" alt="Nulltrap" width="120">

# Nulltrap

**An alternative Roblox bootstrapper for Windows.**
Small, auditable, and honest about what it can and cannot do.

**English** · [Русский](README.ru.md)

</div>

> [!IMPORTANT]
> This repository is the only place to download Nulltrap. Sites offering
> "Nulltrap" downloads elsewhere are not us. This warning has been here from day
> one because every project in this space has had to add it after the fake
> download sites showed up.

## Install

Download `Nulltrap.exe` from [Releases](https://github.com/L0lopop/Nulltrap/releases)
and run it. Nothing has to be installed first — the .NET runtime is inside the
file — and it never asks for administrator rights: it puts itself in your user
profile and takes over the Roblox launches from there.

To remove it: the **Remove Nulltrap** button in the launcher's settings, or the
Windows list of installed programs. It asks whether to keep your settings and
your downloads.

## Why another one

There are already several: [Bloxstrap], the original, plus [Fishstrap],
[Froststrap] and [Voidstrap]. They are good projects and Nulltrap owes them
(see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)). Nulltrap exists to do
four things they currently do not.

**Stay small.** A bootstrapper downloads a zip, unpacks it, and starts a
process. It does not need a machine-learning runtime or an embedded browser.
The dependency list is short on purpose and the download size is a number CI
checks, not a number that drifts.

**Be verifiable.** Signed releases and reproducible builds. The single largest
unsolved problem in this niche is that users cannot tell a real download from a
malicious one, and no existing project solves it technically.

**Take mods seriously.** Since Roblox introduced the [FastFlag allowlist] in
September 2025, arbitrary FastFlags no longer apply — the client ignores
anything not on Roblox's list. Mods and per-game profiles are what is left, and
they deserve better than an afterthought.

**Be extensible.** A real plugin contract, published as its own package, so
extensions do not depend on Nulltrap internals.

## What Nulltrap does not do

Nulltrap does not inject into, modify, or read the memory of the Roblox client
process. It does not bypass anti-cheat. It does not attempt to work around the
FastFlag allowlist — Roblox has said that editing flags in memory carries
consequences, and that is not a line worth crossing for a graphics setting.

The FastFlag editor validates against the allowlist and tells you plainly when
a flag will be ignored, rather than letting you believe it applied.

## Status

<img src="assets/status.svg" alt="Phases 0, 1, 2 and 3 complete, phase 4 next" width="820">

Working today: self-installation without administrator rights, Roblox and
Studio downloads with a checksum-addressed cache, protocol handlers, the
graphics settings Roblox still allows, per-game profiles that carry both
FastFlags and ordinary Roblox settings, a plugin contract with its own SDK,
a tray icon with a background mode, join notices, and an English and Russian
interface.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The exact
version is pinned in `global.json`.

```bash
dotnet build Nulltrap.slnx -c Release
```

To regenerate the application icon from the source artwork (needs Python with
Pillow):

```bash
python scripts/make-icon.py
```

## Architecture

```
src/
  Nulltrap.Core                    portable — deployment, packages, mods, flags
  Nulltrap.Platform.Abstractions   portable — what Core needs from an OS
  Nulltrap.Platform.Windows        windows  — registry, mutexes, shortcuts
  Nulltrap.Plugins.Sdk             portable — public plugin contract
  Nulltrap.App                     windows  — WPF shell
```

One rule holds the whole thing together: **`Nulltrap.Core` targets `net10.0`,
never `net10.0-windows`.** Every existing bootstrapper is a single Windows
project, which is why none of them has shipped the cross-platform support they
advertise — by the time anyone tries, Windows assumptions are everywhere.

The rule is enforced by the build, not by discipline: CI compiles `Core` and
everything under it on Linux, where a Windows dependency cannot resolve at all.
Reach for the registry from the portable half and the build goes red on the
same push.

## License

[MIT](LICENSE). Nulltrap is an unofficial project, not affiliated with or
endorsed by Roblox Corporation.

[Bloxstrap]: https://github.com/bloxstraplabs/bloxstrap
[Fishstrap]: https://github.com/fishstrap/fishstrap
[Froststrap]: https://github.com/Froststrap/Froststrap
[Voidstrap]: https://github.com/KloBraticc/Voidstrap
[FastFlag allowlist]: https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
