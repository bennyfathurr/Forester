using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace RATF.Infrastructure {
    public sealed class UnityHttpTransport:IHttpTransport {
        [Serializable] private sealed class GatewayFailure { public string error; }
        // Called on Unity's main thread. Task.Yield captures UnitySynchronizationContext.
        public async Task<string> PostAsync(string endpoint,string json,double timeout,string token,CancellationToken ct) {
            using(var request=new UnityWebRequest(endpoint,"POST")) {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type","application/json");
                if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);
                request.timeout=(int)Math.Ceiling(timeout);
                double deadline=Time.realtimeSinceStartupAsDouble+timeout;
                var operation=request.SendWebRequest();
                while(!operation.isDone) {
                    if(ct.IsCancellationRequested) {
                        request.Abort();
                        ct.ThrowIfCancellationRequested();
                    }
                    if(Time.realtimeSinceStartupAsDouble>=deadline) {
                        request.Abort();
                        throw new TimeoutException("Request timeout");
                    }
                    if(request.downloadedBytes>65536) {
                        request.Abort();
                        throw new FormatException("Response too large");
                    }
                    await Task.Yield();
                }
                ct.ThrowIfCancellationRequested();
                if(request.result!=UnityWebRequest.Result.Success) {
                    string error=request.responseCode==401?"Authentication failure":request.responseCode==429?"Rate limited":request.responseCode==404?"Model or endpoint missing":request.responseCode==504?"Gateway timeout":request.responseCode>0?"Provider HTTP failure "+request.responseCode:"Host unreachable or TLS failure";
                    // Only expose the bounded JSON error field, never a raw HTML/provider response.
                    if(request.downloadHandler.data!=null && request.downloadHandler.data.Length<=4096) {
                        try {
                            var failure=JsonUtility.FromJson<GatewayFailure>(request.downloadHandler.text);
                            if(failure!=null && !string.IsNullOrWhiteSpace(failure.error) && failure.error.Length<=240)
                                error+=" — "+failure.error.Replace("\n"," ").Replace("\r"," ");
                        } catch(ArgumentException) { }
                    }
                    if(request.responseCode==401)error+=". Copy the session token from the currently running gateway";
                    throw new InvalidOperationException(error);
                }
                if(request.downloadHandler.data.Length>65536)throw new FormatException("Response too large");
                return request.downloadHandler.text;
            }
        }
    }
}
