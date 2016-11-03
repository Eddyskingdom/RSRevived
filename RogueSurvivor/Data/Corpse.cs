﻿// Decompiled with JetBrains decompiler
// Type: djack.RogueSurvivor.Data.Corpse
// Assembly: RogueSurvivor, Version=0.9.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D2AE4FAE-2CA8-43FF-8F2F-59C173341976
// Assembly location: C:\Private.app\RS9Alpha.Hg\RogueSurvivor.exe

using System;
using System.Drawing;

namespace djack.RogueSurvivor.Data
{
  [Serializable]
  internal class Corpse
  {
    private readonly Actor m_DeadGuy;
    private readonly int m_Turn;
    private Point m_Position;
    private float m_HitPoints;
    private readonly int m_MaxHitPoints;
    private readonly float m_Rotation;
    private readonly float m_Scale; // currently not used properly
    private Actor m_DraggedBy;

    public Actor DeadGuy { get { return m_DeadGuy; } }
    public int Turn { get { return m_Turn; } }

    public Point Position {
      get {
        return m_Position;
      }
      set {
         m_Position = value;
      }
    }

    public float HitPoints {
      get {
        return m_HitPoints;
      }
      set {
        m_HitPoints = value;
      }
    }

    public int MaxHitPoints { get { return m_MaxHitPoints; } }
    public float Rotation { get { return m_Rotation; } }
    public float Scale { get { return m_Scale; } }

    public bool IsDragged
    {
      get {
        return m_DraggedBy != null && !m_DraggedBy.IsDead;
      }
    }

    public Actor DraggedBy {
      get {
        return m_DraggedBy;
      }
      set {
        m_DraggedBy = value;
      }
    }

    public Corpse(Actor deadGuy, float rotation, float scale=1f)
    {
      m_DeadGuy = deadGuy;
      m_Turn = deadGuy.Location.Map.LocalTime.TurnCounter;
      m_HitPoints = (float)deadGuy.MaxHPs;
      m_MaxHitPoints = deadGuy.MaxHPs;
      m_Rotation = rotation;
      m_Scale = Math.Max(0.0f, Math.Min(1f, scale));
      m_DraggedBy = null;
    }

    public int FreshnessPercent {
      get {
        return (int) (100.0 * (double) HitPoints / (double)DeadGuy.MaxHPs);
      }
    }

    public int RotLevel {
      get {
        int num = FreshnessPercent;
        if (num < 5) return 5;
        if (num < 25) return 4;
        if (num < 50) return 3;
        if (num < 75) return 2;
        return num < 90 ? 1 : 0;
      }
    }
  }
}
