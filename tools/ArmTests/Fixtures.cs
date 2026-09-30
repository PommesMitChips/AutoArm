using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

internal static class Fixtures
{
    internal static Rig Serial(out IMyMotorStator rotor, out IMyPistonBase piston, out IMyTerminalBlock head, out IMyShipController cockpit)
    {
        var rig = new Rig(); var shoulder=rig.Grid();var tool=rig.Grid();
        rotor=rig.Rotor("Arm 1 - Base - Rotor",rig.Root,shoulder);
        piston=rig.Piston("unnamed piston",shoulder,tool,new Vector3D(0,0,-2),Vector3D.Forward);
        head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",tool,new Vector3D(0,0,-10));
        cockpit=rig.Cockpit();return rig;
    }
    internal static Rig Parallel(bool flipSecond,out IMyMotorStator first,out IMyMotorStator second)
    {
        var rig=new Rig();var tool=rig.Grid();
        first=rig.Rotor("Arm 1 - Base - Hinge",rig.Root,tool,Vector3D.Zero,Vector3D.Up);
        second=rig.Rotor("unnamed partner",rig.Root,tool,new Vector3D(0,2,0),flipSecond?Vector3D.Down:Vector3D.Up);
        rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",tool,new Vector3D(0,0,-8));
        rig.Cockpit();return rig;
    }
    internal static Rig Rails(bool unequal=false,bool rotaryBranch=false)
    {
        var rig=new Rig();var shoulder=rig.Grid();var left=rig.Grid();var right=rig.Grid();var join=rig.Grid();
        rig.Rotor("Arm 1 - Base - Rotor",rig.Root,shoulder);
        rig.Piston("Piston L2",shoulder,left,new Vector3D(-2,0,0),Vector3D.Forward);
        rig.Piston("Piston L1",left,join,new Vector3D(-2,0,-5),Vector3D.Forward);
        if(rotaryBranch)rig.Rotor("rotary branch",shoulder,right,new Vector3D(2,0,0),Vector3D.Forward);
        else rig.Piston("Piston R2",shoulder,unequal?join:right,new Vector3D(2,0,0),Vector3D.Forward);
        if(!unequal)rig.Piston("Piston R1",right,join,new Vector3D(2,0,-5),Vector3D.Forward);
        rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",join,new Vector3D(0,0,-12));
        rig.Cockpit();return rig;
    }
    internal static Rig FullMixed()
    {
        var rig=new Rig();var g=rig.Root;
        var top=rig.Grid();rig.Rotor("Arm 1 - Base - Rotor",g,top);g=top;
        top=rig.Grid();rig.Rotor("Hinge L",g,top,new Vector3D(0,0,-5),Vector3D.Right);
        rig.Rotor("Hinge R",g,top,new Vector3D(2.5,0,-5),Vector3D.Right);g=top;
        var left=rig.Grid();var right=rig.Grid();top=rig.Grid();
        rig.Piston("L2",g,left,new Vector3D(-2,0,-5),Vector3D.Forward);
        rig.Piston("R2",g,right,new Vector3D(2,0,-5),Vector3D.Forward);
        rig.Piston("L1",left,top,new Vector3D(-2,0,-10),Vector3D.Forward);
        rig.Piston("R1",right,top,new Vector3D(2,0,-10),Vector3D.Forward);g=top;
        for(int i=4;i<12;i++)
        {
            top=rig.Grid();
            if(i==6)rig.Piston("segment 2",g,top,new Vector3D(0,0,-15-i),Vector3D.Forward);
            else rig.Rotor("joint "+i,g,top,new Vector3D(0,0,-15-i),i%2==0?Vector3D.Up:Vector3D.Right);
            g=top;
        }
        rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",g,new Vector3D(0,0,-30));
        rig.Cockpit();return rig;
    }
    internal static Rig MixedBranches(bool offsetRotaryAxis=false)
    {
        var rig=new Rig();var shoulder=rig.Grid();var join=rig.Grid();
        rig.Rotor("Arm 1 - Base - Rotor",rig.Root,shoulder);
        for(int side=0;side<2;side++)
        {
            var a=rig.Grid();var b=rig.Grid();double x=side==0?-2:2;
            rig.Piston("branch piston "+side,shoulder,a,new Vector3D(x,0,0),Vector3D.Forward);
            // Pivots separated along their common axis represent coaxial stabilizing hinges.
            rig.Rotor("branch hinge "+side,a,b,new Vector3D(x,offsetRotaryAxis&&side==1?1:0,-5),Vector3D.Right);
            rig.Piston("distal piston "+side,b,join,new Vector3D(x,0,-8),Vector3D.Forward);
        }
        rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",join,new Vector3D(0,0,-15));
        rig.Cockpit();return rig;
    }
}
