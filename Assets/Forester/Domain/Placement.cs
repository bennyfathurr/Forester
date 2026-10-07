using System;
using System.Collections.Generic;
using System.Linq;
namespace Forester.Domain {
    public sealed class DefenderState {
        public long Id;
        public string CardId,DefinitionId;
        public Cell Cell;
        public int Paid;
        public double NextAttack;
    }
    public sealed class BoardState {
        public readonly LevelDefinition Level;
        public readonly Catalog Catalog;
        public readonly Dictionary<long,DefenderState> Defenders=new Dictionary<long,DefenderState>();
        public readonly Dictionary<Cell,long> Occupancy=new Dictionary<Cell,long>();
        public int PP;
        public Phase Phase=Phase.Preparation;
        public bool Paused;
        internal long NextId=1;
        public BoardState(LevelDefinition l,Catalog c) {
            Level=l;
            Catalog=c;
            PP=l.StartingPP;
        }
    }
    public struct PlacementResult {
        public bool Success;
        public string Error;
        public long EntityId;
        public static PlacementResult Reject(string s)=>new PlacementResult {
            Error=s
        };
        public static PlacementResult Ok(long id)=>new PlacementResult {
            Success=true,EntityId=id
        };
    }
    public struct PlaceCardCommand {
        public string CardId;
        public Cell Cell;
        public PlaceCardCommand(string c,Cell p) {
            CardId=c;
            Cell=p;
        }
    }
    public struct MoveDefenderCommand {
        public long Id;
        public Cell Cell;
        public MoveDefenderCommand(long id,Cell p) {
            Id=id;
            Cell=p;
        }
    }
    public struct SellDefenderCommand {
        public long Id;
        public SellDefenderCommand(long id) {
            Id=id;
        }
    }
    public interface IPlacementService {
        PlacementResult Place(PlaceCardCommand c);
        PlacementResult Move(MoveDefenderCommand c);
        PlacementResult Sell(SellDefenderCommand c);
    }
    public sealed class GridPlacementValidator {
        readonly BoardState b;
        public GridPlacementValidator(BoardState board) {
            b=board;
        }
        public PlacementResult Preview(string id,Cell cell,long moving=0) {
            if(b.Paused)return PlacementResult.Reject("Resume before placing");
            if(!(moving==0?b.Level.Permissions.Place:b.Level.Permissions.Move).Contains(b.Phase))return PlacementResult.Reject("Placement is locked in "+b.Phase);
            if(!b.Catalog.Cards.TryGetValue(id,out var c))return PlacementResult.Reject("Unknown card");
            if(moving==0) {
                if(b.PP<c.Cost)return PlacementResult.Reject("Not enough PP");
                if(b.Defenders.Values.Count(d=>d.CardId==id)>=c.MaxCopies)return PlacementResult.Reject("Maximum copies reached");
            }
            foreach(var p in PathValidator.Footprint(cell,c)) {
                if(!b.Level.Inside(p)||b.Level.Kind(p)!=CellKind.Buildable)return PlacementResult.Reject("Choose a buildable cell");
                if(b.Occupancy.TryGetValue(p,out var owner)&&owner!=moving)return PlacementResult.Reject("Cell occupied");
            }
            return PlacementResult.Ok(moving);
        }
    }
    public sealed class PlacementService:IPlacementService {
        readonly BoardState b;
        public readonly GridPlacementValidator Validator;
        public PlacementService(BoardState board) {
            b=board;
            Validator=new GridPlacementValidator(b);
        }
        public PlacementResult Place(PlaceCardCommand c) {
            var result=Validator.Preview(c.CardId,c.Cell);
            if(!result.Success)return result;
            var card=b.Catalog.Cards[c.CardId];
            long id=b.NextId++;
            var d=new DefenderState {
                Id=id,CardId=c.CardId,DefinitionId=card.DefenderId,Cell=c.Cell,Paid=card.Cost
            };
            b.PP-=card.Cost;
            b.Defenders.Add(id,d);
            foreach(var p in PathValidator.Footprint(c.Cell,card))b.Occupancy.Add(p,id);
            return PlacementResult.Ok(id);
        }
        public PlacementResult Move(MoveDefenderCommand c) {
            if(!b.Defenders.TryGetValue(c.Id,out var d))return PlacementResult.Reject("Defender missing");
            var result=Validator.Preview(d.CardId,c.Cell,c.Id);
            if(!result.Success)return result;
            var card=b.Catalog.Cards[d.CardId];
            foreach(var p in PathValidator.Footprint(d.Cell,card))b.Occupancy.Remove(p);
            d.Cell=c.Cell;
            foreach(var p in PathValidator.Footprint(d.Cell,card))b.Occupancy[p]=d.Id;
            return PlacementResult.Ok(d.Id);
        }
        public PlacementResult Sell(SellDefenderCommand c) {
            if(b.Paused||!b.Level.Permissions.Sell.Contains(b.Phase))return PlacementResult.Reject("Selling locked");
            if(!b.Defenders.TryGetValue(c.Id,out var d))return PlacementResult.Reject("Defender missing");
            foreach(var p in PathValidator.Footprint(d.Cell,b.Catalog.Cards[d.CardId]))b.Occupancy.Remove(p);
            b.Defenders.Remove(d.Id);
            b.PP=Math.Min(b.Level.CapPP,b.PP+(int)Math.Floor(d.Paid*.75));
            return PlacementResult.Ok(d.Id);
        }
    }
}
