using System;
using System.IO;
using RATF.Application;
namespace RATF.Infrastructure {
    public sealed class JsonLineTelemetry:ITelemetrySink,IDisposable {
        readonly StreamWriter writer;
        readonly bool verbose;
        public JsonLineTelemetry(string path,bool verbose=false) {
            this.verbose=verbose;
            writer=new StreamWriter(path,true) {
                AutoFlush=true
            };
        }
        public void Record(TelemetryEvent item) {
            if(verbose||item.kind=="outcome"||item.kind=="purchase"||item.kind=="gate_open")writer.WriteLine(PlanJson.Serialize(item));
        }
        public void Dispose() {
            writer.Dispose();
        }
    }
}
