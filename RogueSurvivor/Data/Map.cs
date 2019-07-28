﻿// Decompiled with JetBrains decompiler
// Decompiled with JetBrains decompiler
// Type: djack.RogueSurvivor.Data.Map
// Assembly: RogueSurvivor, Version=0.9.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D2AE4FAE-2CA8-43FF-8F2F-59C173341976
// Assembly location: C:\Private.app\RS9Alpha.Hg\RogueSurvivor.exe

// #define Z_VECTOR

#define NO_PEACE_WALLS
// #define AUDIT_ACTOR_MOVEMENT
// # define LOCK_ACTORSLIST
// #define AUDIT_ITEM_INVARIANTS

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Linq;
using Zaimoni.Data;

using DoorWindow = djack.RogueSurvivor.Engine.MapObjects.DoorWindow;
using ItemMeleeWeapon = djack.RogueSurvivor.Engine.Items.ItemMeleeWeapon;

// map coordinate definitions.  Want to switch this away from System.Drawing.Point to get a better hash function in.
#if Z_VECTOR
using Point = Zaimoni.Data.Vector2D_short;
using Rectangle = Zaimoni.Data.Box2D_short;
using Size = Zaimoni.Data.Vector2D_short;   // likely to go obsolete with transition to a true vector type
#else
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;   // likely to go obsolete with transition to a true vector type
#endif

namespace djack.RogueSurvivor.Data
{
  [Serializable]
  internal class Map : ISerializable
  {
    public const int GROUND_INVENTORY_SLOTS = 10;
    public readonly int Seed;
    public readonly District District;
	public readonly string Name;
    private string m_BgMusic;  // alpha10
    private Lighting m_Lighting;
	public readonly WorldTime LocalTime;
#if Z_VECTOR
    public readonly Size Extent;
	public short Width { get {return Extent.X;} }
	public short Height { get {return Extent.Y;} }
	[NonSerialized] public readonly Rectangle Rect; // \todo next savefile break: doesn't have to be in savefile, could rebuild this on load
#else
    public readonly int Width;
	public readonly int Height;
	public readonly Rectangle Rect; // \todo next savefile break: doesn't have to be in savefile, could rebuild this on load
    public Size Extent { get { return Rect.Size; } }
#endif
    private readonly byte[,] m_TileIDs;
    private readonly byte[] m_IsInside;
    private readonly Dictionary<Point,HashSet<string>> m_Decorations = new Dictionary<Point,HashSet<string>>();
    private readonly Dictionary<Point, Exit> m_Exits = new Dictionary<Point, Exit>();
    private readonly List<Zone> m_Zones = new List<Zone>(5);
    private readonly List<Actor> m_ActorsList = new List<Actor>(5);
    private int m_iCheckNextActorIndex;
    private readonly List<MapObject> m_MapObjectsList = new List<MapObject>(5);
    private readonly Dictionary<Point, Inventory> m_GroundItemsByPosition = new Dictionary<Point, Inventory>(5);
    private readonly List<Corpse> m_CorpsesList = new List<Corpse>(5);
    private readonly Dictionary<Point, List<OdorScent>> m_ScentsByPosition = new Dictionary<Point, List<OdorScent>>(128);
    private readonly List<TimedTask> m_Timers = new List<TimedTask>(5);
    // position inverting caches
    [NonSerialized]
    private readonly Dictionary<Point, Actor> m_aux_ActorsByPosition = new Dictionary<Point, Actor>(5);
    [NonSerialized]
    private readonly Dictionary<Point, MapObject> m_aux_MapObjectsByPosition = new Dictionary<Point, MapObject>(5);
    [NonSerialized]
    private readonly Dictionary<Point, List<Corpse>> m_aux_CorpsesByPosition = new Dictionary<Point, List<Corpse>>(5);
    // AI support caches, etc.
    [NonSerialized]
    public readonly NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>> Players;
    [NonSerialized]
    public readonly NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>> Police;
    [NonSerialized]
    public readonly Dataflow<List<Actor>,int> UndeadCount;
    [NonSerialized]
    public readonly NonSerializedCache<List<MapObject>, Engine.MapObjects.PowerGenerator, ReadOnlyCollection<Engine.MapObjects.PowerGenerator>> PowerGenerators;
    [NonSerialized]
    public readonly NonSerializedCache<Map, Map, HashSet<Map>> destination_maps;
    // map geometry
#if PRERELEASE_MOTHBALL
    [NonSerialized] private readonly List<LinearChokepoint> m_Chokepoints = new List<LinearChokepoint>();
#endif
    [NonSerialized] private readonly List<Point> m_FullCorner_nw = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FullCorner_ne = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FullCorner_se = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FullCorner_sw = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FlushWall_n = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FlushWall_s = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FlushWall_w = new List<Point>();
    [NonSerialized] private readonly List<Point> m_FlushWall_e = new List<Point>();
    // this is going to want an end-of-turn map updater
    [NonSerialized]
    public readonly TimeCache<string, Dictionary<Location, int>> pathing_exits_to_goals = new TimeCache<string, Dictionary<Location, int>>(); // type is that needed by user, not that generated by pathfinding

    public bool IsSecret { get; private set; }

    public void Expose() {
      IsSecret = false;
      foreach(Map m in destination_maps.Get) {
        m.destination_maps.Recalc();
      }
    }

    public string BgMusic { // alpha10
      get { return m_BgMusic; }
      set { m_BgMusic = value; }    // retain this as we probably want to imposs access controls
    }

    public Lighting Lighting { get { return m_Lighting; } }
    public bool Illuminate(bool on) {
#if DEBUG
      if (Lighting.OUTSIDE == Lighting) throw new InvalidOperationException(nameof(Lighting)+": not useful to artificially light outside ");
#endif
      if (on) {
        if (Lighting.LIT==Lighting) return false;
        m_Lighting = Lighting.LIT;
        return true;
      }
      if (Lighting.DARKNESS==Lighting) return false;
      m_Lighting = Lighting.DARKNESS;
      return true;
    }

    public IEnumerable<Zone> Zones { get { return m_Zones; } }
    public IEnumerable<Exit> Exits { get { return m_Exits.Values; } }
    public IEnumerable<Actor> Actors { get { return m_ActorsList; } }
    public int CountActors { get { return m_ActorsList.Count; } }
    public IEnumerable<MapObject> MapObjects { get { return m_MapObjectsList; } }
    public IEnumerable<Inventory> GroundInventories { get { return m_GroundItemsByPosition.Values; } }
    public IEnumerable<Corpse> Corpses { get { return m_CorpsesList; } }
    public int CountCorpses { get { return m_CorpsesList.Count; } }

    // there is a very rare multi-threading related crash due to m_ActorsList (the parameter for these) being adjusted
    // mid-enumeration
    private static ReadOnlyCollection<Actor> _findPlayers(List<Actor> src)
    {
      return new ReadOnlyCollection<Actor>(src.FindAll(a => a.IsPlayer && !a.IsDead));
    }

    private static ReadOnlyCollection<Actor> _findPolice(List<Actor> src)
    {
      return new ReadOnlyCollection<Actor>(src.FindAll(a => (int)Gameplay.GameFactions.IDs.ThePolice == a.Faction.ID && !a.IsDead));
    }

    private static int _countUndead(IEnumerable<Actor> src)
    {
      return src.Count(a => a.Model.Abilities.IsUndead);
    }

    private static ReadOnlyCollection<Engine.MapObjects.PowerGenerator> _findPowerGenerators(IEnumerable<MapObject> src)
    {
      return new ReadOnlyCollection<Engine.MapObjects.PowerGenerator>(src.OfType< Engine.MapObjects.PowerGenerator >().ToList());
    }

    public Map(int seed, string name, District d, int width, int height, Lighting light=Lighting.OUTSIDE, bool secret=false)
    {
#if DEBUG
      if (null == name) throw new ArgumentNullException(nameof(name));
      if (0 >= width) throw new ArgumentOutOfRangeException(nameof(width), width, "0 >= width");
      if (0 >= height) throw new ArgumentOutOfRangeException(nameof(height), height, "0 >= height");
#endif
      Seed = seed;
      Name = name;
#if Z_VECTOR
      Extent = new Size(width,height);
#else
      Width = width;
      Height = height;
#endif
	  District = d;
      Rect = new Rectangle(0, 0, width, height);
      LocalTime = new WorldTime();
      m_Lighting = light;
      IsSecret = secret;
      m_TileIDs = new byte[width, height];
      m_IsInside = new byte[width*height-1/8+1];
      Players = new NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>>(m_ActorsList, _findPlayers);
      Police = new NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>>(m_ActorsList, _findPolice);
      UndeadCount = new Dataflow<List<Actor>, int>(m_ActorsList, _countUndead); // XXX ... could eliminate this by rewriting end-of-turn event simulation
      PowerGenerators = new NonSerializedCache<List<MapObject>, Engine.MapObjects.PowerGenerator, ReadOnlyCollection<Engine.MapObjects.PowerGenerator>>(m_MapObjectsList, _findPowerGenerators);
      destination_maps = new NonSerializedCache<Map, Map, HashSet<Map>>(this,m=>new HashSet<Map>(m_Exits.Values.Select(exit => exit.ToMap).Where(map => !map.IsSecret)));
      pathing_exits_to_goals.Now(LocalTime.TurnCounter);
    }

#region Implement ISerializable
    protected Map(SerializationInfo info, StreamingContext context)
    {
      Seed = (int) info.GetValue("m_Seed", typeof (int));
      District = (District) info.GetValue("m_District", typeof (District));
      Name = (string) info.GetValue("m_Name", typeof (string));
      LocalTime = (WorldTime) info.GetValue("m_LocalTime", typeof (WorldTime));
#if Z_VECTOR
      Extent = (Size) info.GetValue("m_Extent", typeof (Size));
      Rect = new Rectangle(Point.Empty,Extent);
#else
      Width = (int) info.GetValue("m_Width", typeof (int));
      Height = (int) info.GetValue("m_Height", typeof (int));
      Rect = (Rectangle) info.GetValue("m_Rectangle", typeof (Rectangle));
#endif
      m_Exits = (Dictionary<Point, Exit>) info.GetValue("m_Exits", typeof (Dictionary<Point, Exit>));
      m_Zones = (List<Zone>) info.GetValue("m_Zones", typeof (List<Zone>));
      m_ActorsList = (List<Actor>) info.GetValue("m_ActorsList", typeof (List<Actor>));
      m_MapObjectsList = (List<MapObject>) info.GetValue("m_MapObjectsList", typeof (List<MapObject>));
      m_GroundItemsByPosition = (Dictionary<Point, Inventory>) info.GetValue("m_GroundItemsByPosition", typeof (Dictionary<Point, Inventory>));
      m_CorpsesList = (List<Corpse>) info.GetValue("m_CorpsesList", typeof (List<Corpse>));
      m_Lighting = (Lighting) info.GetValue("m_Lighting", typeof (Lighting));
      m_ScentsByPosition = (Dictionary<Point, List<OdorScent>>) info.GetValue("m_ScentsByPosition", typeof (Dictionary<Point, List<OdorScent>>));
      m_Timers = (List<TimedTask>) info.GetValue("m_Timers", typeof (List<TimedTask>));
      m_TileIDs = (byte[,]) info.GetValue("m_TileIDs", typeof (byte[,]));
      m_IsInside = (byte[]) info.GetValue("m_IsInside", typeof (byte[]));
      m_Decorations = (Dictionary<Point, HashSet<string>>) info.GetValue("m_Decorations", typeof(Dictionary<Point, HashSet<string>>));
      m_BgMusic = (string)info.GetValue("m_BgMusic", typeof(string));   // alpha10
      Players = new NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>>(m_ActorsList, _findPlayers);
      Police = new NonSerializedCache<List<Actor>, Actor, ReadOnlyCollection<Actor>>(m_ActorsList, _findPolice);
      UndeadCount = new Dataflow<List<Actor>, int>(m_ActorsList,_countUndead);
      PowerGenerators = new NonSerializedCache<List<MapObject>, Engine.MapObjects.PowerGenerator, ReadOnlyCollection<Engine.MapObjects.PowerGenerator>>(m_MapObjectsList, _findPowerGenerators);
      destination_maps = new NonSerializedCache<Map, Map, HashSet<Map>>(this,m=>new HashSet<Map>(m_Exits.Values.Select(exit => exit.ToMap).Where(map => !map.IsSecret)));
      ReconstructAuxiliaryFields();
      RegenerateMapGeometry();
      pathing_exits_to_goals.Now(LocalTime.TurnCounter);
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("m_Seed", Seed);
      info.AddValue("m_District", District);
      info.AddValue("m_Name", Name);
      info.AddValue("m_LocalTime", LocalTime);
#if Z_VECTOR
      info.AddValue("m_Extent", Extent);
#else
      info.AddValue("m_Width", Width);
      info.AddValue("m_Height", Height);
      info.AddValue("m_Rectangle", Rect);
#endif
      info.AddValue("m_Exits", m_Exits);
      info.AddValue("m_Zones", m_Zones);
      info.AddValue("m_ActorsList", m_ActorsList);
      info.AddValue("m_MapObjectsList", m_MapObjectsList);
      info.AddValue("m_GroundItemsByPosition", m_GroundItemsByPosition);
      info.AddValue("m_CorpsesList", m_CorpsesList);
      info.AddValue("m_Lighting", m_Lighting);
      info.AddValue("m_ScentsByPosition", m_ScentsByPosition);
      info.AddValue("m_Timers", m_Timers);
      info.AddValue("m_TileIDs", m_TileIDs);
      info.AddValue("m_IsInside", m_IsInside);
      info.AddValue("m_Decorations", m_Decorations);
      info.AddValue("m_BgMusic", m_BgMusic);    // alpha10
     }
#endregion

    // once the peace walls are down, IsInBounds will refer to the actual map data.
    // IsValid will allow "translating" coordinates to adjacent maps in order to fulfil the dereference
    // IsStrictlyValid will *require* "translating" coordinates to adjacent maps in order to fulfil the dereference
    // That is, IsValid := IsInBounds XOR IsStrictlyValid
    public bool IsInBounds(int x, int y)
    {
      return 0 <= x && x < Width && 0 <= y && y < Height;
    }

    public bool IsInBounds(Point p)
    {
      int test;
      return 0 <= (test = p.X) && test < Width && 0 <= (test = p.Y) && test < Height;
    }

    public bool IsOnEdge(Point p)   // but not necessarily in bounds
    {
      int test;
      return 0==(test = p.X) || Width-1==test || 0==(test = p.Y) || Height-1==test;
    }

    // return value of zero may be either "in bounds", or "not valid at all"
    public int DistrictDeltaCode(Point pt)
    {
      int ret = 0;

      int test;
      if (0>(test = pt.X)) ret -= 1;
      else if (Width <= test) ret += 1;

      if (0>(test = pt.Y)) ret -= 3;
      else if (Height <= test) ret += 3;

      return ret;
    }

    public void TrimToBounds(ref int x, ref int y)
    {
      int nonstrict_ub;
      if (x < 0) x = 0;
      else if (x > (nonstrict_ub = Width - 1)) x = nonstrict_ub;
      if (y < 0) y = 0;
      else if (y > (nonstrict_ub = Height - 1)) y = nonstrict_ub;
    }

    public void TrimToBounds(ref Point p)
    {
      int nonstrict_ub;
      int test;
      if ((test = p.X) < 0) p.X = 0;
      else if (test > (nonstrict_ub = Width - 1)) p.X = nonstrict_ub;
      if ((test = p.Y) < 0) p.Y = 0;
      else if (test> (nonstrict_ub = Height - 1)) p.Y = nonstrict_ub;
    }

