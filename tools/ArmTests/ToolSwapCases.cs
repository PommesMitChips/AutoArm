using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
internal static partial class Scenarios
{
    static object SwapController(object script)=>Get(script,"Tools")!;
    static string SwapPhase(object script)=>(string)Get(SwapController(script),"Phase")!;
    static void SwapStationUntouched(ToolSwapFixture f)=>Check(!f.StationWritten,"Controller wrote a station merge.");
    static MatrixD SwapGoal(object script,string field)
    {
        var tools=SwapController(script); string anchor=field=="Dock" || field=="Approach"?"DockAnchor":field=="Retreat"?"RetreatAnchor":"AttachAnchor";
        return (MatrixD)Get(tools,field)! * (MatrixD)Get(Get(tools,anchor)!,"WorldMatrix")!;
    }
    static void NativeReverseGeometry(Type type)
    {
        string[] bases={"LargeStator","SmallStator","LargeAdvancedStator","SmallAdvancedStator","SmallAdvancedStatorSmall"};
        var dummies=new[]{new Vector3D(1.033530949712258e-7,.4208642244338989,1.3662192088759184e-7),new Vector3D(2.17650750755638e-7,.036039892584085464,3.7548051068370114e-7),new Vector3D(1.033530949712258e-7,.19979000091552734,1.2796517978586053e-7),Vector3D.Zero,new Vector3D(2.553320221920785e-8,.047984808683395386,5.927423671892029e-7)};
        var geometry=type.GetNestedType("ToolGeometry",All)!; var info=type.GetNestedType("ToolTopInfo",All)!;
        var capture=info.GetMethod("Capture",All)!; var inverse=geometry.GetMethod("TryTopPose",All)!;
        for(int index=0;index<bases.Length;index++) foreach(bool large in new[]{false,true})
        {
            bool hinge=index>=5;
            string[] heads=hinge?(large?new[]{"LargeHingeHead"}:new[]{"MediumHingeHead","SmallHingeHead"}):(large?new[]{"LargeRotor","LargeAdvancedRotor"}:new[]{"SmallRotor","SmallAdvancedRotor","SmallAdvancedRotorSmall"});
            foreach(string head in heads) foreach(double q in new[]{-.4,.4})
            {
                var f=new ToolSwapFixture(); var c=f.Couplers[1];
                RecordProxy.Of(c).Values["BlockDefinition"]=ToolSwapFixture.Definition(bases[index]); RecordProxy.Of(c).Values["Angle"]=(float)q;
                RecordProxy.Of(f.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition(head);
                RecordProxy.Of(f.ArmGrid).Values["GridSize"]=large?2.5f:.5f; RecordProxy.Of(f.ArmGrid).Values["GridSizeEnum"]=large?VRage.Game.MyCubeSize.Large:VRage.Game.MyCubeSize.Small;
                var tipPose=f.ArmTip.WorldMatrix; tipPose.Translation=f.ArmGrid.GridIntegerToWorld(f.ArmTip.Position); RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=tipPose;
                var refPose=f.ArmRef.WorldMatrix; refPose.Translation=f.ArmGrid.GridIntegerToWorld(f.ArmRef.Position); RecordProxy.Of(f.ArmRef).Values["WorldMatrix"]=refPose;
                var top=capture.Invoke(null,new object[]{f.ArmRef,f.ArmTip,f.Rig.Blocks})!; var local=(MatrixD)Get(top,"Local")!;
                double lo=hinge?0:index==3?-.02:(index==0 || index==2) && large?-.4:-.11;
                double hi=hinge?0:(index==0 || index==2) && large?.2:.11;
                foreach(double displacement in new[]{lo,0,hi}.Distinct())
                {
                    RecordProxy.Of(c).Values["Displacement"]=(float)displacement;
                    var args=new object?[]{c,top,MatrixD.Identity,"",false}; bool ok=(bool)inverse.Invoke(null,args)!;
                    Check(ok,"Native inverse refused supported "+bases[index]+"/"+head+" displacement "+displacement+": "+args[3]);
                    var desiredTip=local*(MatrixD)args[2]!;
                    var dummy=hinge?Vector3D.Zero:dummies[index]; double offset=index==1 || index==4?.045:index==3?.11:0;
                    var expected=c.GetPosition()+Vector3D.TransformNormal(dummy+Vector3D.Up*(c.Displacement-offset),c.WorldMatrix);
                    Check(Vector3D.Distance(desiredTip.Translation,expected)<1e-7,"Reverse coupling pivot does not match native stator dummy and displacement.");
                    var forward=desiredTip.Forward*Math.Cos(c.Angle)+Vector3D.Cross(desiredTip.Up,desiredTip.Forward)*Math.Sin(c.Angle);
                    Check(Vector3D.Dot(forward,c.WorldMatrix.Forward)>1-1e-8 && Vector3D.Dot(desiredTip.Up,c.WorldMatrix.Up)>1-1e-8,"Signed retained angle inverse failed native orientation roundtrip.");
                }
                foreach(double bad in new[]{lo-.001,hi+.001,double.NaN})
                {
                    RecordProxy.Of(c).Values["Displacement"]=(float)bad; var args=new object?[]{c,top,MatrixD.Identity,"",false};
                    Check(!(bool)inverse.Invoke(null,args)!,"Native inverse accepted clamping/nonfinite displacement for "+bases[index]+"/"+head);
                }
                RecordProxy.Of(c).Values["Displacement"]=0f; RecordProxy.Of(c).Values["LowerLimitRad"]=.6f; RecordProxy.Of(c).Values["UpperLimitRad"]=.8f;
                var limits=new object?[]{c,top,MatrixD.Identity,"",false}; Check(!(bool)inverse.Invoke(null,limits)!,"Native inverse accepted retained angle outside real tool mount limits.");
            }
        }
    }
}
