﻿// Decompiled with JetBrains decompiler
// Type: djack.RogueSurvivor.Data.District
// Assembly: RogueSurvivor, Version=0.9.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D2AE4FAE-2CA8-43FF-8F2F-59C173341976
// Assembly location: C:\Private.app\RS9Alpha.Hg\RogueSurvivor.exe

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics.Contracts;

namespace djack.RogueSurvivor.Data
{
  [Serializable]
  internal class District
  {
    private readonly List<Map> m_Maps = new List<Map>(3);
    public readonly Point WorldPosition;
    public readonly DistrictKind Kind;
    private string m_Name;
    private Map m_EntryMap;
    private Map m_SewersMap;
    private Map m_SubwayMap;

    public string Name {
      get {
        return m_Name;
      }
      private set {	// XXX GenerateEntryMap updates outside of constructor
        m_Name = value;
      }
    }

    public IEnumerable<Map> Maps {
      get {
        return m_Maps;
      }
    }

    public int CountMaps {
      get {
        return m_Maps.Count;
      }
    }

    public Map EntryMap {
      get {
        return m_EntryMap;
      }
      private set {
        if (m_EntryMap != null) RemoveMap(m_EntryMap);
        m_EntryMap = value;
        if (value == null) return;
        AddMap(value);
        // successfully placing a cop means the police faction knows all outside squares (map revealing effect)
        Point pos = new Point(0);
        if (0 < m_EntryMap.Police.Get.Count) {
          for (pos.X = 0; pos.X < m_EntryMap.Width; ++pos.X) {
            for (pos.Y = 0; pos.Y < m_EntryMap.Height; ++pos.Y) {
              if (m_EntryMap.IsInsideAt(pos)) continue;
              Engine.Session.Get.ForcePoliceKnown(new Location(m_EntryMap, pos));
            }
          }
        }
        // all unknown squares become investigation targets
        for (pos.X = 0; pos.X < m_EntryMap.Width; ++pos.X) {
          for (pos.Y = 0; pos.Y < m_EntryMap.Height; ++pos.Y) {
            if (Engine.Session.Get.PoliceItemMemory.HaveEverSeen(new Location(m_EntryMap, pos))) continue;  // police already know this
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "CHAR Office")) continue;   // CHAR company town, police first assume things ok
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "CHAR Agency")) continue;   // CHAR company town, police first assume things ok
            // stores have their own police AI cheat
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "GeneralStore")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Grocery")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Sportswear")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Pharmacy")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Construction")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Gunshop")) continue;
            if (m_EntryMap.HasZonePartiallyNamedAt(pos, "Hunting Shop")) continue;

            Engine.Session.Get.PoliceInvestigate.Record(m_EntryMap,pos);
          }
        }
      }
    }

    public Map SewersMap {
      get {
        return m_SewersMap;
      }
      set {
        if (m_SewersMap != null) RemoveMap(m_SewersMap);
        m_SewersMap = value;
        if (value == null) return;
        AddMap(value);
      }
    }

    public Map SubwayMap {
      get {
        return m_SubwayMap;
      }
      set {
        if (m_SubwayMap != null) RemoveMap(m_SubwayMap);
        m_SubwayMap = value;
        if (value == null) return;
        AddMap(value);
      }
    }

    public bool HasSubway {
      get {
        return m_SubwayMap != null;
      }
    }

    public District(Point worldPos, DistrictKind kind)
    {
      WorldPosition = worldPos;
      Kind = kind;
    }

    // map manipulation
    protected void AddMap(Map map)
    {
      Contract.Requires(null != map);
	  Contract.Requires(map.District == this);
      if (m_Maps.Contains(map)) return;
//    map.District = this;
      m_Maps.Add(map);
    }

    public void AddUniqueMap(Map map)
    {
      AddMap(map);
    }

    public Map GetMap(int index)
    {
      return m_Maps[index];
    }

    protected void RemoveMap(Map map)
    {
      Contract.Requires(null != map);
      m_Maps.Remove(map);
//    map.District = null;
    }

    // return value is 0 for "no", otherwise something that should have been an enum
    public int UsesCrossDistrictView(Map m)
    {
      if (m==m_EntryMap) return 1;
      if (m==m_SewersMap) return 2;
      if (m==m_SubwayMap) return 3;
      return 0;
    }

    public Map CrossDistrictViewing(int x)
    {
      switch(x)
      {
      case 1: return m_EntryMap;
      case 2: return m_SewersMap;
      case 3: return m_SubwayMap;
      default: return null;
      }
    }

    public bool HasAccessiblePowerGenerators {
      get {
        if (0 < (m_SubwayMap?.PowerGenerators.Get.Count ?? 0)) return true;
        // The hospital doesn't count, here.
        // The police station is arguable, but the plot consequences are such that the current generator in the jails shouldn't count.
        if (this == Engine.Session.Get.UniqueMaps.PoliceStation_OfficesLevel.TheMap.District) return true;  // \todo when power door locks go in this becomes accessible only to police
        return false;
      }
    }

    // before cross district viewing, this was simply a PlayerCount check
    public bool RequiresUI {
      get {
        if (0 < PlayerCount) return true;
        // \todo Anything that initates UI from outside of the current district has to be PC to avoid a deadlock.
        // once this is working fully, we will never have to reclassify districts.

        // At the base space-time scale (30 turns/hour, 50x50 districts), we have
        // * maximum hearing range 15
        // * maximum viewing radius (8-1)[early evening]+2(big flashlight)+1(on car)=10.
        // so the worst-cases are
        // * LOS: grenade explosion at grid distance 12 spills into view at grid distance 10
        // * sound: a ranged weapon user at grid distance 16 dashes to distance 15, then fires
        // this is evaluated once per scheduling, so we are uncachable.
        // 2017-09-28: grenades are an immediate issue.  melee/ranged combat and noise sources may not be
        // 2017-09-29: moving in PC line of sight is an immediate issue.
        return false;
      }
    }

    // possible micro-optimization target
    public int PlayerCount {
      get {
        int ret = 0;
        foreach(Map tmp in Maps) {
          ret += tmp.PlayerCount;
        }
        return ret;
      }
    }

    public Actor FindPlayer(Map already_failed) {
       foreach(Map tmp in Maps) {
         if (tmp == already_failed) continue;
         Actor tmp2 = tmp.FindPlayer;
         if (null != tmp2) return tmp2;
       }
       return null;
    }

    public void MessagePlayerOnce(Map already_failed, Action<Actor> fn, Func<Actor, bool> pred =null)
    {
      Contract.Requires(null!=fn);
      foreach(Map map in Maps) {
        if (map == already_failed) continue;
        if (map.MessagePlayerOnce(fn,pred)) return;
      }
    }

    public bool ReadyForNextTurn
    {
      get {
        foreach(Map tmp in Maps) {
          if (!tmp.IsSecret && null != tmp.NextActorToAct) return false;
        }
        return true;
      }
    }

    // cheat map similar to savefile viewer
    public void DaimonMap(Zaimoni.Data.OutTextFile dest) {
      if (!Engine.Session.Get.CMDoptionExists("socrates-daimon")) return;
      m_EntryMap.DaimonMap(dest);   // name of this is also the district name
      m_SewersMap.DaimonMap(dest);
      if (null!= m_SubwayMap) m_SubwayMap.DaimonMap(dest);
      foreach(Map map in m_Maps) {
        if (map == m_EntryMap) continue;
        if (map == m_SewersMap) continue;
        if (map == m_SubwayMap) continue;
        map.DaimonMap(dest);
      }
    }

    // low-level support
    public void GenerateEntryMap(World world, Point policeStationDistrictPos, Point hospitalDistrictPos, int districtSize, Gameplay.Generators.BaseTownGenerator m_TownGenerator)
    {
      int x = WorldPosition.X;
      int y = WorldPosition.Y;

      ///////////////////////////
      // 1. Compute unique seed.
      // 2. Set params for kind.
      // 3. Generate map.
      ///////////////////////////

      // 1. Compute unique seed.
      int seed = Engine.Session.Get.Seed + y * world.Size + x;

#region 2. Set gen params.
      // this must be a value copy or else: BaseTownGenerator.Parameters must be a struct, not a class
      Gameplay.Generators.BaseTownGenerator.Parameters parameters = Gameplay.Generators.BaseTownGenerator.DEFAULT_PARAMS;

      parameters.MapWidth = parameters.MapHeight = districtSize;
      parameters.District = this;
      const int num = 8;
      string str;
      switch (Kind)
      {
        case DistrictKind.GENERAL:
          str = "District";
          break;
        case DistrictKind.RESIDENTIAL:
          str = "Residential District";
          parameters.CHARBuildingChance /= num;
          parameters.ParkBuildingChance /= num;
          parameters.ShopBuildingChance /= num;
          break;
        case DistrictKind.SHOPPING:
          str = "Shopping District";
          parameters.CHARBuildingChance /= num;
          parameters.ShopBuildingChance *= num;
          parameters.ParkBuildingChance /= num;
          break;
        case DistrictKind.GREEN:
          str = "Green District";
          parameters.CHARBuildingChance /= num;
          parameters.ParkBuildingChance *= num;
          parameters.ShopBuildingChance /= num;
          break;
        case DistrictKind.BUSINESS:
          str = "Business District";
          parameters.CHARBuildingChance *= num;
          parameters.ParkBuildingChance /= num;
          parameters.ShopBuildingChance /= num;
          break;
        default:
          throw new ArgumentOutOfRangeException("unhandled district kind");
      }

      // Special params.
      parameters.GeneratePoliceStation = WorldPosition == policeStationDistrictPos;
      parameters.GenerateHospital = WorldPosition == hospitalDistrictPos;
#endregion

      // working around an abstract function declaration that *cannot* have the parameters as an argument.
      // different types of maps may have incompatible parameter structs/classes
      // 3. Generate map.
      Gameplay.Generators.BaseTownGenerator.Parameters @params = m_TownGenerator.Params;
      m_TownGenerator.Params = parameters;
      Map map = m_TownGenerator.Generate(seed, string.Format("{0}@{1}", (object)str, (object)World.CoordToString(x, y)));
      m_TownGenerator.Params = @params;

      // done.
      EntryMap = map;
      Name = EntryMap.Name;
    }

    public void OptimizeBeforeSaving()
    {
      m_Maps.TrimExcess();
      foreach (Map mMap in m_Maps)
        mMap.OptimizeBeforeSaving();
    }

    public override int GetHashCode()
    {
      return WorldPosition.GetHashCode();
    }
  }
}