    public void TrimToBounds(ref Rectangle r)
    {
#if DEBUG
      if (r.X >= Width) throw new ArgumentOutOfRangeException(nameof(r.X),r.X, "r.X >= Width");
      if (r.Y >= Height) throw new ArgumentOutOfRangeException(nameof(r.Y),r.Y, "r.Y >= Height");
      if (0 > r.Right) throw new ArgumentOutOfRangeException(nameof(r.Right),r.Right, "0 > r.Right");
      if (0 > r.Bottom) throw new ArgumentOutOfRangeException(nameof(r.Bottom),r.Bottom, "0 > r.Bottom");
#endif
      int nonstrict_ub;
      int test;
      if ((test = r.X) < 0) {
        r.Width += test;
        r.X = 0;
      }

      if ((test = r.Y) < 0) {
        r.Width += test;
        r.Y = 0;
      }

      if ((test = r.Right) > (nonstrict_ub = Width - 1))  r.Width -= (test - nonstrict_ub);
      if ((test = r.Bottom) > (nonstrict_ub = Height - 1)) r.Height -= (test - nonstrict_ub);
    }

    // placeholder for define-controlled redefinitions
    public bool IsValid(int x, int y)
    {
#if NO_PEACE_WALLS
      return IsInBounds(x,y) || IsStrictlyValid(x,y);
#else
      return 0 <= x && x < Width && 0 <= y && y < Height;
#endif
    }

    public bool IsValid(Point p)
    {
#if NO_PEACE_WALLS
      return IsInBounds(p) || IsStrictlyValid(p);
#else
      return 0 <= p.X && p.X < Width && 0 <= p.Y && p.Y < Height;
#endif
    }

    public bool IsStrictlyValid(int x, int y)
    {
#if NO_PEACE_WALLS
      return null != Normalize(new Point(x,y));
#else
      return false;
#endif
    }

    public bool IsStrictlyValid(Point p)
    {
#if NO_PEACE_WALLS
      return null != Normalize(p);
#else
      return false;
#endif
    }
    // end placeholder for define-controlled redefinitions

    public Location? Normalize(Point pt)
    {
      if (IsInBounds(pt)) return null;
      int map_code = District.UsesCrossDistrictView(this);
      if (0>=map_code) return null;
      int delta_code = DistrictDeltaCode(pt);
      if (0==delta_code) return null;
      Point new_district = District.WorldPosition;    // System.Drawing.Point is a struct: this is a value copy
      Vector2D_int_stack district_delta = new Vector2D_int_stack(0,0);
      while(0!=delta_code) {
        var tmp = Zaimoni.Data.ext_Drawing.sgn_from_delta_code(ref delta_code);
        // XXX: reject Y other than 0,1 in debug mode
        if (1==tmp.Y) {
          district_delta.Y = tmp.X;
          new_district.Y += tmp.X;
          if (0>new_district.Y) return null;
          if (Engine.Session.Get.World.Size<=new_district.Y) return null;
        } else if (0==tmp.Y) {
          district_delta.X = tmp.X;
          new_district.X += tmp.X;
          if (0>new_district.X) return null;
          if (Engine.Session.Get.World.Size<=new_district.X) return null;
        }
      }
      // following fails if district size strictly less than the half-view radius
      Map dest = Engine.Session.Get.World[new_district.X,new_district.Y].CrossDistrictViewing(map_code);
      if (null==dest) return null;
      if (1==district_delta.X) pt.X -= Width;
      else if (-1==district_delta.X) pt.X += dest.Width;
      if (1==district_delta.Y) pt.Y -= Height;
      else if (-1==district_delta.Y) pt.Y += dest.Height;
      return dest.IsInBounds(pt) ? new Location(dest, pt) : dest.Normalize(pt);
    }

    public Location? Denormalize(Location loc)
    {
      if (this == loc.Map && IsValid(loc.Position)) return loc;
#if NO_PEACE_WALLS
      int map_code = District.UsesCrossDistrictView(this);
      if (0>=map_code || map_code != District.UsesCrossDistrictView(loc.Map)) return null;
      Vector2D_int_stack district_delta = new Vector2D_int_stack(loc.Map.District.WorldPosition.X-District.WorldPosition.X, loc.Map.District.WorldPosition.Y - District.WorldPosition.Y);

      // fails at district delta coordinates of absolute value 2+ where intermediate maps do not have same width/height as the endpoint of interest
      Point not_in_bounds = loc.Position;
      if (0 < district_delta.X) not_in_bounds.X += district_delta.X*Width;
      else if (0 > district_delta.X) not_in_bounds.X += district_delta.X * loc.Map.Width;
      if (0 < district_delta.Y) not_in_bounds.Y += district_delta.Y*Height;
      else if (0 > district_delta.Y) not_in_bounds.Y += district_delta.Y * loc.Map.Height;

      return new Location(this,not_in_bounds);
#else
      return null;
#endif
    }

    public List<Location> Denormalize(IEnumerable<Location> locs)
    {
      if (null == locs) return null;
      var ret = new List<Location>(locs.Count());
      foreach(var x in locs) {
        Location? test = Denormalize(x);
        if (null != test) ret.Add(test.Value);
      }
      return 0<ret.Count ? ret : null;
    }

    public bool IsInViewRect(Location loc, Rectangle view)
    {
      if (this != loc.Map) {
        Location? test = Denormalize(loc);
        if (null == test) return false;
        loc = test.Value;
      }
      return view.Contains(loc.Position);
    }

    // these two look wrong, may need fixing later
    public bool IsMapBoundary(int x, int y)
    {
      return -1 == x || x == Width || -1 == y || y == Height;
    }

#if DEAD_FUNC
    public bool IsOnMapBorder(int x, int y)
    {
      return 0 == x || x == Width-1 || 0 == y || y == Height-1;
    }
#endif

    public bool IsOnMapBorder(Point pt)
    {
      return 0 == pt.X || pt.X == Width-1 || 0 == pt.Y || pt.Y == Height-1;
    }

    /// <summary>
    /// GetTileAt does not bounds-check for efficiency reasons;
    /// the typical use case is known to be in bounds by construction.
    /// </summary>
    public Tile GetTileAt(int x, int y)
    {
      int i = y*Width+x;
      return new Tile(m_TileIDs[x,y],(0!=(m_IsInside[i/8] & (1<<(i%8)))),new Location(this,new Point(x,y)));
    }

    /// <summary>
    /// GetTileAt does not bounds-check for efficiency reasons;
    /// the typical use case is known to be in bounds by construction.
    /// </summary>
    public Tile GetTileAt(Point p)
    {
      int i = p.Y*Width+p.X;
      return new Tile(m_TileIDs[p.X,p.Y],(0!=(m_IsInside[i/8] & (1<<(i%8)))),new Location(this,p));
    }

    // for when coordinates may be denormalized
    public Tile GetTileAtExt(Point p)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(p)) return GetTileAt(p);
      Location? loc = Normalize(p);
//    if (null == loc) throw ...;
      return loc.Value.Map.GetTileAt(loc.Value.Position);
#else
      int i = p.Y*Width+p.X;
      return new Tile(m_TileIDs[p.X,p.Y],(0!=(m_IsInside[i/8] & (1<<(i%8)))),new Location(this,p));
#endif
    }


    public void SetIsInsideAt(int x, int y, bool inside=true)
    {
      int i = y*Width+x;
      if (inside) {
        m_IsInside[i/8] |= (byte)(1<<(i%8));
      } else {
        m_IsInside[i/8] &= (byte)(255&(~(1<<(i%8))));
      }
    }

    public void SetIsInsideAt(Point pt, bool inside=true)
    {
      SetIsInsideAt(pt.X,pt.Y, inside);
    }

    public bool IsInsideAt(int x, int y)
    {
      int i = y*Width+x;
      return 0!=(m_IsInside[i/8] & (1<<(i%8)));
    }

    public bool IsInsideAt(Point p)
    {
      int i = p.Y*Width+p.X;
      return 0!=(m_IsInside[i/8] & (1<<(i%8)));
    }

    public bool IsInsideAtExt(Point p)
    {
      if (IsInBounds(p)) return IsInsideAt(p);
      Location? test = Normalize(p);
      return null!=test && test.Value.Map.IsInsideAt(test.Value.Position);
    }

    public void SetTileModelAt(int x, int y, TileModel model)
    {
#if DEBUG
      if (null == model) throw new ArgumentNullException(nameof(model));
      if (!IsInBounds(x, y)) throw new ArgumentOutOfRangeException("("+nameof(x)+","+nameof(y)+")", "(" + x.ToString() + "," + y.ToString() + ")", "!IsInBounds(x,y)");
#endif
      m_TileIDs[x, y] = (byte)(model.ID);
    }

    public void SetTileModelAt(Point pt, TileModel model)
    {
#if DEBUG
      if (null == model) throw new ArgumentNullException(nameof(model));
      if (!IsInBounds(pt)) throw new InvalidOperationException("!IsInBounds(pt)");
#endif
      m_TileIDs[pt.X, pt.Y] = (byte)(model.ID);
    }

    public TileModel GetTileModelAt(int x, int y)
    {
      return Models.Tiles[m_TileIDs[x,y]];
    }

    public TileModel GetTileModelAt(Point pt)
    {
      return GetTileModelAt(pt.X,pt.Y);
    }

    // possibly denormalized versions
    public TileModel GetTileModelAtExt(int x, int y)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(x,y)) return Models.Tiles[m_TileIDs[x, y]];   //      return GetTileModelAt(x,y);
      Location? loc = Normalize(new Point(x,y));
//    if (null == loc) throw ...;
      return loc.Value.Map.GetTileModelAt(loc.Value.Position);
#else
      return Models.Tiles[m_TileIDs[x,y]];
#endif
    }

    public TileModel GetTileModelAtExt(Point pt)
    {
      return GetTileModelAtExt(pt.X,pt.Y);
    }

    // thin wrappers based on Tile API
    public bool HasDecorationsAt(Point pt)
    {
      return m_Decorations.ContainsKey(pt);
    }

    public IEnumerable<string> DecorationsAt(Point pt)
    {
      m_Decorations.TryGetValue(pt, out HashSet<string> ret);
      return ret;
    }

    public void AddDecorationAt(string imageID, Point pt)
    {
      if (m_Decorations.TryGetValue(pt, out HashSet<string> ret)) {
        ret.Add(imageID);
      } else {
        m_Decorations[pt] = new HashSet<string>{ imageID };
      }
    }

    public bool HasDecorationAt(string imageID, Point pt)
    {
      return m_Decorations.TryGetValue(pt, out HashSet<string> ret) && ret.Contains(imageID);
    }

    public void RemoveAllDecorationsAt(Point pt) { m_Decorations.Remove(pt); }

    public void RemoveDecorationAt(string imageID, Point pt)
    {
      if (m_Decorations.TryGetValue(pt, out HashSet<string> ret)
          && ret.Remove(imageID)
          && 0 >= ret.Count)
          m_Decorations.Remove(pt);
    }

    public bool HasExitAt(Point pos) { return m_Exits.ContainsKey(pos); }

    public bool HasExitAtExt(Point pos)
    {
      if (IsInBounds(pos)) return HasExitAt(pos);
      Location? test = Normalize(pos);
      if (null == test) return false;
      return test.Value.Map.HasExitAt(test.Value.Position);
    }

    public Exit GetExitAt(Point pos)
    {
      m_Exits.TryGetValue(pos, out Exit exit);
      return exit;
     }

     public Dictionary<Point,Exit> GetExits(Predicate<Exit> fn) {
#if DEBUG
      if (null == fn) throw new ArgumentNullException(nameof(fn));
#endif
      var ret = new Dictionary<Point, Exit>();
      foreach(var x in m_Exits) {
        if (fn(x.Value)) ret[x.Key] = x.Value;
      }
      return ret;
    }

     public Dictionary<Point,Exit> ExitsFor(Map dest) {
#if DEBUG
      if (null == dest) throw new ArgumentNullException(nameof(dest));
#endif
      var ret = new Dictionary<Point, Exit>();
      foreach(var x in m_Exits) {
        if (x.Value.ToMap == dest) ret[x.Key] = x.Value;
      }
      return ret;
    }

    public List<Point> GetEdge()    // \todo refactor to a cache variable setter
    {
      var ret = new List<Point>();
      bool explicit_edge = false;
      foreach(var x in m_Exits) {
        if (IsInBounds(x.Key)) ret.Add(x.Key);
        else explicit_edge = true;
      }
      if (explicit_edge) {
        for(short x = 0; x<Width; x++) {
          Point test = new Point(x,0);
          if (GetTileModelAt(test).IsWalkable) ret.Add(test);
          test.Y = Height-1;
          if (GetTileModelAt(test).IsWalkable) ret.Add(test);
        }
        for(short y = 1; y<Height-1; y++) {
          Point test = new Point(0,y);
          if (GetTileModelAt(test).IsWalkable) ret.Add(test);
          test.X = Width-1;
          if (GetTileModelAt(test).IsWalkable) ret.Add(test);
        }
      }
      return ret;
    }

    public void ForEachExit(Action<Point,Exit> op)
    {
       foreach(var x in m_Exits) op(x.Key,x.Value);
    }

    public void SetExitAt(Point pos, Exit exit) {
      m_Exits.Add(pos, exit);
    }

#if DEAD_FUNC
    public void RemoveExitAt(Point pos)
    {
      m_Exits.Remove(pos);
    }
