# Identity and acknowledgements

AutoArm is an automatic arm controller for the Space Engineers programmable block. It is not an official MArmOS release or a drop-in fork of that framework.

Development began with a mining-arm adaptation of [MArmOS by Philippe117 / Timotei~](https://github.com/Philippe117/MArmOS/blob/master/Script.cs). The later rewrite replaced the original declarative hardware/controller hierarchy with mechanical-graph discovery, synchronized actuator groups, a bounded task-space solver and measured head-pose feedback. The supplied HeadOnly adaptation also informed manual-priority correction and other behavior comparisons.

This acknowledges the development history; it is not a claim that the implementation was created in isolation or that it shares absolutely no utility expressions with earlier scripts. Historical snapshots retain their original names, branding and compatibility descriptions. The current distributed script and generated configuration use AutoArm, and the current Custom Data loader does not migrate older formats.

The behavior reference under `reference/` is retained as supplied for regression comparison. Installed game assemblies and model assets are local development dependencies and are not committed to this repository.
