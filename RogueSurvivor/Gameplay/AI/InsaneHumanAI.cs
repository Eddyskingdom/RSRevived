﻿// Decompiled with JetBrains decompiler
// Type: djack.RogueSurvivor.Gameplay.AI.InsaneHumanAI
// Assembly: RogueSurvivor, Version=0.9.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D2AE4FAE-2CA8-43FF-8F2F-59C173341976
// Assembly location: C:\Private.app\RS9Alpha.Hg\RogueSurvivor.exe

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace djack.RogueSurvivor.Gameplay.AI
{
  [Serializable]
  internal class InsaneHumanAI : BaseAI
  {
    private string[] INSANITIES = new string[56]
    {
      "WHY WALK THERE?",
      "WHAT MAKES YOU FUN?",
      "YOU WEAR TOO MUCH COLORS!",
      "YOU HAVE BAD HABITS!",
      "MOM DIDN'T HANG ME!",
      "LUCIE! LUCIA!",
      "WHAT EGGS AND PASTA?",
      "TURTLE CATS!",
      "I REMEMBER THE CRABS!",
      "IT WAS AFTER THAT NOW!",
      "CUT IT! CUT IT NOW!",
      "DECEASED TOES!",
      "ICE-CREAM COPS!",
      "I SAW THAT FUCKING TWICE! TWICE! TWICE!",
      "FUCK BASTARD SAUSAGE!",
      "HEY YOU! STOP MOVING THE FLOOR!",
      "DROP THE FUCKING EGGS NOW!",
      "YOU GO FIRST AFTER ME!",
      "IT HURTS BUT ITS OK!",
      "LAST TIME WAS OK...",
      "SHE ISN'T NOT YET!",
      "I WAS CRAWLING HAHA!",
      "ROLLING LIKE AN EGG!",
      "THAT IS NOT DECENT!",
      "JUMP LIKE A FLOWER!",
      "NIGGER TRIGGER!",
      "CHEESE LIKE THESE...",
      "ILL-ADVISED LOBSTER!",
      "SSSHHHH! SILENCE... DO YOU SMELL?",
      "NOTHING BEATS. NOTHING!",
      "GROWN-UP MEN DON'T DO THAT!",
      "BARN BUSTER!",
      "SUPER SUPER?",
      "ONE MORE PASTA CRAP!",
      "LAZY LADY!",
      "I HATE TAP WATER!",
      "STILL WANKING FOR FOOD?",
      "LOOK! IT FITS LIKE A HOLE!",
      "PESKY POLAR PRANKS!",
      "LITTLE BY LITTLE YOU DIE...",
      "PLEASE TIE YOUR NECK PROPERLY!",
      "I SEE WHAT I SHIT ALL THE TIME!",
      "THAT'S FUCKING ANNOYING!",
      "I UNLOCK THE WALLS!",
      "I'M NOT SO SURE NOW!?",
      "RUSTY BUT TRUSTY!",
      "CHEESE LICKER!",
      "LAUNDRY TIME AGAIN AND AGAIN!",
      "DON'T YOU SEE I'M ASSEMBLED?",
      "MEXICAN MIDGETS!",
      "RAZOR RASCALS!",
      "PUNCH MY BALLS!",
      "STUCK IN A VICIOUS SQUARE!",
      "HORSE HOLSTER!",
      "THAT WAS COMPLETLY UNCALLED FOR!",
      "ROBOTS WON'T FOOL ME!"
    };
    private const int ATTACK_CHANCE = 80;
    private const int SHOUT_CHANCE = 80;
    private const int USE_EXIT_CHANCE = 50;
    private LOSSensor m_LOSSensor;

    protected override void CreateSensors()
    {
      this.m_LOSSensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
    }

    protected override List<Percept> UpdateSensors(RogueGame game)
    {
      return this.m_LOSSensor.Sense(game, this.m_Actor);
    }

    protected override ActorAction SelectAction(RogueGame game, List<Percept> percepts)
    {
      List<Percept> percepts1 = this.FilterSameMap(percepts);
      ActorAction actorAction1 = this.BehaviorEquipWeapon(game);
      if (actorAction1 != null)
      {
        this.m_Actor.Activity = Activity.IDLE;
        return actorAction1;
      }
      if (game.Rules.RollChance(ATTACK_CHANCE))
      {
        List<Percept> percepts2 = this.FilterEnemies(game, percepts1);
        if (percepts2 != null)
        {
          List<Percept> perceptList1 = FilterCurrent(percepts2);
          if (perceptList1 != null)
          {
            Percept percept1;
            ActorAction actorAction2 = TargetGridMelee(game, perceptList1, out percept1);
            if (actorAction2 != null)
            {
              this.m_Actor.Activity = Activity.CHASING;
              this.m_Actor.TargetActor = percept1.Percepted as Actor;
              return actorAction2;
            }
          }
          List<Percept> perceptList2 = this.Filter(game, percepts2, (Predicate<Percept>) (p => p.Turn != this.m_Actor.Location.Map.LocalTime.TurnCounter));
          if (perceptList2 != null)
          {
          Percept percept1;
          ActorAction actorAction2 = TargetGridMelee(game, perceptList2, out percept1);
          if (actorAction2 != null)
            {
              this.m_Actor.Activity = Activity.CHASING;
              this.m_Actor.TargetActor = percept1.Percepted as Actor;
              return actorAction2;
            }
          }
        }
      }
      if (game.Rules.RollChance(SHOUT_CHANCE))
      {
        string text = this.INSANITIES[game.Rules.Roll(0, this.INSANITIES.Length)];
        this.m_Actor.Activity = Activity.IDLE;
        game.DoEmote(this.m_Actor, text);
      }
      if (game.Rules.RollChance(USE_EXIT_CHANCE))
      {
        ActorAction actorAction2 = this.BehaviorUseExit(game, BaseAI.UseExitFlags.BREAK_BLOCKING_OBJECTS | BaseAI.UseExitFlags.ATTACK_BLOCKING_ENEMIES);
        if (actorAction2 != null)
        {
          this.m_Actor.Activity = Activity.IDLE;
          return actorAction2;
        }
      }
      this.m_Actor.Activity = Activity.IDLE;
      return this.BehaviorWander(game);
    }
  }
}
