using System;
using System.Collections.Generic;
using System.Linq;
namespace Forester.Domain {
    public static class PathValidator {
        public static IEnumerable<Cell> Cells(RouteDefinition r) {
            for(int i=1;      i<r.Waypoints.Length;      i++) {
                var a=r.Waypoints[i-1];
                var b=r.Waypoints[i];
                int dx=Math.Sign(b.Column-a.Column),dz=Math.Sign(b.Row-a.Row);
                if(dx!=0&&dz!=0)throw new ArgumentException("Routes must use axis-aligned segments: "+r.Id);
                int n=Math.Abs(b.Column-a.Column)+Math.Abs(b.Row-a.Row);
                for(int j=0;      j<=n;      j++)yield return new Cell(a.Column+j*dx,a.Row+j*dz);
            }
        }
        public static void Validate(LevelDefinition l,Catalog catalog=null) {
            if(l.Columns<1||l.Rows<1||l.CellSize<=0||l.Integrity<1||l.StartingPP<0||l.StartingPP>l.CapPP||l.Waves==null||l.Waves.Length==0)throw new ArgumentException("Invalid level bounds/economy");
            var ids=new HashSet<string>();
            var spawn=new Dictionary<string,Cell>();
            var goals=new Dictionary<string,Cell>();
            if(l.Routes.Count==0)throw new ArgumentException("No complete routes");
            foreach(var r in l.Routes) {
                if(string.IsNullOrWhiteSpace(r.Id)||!ids.Add(r.Id)||string.IsNullOrWhiteSpace(r.SpawnId)||string.IsNullOrWhiteSpace(r.GoalId)||r.Waypoints==null||r.Waypoints.Length<2)throw new ArgumentException("Invalid route IDs/endpoints");
                var visited=new HashSet<Cell>();
                for(int i=1;      i<r.Waypoints.Length;      i++)if(r.Waypoints[i].Equals(r.Waypoints[i-1]))throw new ArgumentException("Zero segment "+r.Id);
                Cell? previous=null;
                foreach(var cell in Cells(r)) {
                    if(previous.HasValue&&previous.Value.Equals(cell))continue;
                    if(!l.Inside(cell)||!visited.Add(cell))throw new ArgumentException("Out-of-bounds or cyclic route "+r.Id);
                    if(l.Kind(cell)==CellKind.Buildable||l.Kind(cell)==CellKind.Scenic)throw new ArgumentException("Route cell is not marked path: "+cell.Id);
                    previous=cell;
                }
                var first=r.Waypoints[0];
                var last=r.Waypoints.Last();
                if(l.Kind(first)!=CellKind.Spawn||l.Kind(last)!=CellKind.Goal)throw new ArgumentException("Missing spawn/goal tag");
                if(spawn.TryGetValue(r.SpawnId,out var s)&&!s.Equals(first)||goals.TryGetValue(r.GoalId,out var g)&&!g.Equals(last))throw new ArgumentException("Conflicting endpoints");
                spawn[r.SpawnId]=first;
                goals[r.GoalId]=last;
            }
            foreach(var cell in l.Cells.Keys)if(!l.Inside(cell))throw new ArgumentException("Cell outside board");
            foreach(var w in l.Waves)if(w.Budget<1||w.MaxCount<1||w.Window<0||w.Routes.Length==0||w.Routes.Any(id=>!ids.Contains(id))||(catalog!=null&&(w.Enemies.Any(id=>!catalog.Enemies.ContainsKey(id))||w.Conditions.Any(id=>!catalog.Conditions.ContainsKey(id)))))throw new ArgumentException("Invalid wave allowlists");
            if(catalog!=null)foreach(var c in catalog.Cards.Values)if(!l.Cells.Any(x=>x.Value==CellKind.Buildable&&Footprint(x.Key,c).All(p=>l.Inside(p)&&l.Kind(p)==CellKind.Buildable)))throw new ArgumentException("No footprint fits "+c.Id);
        }
        public static IEnumerable<Cell> Footprint(Cell cell,CardDefinition card) {
            for(int x=0;      x<card.Width;      x++)for(int z=0;      z<card.Height;      z++)yield return new Cell(cell.Column+x,cell.Row+z);
        }
    }
    public sealed class RoutePath {
        public readonly RouteDefinition Definition;
        public readonly Point[] Points;
        public readonly double[] Distances;
        public double Length=>Distances[Distances.Length-1];
        public RoutePath(LevelDefinition l,RouteDefinition r) {
            Definition=r;
            Points=r.Waypoints.Select(l.Position).ToArray();
            Distances=new double[Points.Length];
            for(int i=1;      i<Points.Length;      i++)Distances[i]=Distances[i-1]+Point.Distance(Points[i-1],Points[i]);
        }
        public Point At(double d) {
            d=Math.Max(0,Math.Min(Length,d));
            for(int i=1;      i<Points.Length;      i++)if(d<=Distances[i]) {
                double t=(d-Distances[i-1])/(Distances[i]-Distances[i-1]);
                return new Point(Points[i-1].X+(Points[i].X-Points[i-1].X)*t,Points[i-1].Z+(Points[i].Z-Points[i-1].Z)*t);
            }
            return Points.Last();
        }
    }
    public sealed class PathRegistry {
        public readonly Dictionary<string,RoutePath> Routes;
        public PathRegistry(LevelDefinition l) {
            PathValidator.Validate(l);
            Routes=l.Routes.ToDictionary(r=>r.Id,r=>new RoutePath(l,r));
        }
    }
}
