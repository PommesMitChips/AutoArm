# AutoArm documentation

<!-- impeccable:product-schema 1 -->

This record applies to the illustrated documentation in `docs/getting-started.html` and its reusable SVG library. The underlying product remains a Space Engineers programmable-block script.

## Platform

web

## Users

Space Engineers players getting started with AutoArm and following an illustrated build sequence.

## Product Purpose

AutoArm controls mechanical arms by discovering the actuators between a named base and a named head. The requested guide takes a reader through building an arm, naming those endpoints, placing a ship controller and programmable block, loading the script, running `On`, and using movement controls.

## Operating Context

The game terminal provides block Name fields and the programmable block's Edit, Argument and Run controls. The supplied screenshots define the relevant visual references for the mockups. Existing repository documentation and source define script behavior.

## Capabilities and Constraints

- The guide follows the user's nine-step order.
- Names use `<MyArmName> - Base` and `<MyArmName> - Head`; `Arm 1` is the illustrated example.
- The example contains a three-block crossbar, two hinges, two pistons, a second three-block crossbar, then hinge, rotor, hinge, piston, rotor, hinge, a hinge with its axis turned 90 degrees, rotor and drill.
- The same example is shown in front and isometric views, with a programmable block beside the arm in the isometric view.
- SVG geometry is schematic; the example has not been validated by building it in the game.
- Step 3 places the supported arm on the left and illustrates misaligned parallel stages and hydraulic linkages on the right. The user specified these support limits and removed the earlier base warning.
- Step 6 shows the same arm in the user's reference's classical pose: the lower double-piston section bends backward at both hinge heads, the upper section folds forward, and the drill points downward.

## Brand Commitments

The user chose clean technical diagrams with recognizable game shapes and approved the initial steel-blue and amber SVG library. Use the title “Getting Started with AutoArm.” Preserve that asset style.

## Evidence on Hand

`README.md`; `src/AutoArm.Topology.cs.txt`; `src/AutoArm.Host.cs.txt`; `src/AutoArm.Control.cs.txt`; approved SVGs in `docs/assets/blocks`; two user-supplied game-terminal screenshots. Block icons in the locally installed game were inspected for the schematic shapes.
