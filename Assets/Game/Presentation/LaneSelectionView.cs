using UnityEngine;
namespace RATF.Presentation {
    public sealed class LaneSelectionView:MonoBehaviour {
        public int HoveredLane {
            get;
            private set;
        }
        public int Pick(Camera camera,Vector2 screenPosition,float[] laneY) {
            HoveredLane=0;
            var ray=camera.ScreenPointToRay(screenPosition);
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance)) {
                var point=ray.GetPoint(distance);
                if(point.x>=0&&point.x<=26)for(int i=0;    i<laneY.Length;    i++)if(Mathf.Abs(point.z-laneY[i])<1.2f)HoveredLane=i+1;
            }
            return HoveredLane;
        }
    }
}
