using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RATF.Domain;
using RATF.Application;
using RATF.Presentation;
using RATF.Infrastructure;
namespace RATF.Composition {
    public sealed class GameCompositionRoot:MonoBehaviour {
        public MatchDefinitionSO match;
        public LevelDefinitionSO level;
        public UnitDefinitionSO[] units;
        public ProviderProfileSO provider;
        public BattlePresenter presenter;
        int epoch;
        JsonLineTelemetry telemetry;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        void Awake() {
            Validate();
            if(provider!=null)presenter.Settings=new ProviderSettings {
                Mode=provider.mode,Endpoint=provider.endpoint,Model=provider.model,Timeout=provider.timeout,Interval=provider.decisionIntervalTicks
            };
            presenter.Initialize(CreateMatch,Probe);
        }
        IFactoryCommander Commander(ProviderSettings s) {
            switch(s.Mode) {
                case ProviderMode.LocalOllama:return new LocalOllamaCommander(new UnityHttpTransport(),s.Endpoint,s.Model,s.Timeout);
                case ProviderMode.RemoteGateway:return new RemoteGatewayCommander(new UnityHttpTransport(),s.Endpoint,s.SessionToken,s.Timeout);
                default:return new ScriptedCommander();
            }
        }
        public MatchController CreateMatch(ProviderSettings settings) {
            telemetry?.Dispose();
            telemetry=null;
            var defs=new Dictionary<string,UnitDefinition>();
            foreach(var u in units)defs.Add(u.id,u.ToDefinition());
            var world=new SimulationWorld(++epoch,match.ToDefinition(),level.ToDefinition(),defs);
            IFactoryCommander commander;
            string mode=settings.Mode==ProviderMode.Scripted?"scripted":settings.Mode.ToString();
            try {
                commander=Commander(settings);
            }
            catch(ArgumentException) {
                commander=new ScriptedCommander();
                mode="scripted";
                Debug.LogWarning("Invalid AI endpoint configuration; using scripted commander.");
            }
            telemetry=new JsonLineTelemetry(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath,"factory-telemetry.jsonl"),Debug.isDebugBuild);
            return new MatchController(world,commander,()=>Time.realtimeSinceStartupAsDouble,telemetry,mode,settings.Timeout,settings.Interval);
        }
        async Task<string> Probe(ProviderSettings settings) {
            if(settings.Mode==ProviderMode.Scripted)return "Scripted commander ready; no network required.";
            var o=new SnapshotBuilder().Build(new SimulationWorld(),"settings-probe","probe-"+Guid.NewGuid().ToString("N"));
            using(var ct=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)) {
                ct.CancelAfter(TimeSpan.FromSeconds(settings.Timeout));
                try {
                    var p=await Commander(settings).DecideAsync(o,ct.Token);
                    string error=new FactoryPlanValidator().Validate(p,o);
                    return error==null?"Connection and schema-contract probe passed.":"Invalid output: "+error;
                }
                catch(OperationCanceledException) {
                    return "Connection timeout or cancelled.";
                }
                catch(FormatException) {
                    return "Invalid output or unsupported schema.";
                }
                catch(Exception e) {
                    return e is InvalidOperationException||e is ArgumentException?e.Message:"Host unreachable or provider unavailable.";
                }
            }
        }
        public void Validate() {
            if(match==null)throw new InvalidOperationException("Missing match definition");
            if(level==null)throw new InvalidOperationException("Missing level definition");
            if(units==null||units.Length!=6)throw new InvalidOperationException("Expected six units, got "+(units?.Length??-1));
            if(presenter==null)throw new InvalidOperationException("Missing battle presenter");
            presenter.catalog.Validate();
            var set=new HashSet<string>();
            foreach(var u in units) {
                if(u==null||!set.Add(u.id))throw new InvalidOperationException("Invalid unit definitions");
            }
            foreach(var id in MatchDefinition.Units().Keys)if(!set.Contains(id))throw new InvalidOperationException("Missing required unit "+id);
            level.ToDefinition();
        }
        void OnDestroy() {
            lifetime.Cancel();
            lifetime.Dispose();
            presenter?.Controller?.Dispose();
            telemetry?.Dispose();
        }
    }
}
