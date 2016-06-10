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

namespace AnimaExamples.Example2
{
    using BlockModAPIType = /*( Mod API Block Type )*/ SpaceEngineers.Game.ModAPI.Ingame.IMyGravityGenerator;
    [MyEntityComponentDescriptor(typeof(/*( Object Builder Type )*/ MyObjectBuilder_GravityGenerator), /*( Block name to link with gamelogic )*/ "AnimaExample2")]
    public class /*( Name of the gamelogic class )*/ AnimaExample2_Logic : MyGameLogicComponent
    {
        // Main anima and parts
        private Anima m_anima;
        private AnimaPart m_part_core;
        private AnimaPart m_part_topcap;
        private AnimaPart m_part_bottomcap;

        // This holds the last status so we can discover when status toggle
        public bool lastStatus = false;

        // This is a helper to know the sequence playing without comparing strings
        enum GravMode
        {
            POWER_ON, POWER_OFF, ACTIVE, INACTIVE,
        };
        private GravMode gravMode = GravMode.POWER_ON;

        // Your block initialization
        public void BlockInit()
        {
            // No point to run this script if is a dedicated server because there's no graphics
            if (Anima.DedicatedServer) return;

            // Create the main Anima class
            m_anima = new Anima();

            // Initialize Anima
            if (!m_anima.Init(Entity as MyEntity, "Anima Examples", "AnimaExamples")) throw new ArgumentException("Anima failed to initialize!");

            // Add parts
            m_part_core = m_anima.AddPart(null, @"AnimaExamples\ModelCore");
            m_part_topcap = m_anima.AddPart(m_part_core, @"animaexamples\TopCap");
            m_part_bottomcap = m_anima.AddPart(m_part_core, @"animaexamples\BottomCap");

            // Assign sequences
            coreFunctionality(m_part_core);
            m_part_core.OnComplete = coreFunctionality;

            // Play sequences
            m_anima.Play(Anima.Playback.LOOP);

            // Update each frame, note this may not work for all object's types!
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        // Your block update (each frame after simulation ... if works ...)
        public void BlockUpdate()
        {
            // No point to run this script if is a dedicated server because there's no graphics
            if (Anima.DedicatedServer) return;

            // Enable Anima based of player distance and if object is functional
            // It will only enable if it's under 500m AND block is functional
            m_anima.Enable = m_anima.TestPlayerDistance(500.0) && block.IsFunctional;

            // Only update if is enabled!
            if (m_anima.Enable)
            {
                m_anima.Update(m_anima.GetElapsed());

                // This is only for animating the "Emissive" material!
                float corePower = 0.0f;
                switch (gravMode)
                {
                    case GravMode.POWER_ON:
                        corePower = m_part_core.CursorNormal;
                        break;
                    case GravMode.POWER_OFF:
                        corePower = 1.0f - m_part_core.CursorNormal;
                        break;
                    case GravMode.ACTIVE:
                        corePower = 1.0f;
                        break;
                }
                m_part_core.SetEmissive(corePower, Color.Lerp(Color.DarkCyan, Color.Cyan, corePower));
            }
        }

        // Our callback to change sequences
        public void coreFunctionality(AnimaPart part)
        {
            bool status = block.IsWorking;
            if (!lastStatus && status)
            {
                // Powering on
                m_part_core.Sequence = Seq_Core_powerOn.Adquire();
                m_part_topcap.Sequence = Seq_TopCap_powerOn.Adquire();
                m_part_bottomcap.Sequence = Seq_BottomCap_powerOn.Adquire();
                gravMode = GravMode.POWER_ON;
            }
            else if (lastStatus && !status)
            {
                // Powering off
                m_part_core.Sequence = Seq_Core_powerOff.Adquire();
                m_part_topcap.Sequence = Seq_TopCap_powerOff.Adquire();
                m_part_bottomcap.Sequence = Seq_BottomCap_powerOff.Adquire();
                gravMode = GravMode.POWER_OFF;
            }
            else if (status)
            {
                // While active
                m_part_core.Sequence = Seq_Core_active.Adquire();
                m_part_topcap.Sequence = Seq_TopCap_active.Adquire();
                m_part_bottomcap.Sequence = Seq_BottomCap_active.Adquire();
                gravMode = GravMode.ACTIVE;
            }
            else
            {
                // While inactive
                m_part_core.Sequence = Seq_Core_inactive.Adquire();
                m_part_topcap.Sequence = Seq_TopCap_inactive.Adquire();
                m_part_bottomcap.Sequence = Seq_BottomCap_inactive.Adquire();
                gravMode = GravMode.INACTIVE;
            }
            lastStatus = status;
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
