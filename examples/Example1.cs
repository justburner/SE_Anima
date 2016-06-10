using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using SpaceEngineers.Game.ModAPI;
using AnimaScript;
using AnimaData;

namespace AnimaExamples.Example1
{
    using BlockModAPIType = /*( Mod API Block Type )*/ Sandbox.ModAPI.IMyTerminalBlock;
    [MyEntityComponentDescriptor(typeof(/*( Object Builder Type )*/ MyObjectBuilder_TerminalBlock), /*( Block name to link with gamelogic )*/ "AnimaExample1")]
    public class /*( Name of the gamelogic class )*/ AnimaExample1_Logic : MyGameLogicComponent
    {
        // ( Your variables here! )

        // Main anima and parts
        private Anima m_anima;
        private AnimaPart m_part;

        // Your block initialization
        public void BlockInit()
        {
            // ( Your initialization code here! )

            // No point to run this script if is a dedicated server because there's no graphics
            if (Anima.DedicatedServer) return;

            // Create the main Anima class
            m_anima = new Anima();

            // Initialize Anima
            if (!m_anima.Init(Entity as MyEntity, "Anima Examples", "AnimaExamples")) throw new ArgumentException("Anima failed to initialize!");

            // Add parts
            m_part = m_anima.AddPart(null, @"AnimaExamples\ModelCube");

            // Assign sequences
            m_part.Sequence = Seq_Cube.Adquire();

            // Play sequences
            m_part.Play(Anima.Playback.LOOP);
        }

        // Your block update (each frame after simulation ... if works ...)
        public void BlockUpdate()
        {
            // ( Your update code here! )

            // No point to run this script if is a dedicated server because there's no graphics
            if (Anima.DedicatedServer) return;

            // Enable Anima based of player distance
            m_anima.Enable = m_anima.TestPlayerDistance(500.0);

            // Only update if is enabled!
            if (m_anima.Enable)
            {
                m_anima.Update(m_anima.GetElapsed());
            }
        }

        // There's no reason to change code below unless you know what you're doing!

        private BlockModAPIType block;
        private bool active = false;

        // Gamelogic initialization
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // Update each frame, note this may not work for all object's types!
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            block = Entity as BlockModAPIType;
            if (block == null || MyAPIGateway.Session == null) return;

            BlockInit();

            active = true;
        }

        // Gamelogic update (each frame after simulation)
        public override void UpdateAfterSimulation()
        {
            if (!active) Init(null);
            if (!active || block == null || block.MarkedForClose || block.Closed) return;
            BlockUpdate();
        }

        // Gamelogic close when the block gets deleted
        public override void Close()
        {
            block = null;
        }

        // Gamelogic object builder, leave it alone ;)
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
}
