using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Forester.Domain;
namespace Forester.Presentation {
    public sealed class CardTrayPresenter:MonoBehaviour {
        public readonly Dictionary<string,Button> Buttons=new Dictionary<string,Button>();
        readonly Dictionary<string,Text> labels=new Dictionary<string,Text>();
        public void Build(Transform parent,CardCatalogSO cards,VisualCatalogSO visuals,Action<string> choose) {
            float width=Mathf.Min(190,1040f/cards.cards.Length);
            for(int i=0;      i<cards.cards.Length;      i++) {
                var card=cards.cards[i];
                string id=card.definition.Id;
                var button=CanvasWidgets.Button(card.definition.Name,parent,10+i*width,9,width-8,156,()=>choose(id));
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);
                var binding=visuals.Find(Array.Find(cards.defenders,d=>d.definition.Id==card.definition.DefenderId).definition.VisualId);
                var image=CanvasWidgets.Rect("Portrait",button.transform,12,12,48,48).gameObject.AddComponent<RawImage>();
                image.texture=card.icon?card.icon:binding.portrait;
                image.color=image.texture?Color.white:Color.clear;
                var name=CanvasWidgets.Label("Name",button.transform,66,10,width-78,57,16);
                name.text=card.definition.Name;
                var label=CanvasWidgets.Label("Stats",button.transform,12,70,width-32,80,14);
                Buttons.Add(id,button);
                labels.Add(id,label);
            }
        }
        public void Refresh(BoardState b,string selected) {
            foreach(var pair in Buttons) {
                var c=b.Catalog.Cards[pair.Key];
                int live=0;
                foreach(var d in b.Defenders.Values)if(d.CardId==c.Id)live++;
                bool available=!b.Paused&&Array.IndexOf(b.Level.Permissions.Place,b.Phase)>=0&&b.PP>=c.Cost&&live<c.MaxCopies;
                pair.Value.interactable=available;
                pair.Value.GetComponent<Image>().color=pair.Key==selected?new Color(.1f,.5f,.4f):new Color(.10f,.24f,.24f);
                labels[pair.Key].text=$"{c.Cost} PP · {live}/{c.MaxCopies}\n{c.Category}\n{c.Description}";
            }
        }
    }
}
