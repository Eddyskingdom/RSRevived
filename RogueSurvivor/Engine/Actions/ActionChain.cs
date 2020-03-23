﻿using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

using ObjectiveAI = djack.RogueSurvivor.Gameplay.AI.ObjectiveAI;

namespace djack.RogueSurvivor.Engine.Actions
{
    [Serializable]
    internal class ActionChain : ActorAction,Resolvable
    {
        private readonly List<ActorAction> m_Actions;

        public ActorAction ConcreteAction { get { return m_Actions[0]; } }

        public ActionChain(Actor actor, List<ActorAction> actions)
        : base(actor)
        {
#if DEBUG
            if (null == actions || 2 > actions.Count) throw new ArgumentNullException(nameof(actions));
            if (!(actor.Controller is ObjectiveAI)) throw new InvalidOperationException("controller not smart enough to plan actions");
#endif
            m_Actions = actions;
            m_FailReason = actions[0].FailReason;
        }

        public override bool IsLegal()
        {
            return m_Actions[0].IsLegal();
        }

        public override bool IsPerformable()
        {
            return m_Actions[0].IsPerformable();
        }

        public override void Perform()
        {
            (m_Actor.Controller as ObjectiveAI).ExecuteActionChain(m_Actions);
        }

        public ActorAction? Next { get {
          return 1<m_Actions.Count ? m_Actions[1] : null;
        } }

        public bool IsSemanticParadox() {
          // VAPORWARE: Checks for things like path loops not broken up by non-move actions
          return false;
        }
    }
}
