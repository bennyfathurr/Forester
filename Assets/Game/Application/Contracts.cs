using System;
using System.Threading;
using System.Threading.Tasks;
using RATF.Domain;
namespace RATF.Application {
    public interface IFactoryCommander {
        Task<FactoryPlan> DecideAsync(FactoryObservation observation,CancellationToken cancellationToken);
    }
    public interface IMatchCommandSink {
        CommandResult Submit(PlayerCommand command);
    }
    public interface IMatchReadModel {
        MatchSnapshot Snapshot();
    }
    public interface ITelemetrySink {
        void Record(TelemetryEvent item);
    }
    public sealed class TelemetryEvent {
        public string request_id,mode,kind,reason;
        public int epoch,issued_tick,receive_tick,execute_tick;
        public double latency;
        public string unit;
        public int lane,rage,oil;
        public int[] lane_deployments,gate_ticks;
        public System.Collections.Generic.Dictionary<string,int> unit_usage;
    }
    public sealed class NullTelemetry : ITelemetrySink {
        public void Record(TelemetryEvent item) {
        }
    }
    [Serializable] public sealed class FactoryAction {
        public string type,unit,slot;
        public int lane;
    }
    [Serializable] public sealed class FactoryPlan {
        public int schema_version=1;
        public string request_id;
        public FactoryAction[] actions=Array.Empty<FactoryAction>();
        public string announcement="";
    }
    [Serializable] public sealed class ObservedEntity {
        public long id;
        public string unit;
        public float path_distance;
        public int hp;
    }
    [Serializable] public sealed class ObservedSlot {
        public string id,state;
        public int revision;
    }
    [Serializable] public sealed class ObservedLane {
        public int lane,gate_hp;
        public ObservedEntity[] villagers,robots;
        public ObservedSlot[] slots;
    }
    [Serializable] public sealed class LegalOption {
        public string action,unit,slot;
        public int lane,cost;
    }
    [Serializable] public sealed class FactoryObservation {
        public int schema_version=1,epoch,issued_tick,expires_tick,factory_cooldown_ticks,seconds_remaining,refinery_hp;
        public float oil;
        public string match_id,request_id;
        public ObservedLane[] lanes;
        public LegalOption[] legal_options;
    }
}
