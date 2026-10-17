# G08-R1 streamed feature width integrity

UTC: 2026-09-24T08:53:39.5803934Z
Evidence: production mesh construction in Edit Mode; manual visual acceptance remains pending.
PASS: Joined accepted features retain both copies of their shared endpoint and their independent widths.
PASS: Road and river meshes retain widths of 4 m and 12 m in one cell.
PASS: The same 4 m span remains identical when the wider neighbor is absent.
PASS: Repeated break indices do not split or discard the following span.
PASS: Misaligned width metadata fails closed.
PASS: The root registry's approved Nordic path resolves through its pack shard, retains the imported texture, and leaves the source material unchanged.
PASS: Imported moving ripple texture T_Water_01a_N is bound to the water shader with positive flow speed.
PASS: The saved lake builds a surface with full interior shader coverage; river banks interpolate from 0 to 1 with full coverage.
Preview: Docs/G08_R1_Surface_Contract_Preview.png (isolated shader/material evidence; not a gameplay screenshot).

Result: PASS
