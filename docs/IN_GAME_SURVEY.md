# Survey two arms before motion testing

Use one Arm PB for both arms and keep Collision PB enabled. The survey is a separate, temporary PB. It reads equipment and ArmService poses; it does not drive joints, switch tools, stop an arm or start a path.

1. Install the current Arm and Collision scripts. Configure both arms on the same Arm PB. For the second arm, use its own base and head names, such as `Arm 2 - Base - Rotor` and `Arm 2 - Head - Drill`. Tool-equipped arms can use their usual numbered head markers.
2. Name a temporary programmable block **`Arm Survey`** and paste the entire [AutoArm_Survey.txt](../experimental/AutoArm_Survey_Source.txt) into it.
3. In the Arm PB's existing `[global]` section, retain the Safety peer and add the observer:

   ```ini
   Peers=
   |Collision PB | Safety
   |Arm Survey | Observe
   ```

   Retain other peer rows too. If an arm has its own `Peers` key, that list overrides the global list: add both rows there instead. Use your actual Collision PB name if it differs.

4. Run `On(Arm 1)` and `On(Arm 2)` on the **Arm PB** to load the peer settings and ready both arms. Let both settle. Keep cockpit controls neutral, leave the ship stationary and do not build, edit blocks or swap tools during the survey. Collision PB remains running.
5. Run `Survey` on **Arm Survey**. When it reports completion, copy **all of Arm Survey's Custom Data** and send that text back. The report follows the `---` separator. You can also save it as a `.txt` file. `Page 1`, `Page 2`, etc. preview the report on the PB's terminal output.

The survey automatically finds a single Arm PB configured with both arms. Its generated Custom Data starts with:

```ini
[Survey]
Format=1
ArmPB=
Arms=Arm 1|Arm 2
CellLimit=262144
```

Set `ArmPB` to an exact PB name or `@EntityId` if discovery is ambiguous. Change `Arms` if your arm names differ; keep names separated by `|`. Both must belong to that same owner PB. Names with spaces work.

The report includes each arm's actual starting head pose, block/grid geometry, joint connections, native positions and limits, occupied grid cells, cockpit/gravity readings and configuration. It also compares ending poses and attachments against the initial samples. A bare arm uses the focus reported by ArmService, rather than assuming a named tool is attached.

If it says **movement detected**, wait for the arms to settle and run `Survey` again. Endpoint checks cannot rule out movement that returned to its starting point. If `CellLimit` is reached, increase that setting, up to `2000000`, and retry. The limit counts inspected cells inside grid bounds, including empty cells. A failed or cancelled survey retains the previous completed report; check the latest terminal status before submitting it. `Cancel` only cancels this survey.

The observer verifies that a Safety peer is configured and accessible; it does not certify that clearance guidance is current or that a proposed route is safe. Collision remains responsible for checking motion when we run the later test.

## The motion test after the report

The report defines the next script's starting poses and excursions. The [test runner](IN_GAME_BENCH.md) submits head position/orientation paths to the **same ArmService PB**, with Collision/Safety enabled. It keeps separate progress and results for each arm, uses matching head displacements in a common reference frame, and finishes each successful sequence at its starting head pose. Returning the head pose does not necessarily restore every redundant joint angle.

Do not start a motion test from guessed coordinates. The survey deliberately contains no test path. The later runner will report a blocked or cancelled run instead of forcing a return through an obstacle or after an emergency stop.
