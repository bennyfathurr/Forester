using System;
using RATF.Domain;
namespace RATF.Application {
    public sealed class MatchController : IMatchCommandSink,IMatchReadModel,IDisposable {
        public readonly SimulationWorld World;
        public readonly FactoryDecisionCoordinator AI;
        double accumulator;
        readonly ITelemetrySink telemetry;
        bool outcomeRecorded;
        public MatchController(SimulationWorld world,IFactoryCommander commander,Func<double> clock,ITelemetrySink telemetry=null,string mode="scripted",double timeout=8,int interval=100) {
            World=world;
            this.telemetry=telemetry??new NullTelemetry();
            AI=new FactoryDecisionCoordinator(world,commander,clock,telemetry,mode,timeout,interval);
        }
        public CommandResult Submit(PlayerCommand c) {
            var result=World.Submit(c);
            telemetry.Record(new TelemetryEvent {
                kind=result.Accepted?"purchase":"purchase_rejected",unit=c.Unit,lane=c.Lane,execute_tick=World.State.Tick,epoch=World.State.Epoch,reason=result.Reason,rage=World.State.Rage,oil=World.State.Oil
            });
            return result;
        }
        public MatchSnapshot Snapshot() {
            return World.Snapshot();
        }
        public void Advance(double delta) {
            if(World.State.Lifecycle!=Lifecycle.Running)return;
            AI.Pump();
            accumulator+=Math.Max(0,Math.Min(delta,.5));
            while(accumulator>=.05&&World.State.Lifecycle==Lifecycle.Running) {
                accumulator-=.05;
                AI.Pump();
                int before=World.Events.Count;
                World.Step();
                for(int i=before;    i<World.Events.Count;    i++)if(World.Events[i].Kind=="gate_open")telemetry.Record(new TelemetryEvent {
                    kind="gate_open",lane=(int)World.Events[i].Id,execute_tick=World.State.Tick,epoch=World.State.Epoch
                });
            }
            if(World.State.Lifecycle!=Lifecycle.Running) {
                AI.Invalidate();
                if(!outcomeRecorded) {
                    outcomeRecorded=true;
                    var snapshot=World.Snapshot();
                    telemetry.Record(new TelemetryEvent {
                        kind="outcome",reason=snapshot.Lifecycle.ToString(),epoch=snapshot.Epoch,execute_tick=snapshot.Tick,rage=snapshot.Rage,oil=snapshot.Oil,lane_deployments=snapshot.LaneDeployments,gate_ticks=snapshot.GateTicks,unit_usage=snapshot.UnitUsage
                    });
                }
            }
        }
        public void Pause(bool pause) {
            if(World.State.Lifecycle!=Lifecycle.Running&&World.State.Lifecycle!=Lifecycle.Paused)return;
            World.State.Lifecycle=pause?Lifecycle.Paused:Lifecycle.Running;
            World.State.Epoch++;
            AI.Invalidate();
            accumulator=0;
        }
        public void Dispose() {
            AI.Dispose();
            World.State.Lifecycle=Lifecycle.Disposed;
            World.Events.Clear();
        }
    }
}
