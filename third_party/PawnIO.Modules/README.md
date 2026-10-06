# PawnIO modules

Signed module binaries from https://github.com/namazso/PawnIO.Modules, release 0.2.11, unmodified. They are
embedded in `Seal.exe` as resources (see `build.cmd`) and loaded into the PawnIO driver at run time. The
official PawnIO driver accepts only modules signed by its author, which is why these are the release
binaries and not a build of our own.

| File | Source | SHA-256 |
|---|---|---|
| `LpcACPIEC.bin` | `LpcACPIEC.p`, byte read/write on the ACPI EC ports 0x62 / 0x66 | `c38fd116e7aff4d1fdb0a494e296be0a6708e5a22fc72f14587442fb7f8f7906` |
| `IntelMSR.bin` | `IntelMSR.p`, Intel MSR read (allow-listed) and write (six registers) | `d6ed85d65ab17a22f813ef98207d6d537155ee2ded5976a21cb48413c9b92e5f` |
| `AMDFamily17.bin` | `AMDFamily17.p`, AMD family 17h–1Ah MSR read and SMN read | `dae74615761b78bdf064dfb3e136252ddcc6fc727d88f14738d0e5800d427a91` |

Licence: LGPL-2.1-or-later, see `COPYING`. The driver itself (https://pawnio.eu) is not part of this
repository: Seal downloads its signed installer from https://github.com/namazso/PawnIO.Setup when the owner
asks for it, and never otherwise.