#endif

    public bool HasAnExitIn(Rectangle rect)
    {
      return rect.Any(pt => HasExitAt(pt));
    }

    // <remark>only caller wants result in-bounds</remark>
	public List<Point> ExitLocations(HashSet<Exit> src)
	{
      if (0 >= (src?.Count ?? 0)) return null;
	  var ret = new HashSet<Point>();
      foreach (KeyValuePair<Point, Exit> mExit in m_Exits) {
        if (!src.Contains(mExit.Value)) continue;
        if (IsInBounds(mExit.Key)) ret.Add(mExit.Key);
        else {
          foreach(var pt in mExit.Key.Adjacent()) {
            if (IsInBounds(pt)) ret.Add(pt);
          }
        }
      }
	  return (0<ret.Count ? ret.ToList() : null);
	}

    public static int PathfinderMoveCosts(ActorAction act)
    {
        int teardown_turns(MapObject obj) {
		    int cost = 1;
            if (obj is DoorWindow door && 0<door.BarricadePoints) cost += (door.BarricadePoints+7)/8;	// handwave time cost for fully rested unarmed woman with infinite stamina
            else cost += (obj.HitPoints+7)/8;	// time cost to break, as per barricade
            return cost;
        }

        if (act is Engine.Actions.ActionMoveDelta delta) return PathfinderMoveCosts(delta.ConcreteAction);
        if (act is Engine.Actions.ActionShove) return 4;    // impolite so penalize just more than walking around
        if (   act is Engine.Actions.ActionOpenDoor  // extra turn
            || act is Engine.Actions.ActionPush  // assume non-moving i.e. extra turn; also costs stamina
            || act is Engine.Actions.ActionPull  // extra turn; also costs stamina
            || act is Engine.Actions.ActionSwitchPlace // yes, this is hard-coded as 2 standard actions
            || act is Engine.Actions.ActionSwitchPlaceEmergency)
            return 2;
        if (act is Engine.Actions.ActionBashDoor bash) return teardown_turns(bash.Target);
        if (act is Engine.Actions.ActionBreak act_break) return teardown_turns(act_break.Target);
        return 1;  // normal case
    }

    private static Dictionary<Location,int> OneStepForPathfinder(Location loc, Actor a, Dictionary<Location,ActorAction> already)
	{
	  var ret = new Dictionary<Location, int>();
      Dictionary<Location, ActorAction> moves = a.OnePath(loc, already);
      foreach(var move in moves) {
        if (1>= a.Controller.FastestTrapKill(move.Key)) continue;
        ret[move.Key] = PathfinderMoveCosts(move.Value);
      }
	  return ret;
	}

    private Dictionary<Point,int> OneStepForPathfinder(Point pt, Actor a, Dictionary<Point,ActorAction> already)
	{
	  var ret = new Dictionary<Point, int>();
      Dictionary<Point,ActorAction> moves = a.OnePath(this, pt, already);
      foreach(var move in moves) {
        if (1>= a.Controller.FastestTrapKill(new Location(a.Location.Map, move.Key))) continue;
        ret[move.Key] = PathfinderMoveCosts(move.Value);
      }
	  return ret;
	}

    public bool WouldBlacklistFor(Point pt,Actor actor,bool is_real=false)
    {
      if (pt == actor.Location.Position && this == actor.Location.Map) return false;
      if (   1 == Engine.Rules.InteractionDistance(new Location(this,pt),actor.Location)
          && null == Engine.Rules.IsPathableFor(actor, new Location(this, pt))) return true;
      if (actor.CanEnter(new Location(this,pt))) return false;
      // generators may not be entered, but are still (unreliably) pathable
      if (GetMapObjectAtExt(pt) is Engine.MapObjects.PowerGenerator) return false;
#if OBSOLETE
      // most of the following is likely obsolete, if not all
      if (null != Engine.Rules.IsPathableFor(actor, new Location(this, pt))) return false;
      var mapobj = GetMapObjectAtExt(pt);
      if (null!=mapobj) {
        Location loc = new Location(this, pt);
        if (mapobj.IsContainer) {
          var inv = GetItemsAt(pt);
          if (null==inv || inv.IsEmpty) {
            // cheating ai: update item memory immediately since we had to check anyway
            if (is_real) actor.Controller.ItemMemory?.Set(loc,null,LocalTime.TurnCounter);
          } else if (actor.Location.Map!=this) return false;  // not correct, but the correct test below is using a class that assumes same-map
          else if (actor.Controller is Gameplay.AI.OrderableAI ai && null!=ai.WouldGrabFromAccessibleStack(loc, inv)) return false;
        }
        if (mapobj is Engine.MapObjects.PowerGenerator) return false;
        if (mapobj is DoorWindow) return false;
      }
#endif
      return true;
    }

	public Zaimoni.Data.FloodfillPathfinder<Location> PathfindLocSteps(Actor actor)
	{
      var already = new Dictionary<Location,ActorAction>();

      Dictionary<Location,int> fn(Location loc) { return OneStepForPathfinder(loc, actor, already); }

	  var m_StepPather = new Zaimoni.Data.FloodfillPathfinder<Location>(fn, fn, Location.IsInBounds);
      var ret = new FloodfillPathfinder<Location>(m_StepPather);
      Rect.DoForEach(pt => ret.Blacklist(new Location(this, pt)), pt => WouldBlacklistFor(pt, actor, true));
      return ret;
    }

    // Default pather.  Recovery options would include allowing chat, and allowing pushing.
	public Zaimoni.Data.FloodfillPathfinder<Point> PathfindSteps(Actor actor)
	{
      var already = new Dictionary<Point,ActorAction>();

      Dictionary<Point,int> fn(Point pt) { return OneStepForPathfinder(pt, actor, already); }

	  var m_StepPather = new Zaimoni.Data.FloodfillPathfinder<Point>(fn, fn, (pt=> this.IsInBounds(pt)));
      var ret = new FloodfillPathfinder<Point>(m_StepPather);
      Rect.DoForEach(pt => ret.Blacklist(pt), pt => WouldBlacklistFor(pt, actor, true));
      return ret;
    }

    // for AI pathing, currently.
    private HashSet<Map> _PathTo(Map dest, out HashSet<Exit> exits)
    { // disallow secret maps
	  // should be at least one by construction
	  exits = new HashSet<Exit>(m_Exits.Values.Where(e => string.IsNullOrEmpty(e.ReasonIsBlocked())));
	  var exit_maps = new HashSet<Map>(destination_maps.Get);
      if (1>=exit_maps.Count) return exit_maps;
retry:
      if (exit_maps.Contains(dest)) {
        exit_maps.Clear();
        exit_maps.Add(dest);
        exits.RemoveWhere(e => e.ToMap!=dest);
        return exit_maps;
      }
      exit_maps.RemoveWhere(m=> 1==m.destination_maps.Get.Count);
      exits.RemoveWhere(e => !exit_maps.Contains(e.ToMap));
      if (1>=exit_maps.Count) return exit_maps;

	  var dest_exit_maps = new HashSet<Map>(dest.destination_maps.Get);
      if (1 == dest_exit_maps.Count) {
        dest = dest_exit_maps.ToList()[0];
        goto retry;
      }

      dest_exit_maps.IntersectWith(exit_maps);
      if (1 <= dest_exit_maps.Count) {
        dest = dest_exit_maps.ToList()[0];
        goto retry;
      }

      // special area navigation
      {
      KeyValuePair<Map,Map>? src_alt = Engine.Session.Get.UniqueMaps.NavigatePoliceStation(this);
      KeyValuePair<Map,Map>? dest_alt = Engine.Session.Get.UniqueMaps.NavigatePoliceStation(dest);
      if (null != src_alt && null == dest_alt) {    // probably dead code
        dest = src_alt.Value.Key;
        goto retry;
      }
      }
      {
      KeyValuePair<Map,Map>? src_alt = Engine.Session.Get.UniqueMaps.NavigateHospital(this);
      KeyValuePair<Map,Map>? dest_alt = Engine.Session.Get.UniqueMaps.NavigateHospital(dest);
      if (null != src_alt && null == dest_alt) {
        dest = src_alt.Value.Key;
        goto retry;
      }
      }

      if (dest.District != District) {
        int dest_extended = District.UsesCrossDistrictView(dest);
        if (0 == dest_extended) {
          dest = dest.District.EntryMap;
          goto retry;
        }
        if (3 == dest_extended && dest.Exits.Any(e => e.ToMap == dest.District.EntryMap)) {
          dest = dest.District.EntryMap;
          goto retry;
        }
        int this_extended = District.UsesCrossDistrictView(this);
        if (0==this_extended) {
          dest = District.EntryMap;
          goto retry;
        }
        if (3 == this_extended && Exits.Any(e => e.ToMap == District.EntryMap)) {
          dest = District.EntryMap;
          goto retry;
        }
        if (2==dest_extended && 2==this_extended) {
          dest = District.EntryMap;
          goto retry;
        }
        if (1==dest_extended && 2==this_extended) {
          dest = District.EntryMap;
          goto retry;
        }
        if (2==dest_extended && 1==this_extended) {
          dest = dest.District.EntryMap;
          goto retry;
        }
        if (1==dest_extended && 1==this_extended) {
          int x_delta = dest.District.WorldPosition.X - District.WorldPosition.X;
          int y_delta = dest.District.WorldPosition.Y - District.WorldPosition.Y;
          int abs_x_delta = (0<=x_delta ? x_delta : -x_delta);
          int abs_y_delta = (0<=y_delta ? y_delta : -y_delta);
          int sgn_x_delta = (0<=x_delta ? (0 == x_delta ? 0 : 1) : -1);
          int sgn_y_delta = (0<=y_delta ? (0 == y_delta ? 0 : 1) : -1);
          if (abs_x_delta<abs_y_delta) {
            dest = Engine.Session.Get.World[District.WorldPosition.X, District.WorldPosition.Y + sgn_y_delta].EntryMap;
            goto retry;
          } else if (abs_x_delta > abs_y_delta) {
            dest = Engine.Session.Get.World[District.WorldPosition.X + sgn_x_delta, District.WorldPosition.Y].EntryMap;
            goto retry;
          } else if (2 <= abs_x_delta) {
            dest = Engine.Session.Get.World[District.WorldPosition.X + sgn_x_delta, District.WorldPosition.Y + sgn_y_delta].EntryMap;
            goto retry;
          } else return exit_maps;  // no particular insight, not worth a debug crash
        }
      }
#if DEBUG
      if (dest.District != District) throw new InvalidOperationException("test case: cross-district map not handled");
#endif
      // no particular insight
      return exit_maps;
    }

    // for AI pathing, currently.
    public HashSet<Map> PathTo(Map dest, out HashSet<Exit> exits)
    {
      HashSet<Map> exit_maps = _PathTo(dest,out exits);
      if (1>=exit_maps.Count) return exit_maps;

      HashSet<Map> inv_exit_maps = dest._PathTo(this,out HashSet<Exit> inv_exits);

      var intersect = new HashSet<Map>(exit_maps);
      intersect.IntersectWith(inv_exit_maps);
      if (0<intersect.Count) {
        exit_maps = intersect;
        exits.RemoveWhere(e => !exit_maps.Contains(e.ToMap));
        if (1>=exit_maps.Count) return exit_maps;
      }

#if FAIL
      // XXX topology of these special locations has to be accounted for as they're more than 1 level deep
      bool is_special = name.StartsWith("Police Station - ");
      bool dest_is_special = name.StartsWith("Police Station - ");
      // ...
      bool is_special = name.StartsWith("Hospital - ");
      bool dest_is_special = name.StartsWith("Hospital - ");
      // ...
#endif

      // do something uninteillgent
      return exit_maps;
    }

#region zones: map generation support
    public void AddZone(Zone zone)
    {
      m_Zones.Add(zone);
    }

    public void RemoveZone(Zone zone)
    {
      m_Zones.Remove(zone);
    }

    public void RemoveAllZonesAt(Point pt)
    {
      List<Zone> zonesAt = GetZonesAt(pt);
      if (zonesAt == null) return;
      foreach (Zone zone in zonesAt)
        RemoveZone(zone);
    }
#endregion

    /// <remark>shallow copy needed to be safe for foreach loops</remark>
    /// <returns>null, or a non-empty list of zones</returns>
    public List<Zone> GetZonesAt(Point pt)
    {
      var zoneList = m_Zones.FindAll(z => z.Bounds.Contains(pt));
      return (0<zoneList.Count) ? zoneList : null;
    }

#if DEAD_FUNC
    public Zone GetZoneByName(string name)
    {
      return m_Zones.FirstOrDefault(mZone => mZone.Name == name);
    }
#endif

    public List<Zone> GetZonesByPartialName(string partOfname)
    {
      var ret = m_Zones.Where(z => z.Name.Contains(partOfname));
      return ret.Any() ? ret.ToList() : null;
    }

    public Zone GetZoneByPartialName(string partOfname)
    {
      return m_Zones.FirstOrDefault(mZone => mZone.Name.Contains(partOfname));
    }

    public bool HasZonePartiallyNamedAt(Point pos, string partOfName)
    {
      return GetZonesAt(pos)?.Any(zone=>zone.Name.Contains(partOfName)) ?? false;
    }

    public void OnMapGenerated()
    { // coordinates with StdTownGenerator::Generate
      // 1) flush all NoCivSpawn zones
      int i = m_Zones.Count;
      while(0 < i--) {
        if ("NoCivSpawn"==m_Zones[i].Name) m_Zones.RemoveAt(i);
      }
    }

    // Actor manipulation functions
    public bool HasActor(Actor actor)
    {
      return m_ActorsList.Contains(actor);
    }

    public Actor GetActor(int index)
    {
      return m_ActorsList[index];
    }

    public Actor GetActorAt(Point position)
    {
      m_aux_ActorsByPosition.TryGetValue(position, out Actor actor);
      return actor;
    }

    public Actor GetActorAtExt(Point pt)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(pt)) return GetActorAt(pt);
      Location? test = Normalize(pt);
      return null == test ? null : test.Value.Map.GetActorAt(test.Value.Position);
#else
      return GetActorAt(pt);
