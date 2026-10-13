# VRM Mode Setup (Jarvis)

Jarvis now uses compile-time symbols so only one VRM backend is compiled at a time:

- `JARVIS_VRM10` (recommended, default target)
- `JARVIS_VRM0` (compile-safe compatibility mode)
- no symbol (safe no-VRM fallback backend)

## How to switch mode

1. Open **Project Settings > Player > Other Settings > Scripting Define Symbols**.
2. Add exactly one symbol:
   - `JARVIS_VRM10` for VRM 1.0 projects.
   - `JARVIS_VRM0` for VRM 0.x compatibility mode.
3. If both are set, Jarvis deterministically prefers `JARVIS_VRM10` and logs a warning.

## Assembly behavior

- `AICompanion` (base runtime) now contains only the `VRMService` facade and safe fallback backends.
- `AICompanion.VRM10` is compiled only when `JARVIS_VRM10` is defined and contains VRM 1.0 API usage.
- This prevents unresolved VRM namespaces/types from compiling in unsupported setups.

## Package cache note

Do **not** edit files under `Library/PackageCache`.
Use symbols/package versions to control which backend compiles instead.
