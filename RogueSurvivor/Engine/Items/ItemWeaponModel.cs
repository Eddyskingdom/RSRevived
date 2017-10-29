﻿// Decompiled with JetBrains decompiler
// Type: djack.RogueSurvivor.Engine.Items.ItemWeaponModel
// Assembly: RogueSurvivor, Version=0.9.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D2AE4FAE-2CA8-43FF-8F2F-59C173341976
// Assembly location: C:\Private.app\RS9Alpha.Hg\RogueSurvivor.exe

using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine.Items
{
  internal class ItemWeaponModel : ItemModel
  {
    private readonly Attack m_Attack;

    public Attack Attack { get { return m_Attack; } }   // need value copy here for safety

    protected ItemWeaponModel(string aName, string theNames, string imageID, Attack attack, string flavor, bool is_artifact)
      : base(aName, theNames, imageID, flavor, DollPart.RIGHT_HAND)
    {
      m_Attack = attack;
      if (is_artifact) {
        IsProper = true;
        IsUnbreakable = true;
        IsUnique = true;
      }
    }
  }
}
