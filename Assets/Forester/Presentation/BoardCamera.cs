using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Forester.Presentation {
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCamera : MonoBehaviour {
        LevelDefinitionSO configuration;
        Camera cameraView;
        Vector3 home, target, focus;
        float yaw, pitch, distance, homeDistance, currentDistance, currentYaw, currentPitch;
        bool orbiting, panning;
        public bool IsNavigating => orbiting || panning;
        public float Pitch => pitch;
        public float Distance => distance;
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        public void Frame(LevelDefinitionSO level) {
            configuration = level;
            cameraView = GetComponent<Camera>();
            // A partial base-camera viewport leaves the rest of the Metal target undefined.
            cameraView.rect = new Rect(0,0,1,1);
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.fieldOfView = level.fieldOfView;
            cameraView.orthographic = level.orthographic;
            home = level.origin + new Vector3((level.columns-1)*level.cellSize*.5f,0,(level.rows-1)*level.cellSize*.5f);
            var rotation = Quaternion.Euler(level.pitch, level.yaw, 0);
            var extents = new Vector3(level.columns*level.cellSize*.5f, 2, level.rows*level.cellSize*.5f);
            float tangent = Mathf.Tan(level.fieldOfView*.5f*Mathf.Deg2Rad);
            homeDistance = 1;
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2) {
                var point = Quaternion.Inverse(rotation)*Vector3.Scale(extents,new Vector3(x,y,z));
                homeDistance = Mathf.Max(homeDistance, Mathf.Abs(point.y)/tangent-point.z, Mathf.Abs(point.x)/(tangent*cameraView.aspect)-point.z);
            }
            homeDistance *= 1.18f;
            cameraView.nearClipPlane = .1f;
            cameraView.farClipPlane = 300;
            ResetView();
        }
        public void ResetView() {
            if(!configuration) return;
            target = focus = home;
            yaw = currentYaw = configuration.yaw;
            pitch = currentPitch = Mathf.Clamp(configuration.pitch,25,80);
            distance = currentDistance = homeDistance;
            Apply();
        }
        public void Orbit(Vector2 delta) {
            yaw += delta.x*.22f;
            pitch = Mathf.Clamp(pitch-delta.y*.18f,25,80);
        }
        public void Zoom(float steps) { distance = Mathf.Clamp(distance*Mathf.Exp(-steps*.12f),homeDistance*.35f,homeDistance*1.5f); }
        bool OverUI(Vector2 pointer) {
            if(!EventSystem.current) return false;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pointer},hits);
            return hits.Count > 0;
        }
        void Update() {
            if(!configuration) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            bool over = mouse != null && OverUI(mouse.position.ReadValue());
            if(mouse != null) {
                if(mouse.rightButton.wasPressedThisFrame && !over) orbiting=true;
                if(mouse.middleButton.wasPressedThisFrame && !over) panning=true;
                if(!mouse.rightButton.isPressed) orbiting=false;
                if(!mouse.middleButton.isPressed) panning=false;
                if(orbiting) Orbit(mouse.delta.ReadValue());
                if(panning) Pan(-mouse.delta.ReadValue()*.035f*(distance/homeDistance));
                if(!over) Zoom(mouse.scroll.ReadValue().y/120f);
            }
            if(keyboard != null && !over) {
                if(keyboard.homeKey.wasPressedThisFrame) ResetView();
                yaw += ((keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0))*65*Time.unscaledDeltaTime;
                Pan(new Vector2((keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0),(keyboard.upArrowKey.isPressed?1:0)-(keyboard.downArrowKey.isPressed?1:0))*Time.unscaledDeltaTime*10);
            }
            float blend = 1-Mathf.Exp(-12*Time.unscaledDeltaTime);
            focus = Vector3.Lerp(focus,target,blend);
            currentYaw = Mathf.Lerp(currentYaw,yaw,blend);
            currentPitch = Mathf.Lerp(currentPitch,pitch,blend);
            currentDistance = Mathf.Lerp(currentDistance,distance,blend);
            Apply();
        }
        void Pan(Vector2 shift) {
            var rotation = Quaternion.Euler(0,yaw,0);
            target += rotation*new Vector3(shift.x,0,shift.y);
            target.x = Mathf.Clamp(target.x,home.x-configuration.columns*configuration.cellSize*.4f,home.x+configuration.columns*configuration.cellSize*.4f);
            target.z = Mathf.Clamp(target.z,home.z-configuration.rows*configuration.cellSize*.4f,home.z+configuration.rows*configuration.cellSize*.4f);
        }
        void Apply() {
            transform.rotation = Quaternion.Euler(currentPitch,currentYaw,0);
            transform.position = focus-transform.forward*currentDistance-transform.up*3.5f;
            cameraView.orthographicSize = currentDistance*Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad);
        }
    }
}
