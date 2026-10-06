# Contributing

## Adding a laptop

1. Open a **New laptop support** issue (the template asks for `tools\support-info.cmd` output and,
   ideally, OMEN Gaming Hub's background log with every mode clicked once).
2. The issue's evidence becomes one `PlatformProfile` entry in `src/Platform.cs`, in a PR whose body
   lists the proving log line for every byte. Anyone can raise that PR; nothing in it is guesswork.
3. The PR is merged only after the checklist in it has been run on the real machine by the issue author.

Everything model-specific lives in `src/Platform.cs`: verified profiles, the board families generic mode uses,
and the firmware probes. Please don't special-case a model anywhere else.

Two fields there are about the optional driver rather than the firmware, and both are applied whichever way a
profile was built:

- `DriverFor` says what this board's mailbox refuses and the driver can do instead, today only
  `DriverFor.FanLevels`, and only for boards whose firmware has been seen refusing `0x2E`. It is what puts the
  "this board needs a driver" note on the Home page, so it wants an issue number in a comment beside it.
- `Ec` is the embedded-controller register map. Being given one earns a board nothing on its own: nothing is
  written until `EmbeddedController.Verify` has recognised the registers on the machine itself. That check is
  what `tools\drivertest.cmd` prints, so a contributed `drivertest.txt` is the evidence for it.

## Code

- C# 5 only: the project builds with the compiler that ships inside Windows (`build.cmd`), no SDK.
- Every firmware write must be backed by a primary source (OGH's own log or code) and, for anything
  touching fans, a measurement on the device. `docs/research.md` documents the interface.
- Keep the UI free of prose; short labels with a subscript for context.

To see what generic mode would build for a board without running on it: `preview\Seal.exe --demo --board 8A25`
and read `preview\seal.log`.

## Reporting a problem

Use the **Bug report** template and attach `seal.log` (no personal data in it). If fans or temperatures
did anything surprising, say when: the log shows whether the thermal guard engaged. If you have the driver
installed, add `tools\drivertest.cmd` output, a temperature that looks wrong is usually answered by the one
line in it that compares the CPU's own sensor against the one Windows exposes.
