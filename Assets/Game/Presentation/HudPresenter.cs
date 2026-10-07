using UnityEngine;
namespace RATF.Presentation {
    // HUD drawing helpers read normalized values supplied by the presentation controller.
    public static class HudPresenter {
        public static void Bar(Rect area,float value,Color color,Texture texture) {
            var previous=GUI.color;
            GUI.color=new Color(.15f,.2f,.27f);
            GUI.DrawTexture(area,texture);
            GUI.color=color;
            GUI.DrawTexture(new Rect(area.x,area.y,area.width*Mathf.Clamp01(value),area.height),texture);
            GUI.color=previous;
        }
    }
}
