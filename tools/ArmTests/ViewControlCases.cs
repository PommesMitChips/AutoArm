using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static readonly string[] VCModes={"HEAD","HRZ","VRT"};
    static Vector3D VCVector(object script,string field)=>(Vector3D)Get(script,field)!;
    static Vector3D VCInFrame(Vector3D world,MatrixD basis)=>new Vector3D(Vector3D.Dot(world,basis.Forward),Vector3D.Dot(world,basis.Left),Vector3D.Dot(world,basis.Up));
    static void VCNear(Vector3D actual,Vector3D expected,string label,double tolerance=1e-9)=>Check((actual-expected).Length()<tolerance,label+": actual "+actual+", expected "+expected);
    static void VCCommand(object script,Rig rig,string command)=>InvokeFrame(script,rig,command,UpdateType.Terminal,0);
    static void VCTick(object script,Rig rig)=>InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
    static void VCInput(IMyShipController cockpit,Vector3 move,Vector2 mouse,float roll=0)
    {
        var values=RecordProxy.Of(cockpit).Values;values["MoveIndicator"]=move;values["RotationIndicator"]=mouse;values["RollIndicator"]=roll;
    }
    static object VCStart(Type type,out Rig rig,out IMyMotorStator rotor,out IMyPistonBase piston,out IMyTerminalBlock head,out IMyShipController cockpit)
    {
        rig=Fixtures.Serial(out rotor,out piston,out head,out cockpit);
        RecordProxy.Of(rotor).Values["WorldMatrix"]=MatrixD.CreateWorld(rotor.GetPosition(),Vector3D.Normalize(new Vector3D(1,2,-3)),Vector3D.Normalize(new Vector3D(-2,1,0)));
        RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(head.GetPosition(),Vector3D.Normalize(new Vector3D(1,.3,-.2)),Vector3D.Up);
        RecordProxy.Of(cockpit).Values["WorldMatrix"]=MatrixD.CreateWorld(cockpit.GetPosition(),Vector3D.Normalize(new Vector3D(.2,-.7,-1)),Vector3D.Up);
        var script=Start(type,rig);VCCommand(script,rig,"On");Check(Enabled(script),"View-control fixture did not enable.");return script;
    }
    static (Vector3D F,Vector3D L,Vector3D U) VCBasis(string mode,IMyTerminalBlock head,IMyShipController cockpit)
    {
        var m=mode=="HEAD"?head.WorldMatrix:cockpit.WorldMatrix;
        return mode=="VRT"?(m.Up,m.Left,-m.Forward):(m.Forward,m.Left,m.Up);
    }
    static void VCConfigure(object script,Rig rig,Action<MyIni> change)
    {
        var ini=new MyIni();Check(ini.TryParse(rig.PB.CustomData),"View fixture INI invalid.");change(ini);
        RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();VCCommand(script,rig,"Reload");
        for(int i=0;i<10;i++)VCTick(script,rig);
    }

    static void ViewControlCases(Type type)
    {
        VCKeyboardFrames(type);
        VCMouseAndRoll(type);
        VCModeAndConfig(type);
        VCControllerSelection(type);
        VCFaceAndStep(type);
        VCHomeRawInput(type);
        VCInvalidInput(type);
        Console.WriteLine("View controls: rotated HEAD/HRZ/VRT frames, translation hold, mouse and head roll, controller handover, presets, transient settings and safe refusal.");
    }

    static void VCKeyboardFrames(Type type)
    {
        var inputs=new[]{new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(0,-1,0),new Vector3(1,1,-1),new Vector3(-1,-1,1)};
        foreach(string mode in VCModes)foreach(var input in inputs)
        {
            var script=VCStart(type,out var rig,out var rotor,out _,out var head,out var cockpit);VCCommand(script,rig,"Mode "+mode);
            var b=VCBasis(mode,head,cockpit);var direction=b.F*(-input.Z)+b.L*(-input.X)+b.U*input.Y;
            if(direction.Length()>1)direction=Vector3D.Normalize(direction);
            var f=VCVector(script,"TargetF");var u=VCVector(script,"TargetU");
            // Add both position and orientation error while retaining the previously captured targets.
            var disturbed=head.WorldMatrix;disturbed.Translation+=Vector3D.Right*.08;
            disturbed=MatrixD.CreateWorld(disturbed.Translation,Vector3D.Normalize(disturbed.Forward+.15*disturbed.Left),disturbed.Up);
            RecordProxy.Of(head).Values["WorldMatrix"]=disturbed;
            b=VCBasis(mode,head,cockpit);direction=b.F*(-input.Z)+b.L*(-input.X)+b.U*input.Y;
            if(direction.Length()>1)direction=Vector3D.Normalize(direction);
            VCInput(cockpit,input,Vector2.Zero);VCTick(script,rig);
            VCNear(VCVector(script,"LastPilotLinear"),VCInFrame(direction,rotor.WorldMatrix)*Convert.ToDouble(Get(script,"MoveMps")),mode+" keyboard "+input);
            VCNear(VCVector(script,"TargetF"),f,mode+" translation changed facing");VCNear(VCVector(script,"TargetU"),u,mode+" translation changed up");
            Check(VCVector(script,"LastFeedbackLinear").LengthSquared()>1e-12,mode+" missing translation feedback.");
            Check(VCVector(script,"LastFeedbackAngular").LengthSquared()>1e-12,mode+" missing orientation feedback.");
            VCNear(VCVector(script,"LastRequestedLinear"),VCVector(script,"LastPilotLinear")+VCVector(script,"LastFeedbackLinear"),mode+" pilot/feedback addition");
        }
    }

    static void VCMouseAndRoll(Type type)
    {
        foreach(string mode in VCModes)
        {
            var script=VCStart(type,out var rig,out var rotor,out _,out var head,out var cockpit);VCCommand(script,rig,"Mode "+mode);
            double turn=Convert.ToDouble(Get(script,"TurnDeg"))*Math.PI/180;
            VCInput(cockpit,Vector3.Zero,new Vector2(1,1));VCTick(script,rig);VCNear(VCVector(script,"LastPilotAngular"),Vector3D.Zero,mode+" mouse must default Off");
            VCCommand(script,rig,"ReadMouse On");Check((bool)Get(script,"ReadMouse")!,"ReadMouse On failed.");
            foreach(var mouse in new[]{new Vector2(1,0),new Vector2(0,1),new Vector2(-1,0),new Vector2(0,-1)})
            {
                var b=VCBasis(mode=="VRT"?"HRZ":mode,head,cockpit);VCInput(cockpit,Vector3.Zero,mouse);VCTick(script,rig);
                var expected=(b.L*mouse.X-b.U*mouse.Y)*Convert.ToDouble(Get(script,"MouseSensitivity"))*turn;
                VCNear(VCVector(script,"LastPilotAngular"),VCInFrame(expected,rotor.WorldMatrix),mode+" signed mouse "+mouse);
            }
            foreach(float roll in new[]{-1f,1f})
            {
                VCInput(cockpit,Vector3.Zero,Vector2.Zero,roll);VCTick(script,rig);
                VCNear(VCVector(script,"LastPilotAngular"),VCInFrame(head.WorldMatrix.Forward,rotor.WorldMatrix)*turn*roll,mode+" Q/E must roll about actual head");
            }
            VCInput(cockpit,Vector3.Zero,new Vector2(100,-80),1);VCTick(script,rig);
            var view=VCBasis(mode=="VRT"?"HRZ":mode,head,cockpit);var combined=(view.L*100+view.U*80)*Convert.ToDouble(Get(script,"MouseSensitivity"))+head.WorldMatrix.Forward;
            VCNear(VCVector(script,"LastPilotAngular"),VCInFrame(Vector3D.Normalize(combined),rotor.WorldMatrix)*turn,mode+" combined angular cap");
            VCCommand(script,rig,"ReadMouse Toggle");Check(!(bool)Get(script,"ReadMouse")!,"ReadMouse Toggle failed.");
            VCInput(cockpit,Vector3.Zero,new Vector2(7,9));VCTick(script,rig);VCNear(VCVector(script,"LastPilotAngular"),Vector3D.Zero,mode+" disabled mouse still moved");
            VCCommand(script,rig,"ReadMouse On");VCCommand(script,rig,"ReadMouse Off");Check(!(bool)Get(script,"ReadMouse")!,"ReadMouse Off failed.");
        }
    }

    static void VCModeAndConfig(Type type)
    {
        var script=VCStart(type,out var rig,out _,out _,out _,out _);var defaults=new MyIni();Check(defaults.TryParse(rig.PB.CustomData),"Default view INI invalid.");
        Check(defaults.Get("Config","MovementMode").ToString()=="HEAD","MovementMode default must be HEAD.");
        Check(!(bool)Get(script,"ReadMouse")!&&Math.Abs(Convert.ToDouble(Get(script,"MouseSensitivity"))-.025)<1e-12,"Mouse defaults incorrect.");
        for(int i=1;i<=9;i++)Check(!defaults.ContainsKey("Config","Bind"+i),"Removed Bind"+i+" was generated.");
        foreach(string alias in new[]{"Mode","MovementMode","ControlMode"})foreach(string mode in VCModes)
        {
            var p=VCVector(script,"TargetP");var f=VCVector(script,"TargetF");var u=VCVector(script,"TargetU");
            VCCommand(script,rig,alias+" "+mode);
            Check(Get(script,"MovementMode")!.ToString()==mode&&Enabled(script),alias+" did not switch enabled mode.");
            VCNear(VCVector(script,"TargetP"),p,alias+" rebased position");VCNear(VCVector(script,"TargetF"),f,alias+" rebased facing");VCNear(VCVector(script,"TargetU"),u,alias+" rebased up");
        }
        VCConfigure(script,rig,x=>{x.Set("Config","MovementMode","VRT");x.Set("Config","ReadMouse",true);x.Set("Config","MouseSensitivity",.125);});
        Check(Get(script,"MovementMode")!.ToString()=="VRT"&&(bool)Get(script,"ReadMouse")!&&Convert.ToDouble(Get(script,"MouseSensitivity"))==.125,"Configured view settings did not load.");
        VCCommand(script,rig,"Mode HRZ");VCCommand(script,rig,"ReadMouse Off");VCCommand(script,rig,"Reload");
        Check(Get(script,"MovementMode")!.ToString()=="VRT"&&(bool)Get(script,"ReadMouse")!,"Reload retained transient overrides.");
        foreach(var bad in new[]{("MovementMode","SIDE"),("ReadMouse","maybe"),("MouseSensitivity","NaN"),("MouseSensitivity","Infinity"),("MouseSensitivity","-0.01"),("MouseSensitivity","10.01")})
        {
            var ini=new MyIni();ini.TryParse(rig.PB.CustomData);ini.Set("Config",bad.Item1,bad.Item2);string text=ini.ToString();RecordProxy.Of(rig.PB).Values["CustomData"]=text;
            VCCommand(script,rig,"Reload");VCCommand(script,rig,"On");Check(!Enabled(script),"Malformed "+bad.Item1+" enabled motion.");Check(rig.PB.CustomData==text,"Malformed view setting was rewritten.");NoVelocity(rig);
            RecordProxy.Of(rig.PB).Values["CustomData"]=defaults.ToString();VCCommand(script,rig,"Reload");for(int i=0;i<10;i++)VCTick(script,rig);
        }
        VCConfigure(script,rig,x=>{x.Set("Config","Bind1","Stop");x.Set("Config","Bind2","Forward");x.Set("Config","Bind9","On Stop");});
        VCCommand(script,rig,"On");Check(Enabled(script),"Obsolete binding keys should be ignored.");VCCommand(script,rig,"1");Check(Enabled(script),"Obsolete Bind1 changed the original numeric alias.");
    }

    static void VCControllerSelection(Type type)
    {
        var script=VCStart(type,out var rig,out var rotor,out _,out var head,out var first);VCCommand(script,rig,"Mode HRZ");
        var second=rig.Cockpit();RecordProxy.Of(second).Values["CubeGrid"]=head.CubeGrid;
        RecordProxy.Of(second).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Up,Vector3D.Right);
        RecordProxy.Of(second).Values["IsUnderControl"]=false;VCInput(first,new Vector3(0,0,-1),Vector2.Zero);VCTick(script,rig);
        VCNear(VCVector(script,"LastPilotLinear"),VCInFrame(first.WorldMatrix.Forward,rotor.WorldMatrix)*Convert.ToDouble(Get(script,"MoveMps")),"Initial cockpit selection");
        RecordProxy.Of(first).Values["IsUnderControl"]=false;RecordProxy.Of(second).Values["IsUnderControl"]=true;VCInput(second,new Vector3(0,0,-1),Vector2.Zero);
        for(int i=0;i<10;i++)VCTick(script,rig);
        VCNear(VCVector(script,"LastPilotLinear"),VCInFrame(second.WorldMatrix.Forward,rotor.WorldMatrix)*Convert.ToDouble(Get(script,"MoveMps")),"Active cockpit handover/head-grid selection");
        RecordProxy.Of(second).Values["IsUnderControl"]=false;VCTick(script,rig);VCNear(VCVector(script,"LastPilotLinear"),Vector3D.Zero,"Inactive cached cockpit still supplied input");
        var outsider=rig.Cockpit();RecordProxy.Of(outsider).Values["CubeGrid"]=rig.Grid();VCInput(outsider,new Vector3(0,0,-1),Vector2.Zero);
        var outsiderProxy=RecordProxy.Of(outsider);var outsiderCall=outsiderProxy.Call;
        outsiderProxy.Call=(method,args)=>method.Name=="IsSameConstructAs"?false:outsiderCall!(method,args);
        for(int i=0;i<10;i++)VCTick(script,rig);VCNear(VCVector(script,"LastPilotLinear"),Vector3D.Zero,"Unrelated-grid cockpit supplied input");
        var connected=rig.Cockpit();RecordProxy.Of(connected).Values["CubeGrid"]=rig.Grid();
        RecordProxy.Of(connected).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Right,Vector3D.Up);
        VCInput(connected,new Vector3(0,0,-1),Vector2.Zero);
        for(int i=0;i<10;i++)VCTick(script,rig);
        VCNear(VCVector(script,"LastPilotLinear"),VCInFrame(connected.WorldMatrix.Forward,rotor.WorldMatrix)*Convert.ToDouble(Get(script,"MoveMps")),"Connected ship-module cockpit outside the arm corridor");
        RecordProxy.Of(connected).Values["IsUnderControl"]=false;
        RecordProxy.Of(first).Values["IsUnderControl"]=true;VCCommand(script,rig,"Forward");Check(Enabled(script),"Forced direction command failed immediate cockpit selection.");
        VCNear(VCVector(script,"TargetF"),VCInFrame(first.WorldMatrix.Forward,rotor.WorldMatrix),"Immediate controller frame for Face");
    }

    static void VCFaceAndStep(Type type)
    {
        var directions=new[]{"Forward","Back","Left","Right","Up","Down"};
        foreach(string mode in VCModes)foreach(string direction in directions)
        {
            var script=VCStart(type,out var rig,out var rotor,out _,out _,out var cockpit);VCCommand(script,rig,"Mode "+mode);
            MatrixD reference=mode=="HEAD"?rotor.WorldMatrix:cockpit.WorldMatrix;
            var world=direction switch{"Forward"=>reference.Forward,"Back"=>reference.Backward,"Left"=>reference.Left,"Right"=>reference.Right,"Up"=>reference.Up,_=>reference.Down};
            VCCommand(script,rig,direction);VCNear(VCVector(script,"TargetF"),VCInFrame(world,rotor.WorldMatrix),mode+" toolbar "+direction);
        }
        foreach(string mode in VCModes)foreach(string axisName in new[]{"Yaw","Pitch","Roll"})foreach(double degrees in new[]{-7d,11d})
        {
            var script=VCStart(type,out var rig,out var rotor,out _,out _,out var cockpit);VCCommand(script,rig,"Mode "+mode);
            var f=VCVector(script,"TargetF");var u=VCVector(script,"TargetU");
            Vector3D axis=axisName=="Roll"?f:axisName=="Yaw"?(mode=="HEAD"?new Vector3D(0,0,1):VCInFrame(cockpit.WorldMatrix.Up,rotor.WorldMatrix)):
                (mode=="HEAD"?-Vector3D.Cross(u,f):VCInFrame(cockpit.WorldMatrix.Right,rotor.WorldMatrix));
            double angle=degrees*Math.PI/180;
            VCCommand(script,rig,"Step "+axisName+" "+degrees.ToString(System.Globalization.CultureInfo.InvariantCulture));
            VCNear(VCVector(script,"TargetF"),VCRotate(f,axis,angle),mode+" Step "+axisName+" facing");
            VCNear(VCVector(script,"TargetU"),VCRotate(u,axis,angle),mode+" Step "+axisName+" up");
        }
        var aliases=new[]{("1","PitchUp"),("2","Back"),("3","PitchDown"),("4","Left"),("5","Down"),("6","Right"),("7","YawLeft"),("8","Forward"),("9","YawRight")};
        foreach(var alias in aliases)
        {
            var named=VCStart(type,out var namedRig,out _,out _,out _,out _);var numbered=VCStart(type,out var numberedRig,out _,out _,out _,out _);
            VCCommand(named,namedRig,alias.Item2);VCCommand(numbered,numberedRig,alias.Item1);
            VCNear(VCVector(numbered,"TargetF"),VCVector(named,"TargetF"),"Legacy numerical alias "+alias.Item1+" facing");
            VCNear(VCVector(numbered,"TargetU"),VCVector(named,"TargetU"),"Legacy numerical alias "+alias.Item1+" up");
        }
        foreach(string mode in new[]{"HRZ","VRT"})
        {
            foreach(string command in new[]{"Forward","Step Yaw 5","Step Pitch 5"})
            {
                var script=VCStart(type,out var rig,out _,out _,out _,out var cockpit);VCCommand(script,rig,"Mode "+mode);RecordProxy.Of(cockpit).Values["IsUnderControl"]=false;
                VCCommand(script,rig,command);Check(!Enabled(script),mode+" missing active cockpit "+command+" silently fell back.");NoVelocity(rig);
            }
        }
    }

    static Vector3D VCRotate(Vector3D value,Vector3D axis,double angle)
    {
        axis=Vector3D.Normalize(axis);double cosine=Math.Cos(angle),sine=Math.Sin(angle);
        return value*cosine+Vector3D.Cross(axis,value)*sine+axis*(Vector3D.Dot(axis,value)*(1-cosine));
    }

    static void VCHomeRawInput(Type type)
    {
        foreach(string kind in new[]{"translation-zero-speed","mouse","cancelled-angular"})
        {
            var script=VCStart(type,out var rig,out var rotor,out var piston,out var head,out var cockpit);
            VCConfigure(script,rig,x=>{x.Set("Config","HeadSpeed",0);x.Set("Config","ReadMouse",true);x.Set("Config","MovementMode","HRZ");});
            if(kind=="cancelled-angular")RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(head.GetPosition(),cockpit.WorldMatrix.Left,cockpit.WorldMatrix.Up);
            VCCommand(script,rig,"SetHome");RecordProxy.Of(piston).Values["CurrentPosition"]=6f;VCCommand(script,rig,"GoHome");VCTick(script,rig);
            Check((bool)Get(script,"GoingHome")!,"Raw-input cancellation homing precondition failed.");
            if(kind=="translation-zero-speed")VCInput(cockpit,new Vector3(0,0,-1),Vector2.Zero);
            else if(kind=="mouse")VCInput(cockpit,Vector3.Zero,new Vector2(1,0));
            else
            {
                VCInput(cockpit,Vector3.Zero,new Vector2(-40,0),1);
                var args=new object[]{VCInFrame(head.WorldMatrix.Forward,rotor.WorldMatrix),VCInFrame(head.WorldMatrix.Up,rotor.WorldMatrix),Vector3D.Zero,Vector3D.Zero};
                script.GetType().GetMethod("ReadPilot",All)!.Invoke(script,args);
                VCNear((Vector3D)args[2],Vector3D.Zero,"Canceling angular contributions also generated translation");
                VCNear((Vector3D)args[3],Vector3D.Zero,"Mouse/roll cancellation must have zero net angular motion before raw-input takeover");
            }
            VCTick(script,rig);Check(!(bool)Get(script,"GoingHome")!&&!Enabled(script),kind+" did not cancel GoHome and stop.");NoVelocity(rig);
        }
    }

    static void VCInvalidInput(Type type)
    {
        foreach(string kind in new[]{"move","mouse","roll","controller-axis","head-axis"})
        {
            var script=VCStart(type,out var rig,out _,out _,out var head,out var cockpit);VCCommand(script,rig,"Mode HRZ");VCCommand(script,rig,"ReadMouse On");
            if(kind=="move")VCInput(cockpit,new Vector3(float.NaN,0,0),Vector2.Zero);
            if(kind=="mouse")VCInput(cockpit,Vector3.Zero,new Vector2(float.PositiveInfinity,0));
            if(kind=="roll")VCInput(cockpit,Vector3.Zero,Vector2.Zero,float.NaN);
            if(kind.EndsWith("axis"))
            {
                IMyTerminalBlock block=kind=="head-axis"?head:cockpit;var m=block.WorldMatrix;m.M31=double.NaN;RecordProxy.Of(block).Values["WorldMatrix"]=m;
                VCInput(cockpit,new Vector3(0,0,-1),Vector2.Zero,1);
            }
            VCTick(script,rig);Check(!Enabled(script),kind+" failed to stop unsafe control.");NoVelocity(rig);
            foreach(var block in rig.Blocks)
            foreach(var write in RecordProxy.Of(block).Writes.Where(x=>x.Name is "TargetVelocityRad" or "Velocity"))
                Check(double.IsFinite(Convert.ToDouble(write.Value)),kind+" wrote nonfinite actuator velocity.");
        }
    }
}
