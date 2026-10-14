# vrm mode setup (jarvis)

jarvis now uses compile-time symbols so only one vrm backend is compiled at a time:

- `JARVIS_VRM10` (recommended, default target)
- `JARVIS_VRM0` (compile-safe compatibility mode)
- no symbol (safe no-vrm fallback backend)

## how to switch mode

1. open **project settings > player > other settings > scripting define symbols**.
2. add exactly one symbol:
   - `JARVIS_VRM10` for vrm 1.0 projects.
   - `JARVIS_VRM0` for vrm 0.x compatibility mode.
3. if both are set, jarvis deterministically prefers `JARVIS_VRM10` and logs a warning.

## assembly behavior

- `AICompanion` (base runtime) now contains only the `VRMService` facade and safe fallback backends.
- `AICompanion.VRM10` is compiled only when `JARVIS_VRM10` is defined and contains vrm 1.0 api usage.
- this prevents unresolved vrm namespaces/types from compiling in unsupported setups.

## package cache note

do **not** edit files under `Library/PackageCache`.
use symbols/package versions to control which backend compiles instead.