#endif
    }

    public bool HasActorAt(Point position)
    {
#if NO_PEACE_WALLS
      if (m_aux_ActorsByPosition.ContainsKey(position)) return true;
      if (IsInBounds(position)) return false;
      Location? tmp = Normalize(position);
      if (null == tmp) return false;
      return tmp.Value.Map.m_aux_ActorsByPosition.ContainsKey(tmp.Value.Position);
#else
      return m_aux_ActorsByPosition.ContainsKey(position);
#endif
    }

    public bool HasActorAt(int x, int y)
    {
      return HasActorAt(new Point(x, y));
    }

    public void PlaceAt(Actor actor, Point position)
    {
#if DEBUG
      if (null == actor) throw new ArgumentNullException(nameof(actor));
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
      Actor actorAt = GetActorAt(position);
      if (null != actorAt) throw new ArgumentOutOfRangeException(nameof(position),position, (actorAt == actor ? "actor already at position" : "another actor already at position"));
#endif
      lock(m_aux_ActorsByPosition) {
        // test game behaved rather badly when a second Samantha Collins was imprisoned on turn 0
        bool knows_on_map = actor.Location.Map == this;
        bool already_on_map = m_ActorsList.Contains(actor);
        if (already_on_map) {
          if (!knows_on_map) throw new InvalidOperationException(actor.Name+" did not know s/he was in the map");
#if AUDIT_ACTOR_MOVEMENT
          if (m_ActorsList.IndexOf(actor)<m_ActorsList.LastIndexOf(actor)) throw new InvalidOperationException(actor.Name + " is double-included");
          if (!m_aux_ActorsByPosition.ContainsKey(actor.Location.Position)) {
            foreach(var x in m_aux_ActorsByPosition) {
              if (x.Value==actor) new InvalidOperationException("map location cache out of sync");
            }
            throw new InvalidOperationException("map location cache out of sync");
          }
          if (m_aux_ActorsByPosition[actor.Location.Position]!=actor) {
            foreach(var x in m_aux_ActorsByPosition) {
             if (x.Value==actor) new InvalidOperationException("map location cache out of sync");
            }
            throw new InvalidOperationException("map location cache out of sync");
          }
#endif
          m_aux_ActorsByPosition.Remove(actor.Location.Position);
        } else {
#if AUDIT_ACTOR_MOVEMENT
          foreach(var x in m_aux_ActorsByPosition) {
           if (x.Value==actor) new InvalidOperationException("map location cache out of sync");
          }
#endif
          if (!knows_on_map) actor.RemoveFromMap();
          m_ActorsList.Add(actor);
          Engine.LOS.Now(this);
          if (actor.IsPlayer) Players.Recalc();
          if ((int)Gameplay.GameFactions.IDs.ThePolice == actor.Faction.ID) Police.Recalc();
        }
        m_aux_ActorsByPosition.Add(position, actor);
        actor.Location = new Location(this, position);
      } // lock(m_aux_ActorsByPosition)
      m_iCheckNextActorIndex = 0;
    }

    public void MoveActorToFirstPosition(Actor actor)
    {
#if DEBUG
      if (!m_ActorsList.Contains(actor)) throw new ArgumentException("actor not in map");
#endif
#if AUDIT_ACTOR_MOVEMENT
      if (m_ActorsList.IndexOf(actor)<m_ActorsList.LastIndexOf(actor)) throw new InvalidOperationException(actor.Name + " is double-included");
#endif
      if (1 == m_ActorsList.Count) return;
      m_ActorsList.Remove(actor);
      m_ActorsList.Insert(0, actor);
      m_iCheckNextActorIndex = 0;
      if (actor.IsPlayer) Players.Recalc();
      if ((int)Gameplay.GameFactions.IDs.ThePolice == actor.Faction.ID) Police.Recalc();
    }

    public void Remove(Actor actor)
    {
#if DEBUG
      // why you *really* should be using Actor::RemoveFromMap()
      if (this!=actor.Location.Map) throw new InvalidOperationException(actor.Name + " does not think he is in map to be removed from");
#endif
      lock(m_aux_ActorsByPosition) {
#if AUDIT_ACTOR_MOVEMENT
        if (m_ActorsList.IndexOf(actor)<m_ActorsList.LastIndexOf(actor)) throw new InvalidOperationException(actor.Name+" is double-included");
#endif
        if (m_ActorsList.Remove(actor)) {
#if AUDIT_ACTOR_MOVEMENT
          if (!m_aux_ActorsByPosition.ContainsKey(actor.Location.Position)) {
            foreach(var x in m_aux_ActorsByPosition) {
              if (x.Value==actor) new InvalidOperationException("map location cache out of sync");
            }
            throw new InvalidOperationException("map location cache out of sync");
          }
          if (m_aux_ActorsByPosition[actor.Location.Position]!=actor) {
            foreach(var x in m_aux_ActorsByPosition) {
              if (x.Value==actor) new InvalidOperationException("map location cache out of sync");
            }
            throw new InvalidOperationException("map location cache out of sync");
          }
#endif
          m_aux_ActorsByPosition.Remove(actor.Location.Position);
          m_iCheckNextActorIndex = 0;
          if (actor.IsPlayer) Players.Recalc();
          if ((int)Gameplay.GameFactions.IDs.ThePolice == actor.Faction.ID) Police.Recalc();
        }
#if AUDIT_ACTOR_MOVEMENT
        foreach(var x in m_aux_ActorsByPosition) {
          if (x.Value == actor) throw new InvalidOperationException(actor.Name+" still in position cache");
        }
        if (m_ActorsList.Contains(actor)) throw new InvalidOperationException(actor.Name + " still in map");
#endif
      }
    }

    public Actor NextActorToAct {
      get {
        int countActors = m_ActorsList.Count;
        // use working copy of m_iCheckNextActorIndex to mitigate multi-threading issues
        for (int checkNextActorIndex = m_iCheckNextActorIndex; checkNextActorIndex < countActors; ++checkNextActorIndex) {
          Actor actor = m_ActorsList[checkNextActorIndex];
          if (actor.CanActThisTurn && !actor.IsSleeping) {
            m_iCheckNextActorIndex = checkNextActorIndex;
            return actor;
          }
        }
        m_iCheckNextActorIndex = countActors;
        return null;
      }
    }

    // 2019-01-24: profiling indicates this is a cache target, but CPU cost of using cache ~25% greater than not having one
    private string ReasonNotWalkableFor(Point pt, ActorModel model)
    {
#if DEBUG
      if (null == model) throw new ArgumentNullException(nameof(model));
#endif
#if NO_PEACE_WALLS
      if (!IsInBounds(pt) && !HasExitAt(pt)) return "out of map";
#else
      if (!IsInBounds(pt)) return "out of map";
#endif
      if (!GetTileModelAtExt(pt).IsWalkable) return "blocked";
      MapObject mapObjectAt = GetMapObjectAtExt(pt);
      if (!mapObjectAt?.IsWalkable ?? false) {
        if (mapObjectAt.IsJumpable) {
          if (!model.Abilities.CanJump) return "cannot jump";
        } else if (model.Abilities.IsSmall) {
          if (mapObjectAt is DoorWindow doorWindow && doorWindow.IsClosed) return "cannot slip through closed door";
        } else return "blocked by object";
      }
      if (HasActorAt(pt)) return "someone is there";  // XXX includes actor himself
      return "";
    }

    public bool IsWalkableFor(Point p, ActorModel model)
    {
      return string.IsNullOrEmpty(ReasonNotWalkableFor(p, model));
    }

    public bool IsWalkableFor(Point p, ActorModel model, out string reason)
    {
      reason = ReasonNotWalkableFor(p, model);
      return string.IsNullOrEmpty(reason);
    }

    private string ReasonNotWalkableFor(Point pt, Actor actor)
    {
#if DEBUG
      if (null == actor) throw new ArgumentNullException(nameof(actor));
#endif
#if NO_PEACE_WALLS
      if (!IsInBounds(pt) && !HasExitAt(pt)) return "out of map";
#else
      if (!IsInBounds(pt)) return "out of map";
#endif
      if (!GetTileModelAtExt(pt).IsWalkable) return "blocked";
      MapObject mapObjectAt = GetMapObjectAtExt(pt);
      if (!mapObjectAt?.IsWalkable ?? false) {
        if (mapObjectAt.IsJumpable) {
          if (!actor.CanJump) return "cannot jump";
          // We only have to be completely accurate when adjacent to a square.
          if (actor.StaminaPoints < Engine.Rules.STAMINA_COST_JUMP && Engine.Rules.IsAdjacent(actor.Location,new Location(this,pt))) return "not enough stamina to jump";
        } else if (actor.Model.Abilities.IsSmall) {
          if (mapObjectAt is DoorWindow doorWindow && doorWindow.IsClosed) return "cannot slip through closed door";
        } else return "blocked by object";
      }
      // 1) does not have to be accurate except when adjacent
      // 2) treat null map as "omni-adjacent" (happens during spawning)
      if ((null==actor.Location.Map || Engine.Rules.IsAdjacent(actor.Location,new Location(this, pt))) && HasActorAt(pt)) return "someone is there";  // XXX includes actor himself
      if (actor.DraggedCorpse != null && actor.IsTired) return "dragging a corpse when tired";
      return "";
    }

    public bool IsWalkableFor(Point p, Actor actor)
    {
      return string.IsNullOrEmpty(ReasonNotWalkableFor(p, actor));
    }

    public bool IsWalkableFor(Point p, Actor actor, out string reason)
    {
      reason = ReasonNotWalkableFor(p, actor);
      return string.IsNullOrEmpty(reason);
    }

    // AI-ish, but essentially a map geometry property
    // we are considering a non-jumpable pushable object here (e.g. shop shelves)
    public bool PushCreatesSokobanPuzzle(Point dest,Actor actor)
    {
      if (HasExitAt(dest)) return true;   // this just isn't a good idea for pathing

      Span<bool> is_wall = stackalloc bool[8];   // these default-initialize to false
      Span<bool> blocked = stackalloc bool[8];
      Span<bool> no_go = stackalloc bool[8];
      foreach(Point pt2 in dest.Adjacent()) {
        if (actor.Location.Map==this && actor.Location.Position==pt2) continue;
        if (IsWalkableFor(pt2,actor.Model)) continue;   // not interested in stamina for this
        Direction dir = Direction.FromVector(pt2.X-dest.X,pt2.Y-dest.Y);
#if DEBUG
        if (null == dir) throw new ArgumentNullException(nameof(dir));
#endif
        no_go[dir.Index] = true;
        if (IsValid(pt2) && GetTileModelAtExt(pt2).IsWalkable) blocked[dir.Index] = true;
        else is_wall[dir.Index] = true;
      }
      // corners and walls are generally ok.  2019-01-04: preliminary tests suggest this is not a micro-optimization target
      if (is_wall[(int)Compass.XCOMlike.NW] && is_wall[(int)Compass.XCOMlike.N] && is_wall[(int)Compass.XCOMlike.NE] && !is_wall[(int)Compass.XCOMlike.S] && (!is_wall[(int)Compass.XCOMlike.E] || !is_wall[(int)Compass.XCOMlike.W])) return false;
      if (is_wall[(int)Compass.XCOMlike.SW] && is_wall[(int)Compass.XCOMlike.S] && is_wall[(int)Compass.XCOMlike.SE] && !is_wall[(int)Compass.XCOMlike.N] && (!is_wall[(int)Compass.XCOMlike.E] || !is_wall[(int)Compass.XCOMlike.W])) return false;
      if (is_wall[(int)Compass.XCOMlike.NW] && is_wall[(int)Compass.XCOMlike.W] && is_wall[(int)Compass.XCOMlike.SW] && !is_wall[(int)Compass.XCOMlike.E] && (!is_wall[(int)Compass.XCOMlike.N] || !is_wall[(int)Compass.XCOMlike.S])) return false;
      if (is_wall[(int)Compass.XCOMlike.NE] && is_wall[(int)Compass.XCOMlike.E] && is_wall[(int)Compass.XCOMlike.SE] && !is_wall[(int)Compass.XCOMlike.W] && (!is_wall[(int)Compass.XCOMlike.N] || !is_wall[(int)Compass.XCOMlike.S])) return false;

      // blocking access to something that could be next to wall/corner is problematic
      if (blocked[(int)Compass.XCOMlike.N] && blocked[(int)Compass.XCOMlike.W] && no_go[(int)Compass.XCOMlike.NW] 
          && (no_go[(int)Compass.XCOMlike.NE] || no_go[(int)Compass.XCOMlike.SW])) return true;
      if (blocked[(int)Compass.XCOMlike.N] && blocked[(int)Compass.XCOMlike.E] && no_go[(int)Compass.XCOMlike.NE] 
          && (no_go[(int)Compass.XCOMlike.NW] || no_go[(int)Compass.XCOMlike.SE])) return true;
      if (blocked[(int)Compass.XCOMlike.S] && blocked[(int)Compass.XCOMlike.W] && no_go[(int)Compass.XCOMlike.SW]
          && (no_go[(int)Compass.XCOMlike.SE] || no_go[(int)Compass.XCOMlike.NW])) return true;
      if (blocked[(int)Compass.XCOMlike.S] && blocked[(int)Compass.XCOMlike.E] && no_go[(int)Compass.XCOMlike.SE]
          && (no_go[(int)Compass.XCOMlike.SW] || no_go[(int)Compass.XCOMlike.NE])) return true;

      return false;
    }

    // tracking players on map
    public int PlayerCorpseCount {
      get {
        int now = Engine.Session.Get.WorldTime.TurnCounter;
        return m_CorpsesList.Count(c => c.DeadGuy.IsPlayer && Engine.Rules.CORPSE_ZOMBIFY_DELAY<= now - c.Turn);    // align with Rules::CorpseZombifyChance
      }
    }

    public int PlayerCount { get { return Players.Get.Count; } }
    public Actor FindPlayer { get { return Players.Get.FirstOrDefault(); } }

    public bool MessagePlayerOnce(Action<Actor> fn, Func<Actor, bool> pred =null)
    {
#if DEBUG
      if (null == fn) throw new ArgumentNullException(nameof(fn));
#endif
      void pan_to(Actor a) {
          RogueForm.Game.PanViewportTo(a);
          fn(a);
      };

      return (null == pred ? Players.Get.ActOnce(pan_to)
                           : Players.Get.ActOnce(pan_to, pred));
    }

    // map object manipulation functions
    public bool HasMapObject(MapObject mapObj)
    {
      return m_MapObjectsList.Contains(mapObj);
    }

    public MapObject GetMapObjectAt(Point position)
    {
      if (m_aux_MapObjectsByPosition.TryGetValue(position, out MapObject mapObject)) {
#if DEBUG
        // existence check for bugs relating to map object location
        if (this!=mapObject.Location.Map) throw new InvalidOperationException("map object and map disagree on map");
        if (position!=mapObject.Location.Position) throw new InvalidOperationException("map object and map disagree on position");
#endif
        return mapObject;
      }
      return null;
    }

    public MapObject GetMapObjectAtExt(int x, int y)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(x,y)) return GetMapObjectAt(new Point(x, y));
      Location? test = Normalize(new Point(x,y));
      if (null==test) return null;
      return test.Value.Map.GetMapObjectAt(test.Value.Position);
#else
      return GetMapObjectAt(new Point(x, y));
#endif
    }

    public MapObject GetMapObjectAtExt(Point pt)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(pt)) return GetMapObjectAt(pt);
      Location? test = Normalize(pt);
      if (null==test) return null;
      return test.Value.Map.GetMapObjectAt(test.Value.Position);
#else
      return GetMapObjectAt(new Point(x, y));
#endif
    }

    public bool HasMapObjectAt(Point position)
    {
      return m_aux_MapObjectsByPosition.ContainsKey(position);
    }

    public bool HasMapObjectAtExt(Point position)
    {
      if (m_aux_MapObjectsByPosition.ContainsKey(position)) return true;
      Location? test = Normalize(position);
      if (null == test) return false;
      return test.Value.Map.HasMapObjectAt(test.Value.Position);
    }

    public void PlaceAt(MapObject mapObj, Point position)
    {
#if DEBUG
      if (null == mapObj) throw new ArgumentNullException(nameof(mapObj));
#endif
      if (!IsInBounds(position)) {
        // cross-map push or similar
        Location? test = Normalize(position);
#if DEBUG
        if (null == test) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsValid(position)");
#endif
        test.Value.Map.PlaceAt(mapObj,test.Value.Position); // intentionally not using thin wrapper
        return;
      }
#if DEBUG
      if (!GetTileModelAt(position).IsWalkable) throw new ArgumentOutOfRangeException(nameof(position),position, "!GetTileModelAt(position).IsWalkable");
#endif
      MapObject mapObjectAt = GetMapObjectAt(position);
      if (mapObjectAt == mapObj) return;
#if DEBUG
      if (null != mapObjectAt) throw new ArgumentOutOfRangeException(nameof(position), position, "null != GetMapObjectAt(position)");
#endif
      // cf Map::PlaceAt(Actor,Position)
      if (null != mapObj.Location.Map && HasMapObject(mapObj))
        m_aux_MapObjectsByPosition.Remove(mapObj.Location.Position);
      else {
        if (null != mapObj.Location.Map && this != mapObj.Location.Map) mapObj.Remove();
        m_MapObjectsList.Add(mapObj);
      }
      m_aux_MapObjectsByPosition.Add(position, mapObj);
      mapObj.Location = new Location(this, position);
    }

    public void RemoveMapObjectAt(Point pt)
    {
      MapObject mapObjectAt = GetMapObjectAt(pt);
      if (mapObjectAt == null) return;
      m_MapObjectsList.Remove(mapObjectAt);
      m_aux_MapObjectsByPosition.Remove(pt);
    }

    public bool IsTrapCoveringMapObjectAt(Point pos)
    {
      MapObject mapObjectAt = GetMapObjectAt(pos);
      if (mapObjectAt == null) return false;
      if (mapObjectAt is DoorWindow) return false;
      if (mapObjectAt.IsJumpable) return true;
      return mapObjectAt.IsWalkable;
    }

    public MapObject GetTrapTriggeringMapObjectAt(Point pos)
    {
      MapObject mapObjectAt = GetMapObjectAt(pos);
      if (mapObjectAt == null) return null;
      if (mapObjectAt is DoorWindow) return null;
      if (mapObjectAt.IsJumpable) return null;
      if (mapObjectAt.IsWalkable) return null;
      return mapObjectAt;
    }

    public int TrapsMaxDamageAtFor(Point pos, Actor a)  // XXX exceptionally likely to be a nonserialized cache target
    {
      Inventory itemsAt = GetItemsAt(pos);
      if (itemsAt == null) return 0;
      int num = 0;
      foreach (Item obj in itemsAt.Items) {
        if (obj is Engine.Items.ItemTrap trap && !a.IsSafeFrom(trap)) num += trap.Model.Damage;
      }
      return num;
    }


    public void OpenAllGates()
    {
      foreach(MapObject obj in m_MapObjectsList) {
        if (MapObject.IDs.IRON_GATE_CLOSED != obj.ID) continue;
        obj.ID = MapObject.IDs.IRON_GATE_OPEN;
        RogueForm.Game.OnLoudNoise(obj.Location,this== Engine.Session.Get.UniqueMaps.PoliceStation_JailsLevel.TheMap ? "cell opening" : "gate opening");
      }
    }

    public double PowerRatio {
      get {
        return (double)(PowerGenerators.Get.Count(it => it.IsOn))/PowerGenerators.Get.Count;
      }
    }

    public bool HasItemsAt(Point position)
    {
#if AUDIT_ITEM_INVARIANTS
      if (!IsInBounds(position)) return false;
#endif
      return m_GroundItemsByPosition.ContainsKey(position);
    }

    public Inventory GetItemsAt(Point position)
    {
      if (!IsInBounds(position)) return null;
      if (m_GroundItemsByPosition.TryGetValue(position, out Inventory inventory)) {
#if AUDIT_ITEM_INVARIANTS
        if (inventory?.IsEmpty ?? true) throw new ArgumentNullException(nameof(inventory));
#endif
        return inventory;
      }
      return null;
    }

    public Inventory GetItemsAtExt(Point pt)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(pt)) return GetItemsAt(pt);
      Location? test = Normalize(pt);
      if (null==test) return null;
      return test.Value.Map.GetItemsAt(test.Value.Position);
