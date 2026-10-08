using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Forester.Domain;
using Forester.Application;
using Forester.Infrastructure;
using Forester.Presentation;
using RATF.Infrastructure;
namespace Forester.Composition {
    public sealed class ForesterCompositionRoot:MonoBehaviour {
        public LevelDefinitionSO level;
        public CardCatalogSO cards;
        public VisualCatalogSO visuals;
        public ProviderProfileSO provider;
        public GamePresenter presenter;
        public void Validate() {
            if(!level||!cards||!visuals||!provider||!provider.planSchema||!presenter||!presenter.boardCamera||!presenter.hud||!presenter.hud.canvas||!presenter.viewsRoot||!presenter.placementPreviewMaterial)throw new ArgumentException("Missing Forester composition reference");
            var catalog=cards.Copy();
            PathValidator.Validate(level.Copy(),catalog);
            visuals.Validate(catalog);
        }
        void Awake() {
            try {
                Validate();
                presenter.Settings=provider.settings.Copy();
                presenter.boardCamera.GetComponent<BoardCamera>()?.Frame(level);
#if UNITY_WEBGL && !UNITY_EDITOR
                StartCoroutine(InitializeWeb());
#else
                presenter.Initialize(Create,Probe);
#endif
            }
            catch(Exception e) {
                Debug.LogError("FORESTER_AUTHORING_ERROR "+e.Message);
                enabled=false;
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [Serializable] sealed class WebConfiguration {
            public string mode,session_token,model;
        }
        System.Collections.IEnumerator InitializeWeb() {
            var origin=new Uri(UnityEngine.Application.absoluteURL);
            using(var request=UnityEngine.Networking.UnityWebRequest.Get(new Uri(origin,"/api/config").AbsoluteUri)) {
                request.timeout=5;
                yield return request.SendWebRequest();
                presenter.Settings.Mode=ProviderMode.Scripted;
                if(request.result==UnityEngine.Networking.UnityWebRequest.Result.Success) {
                    var configuration=UnityEngine.JsonUtility.FromJson<WebConfiguration>(request.downloadHandler.text);
                    if(configuration!=null&&configuration.mode=="RemoteApertus") {
                        presenter.Settings.Mode=ProviderMode.RemoteApertus;
                        presenter.Settings.Endpoint=new Uri(origin,"/forester/plan").AbsoluteUri;
                        presenter.Settings.SessionToken=configuration.session_token;
                        presenter.Settings.Model=configuration.model;
                    }
                }
                else Debug.LogWarning("Forester server configuration unavailable; starting offline.");
            }
            presenter.Initialize(Create,Probe);
        }
#endif
        ProviderOptions Options(ProviderSettings s)=>new ProviderOptions {
            Endpoint=s.Endpoint,Model=s.Model,Timeout=Math.Max(.1,Math.Min(30,s.Timeout)),Schema=provider.planSchema.text,SessionToken=s.SessionToken,VerifiedSchemaOutput=s.VerifiedSchemaOutput,SendTemperature=s.SendTemperature,SendMaxTokens=s.SendMaxTokens
        };
        IThreatCommander Commander(ProviderSettings s) {
            if(s.Mode==ProviderMode.Scripted)return new ScriptedThreatCommander();
            try {
                return s.Mode==ProviderMode.LocalApertus?(IThreatCommander)new LocalApertusCommander(new UnityHttpTransport(),Options(s)):new RemoteApertusCommander(new UnityHttpTransport(),Options(s));
            }
            catch(Exception e) {
                return new UnavailableCommander(e.Message);
            }
        }
        MatchController Create(ProviderSettings s) {
            Validate();
            return new MatchController(level.Copy(),cards.Copy(),Commander(s),s.Mode!=ProviderMode.Scripted,()=>Time.realtimeSinceStartupAsDouble,s.Timeout);
        }
        async Task<string> Probe(ProviderSettings s,CancellationToken ct) {
            var w=new SimulationWorld(level.Copy(),cards.Copy());
            var observation=ThreatObservationBuilder.Build(w,"probe",1,"probe-wave1");
            var plan=await Commander(s).PlanAsync(observation,ct);
            var valid=ThreatPlanValidator.Validate(plan,w.Board.Level,w.Board.Catalog,1,observation.request_id);
            return s.Mode==ProviderMode.Scripted?"Offline plan validated":$"Plan contract accepted: {valid.Count} threats, cost {valid.Cost}. Verify a complete wave next.";
        }
    }
}
