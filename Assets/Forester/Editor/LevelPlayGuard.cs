using System;
using UnityEngine;
using UnityEditor;
using Forester.Composition;
namespace Forester.Editor {
    [InitializeOnLoad]     public static class LevelPlayGuard {
        static LevelPlayGuard() {
            EditorApplication.playModeStateChanged+=ValidateBeforePlay;
        }
        static void ValidateBeforePlay(PlayModeStateChange state) {
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            foreach(var root in UnityEngine.Object.FindObjectsByType<ForesterCompositionRoot>(FindObjectsSortMode.None)) {
                try {
                    root.Validate();
                }
                catch(Exception error) {
                    Debug.LogError("FORESTER_AUTHORING_ERROR: "+error.Message,root);
                    EditorApplication.isPlaying=false;
                    return;
                }
            }
        }
    }
}