#else
      return GetItemsAt(pt);
#endif
    }

    public Dictionary<Point, Inventory> GetAccessibleInventories(Point pt)
    {
      var ground_inv = new Dictionary<Point, Inventory>();
      Inventory inv = GetItemsAtExt(pt);
      if (!inv?.IsEmpty ?? false) ground_inv[pt] = inv;
      foreach(var adjacent in pt.Adjacent()) {
        inv = GetItemsAtExt(adjacent);
        if (inv?.IsEmpty ?? true) continue;
        MapObject mapObjectAt = GetMapObjectAtExt(adjacent);
        if (null == mapObjectAt) continue;
        if (!mapObjectAt.IsContainer) continue; // XXX this is scheduled for revision
        ground_inv[adjacent] = inv;
      }
      return ground_inv;
    }


    public Engine.Items.ItemTrap GetActivatedTrapAt(Point pos)
    {
      return GetItemsAt(pos)?.GetFirstMatching<Engine.Items.ItemTrap>(it => it.IsActivated);
    }

    public Point? GetGroundInventoryPosition(Inventory groundInv)
    {
      foreach (KeyValuePair<Point, Inventory> keyValuePair in m_GroundItemsByPosition) {
        if (keyValuePair.Value == groundInv) return keyValuePair.Key;
      }
      return null;
    }

    // Clairvoyant.  Useful for fine-tuning map generation and little else
    private KeyValuePair<Point, Inventory>? GetInventoryHaving(Gameplay.GameItems.IDs id)
    {
      if (District.Maps.Contains(this)) throw new InvalidOperationException("do not use GetInventoryHaving except during map generation");
      foreach (KeyValuePair<Point, Inventory> keyValuePair in m_GroundItemsByPosition) {
        if (keyValuePair.Value.Has(id)) return keyValuePair;
      }
      return null;
    }

    public void DropItemAt(Item it, Point position)
    {
#if DEBUG
      if (null == it) throw new ArgumentNullException(nameof(it));
      if (!GetTileModelAt(position).IsWalkable) throw new InvalidOperationException("tried to drop "+it+" on a wall at "+(new Location(this,position)));
#endif
      Inventory itemsAt = GetItemsAt(position);
      if (itemsAt == null) {
        Inventory inventory = new Inventory(GROUND_INVENTORY_SLOTS);
        m_GroundItemsByPosition.Add(position, inventory);
        inventory.AddAll(it);
      } else if (itemsAt.IsFull) {
        int quantity = it.Quantity;
        int quantityAdded = itemsAt.AddAsMuchAsPossible(it);
        if (quantityAdded >= quantity) return;
        // Hammerspace inventory is already gamey.  We can afford to be even more gamey if it makes things more playable.
        // ensure that legendary artifacts don't disappear (yes, could infinite-loop but there aren't that many artifacts)
        Item crushed = itemsAt.BottomItem;
        while(crushed.Model.IsUnbreakable || crushed.IsUnique) {
          itemsAt.RemoveAllQuantity(crushed);
          itemsAt.AddAll(crushed);
          crushed = itemsAt.BottomItem;
        }
        // the test game series ending with the savefile break on April 28 2018 had a number of stacks with lots of baseball bats.  If there are two or more
        // destructible melee weapons in a stack, the worst one can be destroyed with minimal inconvenience.
        // Cf. Actor::GetWorstMeleeWeapon
        {
        var melee = itemsAt.GetItemsByType<ItemMeleeWeapon>()?.Where(m => !m.Model.IsUnbreakable && !m.IsUnique);
        if (2 <= (melee?.Count() ?? 0)) crushed = melee.Minimize(w => w.Model.Attack.Rating);
        }

        // other (un)reality checks go here
        itemsAt.RemoveAllQuantity(crushed);
        itemsAt.AddAsMuchAsPossible(it);
      }
      else
        itemsAt.AddAll(it);
    }

    public void DropItemAtExt(Item it, Point position)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(position)) {
        DropItemAt(it, position);
        return;
      }
      Location? tmp = Normalize(position);
      if (null == tmp) throw new ArgumentOutOfRangeException(nameof(position),position,"invalid position for Item "+nameof(it));
      tmp.Value.Map.DropItemAt(it,tmp.Value.Position);
#else
      DropItemAt(it,position);
#endif
    }

    public void RemoveItemAt(Item it, Point position)
    {
#if DEBUG
      if (null == it) throw new ArgumentNullException(nameof(it));
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
#endif
      Inventory itemsAt = GetItemsAt(position);
#if DEBUG
      if (null == itemsAt) throw new ArgumentNullException(nameof(itemsAt),":= GetItemsAt(position)");
      if (!itemsAt.Contains(it)) throw new ArgumentOutOfRangeException(nameof(itemsAt),"item not at this position");
#endif
      itemsAt.RemoveAllQuantity(it);
      if (itemsAt.IsEmpty) m_GroundItemsByPosition.Remove(position);
    }

    public void RemoveAt<T>(IEnumerable<T> src, Point position) where T:Item
    {
#if DEBUG
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
#endif
      if (null==src) return;
      Inventory itemsAt = GetItemsAt(position);
#if DEBUG
      if (null == itemsAt) throw new ArgumentNullException(nameof(itemsAt));
#endif
      foreach(T it in src) {
#if DEBUG
        if (!itemsAt.Contains(it)) throw new InvalidOperationException("item not at this position");
#endif
        itemsAt.RemoveAllQuantity(it);
      }
      if (itemsAt.IsEmpty) m_GroundItemsByPosition.Remove(position);
    }

    // Clairvoyant.
    public bool TakeItemType(Gameplay.GameItems.IDs id, Inventory dest)
    {
#if DEBUG
      if (null == dest) throw new ArgumentNullException(nameof(dest));
#endif
      var src = GetInventoryHaving(id);
      if (null == src) return false;
      Item it = src.Value.Value.GetFirst(id);
      if (null == it) return false;
      src.Value.Value.RemoveAllQuantity(it);
      dest.AddAsMuchAsPossible(it);
      if (src.Value.Value.IsEmpty) m_GroundItemsByPosition.Remove(src.Value.Key);
      return true;
    }

    // Clairvoyant.
    public bool SwapItemTypes(Gameplay.GameItems.IDs want, Gameplay.GameItems.IDs donate, Inventory dest)
    {
#if DEBUG
      if (null == dest) throw new ArgumentNullException(nameof(dest));
#endif
      Item giving = dest.GetFirst(donate);
      if (null == giving) return TakeItemType(want, dest);

      var src = GetInventoryHaving(want);
      if (null == src) return false;
      Item it = src.Value.Value.GetFirst(want);
      if (null == it) return false;
      it.Unequip();

      src.Value.Value.RemoveAllQuantity(it);
      dest.RemoveAllQuantity(giving);
      src.Value.Value.AddAsMuchAsPossible(giving);
      dest.AddAsMuchAsPossible(it);
      return true;
    }

    public void RemoveItemAtExt(Item it, Point position)
    {
#if DEBUG
      if (null == it) throw new ArgumentNullException(nameof(it));
#endif
      if (IsInBounds(position)) {
        RemoveItemAt(it,position);
        return;
      }
      Location? test = Normalize(position);
      if (null != test) test.Value.Map.RemoveItemAt(it, test.Value.Position);
    }

    public void RemoveAtExt<T>(IEnumerable<T> src, Point position) where T:Item
    {
      if (null == src) return;
      if (IsInBounds(position)) {
        RemoveAt(src,position);
        return;
      }
      Location? test = Normalize(position);
      if (null != test) test.Value.Map.RemoveAt(src, test.Value.Position);
    }

#if DEAD_FUNC
    public void RemoveItemAt(Item it, int x, int y)
    {
      RemoveItemAt(it, new Point(x, y));
    }
#endif

    /// <remark>Map generation depends on this being no-fail</remark>
    public void RemoveAllItemsAt(Point position)
    {
      m_GroundItemsByPosition.Remove(position);
    }

    public List<Corpse> GetCorpsesAt(Point p)
    {
      if (m_aux_CorpsesByPosition.TryGetValue(p, out List<Corpse> corpseList))
        return corpseList;
      return null;
    }

    public List<Corpse> GetCorpsesAtExt(Point p)
    {
#if NO_PEACE_WALLS
      if (IsInBounds(p)) return GetCorpsesAt(p);
      Location? test = Normalize(p);
      if (null==test) return null;
      return test.Value.Map.GetCorpsesAt(test.Value.Position);
#else
      return GetCorpsesAt(p);
#endif
    }

    public bool HasCorpsesAt(Point p)
    {
      return m_aux_CorpsesByPosition.ContainsKey(p);
    }


    public bool Has(Corpse c)
    {
      return m_CorpsesList.Contains(c);
    }

    public void AddAt(Corpse c, Point p)
    {
      if (m_CorpsesList.Contains(c)) throw new ArgumentException("corpse already in this map");
      c.Position = p;
      m_CorpsesList.Add(c);
      InsertAtPos(c);
      c.DeadGuy.Location = new Location(this, p);
    }

    public void MoveTo(Corpse c, Point newPos)
    {
      if (!m_CorpsesList.Contains(c)) throw new ArgumentException("corpse not in this map");
      RemoveFromPos(c);
      c.Position = newPos;
      InsertAtPos(c);
      c.DeadGuy.Location = new Location(this, newPos);
    }

    public void Remove(Corpse c)
    {
      if (!m_CorpsesList.Remove(c)) throw new ArgumentException("corpse not in this map");
      RemoveFromPos(c);
    }

    public void Destroy(Corpse c)
    {
      c.DraggedBy?.StopDraggingCorpse();
      Remove(c);
    }

    public bool TryRemoveCorpseOf(Actor a)
    {
      foreach (Corpse mCorpses in m_CorpsesList) {
        if (mCorpses.DeadGuy == a) {
          Remove(mCorpses);
          return true;
        }
      }
      return false;
    }

    private void RemoveFromPos(Corpse c)
    {
      if (!m_aux_CorpsesByPosition.TryGetValue(c.Position, out List<Corpse> corpseList)) return;
      corpseList.Remove(c);
      if (corpseList.Count != 0) return;
      m_aux_CorpsesByPosition.Remove(c.Position);
    }

    private void InsertAtPos(Corpse c)
    {
      if (m_aux_CorpsesByPosition.TryGetValue(c.Position, out List<Corpse> corpseList))
        corpseList.Insert(0, c);
      else
        m_aux_CorpsesByPosition.Add(c.Position, new List<Corpse>(1) { c });
    }

    public void AddTimer(TimedTask t)
    {
      m_Timers.Add(t);
    }

#if DEAD_FUNC
    public void RemoveTimer(TimedTask t)    // would be expected by Create-Read-Update-Delete idiom
    {
      m_Timers.Remove(t);
    }
#endif

    public void UpdateTimers()
    {
      int i = m_Timers?.Count ?? 0;
      if (0 >= i) return;
      // we use this idiom to allow a triggering timer to add more timers to the map safely
      while(0 < i--) {
        var timer = m_Timers[i];
        timer.Tick(this);
        if (timer.IsCompleted) m_Timers.RemoveAt(i);
      }
    }

    public int GetScentByOdorAt(Odor odor, Point position)
    {
      if (IsInBounds(position)) {
        OdorScent scentByOdor = GetScentByOdor(odor, position);
        if (scentByOdor != null) return scentByOdor.Strength;
#if NO_PEACE_WALLS
      } else if (IsStrictlyValid(position)) {
        Location? tmp = Normalize(position);
        if (null != tmp) {
          OdorScent scentByOdor = tmp.Value.Map.GetScentByOdor(odor, tmp.Value.Position);
          if (scentByOdor != null) return scentByOdor.Strength;
        }
#endif
      }
      return 0;
    }

    private OdorScent GetScentByOdor(Odor odor, Point p)
    {
      if (!m_ScentsByPosition.TryGetValue(p, out List<OdorScent> odorScentList)) return null;
      foreach (OdorScent odorScent in odorScentList) {
        if (odorScent.Odor == odor) return odorScent;
      }
      return null;
    }

    private void AddNewScent(OdorScent scent, Point position)
    {
      if (m_ScentsByPosition.TryGetValue(position, out List<OdorScent> odorScentList)) {
        odorScentList.Add(scent);
      } else {
        m_ScentsByPosition.Add(position, new List<OdorScent>(2) { scent });
      }
    }

#if OBSOLETE
    public void ModifyScentAt(Odor odor, int strengthChange, Point position)
    {
#if DEBUG
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
#endif
      OdorScent scentByOdor = GetScentByOdor(odor, position);
      if (scentByOdor == null) {
        if (0 < strengthChange) AddNewScent(new OdorScent(odor, strengthChange), position);
      } else
        scentByOdor.Strength += strengthChange;
    }
#endif

    public void RefreshScentAt(Odor odor, int freshStrength, Point position)
    {
#if DEBUG
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
#endif
      OdorScent scentByOdor = GetScentByOdor(odor, position);
      if (scentByOdor == null) {
        AddNewScent(new OdorScent(odor, freshStrength), position);
      } else if (scentByOdor.Strength < freshStrength) {
        scentByOdor.Strength = freshStrength;
      }
    }

#if DEAD_FUNC
    public void RemoveScent(OdorScent scent)
    {
      if (!m_ScentsByPosition.TryGetValue(scent.Position, out List<OdorScent> odorScentList)) return;
      odorScentList.Remove(scent);
      if (0 >= odorScentList.Count) m_ScentsByPosition.Remove(scent.Position);
    }
#endif

#if OBSOLETE
    public void ApplyArtificialStench()
    {
        var living_suppress = new Dictionary<Point,int>();
        var living_generate = new Dictionary<Point,int>();
        foreach(var tmp in m_ScentsByPosition) {
          living_suppress[tmp.Key] = 0;
          living_generate[tmp.Key] = 0;
          foreach(OdorScent scent in tmp.Value) {
            switch (scent.Odor) {
              case Odor.PERFUME_LIVING_SUPRESSOR:
                living_suppress[tmp.Key] += scent.Strength;
                continue;
              case Odor.PERFUME_LIVING_GENERATOR:
                living_generate[tmp.Key] += scent.Strength;
                continue;
              default:
                continue;
            }
          }
          if (0 < living_suppress[tmp.Key] && 0 < living_generate[tmp.Key]) {
            int tmp2 = Math.Min(living_suppress[tmp.Key],living_generate[tmp.Key]);
            living_suppress[tmp.Key] -= tmp2;
            living_generate[tmp.Key] -= tmp2;
          }
          if (0 >= living_suppress[tmp.Key]) living_suppress.Remove(tmp.Key);
          if (0 >= living_generate[tmp.Key]) living_generate.Remove(tmp.Key);
        }
        foreach(var x in living_generate) ModifyScentAt(Odor.LIVING, x.Value, x.Key);
        foreach(var x2 in living_suppress) ModifyScentAt(Odor.LIVING, -x2.Value, x2.Key);
    }
