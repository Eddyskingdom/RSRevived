﻿using System;
using System.Collections.Generic;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using Zaimoni.Data;

using Point = Zaimoni.Data.Vector2D_short;

#nullable enable

namespace djack.RogueSurvivor.Gameplay.AI.Goals
{
    // Civilian: view every single coordinate in the zone
    // Police: view every contaminated square in the zone (tourism and/or threat)
    // ZoneLoc member must be "suitable for clearing"
    [Serializable]
    class ClearZone : Objective,Pathable
    {
        private ZoneLoc m_Zone;
        private HashSet<Point> m_Unverified = new HashSet<Point>();

        public ClearZone(int t0, Actor who, ZoneLoc dest) : base(t0, who) {
            m_Zone = dest;
            var threats = who.Threats;    // these two should agree on whether they're null or not
            var sights_to_see = who.InterestingLocs;
            if (null != threats) m_Unverified.UnionWith(threats.ThreatWhere(dest.m).Where(pt => m_Zone.Rect.Contains(pt)));
            if (null != sights_to_see) m_Unverified.UnionWith(sights_to_see.In(dest.m).Where(pt => m_Zone.Rect.Contains(pt)));
            if (null == threats && null == sights_to_see) {
                m_Unverified.UnionWith(m_Zone.Rect.Where(pt => who.CanEnter(new Location(m_Zone.m, pt)))); // \todo? eliminate GC thrashing
            }
        }

        public override bool UrgentAction(out ActorAction? ret)
        {
            ret = null;
            var threats_at = m_Actor.Threats?.ThreatWhere(m_Zone.m); // should have both of these null or non-null; other cases are formal completeness
            var tourism_at = m_Actor.InterestingLocs?.In(m_Zone.m);
            if (null != threats_at) {
                if (null != tourism_at) m_Unverified.RemoveWhere(pt => !threats_at.Contains(pt) && !tourism_at.Contains(pt));
                else m_Unverified.RemoveWhere(pt => !threats_at.Contains(pt));
            } else if (null != tourism_at) m_Unverified.RemoveWhere(pt => !tourism_at.Contains(pt));
            if (null == tourism_at || null != threats_at) {
                foreach (var loc in m_Actor.Controller.FOVloc) {
                    if (m_Zone.m == loc.Map) {
                        m_Unverified.Remove(loc.Position);
                        continue;
                    }
                    var denorm = m_Zone.m.Denormalize(loc);
                    if (null != denorm) m_Unverified.Remove(denorm.Value.Position);
                }
            }
            if (0 >= m_Unverified.Count) {
                _isExpired = true;
                return true;
            }
            if (0 < (m_Actor.Controller as ObjectiveAI).InterruptLongActivity()) return false;
            ret = Pathing();
            return true;
        }

        public ActorAction? Pathing() {
            var goals = new HashSet<Location>();
            foreach (var pt in m_Unverified) goals.Add(new Location(m_Zone.m, pt));
            return (m_Actor.Controller as ObjectiveAI).BehaviorPathTo(goals); // would need value-copy anyway of goals
        }
    }
}
