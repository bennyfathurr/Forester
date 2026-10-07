using System;
using System.Collections.Generic;
using System.Linq;
namespace Forester.Domain {
    public enum Phase {
        Preparation, Planning, Telegraph, Active, Cleanup, Won, Lost, Faulted
    }
    public enum CellKind {
        Scenic, Buildable, Path, Spawn, Goal
    }
    public enum CombatKind {
        Direct, SplashSlow, Aura
    }
    [Serializable] public struct Cell : IEquatable<Cell> {
        public int Column,Row;
        public Cell(int c,int r) {
            Column=c;
            Row=r;
        }
        public string Id=>$"C{Column:00}_R{Row:00}";
        public bool Equals(Cell b)=>Column==b.Column&&Row==b.Row;
        public override bool Equals(object b)=>b is Cell c&&Equals(c);
        public override int GetHashCode()=>Column*397^Row;
    }
    [Serializable] public struct Point {
        public double X,Z;
        public Point(double x,double z) {
            X=x;
            Z=z;
        }
        public static double Distance(Point a,Point b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
    }
    [Serializable] public class CardDefinition {
        public string Id,Name,Description,DefenderId,Category;
        public int Cost,MaxCopies,Width=1,Height=1;
    }
    [Serializable] public class DefenderDefinition {
        public string Id,VisualId;
        public CombatKind Kind;
        public double Damage,Interval,Range,SplashRadius,SlowMultiplier=.65,SlowSeconds=2,AuraMultiplier=1.15;
    }
    [Serializable] public class EnemyDefinition {
        public string Id,VisualId;
        public int Cost,IntegrityLoss;
        public double HP,Speed;
    }
    [Serializable] public class ConditionDefinition {
        public string Id="gust_front",Name="Gust Front";
        public int Cost=4;
        public double Duration=8,SpeedMultiplier=1.15,Telegraph=3;
    }
    [Serializable] public class RouteDefinition {
        public string Id,SpawnId,GoalId;
        public Cell[] Waypoints;
    }
    [Serializable] public class WaveRule {
        public int Budget,MaxCount,Window;
        public string[] Routes,Enemies,Conditions=new string[0];
    }
    [Serializable] public class PhasePermissions {
        public Phase[] Place= {
            Phase.Preparation
        },Move= {
            Phase.Preparation
        },Sell= {
            Phase.Preparation
        };
    }
    public sealed class LevelDefinition {
        public int Columns=16,Rows=12,Integrity=20,StartingPP=120,CapPP=200;
        public double CellSize=2;
        public Point Origin;
        public Dictionary<Cell,CellKind> Cells=new Dictionary<Cell,CellKind>();
        public List<RouteDefinition> Routes=new List<RouteDefinition>();
        public WaveRule[] Waves;
        public int[] Awards= {
            55,65
        };
        public PhasePermissions Permissions=new PhasePermissions();
        public Point Position(Cell c)=>new Point(Origin.X+c.Column*CellSize,Origin.Z+c.Row*CellSize);
        public bool Inside(Cell c)=>c.Column>=0&&c.Row>=0&&c.Column<Columns&&c.Row<Rows;
        public CellKind Kind(Cell c)=>Cells.TryGetValue(c,out var k)?k:CellKind.Scenic;
    }
    public sealed class Catalog {
        public Dictionary<string,CardDefinition> Cards=new Dictionary<string,CardDefinition>();
        public Dictionary<string,DefenderDefinition> Defenders=new Dictionary<string,DefenderDefinition>();
        public Dictionary<string,EnemyDefinition> Enemies=new Dictionary<string,EnemyDefinition>();
        public Dictionary<string,ConditionDefinition> Conditions=new Dictionary<string,ConditionDefinition>();
        public void Validate() {
            foreach(var c in Cards.Values)if(string.IsNullOrWhiteSpace(c.Id)||c.Cost<0||c.MaxCopies<1||c.Width<1||c.Height<1||!Defenders.ContainsKey(c.DefenderId))throw new ArgumentException("Invalid card "+c.Id);
            foreach(var d in Defenders.Values)if(d.Range<=0||d.Interval<=0||d.Damage<0||!Enum.IsDefined(typeof(CombatKind),d.Kind)||d.SlowMultiplier<=0||d.SlowMultiplier>1||d.AuraMultiplier<1)throw new ArgumentException("Invalid defender "+d.Id);
            foreach(var e in Enemies.Values)if(e.Cost<1||e.HP<=0||e.Speed<=0||e.IntegrityLoss<1)throw new ArgumentException("Invalid threat "+e.Id);
            foreach(var c in Conditions.Values)if(c.Cost<0||c.Duration<=0||c.SpeedMultiplier<1||c.SpeedMultiplier>2)throw new ArgumentException("Invalid condition");
        }
    }
    public static class Defaults {
        public static LevelDefinition Level() {
            var l=new LevelDefinition();
            l.Routes.Add(new RouteDefinition {
                Id="NORTH",SpawnId="S_NORTH",GoalId="G_FOREST",Waypoints=new[] {
                    new Cell(0,9),new Cell(5,9),new Cell(5,6),new Cell(10,6),new Cell(10,3),new Cell(15,3)
                }
            });
            l.Routes.Add(new RouteDefinition {
                Id="SOUTH",SpawnId="S_SOUTH",GoalId="G_FOREST",Waypoints=new[] {
                    new Cell(0,2),new Cell(4,2),new Cell(4,6),new Cell(10,6),new Cell(10,3),new Cell(15,3)
                }
            });
            foreach(var r in l.Routes) {
                foreach(var c in PathValidator.Cells(r))l.Cells[c]=CellKind.Path;
                l.Cells[r.Waypoints[0]]=CellKind.Spawn;
                l.Cells[r.Waypoints.Last()]=CellKind.Goal;
            }
            int[,] a= {
                {
                    2,8
                }, {
                    4,8
                }, {
                    6,8
                }, {
                    2,10
                }, {
                    6,10
                }, {
                    2,1
                }, {
                    3,3
                }, {
                    5,1
                }, {
                    6,3
                }, {
                    3,5
                }, {
                    6,5
                }, {
                    8,5
                }, {
                    8,7
                }, {
                    10,7
                }, {
                    9,2
                }, {
                    11,2
                }, {
                    12,4
                }, {
                    14,4
                }
            };
            for(int i=0;      i<a.GetLength(0);      i++)l.Cells[new Cell(a[i,0],a[i,1])]=CellKind.Buildable;
            l.Waves=new[] {
                new WaveRule {
                    Budget=12,MaxCount=12,Window=24,Routes=new[] {
                        "NORTH"
                    },Enemies=new[] {
                        "emberling","brush_cluster"
                    }
                },new WaveRule {
                    Budget=22,MaxCount=18,Window=36,Routes=new[] {
                        "NORTH","SOUTH"
                    },Enemies=new[] {
                        "emberling","wind_runner","brush_cluster"
                    }
                },new WaveRule {
                    Budget=32,MaxCount=24,Window=48,Routes=new[] {
                        "NORTH","SOUTH"
                    },Enemies=new[] {
                        "emberling","wind_runner","brush_cluster"
                    },Conditions=new[] {
                        "gust_front"
                    }
                }
            };
            return l;
        }
        public static Catalog Catalog() {
            var c=new Catalog();
            string[] ids= {
                "water_team","watch_post","foam_station","warga","water_tank"
            };
            string[] names= {
                "Water Team","Watch Post","Foam Station","Community Response Team","Water Tank"
            };
            int[] costs= {
                35,25,45,30,25
            },caps= {
                4,3,2,3,2
            };
            double[] damage= {
                10,18,5,6,0
            },interval= {
                .8,1.5,1.2,.5,1
            },range= {
                6,9,6,4,5
            };
            for(int i=0;      i<5;      i++) {
                c.Defenders.Add(ids[i],new DefenderDefinition {
                    Id=ids[i],VisualId=ids[i],Kind=i==4?CombatKind.Aura:i==2?CombatKind.SplashSlow:CombatKind.Direct,Damage=damage[i],Interval=interval[i],Range=range[i],SplashRadius=i==2?2:0
                });
                c.Cards.Add("card_"+ids[i],new CardDefinition {
                    Id="card_"+ids[i],Name=names[i],DefenderId=ids[i],Cost=costs[i],MaxCopies=caps[i],Category=i==4?"Support":"Containment",Description=i==4?"Nearby allies attack 15% faster. Does not stack.":$"{damage[i]} containment / {interval[i]:0.0}s · range {range[i]}"
                });
            }
            string[] eids= {
                "emberling","wind_runner","brush_cluster"
            };
            int[] ec= {
                1,2,3
            },loss= {
                1,1,2
            };
            double[] hp= {
                35,25,90
            },sp= {
                2,3.2,1.3
            };
            for(int i=0;      i<3;      i++)c.Enemies.Add(eids[i],new EnemyDefinition {
                Id=eids[i],VisualId=eids[i],Cost=ec[i],IntegrityLoss=loss[i],HP=hp[i],Speed=sp[i]
            });
            c.Conditions.Add("gust_front",new ConditionDefinition());
            return c;
        }
    }
}