#endif

    public void DecayScents()
    {
      // Cf. Location.OdorsDecay
      int mapOdorDecayRate = 1;
      if (this == District.SewersMap) mapOdorDecayRate += 2;

      var discard = new List<OdorScent>();
      List<Point> discard2 = null;
      foreach(var tmp in m_ScentsByPosition) {
        int odorDecayRate = (3==mapOdorDecayRate ? mapOdorDecayRate : new Location(this,tmp.Key).OdorsDecay()); // XXX could micro-optimize further
        foreach(OdorScent scent in tmp.Value) {
          scent.Strength -= odorDecayRate;
          if (0 >= scent.Strength) discard.Add(scent);  // XXX looks like it could depend on OdorScent being class rather than struct, but if that were to matter we'd have to lock anyway.
        }
        if (0 < discard.Count) {
          foreach(var x in discard) tmp.Value.Remove(x);
          discard.Clear();
          if (0 >= tmp.Value.Count) (discard2 ?? (discard2 = new List<Point>())).Add(tmp.Key);
        }
      }
      if (null != discard2) {
        foreach(var x in discard2) m_ScentsByPosition.Remove(x);
      }
    }

    public void PreTurnStart()
    {
      m_iCheckNextActorIndex = 0;
      foreach (var actor in m_ActorsList) actor.PreTurnStart();
    }

    public bool IsTransparent(int x, int y)
    {
      if (!IsValid(x, y) || !GetTileModelAtExt(x, y).IsTransparent) return false;
      return GetMapObjectAtExt(x, y)?.IsTransparent ?? true;
    }

    public bool IsWalkable(int x, int y)
    {
      if (!IsValid(x, y) || !GetTileModelAtExt(x, y).IsWalkable) return false;
      return GetMapObjectAtExt(x, y)?.IsWalkable ?? true;
    }

    public bool IsWalkable(Point p)
    {
      return IsWalkable(p.X, p.Y);
    }

    public bool IsBlockingFire(int x, int y)
    {
      if (!IsValid(x, y) || !GetTileModelAtExt(x, y).IsTransparent || HasActorAt(x, y)) return true;
      return !GetMapObjectAtExt(x, y)?.IsTransparent ?? false;
    }

    public bool IsBlockingThrow(int x, int y)
    {
      if (!IsValid(x, y) || !GetTileModelAtExt(x, y).IsWalkable) return true;
      MapObject mapObjectAt = GetMapObjectAtExt(x, y);
      return mapObjectAt != null && !mapObjectAt.IsWalkable && !mapObjectAt.IsJumpable;
    }

    /// <returns>0 not blocked, 1 jumping required, 2 blocked (for livings)</returns>
    public int IsBlockedForPathing(Point pt)
    {
      // blockers are:
      // walls (hard) !map.GetTileModelAt(pt).IsWalkable
      // non-enterable objects (hard)
      // jumpable objects (soft) map.GetMapObjectAt(pt)
      MapObject obj = GetMapObjectAtExt(pt);
      if (null == obj) return 0;
      if (obj.IsCouch) return 0;
      if (obj.IsWalkable) return 0;
      if (obj.IsJumpable) return 1;
      return 2;
    }

#if DEAD_FUNC
    /// <returns>0 not blocked, 1 jumping required both ways, 2 one wall one jump, 3 two walls (for livings)</returns>
    private int IsPathingChokepoint(Point x0, Point x1)
    {
      int x0_blocked = IsBlockedForPathing(x0);
      if (0== x0_blocked) return 0;
      int blocked = x0_blocked*IsBlockedForPathing(x1);
      // range is: 0,1,2,4; want to return 0...3
      return 4==blocked ? 3 : blocked;
    }
#endif

    /// <returns>worst blockage status code of IsBlockedForPathing</returns>
    public int CreatesPathingChokepoint(Point pt)
    {
      int block_N = IsBlockedForPathing(pt+Direction.N);
      int block_S = IsBlockedForPathing(pt+Direction.S);
      if (2==block_N && 2==block_S) return 2;
      int block_W = IsBlockedForPathing(pt+Direction.W);
      int block_E = IsBlockedForPathing(pt+Direction.E);
      if (2==block_W && 2==block_E) return 2;
      if (1==block_N*block_S) return 1;
      if (1==block_W*block_E) return 1;
      // would return 0 here when testing for *is* a pathing chokepoint
      if (1==block_N && 0<IsBlockedForPathing(pt+Direction.N+Direction.N)) return 1;
      if (1==block_S && 0<IsBlockedForPathing(pt+Direction.S+Direction.S)) return 1;
      if (1==block_W && 0<IsBlockedForPathing(pt+Direction.W+Direction.W)) return 1;
      if (1==block_E && 0<IsBlockedForPathing(pt+Direction.E+Direction.E)) return 1;
      return 0;
    }

    public Dictionary<Point,Direction> ValidDirections(Point pos, Func<Map, Point, bool> testFn)
    {
#if DEBUG
      if (null == testFn) throw new ArgumentNullException(nameof(testFn));
#endif
      var ret = new Dictionary<Point,Direction>(8);
      foreach(Direction dir in Direction.COMPASS) {
        Point pt = pos+dir;
        if (!testFn(this,pt)) continue;
        ret[pt] = dir;
      }
      return ret;
    }

    public void EndTurn()
    {
        if (IsSecret) return;   // time-stopped
        pathing_exits_to_goals.Now(LocalTime.TurnCounter);
    }

    /// <remark>testFn has to tolerate denormalized coordinates</remark>
    public Dictionary<Point,T> FindAdjacent<T>(Point pos, Func<Map,Point,T> testFn) where T:class
    {
#if DEBUG
      if (null == testFn) throw new ArgumentNullException(nameof(testFn));
      if (!IsInBounds(pos)) throw new InvalidOperationException("!IsInBounds(pos)");
#endif
      var ret = new Dictionary<Point,T>();
      foreach(Point pt in Direction.COMPASS.Select(dir => pos + dir)) {
        T test = testFn(this,pt);
        if (null == test) continue;
        ret[pt] = test;
      }
      return ret;
    }

    public List<Point> FilterAdjacentInMap(Point position, Predicate<Point> predicateFn)
    {
#if DEBUG
      if (null == predicateFn) throw new ArgumentNullException(nameof(predicateFn));
#endif
      if (!IsInBounds(position)) return null;
      IEnumerable<Point> tmp = Direction.COMPASS.Select(dir=>position+dir).Where(p=>IsInBounds(p) && predicateFn(p));
      return (tmp.Any() ? tmp.ToList() : null);
    }

    public bool HasAnyAdjacentInMap(Point position, Predicate<Point> predicateFn)
    {
#if DEBUG
      if (null == predicateFn) throw new ArgumentNullException(nameof(predicateFn));
#endif
      if (!IsInBounds(position)) return false;
      return position.Adjacent().Any(p=>IsInBounds(p) && predicateFn(p));
    }

#if DEAD_FUNC
    public bool HasAnyAdjacent(Point position, Predicate<Point> predicateFn)
    {
#if DEBUG
      if (null == predicateFn) throw new ArgumentNullException(nameof(predicateFn));
#endif
      if (!IsValid(position)) return false;
      return position.Adjacent().Any(p=>IsValid(p) && predicateFn(p));
    }
#endif

    public bool HasAnyAdjacent(Point position, Predicate<Actor> test)
    {
#if DEBUG
      if (null == test) throw new ArgumentNullException(nameof(test));
#endif
      if (!IsValid(position)) return false;
      foreach(Point pt in position.Adjacent()) {
        if (!IsValid(pt)) continue;
        Actor a = GetActorAtExt(pt);
        if (null != a && test(a)) return true;
      }
      return false;
    }

    public int CountAdjacentTo(Point position, Predicate<Point> predicateFn)
    {
#if DEBUG
      if (null == predicateFn) throw new ArgumentNullException(nameof(predicateFn));
#endif
      if (!IsInBounds(position)) return 0;
      return Direction.COMPASS.Select(dir => position + dir).Count(p=>IsInBounds(p) && predicateFn(p));
    }

    public int CountAdjacent<T>(Point pos) where T:MapObject
    {
      return CountAdjacentTo(pos, pt => GetMapObjectAt(pt) is T);
    }

    public int CountAdjacent<T>(Point pos,Predicate<T> test) where T:MapObject
    {
#if DEBUG
      if (null == test) throw new ArgumentNullException(nameof(test));
#endif
      return CountAdjacentTo(pos, pt => GetMapObjectAt(pt) is T obj && test(obj));
    }

    public bool AnyAdjacent<T>(Point pos) where T:MapObject
    {
      return HasAnyAdjacentInMap(pos, pt => GetMapObjectAt(pt) is T);
    }

    public bool AnyAdjacentExt<T>(Point pos) where T:MapObject
    {
      return HasAnyAdjacentInMap(pos, pt => GetMapObjectAtExt(pt) is T);
    }

    public bool AnyAdjacent<T>(Point pos,Predicate<T> test) where T:MapObject
    {
#if DEBUG
      if (null == test) throw new ArgumentNullException(nameof(test));
#endif
      return HasAnyAdjacentInMap(pos, pt => GetMapObjectAt(pt) is T obj && test(obj));
    }


    public void ForEachAdjacent(Point position, Action<Point> fn)
    {
#if DEBUG
      if (null == fn) throw new ArgumentNullException(nameof(fn));
      if (!IsInBounds(position)) throw new ArgumentOutOfRangeException(nameof(position),position, "!IsInBounds(position)");
#endif
      foreach (Point p in Direction.COMPASS.Select(d => position+d)) {
        if (IsInBounds(p)) fn(p);
      }
    }

#if DEAD_FUNC
    public void ForEachAdjacent(int x, int y, Action<Point> fn)
    {
      ForEachAdjacent(new Point(x,y),fn);
    }
#endif

    // pathfinding support
    public Rectangle NavigationScope {
      get {
       if (this == District.SewersMap) return new Rectangle(District.WorldPosition+Direction.NW,new Size(3,3)); // sewers are not well-connected...next district over may be needed
       if (this == District.SubwayMap && 0>= PowerGenerators.Get.Count) return new Rectangle(District.WorldPosition+Direction.NW,new Size(3,3)); // subway w/o generators should have an entrance "close by"
       return new Rectangle(District.WorldPosition, new Size(1, 1));
     }
    }

    private void fuse_chokepoints(List<Point[]> candidates, Direction normal_dir, Action<Location[]> install)
    {
        var one_adjacent = new List<Point[]>(candidates.Count);
        var two_adjacent = new List<Point[]>(candidates.Count);

        foreach(var x in candidates) {
          switch(candidates.Count(y => 1==Engine.Rules.GridDistance(x[0],y[0]) && (x[0].X==y[0].X || x[0].Y == y[0].Y)))
          {
          case 2: two_adjacent.Add(x); break;
          case 1: one_adjacent.Add(x); break;
          case 0: install(new Location[] { new Location(this, x[0]) }); break;
          default: throw new InvalidOperationException("should not have more than two chokepoints adjacent to a chokepoint");
          }
        }

        var inverse_dir = normal_dir.Right.Right.Right.Right;   // \todo concise wrapper for this
        int i = one_adjacent.Count;
        while (0 < i--) {
#if DEBUG
          if (0 != one_adjacent.Count%2) throw new InvalidProgramException("expected paired endpoints");
#endif
          var choke = one_adjacent[i];
          one_adjacent.RemoveAt(i);
          var anchor = new List<Location> { new Location(this,choke[0]) };
          var check_normal = choke[0]+normal_dir;
          int test_for;
          if (0 <= (test_for = one_adjacent.FindIndex(x => x[0] == check_normal))) {
            anchor.Add(new Location(this,one_adjacent[test_for][0]));
            one_adjacent.RemoveAt(test_for);
            i--;
            install(anchor.ToArray());
            continue;
          }
          var check_inverse = choke[0]+ inverse_dir;
          if (0 <= (test_for = one_adjacent.FindIndex(x => x[0] == check_inverse))) {
            anchor.Insert(0,new Location(this,one_adjacent[test_for][0]));
            one_adjacent.RemoveAt(test_for);
            i--;
            install(anchor.ToArray());
            continue;
          }
          test_for = two_adjacent.FindIndex(x => x[0] == check_normal);
          if (0 <= test_for) {
            do {
              anchor.Add(new Location(this, two_adjacent[test_for][0]));
              check_normal = two_adjacent[test_for][0]+normal_dir;
              two_adjacent.RemoveAt(test_for);
            } while(0 <= (test_for = two_adjacent.FindIndex(x => x[0] == check_normal)));
            if (0 > (test_for = one_adjacent.FindIndex(x => x[0] == check_normal))) throw new InvalidProgramException("expected to find matching endpoint");
            anchor.Add(new Location(this,one_adjacent[test_for][0]));
            one_adjacent.RemoveAt(test_for);
            install(anchor.ToArray());
            i--;
            continue;
          }
          test_for = two_adjacent.FindIndex(x => x[0] == check_inverse);
          if (0 <= test_for) {
            do {
              anchor.Insert(0,new Location(this, two_adjacent[test_for][0]));
              check_inverse = two_adjacent[test_for][0]+inverse_dir;
              two_adjacent.RemoveAt(test_for);
            } while(0 <= (test_for = two_adjacent.FindIndex(x => x[0] == check_inverse)));
            if (0 > (test_for = one_adjacent.FindIndex(x => x[0] == check_inverse))) throw new InvalidProgramException("expected to find matching endpoint");
            anchor.Insert(0,new Location(this,one_adjacent[test_for][0]));
            one_adjacent.RemoveAt(test_for);
            install(anchor.ToArray());
            i--;
            continue;
          }
          throw new InvalidProgramException("expected matching endpoint, not a singleton");
        }
    }

