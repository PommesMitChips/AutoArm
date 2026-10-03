# AutoArm repository methodology

- Inspect Git status and folder instructions before editing. Preserve others'
  edits; stage only task-owned changes. Never deploy, publish, merge or update
  installed game copies implicitly.
- Edit `experimental/src/<script>/*.cs.txt`; common fragments are in that track's
  `src/shared`. Generated `_Source.txt` and `_Compact.txt` files live beside src;
  never patch them directly.
- Stable is the user-approved 1.1 non-prototype baseline. Experimental contains
  WIP and prototypes. Builds do not promote between tracks. Promote matching
  sources, dependencies and outputs only after explicit confirmation.
- Use the single root `Build.ps1`. Readable iteration: `-Track experimental
  -Scripts AutoArm -SourceOnly`. Full regression: `-Track experimental -Collection
  Release -Test`. Use `-List` for available targets.
- Retain C#6, the 100,000 UTF-16 compact limit, ScriptPack semantic/token/IL checks
  and installed SE rewriting. Report measured sizes; distinguish simulations
  from live physics. Release versions, protocol and configuration formats are
  separate identifiers.
- Keep setup, multi-arm state, cockpit input and actuator writes in AutoArm.
  Optional services request motion through the authenticated link. Stop only the
  affected arm's verified actuators; preserve neighboring owners, topology
  checks, heartbeat refusal and Safety boundaries.
- Prefer discovery and minimal configuration. Settings inherit defaults < global
  < arm. Reject unsupported formats clearly; do not add migration/compatibility
  behavior without authorization.
- Tools, generators, tests and developer records belong in tools. Guides and
  examples belong in docs. Every generated graphic/model/preview and graphic
  package belongs in docs/assets; executable tool distributions belong under
  their tool's dist. Generator code never belongs in docs/assets.
- Preserve historical tags and evidence. Ignore temporary caches/compiler output.
  Do not add archived release directories or publish implicitly.
- Communicate concisely. Use agents only when requested or required by applicable
  instructions; default requested agents to GPT 6.1 Sol, medium effort unless
  specified otherwise. Assign ownership and preserve others' work.
