using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace Forester.Presentation {
    public static class CanvasWidgets {
        public static readonly Color Panel=new Color(.025f,.065f,.075f,.96f),Accent=new Color(.25f,.84f,.7f);
        public static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h) {
            var go=new GameObject(name,typeof(RectTransform));
            var r=go.GetComponent<RectTransform>();
            r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y);
            r.sizeDelta=new Vector2(w,h);
            return r;
        }
        public static GameObject PanelAt(string name,Transform parent,float x,float y,float w,float h) {
            var r=Rect(name,parent,x,y,w,h);
            r.gameObject.AddComponent<Image>().color=Panel;
            return r.gameObject;
        }
        public static Text Label(string name,Transform parent,float x,float y,float w,float h,int size=18) {
            var r=Rect(name,parent,x,y,w,h);
            var t=r.gameObject.AddComponent<Text>();
            t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize=size;
            t.color=Color.white;
            t.supportRichText=false;
            t.raycastTarget=false;
            return t;
        }
        public static Button Button(string name,Transform parent,float x,float y,float w,float h,UnityAction action) {
            var r=Rect(name,parent,x,y,w,h);
            var im=r.gameObject.AddComponent<Image>();
            im.color=new Color(.10f,.24f,.24f);
            var b=r.gameObject.AddComponent<Button>();
            b.onClick.AddListener(action);
            var t=Label("Label",r,8,7,w-16,h-10,16);
            t.text=name;
            t.alignment=TextAnchor.MiddleCenter;
            t.fontStyle=FontStyle.Bold;
            var colors=b.colors;
            colors.highlightedColor=new Color(.65f,1,.85f);
            colors.pressedColor=new Color(.3f,.75f,.6f);
            b.colors=colors;
            return b;
        }
        public static InputField Input(string name,Transform parent,float x,float y,float w,string initial,bool secret=false) {
            var r=Rect(name,parent,x,y,w,38);
            r.gameObject.AddComponent<Image>().color=new Color(.13f,.22f,.23f);
            var text=Label("Value",r,10,8,w-20,26,16);
            var input=r.gameObject.AddComponent<InputField>();
            input.textComponent=text;
            input.text=initial;
            input.characterLimit=512;
            input.contentType=secret?InputField.ContentType.Password:InputField.ContentType.Standard;
            return input;
        }
    }
}