#if PRERELEASE_MOTHBALL
    public void RegenerateChokepoints() {
      var working = new List<LinearChokepoint>();

      // define a chokepoint as a width-1 corridor.  We don't handle vertical exits here (those need entries in map pairs)
      // we assume the vertical exits are added after us, or in a different cache
      var test = new Dictionary<Point,int>();
      var chokepoint_candidates = new List<Point[]>(Rect.Width * Rect.Height);
      Rect.DoForEach(pt => {
          test[pt] = 0;
          if (0 == pt.X) test[pt + Direction.W] = 0;
          if (0 == pt.Y) test[pt + Direction.N] = 0;
          if (Rect.Width - 1 == pt.X) test[pt + Direction.E] = 0;
          if (Rect.Height - 1 == pt.Y) test[pt + Direction.S] = 0;
      });
      Rect.DoForEach(pt => {
          Point[] candidate = { pt, pt + Direction.W, pt + Direction.E, pt + Direction.N, pt + Direction.S };
          int i = candidate.Length;
          while (0 < i--) test[candidate[i]] += 1;
          foreach (var x in candidate) test[x] += 1;
          chokepoint_candidates.Add(candidate);
      });
      var chokepoint_candidates_ns = new List<Point[]>(chokepoint_candidates.Count);
      var chokepoint_candidates_ew = new List<Point[]>(chokepoint_candidates.Count);

#region chokepoint rejection
      void chokepoint_rejected(Point[] candidate) {
         int i = candidate.Length;
         while(0 < i--) {
           if (!test.ContainsKey(candidate[i])) continue;
           if (0 >= (test[candidate[i]] -= 1)) test.Remove(candidate[i]);
         }
      }
      void ew_chokepoint_rejected(Point[] candidate) {
         if (test.ContainsKey(candidate[0]) && 0 >= (test[candidate[0]] -= 1)) test.Remove(candidate[0]);
         if (test.ContainsKey(candidate[3]) && 0 >= (test[candidate[3]] -= 1)) test.Remove(candidate[3]);
         if (test.ContainsKey(candidate[4]) && 0 >= (test[candidate[4]] -= 1)) test.Remove(candidate[4]);
      }
      void ns_chokepoint_rejected(Point[] candidate) {
         if (test.ContainsKey(candidate[0]) && 0 >= (test[candidate[0]] -= 1)) test.Remove(candidate[0]);
         if (test.ContainsKey(candidate[1]) && 0 >= (test[candidate[1]] -= 1)) test.Remove(candidate[1]);
         if (test.ContainsKey(candidate[2]) && 0 >= (test[candidate[2]] -= 1)) test.Remove(candidate[2]);
      }
      void chokepoint_rejected_ns(Point[] candidate) {
         if (test.ContainsKey(candidate[1]) && 0 >= (test[candidate[1]] -= 1)) test.Remove(candidate[1]);
         if (test.ContainsKey(candidate[2]) && 0 >= (test[candidate[2]] -= 1)) test.Remove(candidate[2]);
         chokepoint_candidates_ew.Add(candidate);
      }
      void chokepoint_rejected_ew(Point[] candidate) {
         if (test.ContainsKey(candidate[3]) && 0 >= (test[candidate[3]] -= 1)) test.Remove(candidate[3]);
         if (test.ContainsKey(candidate[4]) && 0 >= (test[candidate[4]] -= 1)) test.Remove(candidate[4]);
         chokepoint_candidates_ns.Add(candidate);
      }
#endregion

      bool pt_walkable(Point p) {
        bool walkable = IsValid(p) && GetTileModelAtExt(p).IsWalkable;
#if PROTOTYPE
        if (walkable) {
          var obj = GetMapObjectAtExt(p);
          if (null != obj && !(obj is DoorWindow) && !obj.IsWalkable && !obj.IsJumpable) walkable = false;  // i.e., car extinguishing can trigger recalc
        }
#endif
        return walkable;
      }

      void test_for_chokepoint(Point p) {
        bool walkable = pt_walkable(p);
        test.Remove(p);
#region List::RemoveAt incompatible with iterating when rejecting chokepoints
        if (walkable) {
          int i = chokepoint_candidates_ew.Count;
          while(0 < i--) {
            var tmp = chokepoint_candidates_ew[i];
            if (   p == tmp[1]
                || p == tmp[2]) {
              ew_chokepoint_rejected(tmp);
              chokepoint_candidates_ew.RemoveAt(i);
            };
          }
          i = chokepoint_candidates_ns.Count;
          while(0 < i--) {
            var tmp = chokepoint_candidates_ns[i];
            if (   p == tmp[3]
                || p == tmp[4]) {
              ns_chokepoint_rejected(tmp);
              chokepoint_candidates_ns.RemoveAt(i);
            };
          }
          i = chokepoint_candidates.Count;
          while(0 < i--) {
            var tmp = chokepoint_candidates[i];
            if (   p == tmp[1]
                || p == tmp[2]) {
              chokepoint_rejected_ew(tmp);
              chokepoint_candidates.RemoveAt(i);
            };
            if (   p == tmp[3]
                || p == tmp[4]) {
              chokepoint_rejected_ns(tmp);
              chokepoint_candidates.RemoveAt(i);
            };
          }
        } else {
         int i = chokepoint_candidates_ew.Count;
         while(0 < i--) {
            var tmp = chokepoint_candidates_ew[i];
            if (p == tmp[0]) {
              ew_chokepoint_rejected(tmp);
              chokepoint_candidates_ew.RemoveAt(i);
              break;
            };
          }
         i = chokepoint_candidates_ns.Count;
         while(0 < i--) {
            var tmp = chokepoint_candidates_ns[i];
            if (p == tmp[0]) {
              ns_chokepoint_rejected(tmp);
              chokepoint_candidates_ns.RemoveAt(i);
              break;
            };
          }
         i = chokepoint_candidates.Count;
         while(0 < i--) {
            var tmp = chokepoint_candidates[i];
            if (p == tmp[0]) {
              chokepoint_rejected(tmp);
              chokepoint_candidates.RemoveAt(i);
              break;
            };
          }
        }
#endregion
      }

      while(0 < test.Count) {
        var x = test.First();
        test_for_chokepoint(x.Key);
      }

      if (0 >= chokepoint_candidates.Count && 0 >= chokepoint_candidates_ew.Count && 0 >= chokepoint_candidates_ns.Count) {
        m_Chokepoints.Clear();
        return;
      }

      // if there are dual-mode chokepoints, they will not fuse.  Double-list them (once for each orientation)
      foreach(var choke in chokepoint_candidates) {
        var anchor = new List<Location> { new Location(this,choke[0]) };
        var ns_entrance = new List<Location>();
        var ns_exit = new List<Location>();
        var ew_entrance = new List<Location>();
        var ew_exit = new List<Location>();

        var pt_test = choke[0] + Direction.N;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) ns_entrance.Add(loc);
        };
        pt_test = choke[0] + Direction.S;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) ns_exit.Add(loc);
        };
        pt_test = choke[0] + Direction.W;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) ew_entrance.Add(loc);
        };
        pt_test = choke[0] + Direction.E;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) ew_exit.Add(loc);
        };
        pt_test = choke[0] + Direction.NW;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) {
            ns_entrance.Add(loc);
            ew_entrance.Add(loc);
          }
        };
        pt_test = choke[0] + Direction.NE;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) {
            ns_entrance.Add(loc);
            ew_exit.Add(loc);
          }
        };
        pt_test = choke[0] + Direction.SE;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) {
            ns_exit.Add(loc);
            ew_exit.Add(loc);
          }
        };
        pt_test = choke[0] + Direction.SW;
        if (pt_walkable(pt_test)) {
          var loc = new Location(this, pt_test);
          if (loc.ForceCanonical()) {
            ns_exit.Add(loc);
            ew_entrance.Add(loc);
          }
        };

        var anchor_array = anchor.ToArray();
        working.Add(new LinearChokepoint(ns_entrance.ToArray(), anchor_array, ns_exit.ToArray()));
        working.Add(new LinearChokepoint(ew_entrance.ToArray(), anchor_array, ew_exit.ToArray()));
      }

      if (0 >= chokepoint_candidates_ew.Count && 0 >= chokepoint_candidates_ns.Count) {
        // nearly ACID update
        m_Chokepoints.Clear();
        m_Chokepoints.AddRange(working);
        return;
      }

      void install_ns_chokepoint(Location[] anchor)
      {
        var ns_entrance = new List<Location>();
        var ns_exit = new List<Location>();

        var pt_test = anchor[0] + Direction.N;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_entrance.Add(pt_test);
        };
        pt_test = anchor[anchor.Length-1] + Direction.S;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_exit.Add(pt_test);
        };
        pt_test = anchor[0] + Direction.NW;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_entrance.Add(pt_test);
        };
        pt_test = anchor[0] + Direction.NE;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_entrance.Add(pt_test);
        };
        pt_test = anchor[anchor.Length - 1] + Direction.SE;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_exit.Add(pt_test);
        };
        pt_test = anchor[anchor.Length - 1] + Direction.SW;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ns_exit.Add(pt_test);
        };

        working.Add(new LinearChokepoint(ns_entrance.ToArray(), anchor, ns_exit.ToArray()));
      }

      void install_ew_chokepoint(Location[] anchor)
      {
        var ew_entrance = new List<Location>();
        var ew_exit = new List<Location>();

        var pt_test = anchor[0] + Direction.W;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_entrance.Add(pt_test);
        };
        pt_test = anchor[anchor.Length - 1] + Direction.E;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_exit.Add(pt_test);
        };
        pt_test = anchor[0] + Direction.NW;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_entrance.Add(pt_test);
        };
        pt_test = anchor[anchor.Length - 1] + Direction.NE;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_exit.Add(pt_test);
        };
        pt_test = anchor[anchor.Length - 1] + Direction.SE;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_exit.Add(pt_test);
        };
        pt_test = anchor[0] + Direction.SW;
        if (pt_walkable(pt_test.Position)) {
          if (pt_test.ForceCanonical()) ew_entrance.Add(pt_test);
        };

        working.Add(new LinearChokepoint(ew_entrance.ToArray(), anchor, ew_exit.ToArray()));
      }

      // chokepoints can fuse with other chokepoints of the same type.
      fuse_chokepoints(chokepoint_candidates_ns, Direction.E, install_ew_chokepoint);
      fuse_chokepoints(chokepoint_candidates_ew, Direction.S, install_ns_chokepoint);

      // nearly ACID update
      m_Chokepoints.Clear();
      m_Chokepoints.AddRange(working);
    }

    public LinearChokepoint EnteringChokepoint(Location origin, Location dest) {
      var candidates = m_Chokepoints.FindAll(choke => choke.Chokepoint.Contains(dest));
      if (0 >= candidates.Count) return null;
      candidates = candidates.FindAll(choke => choke.Entrance.Contains(origin) || choke.Exit.Contains(origin));
      if (0 >= candidates.Count) return null;
      return candidates[0];
    }
