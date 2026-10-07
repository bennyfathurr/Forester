using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RATF.Domain;
namespace RATF.Application {
    public enum RequestState {
        Idle,Pending,Accepted,Failed,Expired,Cancelled
    }
    public sealed class FactoryDecisionCoordinator : IDisposable {
        sealed class Result {
            public int Serial;
            public FactoryPlan Plan;
            public string Error;
        }
        sealed class Queued {
            public FactoryAction Action;
            public int Deadline,Revision,Epoch;
            public string Request;
        }
        readonly SimulationWorld world;
        readonly IFactoryCommander commander;
        readonly ScriptedCommander fallback=new ScriptedCommander();
        readonly ITelemetrySink telemetry;
        readonly Func<double> clock;
        readonly SnapshotBuilder snapshots=new SnapshotBuilder();
        readonly FactoryPlanValidator validator=new FactoryPlanValidator();
        readonly ConcurrentQueue<Result> inbox=new ConcurrentQueue<Result>();
        readonly Queue<Queued> queue=new Queue<Queued>();
        readonly string matchId=Guid.NewGuid().ToString();
        readonly string mode;
        readonly double timeout;
        readonly int interval;
        CancellationTokenSource cancellation;
        FactoryObservation pending;
        int serial,nextDecision,requests;
        double started;
        bool disposed;
        public RequestState LastRequestState {
            get;
            private set;
        }
        =RequestState.Idle;
        public string Status {
            get;
            private set;
        }
        ="scripted";
        public FactoryDecisionCoordinator(SimulationWorld world,IFactoryCommander commander,Func<double> clock,ITelemetrySink telemetry=null,string mode="scripted",double timeout=8,int interval=100) {
            this.world=world;
            this.commander=commander;
            this.clock=clock;
            this.telemetry=telemetry??new NullTelemetry();
            this.mode=mode;
            this.timeout=timeout;
            this.interval=Math.Max(20,interval);
        }
        public void Pump() {
            if(disposed||world.State.Lifecycle!=Lifecycle.Running)return;
            while(inbox.TryDequeue(out var result)) {
                if(pending==null||result.Serial!=serial)continue;
                if(pending.epoch!=world.State.Epoch) {
                    Invalidate();
                    continue;
                }
                if(clock()-started>=timeout||world.State.Tick>=pending.expires_tick) {
                    Fail("Expired");
                    continue;
                }
                var error=result.Error??validator.Validate(result.Plan,pending);
                if(error!=null) {
                    Fail(error);
                    continue;
                }
                Accept(result.Plan,pending);
                Settle("Accepted","");
            }
            if(pending!=null&&(clock()-started>=timeout||world.State.Tick>=pending.expires_tick))Fail("Timeout or plan expiry");
            while(queue.Count>0) {
                var q=queue.Peek();
                if(q.Epoch!=world.State.Epoch||world.State.Tick>=q.Deadline) {
                    queue.Dequeue();
                    Log("queue_expiry",q.Request);
                    continue;
                }
                if(world.State.Tick<world.State.FactoryReady)break;
                queue.Dequeue();
                var r=world.Submit(new PlayerCommand(q.Action.unit,q.Action.lane,q.Action.slot,revision:q.Revision));
                Log(r.Accepted?"execute":"reject",q.Request,r.Reason);
                Status=r.Accepted?"executing":"action rejected";
                break;
            }
            if(pending==null&&world.State.Tick>=nextDecision) {
                nextDecision=world.State.Tick+interval;
                var o=snapshots.Build(world,matchId,"req-"+(++serial),interval);
                if(mode=="scripted"||requests>=80) {
                    Accept(fallback.Decide(o),o);
                    Status=requests>=80?"scripted (request limit)":"scripted";
                }
                else {
                    requests++;
                    pending=o;
                    started=clock();
                    cancellation=new CancellationTokenSource();
                    Status="planning";
                    LastRequestState=RequestState.Pending;
                    Log("issue",o.request_id);
                    var token=cancellation.Token;
                    _ = Dispatch(o,serial,token);
                }
            }
        }
        async Task Dispatch(FactoryObservation o,int id,CancellationToken ct) {
            try {
                var p=await commander.DecideAsync(o,ct).ConfigureAwait(false);
                inbox.Enqueue(new Result {
                    Serial=id,Plan=p
                });
            }
            catch(Exception e) {
                inbox.Enqueue(new Result {
                    Serial=id,Error=e is OperationCanceledException?"Cancelled":e.Message
                });
            }
        }
        void Accept(FactoryPlan plan,FactoryObservation o) {
            queue.Clear();
            foreach(var a in plan.actions) {
                int rev=-1;
                if(a.type=="build")rev=o.lanes.First(l=>l.lane==a.lane).slots.First(s=>s.id==a.slot).revision;
                queue.Enqueue(new Queued {
                    Action=a,Deadline=o.expires_tick,Revision=rev,Epoch=o.epoch,Request=o.request_id
                });
            }
            Status=plan.actions.Length==0?"waiting":"executing";
        }
        void Fail(string error) {
            var id=pending.request_id;
            Settle(error=="Expired"||world.State.Tick>=pending.expires_tick?"Expired":"Failed",error);
            var fresh=snapshots.Build(world,matchId,id+"-fallback",interval);
            Accept(fallback.Decide(fresh),fresh);
            Status="timeout/fallback";
            Log("fallback",fresh.request_id,error);
        }
        void Settle(string kind,string reason) {
            if(Enum.TryParse(kind,out RequestState state))LastRequestState=state;
            Log(kind,pending.request_id,reason);
            pending=null;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation=null;
        }
        void Log(string kind,string request,string reason="") {
            telemetry.Record(new TelemetryEvent {
                kind=kind,request_id=request,mode=mode,epoch=world.State.Epoch,issued_tick=pending?.issued_tick??world.State.Tick,receive_tick=world.State.Tick,execute_tick=kind=="execute"?world.State.Tick:0,reason=reason,latency=pending==null?0:clock()-started
            });
        }
        public void Invalidate() {
            serial++;
            if(pending!=null)Settle("Cancelled","Lifecycle changed");
            queue.Clear();
            while(inbox.TryDequeue(out _)) {
            }
            nextDecision=world.State.Tick;
        }
        public void Dispose() {
            if(disposed)return;
            Invalidate();
            disposed=true;
        }
    }
}