#endif

    public void RegenerateMapGeometry() {
      int crm_encode(Vector2D_int_stack pt) { return pt.X + Rect.Width*pt.Y; }    // chinese remainder theorem encoding
      Vector2D_int_stack crm_decode(int n) { return new Vector2D_int_stack(n%Rect.Width,n/Rect.Width); }    // chinese remainder theorem decoding

      // we don't care about being completely correct for outdoors, here.  This has to support the indoor situation only
      Span<bool> wall_horz3 = stackalloc bool[Rect.Height*Rect.Width];
      Span<bool> wall_vert3 = stackalloc bool[Rect.Height*Rect.Width];
      Span<bool> space_horz3 = stackalloc bool[Rect.Height*Rect.Width];
      Span<bool> space_vert3 = stackalloc bool[Rect.Height*Rect.Width];
      Vector2D_int_stack p;
      p.X = Rect.Width;
      while(0 < p.X--) {
        p.Y = Rect.Height;
        while(0 < p.Y--) {
          if (Width - 3 > p.X) {
            if (  !GetTileModelAt(p.X, p.Y).IsWalkable
               && !GetTileModelAt(p.X + 1, p.Y).IsWalkable
               && !GetTileModelAt(p.X + 2, p.Y).IsWalkable) wall_horz3[crm_encode(p)] = true;
            if (  GetTileModelAt(p.X, p.Y).IsWalkable
               && GetTileModelAt(p.X + 1, p.Y).IsWalkable
               && GetTileModelAt(p.X + 2, p.Y).IsWalkable) space_horz3[crm_encode(p)] = true;
            }
          if (Height - 3 > p.Y) {
            if (  !GetTileModelAt(p.X, p.Y).IsWalkable
               && !GetTileModelAt(p.X, p.Y + 1).IsWalkable
               && !GetTileModelAt(p.X, p.Y + 2).IsWalkable) wall_vert3[crm_encode(p)] = true;
            if (  GetTileModelAt(p.X, p.Y).IsWalkable
               && GetTileModelAt(p.X, p.Y + 1).IsWalkable
               && GetTileModelAt(p.X, p.Y + 2).IsWalkable) space_vert3[crm_encode(p)] = true;
          }
        }
      }

      // We run very early (map loading/new game) so we get no benefit from trying to fake ACID update.
      m_FullCorner_nw.Clear();
      m_FullCorner_ne.Clear();
      m_FullCorner_se.Clear();
      m_FullCorner_sw.Clear();
      m_FlushWall_n.Clear();
      m_FlushWall_s.Clear();
      m_FlushWall_w.Clear();
      m_FlushWall_e.Clear();

      int i = Rect.Width*Rect.Height;
      Vector2D_int_stack tmp;
      int tmp_i;
      while(0 < i--) {
        if (!wall_horz3[i] && !wall_vert3[i]) continue;
        p = crm_decode(i);
        if (wall_horz3[i] && wall_vert3[i]) {
          // nw corner candidate
          if (   space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y+1))]
              && space_vert3[tmp_i]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y+2))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+2,p.Y+1))]) m_FullCorner_nw.Add(new Point(p.X+1,p.Y+1));
        }
        // [tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X,p.Y))]
        if (wall_horz3[i]) {
          // must test for: flush wall n/s
          // can test for cleanly: corner ne
          if (   Rect.Height-2 > p.Y
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X, p.Y+1))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X, p.Y+2))]) m_FlushWall_n.Add(new Point(p.X+1,p.Y+1));
          if (   2 <= p.Y
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X, p.Y-1))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X, p.Y-2))]) m_FlushWall_s.Add(new Point(p.X+1,p.Y-1));
          if (   Rect.Width-2 > p.X
              && 1 <= p.X
              && wall_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+2, p.Y))] 
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-1,p.Y+1))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-1,p.Y+2))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X  ,p.Y+1))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y+1))]) m_FullCorner_ne.Add(new Point(p.X-1,p.Y+1));
          // do SE here as well
          if (   Rect.Width-2 > p.X
              && 1 <= p.X
              && 3 <= p.Y
              && wall_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+2, p.Y-2))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-1,p.Y-1))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-1,p.Y-2))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X  ,p.Y-3))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y-3))]) m_FullCorner_se.Add(new Point(p.X-1,p.Y-1));
        }
        if (wall_vert3[i]) {
          // must test for: flush wall e/w
          // can test for cleanly: corner sw
          if (   Rect.Width-2 > p.X
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1, p.Y))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+2, p.Y))]) m_FlushWall_w.Add(new Point(p.X+1,p.Y+1));
          if (   2 <= p.X
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-1, p.Y))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X-2, p.Y))]) m_FlushWall_e.Add(new Point(p.X-1,p.Y+1));
          if (   Rect.Width-2 > p.X
              && 1 <= p.Y
              && Rect.Height - 2 > p.Y
              && wall_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X, p.Y+2))] 
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y))]
              && space_horz3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y+1))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+1,p.Y-1))]
              && space_vert3[tmp_i = crm_encode(tmp = new Vector2D_int_stack(p.X+2,p.Y-1))]) m_FullCorner_sw.Add(new Point(p.X+1,p.Y-1));
        }
      } // end while(0 < i--)
    }

    // accessors for map geometry
    // XXX linear...may want to do something about that
    public bool IsNWCorner(Point pt) { return m_FullCorner_nw.Contains(pt); }
    public bool IsNECorner(Point pt) { return m_FullCorner_ne.Contains(pt); }
    public bool IsSWCorner(Point pt) { return m_FullCorner_sw.Contains(pt); }
    public bool IsSECorner(Point pt) { return m_FullCorner_se.Contains(pt); }
    public bool IsFlushNWall(Point pt) { return m_FlushWall_n.Contains(pt); }
    public bool IsFlushSWall(Point pt) { return m_FlushWall_s.Contains(pt); }
    public bool IsFlushWWall(Point pt) { return m_FlushWall_w.Contains(pt); }
    public bool IsFlushEWall(Point pt) { return m_FlushWall_e.Contains(pt); }

    // cheat map similar to savefile viewer
    public void DaimonMap(Zaimoni.Data.OutTextFile dest) {
      if (!Engine.Session.Get.CMDoptionExists("socrates-daimon")) return;
      dest.WriteLine(Name+"<br>");
      // XXX since the lock at the district level causes deadlocking, we may be inconsistent for simulation districtions
      List<Actor> tmp_Actors = (0< m_ActorsList.Count ? new List<Actor>(m_ActorsList) : null);
      List<Point> inv_locs = (0<m_GroundItemsByPosition.Count ? new List<Point>(m_GroundItemsByPosition.Keys) : null);
      if (null==tmp_Actors && null==inv_locs) return;
      // we have one of actors or items here...full map has motivation
      var inv_data = new List<string>();
      string[] actor_headers = { "pos", "name", "Priority", "AP", "HP", "Inventory" };  // XXX would be function-static in C++
      List<string> actor_data = new List<string>();
      string[][] ascii_map = new string[Height][];
      foreach(short y in Enumerable.Range(0, Height)) {
        ascii_map[y] = new string[Width];
        foreach(short x in Enumerable.Range(0, Width)) {
          // XXX does not handle transparent walls or opaque non-walls
          Point pt = new Point(x,y);
          ascii_map[y][x] = (GetTileModelAt(x,y).IsWalkable ? "." : "#");    // typical floor tile if walkable, typical wall otherwise
          if (HasExitAt(pt)) ascii_map[y][x] = ">";                  // downwards exit
#region map objects
          const string tree_symbol = "&#x2663;"; // unicode: card suit club looks enough like a tree
          const string car_symbol = "<span class='car'>&#x1F698;</span>";   // unicode: oncoming car
          const string drawer_symbol = "&#x2584;";    // unicode: block elements
          const string shop_shelf_symbol = "&#x25A1;";    // unicode: geometric shapes
          const string large_fortification_symbol = "<span class='lfort'>&#x25A6;</span>";    // unicode: geometric shapes
          const string power_symbol = "&#x2B4D;";    // unicode: misc symbols & arrows
          const string closed_gate = "<span class='lfort'>&#x2630;</span>";    // unicode: misc symbols (I Ching heaven)
          const string iron_fence = "<span class='lfort'>&#x2632;</span>";    // unicode: misc symbols (I Ching fire)
          const string open_gate = "<span class='lfort'>&#x2637;</span>";    // unicode: misc symbols (I Ching earth)
          const string chair = "<span class='chair'>&#x2441;</span>";    // unicode: OCR chair
          MapObject tmp_obj = GetMapObjectAt(pt);  // micro-optimization target (one Point temporary involved)
          if (null!=tmp_obj) {
            if (tmp_obj.IsCouch) {
              ascii_map[y][x] = "="; // XXX no good icon for bed...we have no rings so this is not-awful
            } else if (MapObject.IDs.TREE == tmp_obj.ID) {
              ascii_map[y][x] = tree_symbol;
            } else if (MapObject.IDs.CAR1 == tmp_obj.ID) {
              ascii_map[y][x] = car_symbol; // unicode: oncoming car
            } else if (MapObject.IDs.CAR2 == tmp_obj.ID) {
              ascii_map[y][x] = car_symbol; // unicode: oncoming car
            } else if (MapObject.IDs.CAR3 == tmp_obj.ID) {
              ascii_map[y][x] = car_symbol; // unicode: oncoming car
            } else if (MapObject.IDs.CAR4 == tmp_obj.ID) {
              ascii_map[y][x] = car_symbol; // unicode: oncoming car
            } else if (MapObject.IDs.DRAWER == tmp_obj.ID) {
              ascii_map[y][x] = drawer_symbol;
            } else if (MapObject.IDs.SHOP_SHELF == tmp_obj.ID) {
              ascii_map[y][x] = shop_shelf_symbol;
            } else if (MapObject.IDs.LARGE_FORTIFICATION == tmp_obj.ID) {
              ascii_map[y][x] = large_fortification_symbol;
            } else if (MapObject.IDs.CHAR_POWER_GENERATOR == tmp_obj.ID) {
              ascii_map[y][x] = power_symbol;
            } else if (MapObject.IDs.IRON_GATE_CLOSED == tmp_obj.ID) {
              ascii_map[y][x] = closed_gate;
            } else if (MapObject.IDs.IRON_FENCE == tmp_obj.ID || MapObject.IDs.WIRE_FENCE == tmp_obj.ID) {
              ascii_map[y][x] = iron_fence;
            } else if (MapObject.IDs.IRON_GATE_OPEN == tmp_obj.ID) {
              ascii_map[y][x] = open_gate;
            } else if (MapObject.IDs.CHAIR == tmp_obj.ID) {
              ascii_map[y][x] = chair;
            } else if (MapObject.IDs.CHAR_CHAIR == tmp_obj.ID) {
              ascii_map[y][x] = chair;
            } else if (MapObject.IDs.HOSPITAL_CHAIR == tmp_obj.ID) {
              ascii_map[y][x] = chair;
            } else if (tmp_obj.IsTransparent && !tmp_obj.IsWalkable) {
              ascii_map[y][x] = "|"; // gate; iron wall
            } else {
              if (tmp_obj is Engine.MapObjects.DoorWindow tmp_door) {
                if (tmp_door.IsBarricaded) {
                  ascii_map[y][x] = large_fortification_symbol; // no good icon...pretend it's a large fortification since it would have to be torn down to be passed through
                } else if (tmp_door.IsClosed) {
                  ascii_map[y][x] = "+"; // typical closed door
                } else if (tmp_door.IsOpen) {
                  ascii_map[y][x] = "'"; // typical open door
                } else /* if (tmp_door.IsBroken */ {
                  ascii_map[y][x] = "'"; // typical broken door
                }
              }
            }
		  }
#endregion
#region map inventory
          Inventory inv = GetItemsAt(pt);
          if (!inv?.IsEmpty ?? false) {
            string p_txt = '('+x.ToString()+','+y.ToString()+')';
            foreach (Item it in inv.Items) {
              inv_data.Add("<tr class='inv'><td>"+p_txt+"</td><td>"+it.ToString()+"</td></tr>");
            }
            ascii_map[y][x] = "&"; // Angband/Nethack pile.
          }
#endregion
#region actors
          Actor a = GetActorAt(pt);
          if (null!=a && !a.IsDead) {
            string p_txt = '('+a.Location.Position.X.ToString()+','+ a.Location.Position.Y.ToString()+')';
            string a_str = a.Faction.ID.ToString(); // default to the faction numeral
            string pos_css = "";
            if (a.Controller is PlayerController) {
              a_str = "@";
              pos_css = " style='background:lightgreen'";
            };
            switch(a.Model.ID) {
              case Gameplay.GameActors.IDs.UNDEAD_SKELETON:
                a_str = "<span style='background:orange'>s</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_RED_EYED_SKELETON:
                a_str = "<span style='background:red'>s</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_RED_SKELETON:
                a_str = "<span style='background:darkred'>s</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_ZOMBIE:
                a_str = "<span style='background:orange'>S</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_DARK_EYED_ZOMBIE:
                a_str = "<span style='background:red'>S</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_DARK_ZOMBIE:
                a_str = "<span style='background:darkred'>S</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_ZOMBIE_MASTER:
                a_str = "<span style='background:orange'>Z</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_ZOMBIE_LORD:
                a_str = "<span style='background:red'>Z</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_ZOMBIE_PRINCE:
                a_str = "<span style='background:darkred'>Z</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_MALE_ZOMBIFIED:
              case Gameplay.GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED:
                a_str = "<span style='background:orange'>d</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_MALE_NEOPHYTE:
              case Gameplay.GameActors.IDs.UNDEAD_FEMALE_NEOPHYTE:
                a_str = "<span style='background:red'>d</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_MALE_DISCIPLE:
              case Gameplay.GameActors.IDs.UNDEAD_FEMALE_DISCIPLE:
                a_str = "<span style='background:darkred'>d</span>"; break;
              case Gameplay.GameActors.IDs.UNDEAD_RAT_ZOMBIE:
                a_str = "<span style='background:orange'>r</span>"; break;
              case Gameplay.GameActors.IDs.MALE_CIVILIAN:
              case Gameplay.GameActors.IDs.FEMALE_CIVILIAN:
                a_str = "<span style='background:lightgreen'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.FERAL_DOG:
                a_str = "<span style='background:lightgreen'>C</span>"; break;    // C in Angband, Nethack
              case Gameplay.GameActors.IDs.CHAR_GUARD:
                a_str = "<span style='background:darkgray;color:white'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.ARMY_NATIONAL_GUARD:
                a_str = "<span style='background:darkgreen;color:white'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.BIKER_MAN:
                a_str = "<span style='background:darkorange;color:white'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.POLICEMAN:
              case Gameplay.GameActors.IDs.POLICEWOMAN:
                a_str = "<span style='background:lightblue'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.GANGSTA_MAN:
                a_str = "<span style='background:red;color:white'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.BLACKOPS_MAN:
                a_str = "<span style='background:black;color:white'>"+a_str+"</span>"; break;
              case Gameplay.GameActors.IDs.SEWERS_THING:
              case Gameplay.GameActors.IDs.JASON_MYERS:
                a_str = "<span style='background:darkred;color:white'>"+a_str+"</span>"; break;
            }
            var actor_stats = new List<string> { " " };

            if (a.Model.Abilities.HasToEat) {
              if (a.IsStarving) actor_stats.Add("<span style='background-color:black; color:red'>H</span>");
              else if (a.IsHungry) actor_stats.Add("<span style='background-color:black; color:yellow'>H</span>");
              else if (a.IsAlmostHungry) actor_stats.Add("<span style='background-color:black; color:green'>H</span>");
            }
            else if (a.Model.Abilities.IsRotting) {
              if (a.IsRotStarving) actor_stats.Add("<span style='background-color:black; color:red'>H</span>");
              else if (a.IsRotHungry) actor_stats.Add("<span style='background-color:black; color:yellow'>R</span>");
              else if (a.IsAlmostRotHungry) actor_stats.Add("<span style='background-color:black; color:green'>R</span>");
            }
            if (a.Model.Abilities.HasSanity) {
              if (a.IsInsane) actor_stats.Add("<span style='background-color:black; color:red'>I</span>");
              else if (a.IsDisturbed) actor_stats.Add("<span style='background-color:black; color:yellow'>I</span>");
            }
            if (a.Model.Abilities.HasToSleep) {
              if (a.IsExhausted) actor_stats.Add("<span style='background-color:black; color:red'>Z</span>");
              else if (a.IsSleepy) actor_stats.Add("<span style='background-color:black; color:yellow'>Z</span>");
              else if (a.IsAlmostSleepy) actor_stats.Add("<span style='background-color:black; color:green'>Z</span>");
            }
            if (a.IsSleeping) actor_stats.Add("<span style='background-color:black; color:cyan'>Z</span>");
            if (0 < a.MurdersCounter) actor_stats.Add("<span style='background-color:black; color:red'>M</span>");
            if (0 < a.CountFollowers) actor_stats.Add("<span style='background-color:black; color:cyan'>L</span>");
            if (null != a.LiveLeader) actor_stats.Add("<span style='background-color:black; color:cyan'>F:"+a.LiveLeader.Name+"</span>");

            actor_data.Add("<tr><td"+ pos_css + ">" + p_txt + "</td><td>" + a.UnmodifiedName + string.Join("", actor_stats) + "</td><td>"+m_ActorsList.IndexOf(a).ToString()+"</td><td>"+a.ActionPoints.ToString()+ "</td><td>"+a.HitPoints.ToString()+ "</td><td class='inv'>"+(null==a.Inventory ? "" : (a.Inventory.IsEmpty ? "" : a.Inventory.ToString()))+"</td></tr>");
            ascii_map[a.Location.Position.Y][a.Location.Position.X] = a_str;
          }
#endregion
        }
      }
      if (0 < m_aux_CorpsesByPosition.Count) {
        // need a value copy of relevant (infected) corpses
        var tmp = new Dictionary<Point, List<Corpse>>(m_aux_CorpsesByPosition.Count);

        bool is_problem_corpse(Corpse c) { return 0 < c.DeadGuy.InfectionPercent; }

        foreach(var x in m_aux_CorpsesByPosition) {
          if (0>= x.Value.Count) continue;
          if (!x.Value.Any(is_problem_corpse)) continue;
          tmp[x.Key] = x.Value.FindAll(is_problem_corpse);
        }
        if (0 < tmp.Count) dest.WriteLine("<pre>Problematic corpses:\n"+tmp.to_s()+"</pre>");
      }
      if (0>=inv_data.Count && 0>=actor_data.Count) return;
      if (0<actor_data.Count) {
        dest.WriteLine("<table border=2 cellspacing=1 cellpadding=1 align=left>");
        dest.WriteLine("<tr><th>"+string.Join("</th><th>", actor_headers) + "</th></tr>");
        foreach(string s in actor_data) dest.WriteLine(s);
        dest.WriteLine("</table>");
      }
      if (0<inv_data.Count) {
        dest.WriteLine("<table border=2 cellspacing=1 cellpadding=1 align=right>");
        foreach(string s in inv_data) dest.WriteLine(s);
        dest.WriteLine("</table>");
      }
      dest.WriteLine("<a name='"+Name+"'></a>");
      dest.WriteLine("<pre style='clear:both'>");
      foreach (int y in Enumerable.Range(0, Height)) {
        dest.WriteLine(String.Join("",ascii_map[y]));
      }
      dest.WriteLine("</pre>");
    }

    private void ReconstructAuxiliaryFields()
    {
      Engine.LOS.Now(this);
      m_aux_ActorsByPosition.Clear();
      foreach (Actor mActors in m_ActorsList) {
        // XXX defensive coding: it is possible for actors to duplicate, apparently
        if (!m_aux_ActorsByPosition.ContainsKey(mActors.Location.Position)) {
          m_aux_ActorsByPosition.Add(mActors.Location.Position, mActors);
        } else {
          Actor doppleganger = m_aux_ActorsByPosition[mActors.Location.Position];
          if (  mActors.Name != doppleganger.Name
             || mActors.SpawnTime!=doppleganger.SpawnTime)
            throw new InvalidOperationException("non-clone savefile corruption");
        }
        (mActors.Controller as PlayerController)?.InstallHandlers();
      }
      m_aux_MapObjectsByPosition.Clear();
      foreach (MapObject mMapObjects in m_MapObjectsList)
        m_aux_MapObjectsByPosition.Add(mMapObjects.Location.Position, mMapObjects);
      m_aux_CorpsesByPosition.Clear();
      foreach (Corpse mCorpses in m_CorpsesList) {
        if (m_aux_CorpsesByPosition.TryGetValue(mCorpses.Position, out List<Corpse> corpseList))
          corpseList.Add(mCorpses);
        else
          m_aux_CorpsesByPosition.Add(mCorpses.Position, new List<Corpse>(1) {
            mCorpses
          });
      }
      // Support savefile hacking.
      // Check the actors.  If any have null controllers, intent was to hand control from the player to the AI.
      // Give them AI controllers here.
      foreach(Actor tmp in m_ActorsList) {
        if (null == tmp.Controller) tmp.Controller = tmp.Model.InstanciateController();
      }
    }

    public void OptimizeBeforeSaving()
    {
      int i = m_ActorsList.Count;
      while (0 < i--) {
        var a = m_ActorsList[i];
        if (a.IsDead) m_ActorsList.RemoveAt(i);
        else a.OptimizeBeforeSaving();
      }

      // alpha10 items stacks
      foreach (Inventory stack in m_GroundItemsByPosition.Values)
        stack.OptimizeBeforeSaving();

      m_ActorsList.TrimExcess();
      m_MapObjectsList.TrimExcess();
      m_Zones.TrimExcess();
      m_CorpsesList.TrimExcess();
      m_Timers.TrimExcess();
    }

    public override int GetHashCode()
    {
      return Name.GetHashCode() ^ District.GetHashCode();
    }

    public override string ToString()
    {
      return Name+" ("+Width.ToString()+","+Height.ToString()+") in "+District.Name;
    }
  }
}
