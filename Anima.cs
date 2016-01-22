/*
The MIT License (MIT)

Copyright (c) 2016 JustBurn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.ModAPI;
using VRage.Game.Entity;
using VRage.Library.Utils;
//using VRage.FileSystem;

namespace AnimaScript
{
    /// <summary>
    /// Anima main class.
    /// </summary>
    public class Anima
    {
        private MyEntity m_entity;
        private string m_modelsFolder;
        private List<AnimaPart> m_partList;
        private bool m_inRange;
        private MyGameTimer m_gameTimer;
        private double m_lastElapse;

        private static int m_instances = 0;

        /// <summary>
        /// Playback mode.
        /// </summary>
        public enum Playback
        {
            /// <summary>Halt playback.</summary>
            /// <remarks>When used in Play() it will be the same as Stop().</remarks>
            HALT,
            /// <summary>Play once.</summary>
            /// <remarks>Playback is stopped once it hit the outside of animation.</remarks>
            ONCE,
            /// <summary>Loop play.</summary>
            /// <remarks>Loop infinitely, to stop call Stop().</remarks>
            LOOP,
            /// <summary>Ping-pong play.</summary>
            /// <remarks>Loop infinitely, invert part speed in each border, to stop call Stop().</remarks>
            PINGPONG,
        }

        /// <summary>
        /// Construct Anima class.
        /// </summary>
        /// <remarks>Don't forget to call Init(MyEntity, string, string) to initialize!</remarks>
        public Anima()
        {
            m_entity = null;
            m_modelsFolder = null;
            m_partList = new List<AnimaPart>();
            m_inRange = true;
            m_gameTimer = new MyGameTimer();
            m_lastElapse = m_gameTimer.Elapsed.Seconds;
            m_instances++;
        }

        ~Anima()
        {
            m_instances--;
            if (m_instances <= 0) AnimaSeqManager.DiscardAll();
        }

        /// <summary>
        /// Script version.
        /// </summary>
        public struct Version
        {
            public const int Major = 0;
            public const int Minor = 4;
        }

        /// <summary>
        /// Return true if is a dedicated server (no game graphics)
        /// </summary>
        public static bool DedicatedServer
        {
            get { return MyAPIGateway.Utilities.IsDedicated; }
        }

        /// <summary>
        /// Return root entity.
        /// </summary>
        public MyEntity Entity
        {
            get { return m_entity; }
        }

        /// <summary>
        /// Enable Anima parts, if disabled all parts won't be updated and will be invisible. Useful when used together with TestPlayerDistance(double).
        /// </summary>
        public bool Enable
        {
            get { return m_inRange; }
            set
            {
                if (value != m_inRange)
                {
                    m_inRange = value;
                    foreach (var part in m_partList)
                    {
                        part.Enable_Recv(value);
                    }
                }
            }
        }

        /// <summary>
        /// Return models folder (FOR DEBUGGING)
        /// </summary>
        public string ModelsFolder
        {
            get { return m_modelsFolder; }
        }

        /// <summary>
        /// Return elapsed time in seconds since last call to GetElapsed().
        /// </summary>
        public double GetElapsed()
        {
            double elapse = m_gameTimer.Elapsed.Seconds;
            double value = elapse - m_lastElapse;
            m_lastElapse = elapse;
            return value;
        }

        /// <summary>
        /// Initialize Anima class.
        /// </summary>
        /// <param name="entity">root block entity.</param>
        /// <param name="modName">mod name in workshop.</param>
        /// <param name="altModName">alternative mod name, for local mod.</param>
        /// <returns>true on success.</returns>
        /// <remarks>Current game version doesn't allow to check directories, if model show in-game like a black cube make sure the modName and/or altModName are correct.</remarks>
        public bool Init(MyEntity entity, string modName, string altModName)
        {
            if (entity == null) return false;

            // Scan for publisher ID
            ulong publishID = 0;
            var mods = MyAPIGateway.Session.GetCheckpoint("null").Mods;
            foreach (var mod in mods)
            {
                if (modName == mod.FriendlyName) publishID = mod.PublishedFileId;
            }

            // Figure out mod's directory
            // A better way was if MyAPIGateway reported the current mod path
            if (publishID != 0)
            {
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}.sbm\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, publishID.ToString()));
                /*if (!Directory.Exists(m_modelsFolder))
                {
                    // Extracted sbm into local copy
                    m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, publishID.ToString()));
                }*/
            }
            else m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, altModName));
            /*if (!MyFileSystem.DirectoryExists(m_modelsFolder) && !string.IsNullOrEmpty(modName))
            {
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, modName));
            }
            if (!MyFileSystem.DirectoryExists(m_modelsFolder) && !string.IsNullOrEmpty(altModName))
            {
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, altModName));
            }
            if (!MyFileSystem.DirectoryExists(m_modelsFolder))
            {
                return false;
            }*/

            // Done!
            m_entity = entity;
            return true;
        }

        /// <summary>
        /// Add anima part for animation.
        /// </summary>
        /// <param name="parent">parent part, null for root.</param>
        /// <param name="modelFile">Model file name inside Models folder, excluding extension.</param>
        /// <param name="smoothAnim">smooth animation, linear lerp between keyframes.</param>
        /// <param name="visible">initialize it visible?</param>
        /// <returns>part or null for failure.</returns>
        public AnimaPart AddPart(AnimaPart parent, string modelFile, bool smoothAnim = false, bool visible = true)
        {
            if (m_entity == null) return null;

            // Create part
            MyEntity parentEntity = (parent != null) ? parent.Entity : m_entity;
            MyEntity part_entity = new MyEntity();
            part_entity.Init(null, m_modelsFolder + modelFile + ".mwm", parentEntity, null, null);
            part_entity.Render.EnableColorMaskHsv = true;
            part_entity.Render.ColorMaskHsv = m_entity.Render.ColorMaskHsv;
            part_entity.Render.PersistentFlags = MyPersistentEntityFlags2.CastShadows;
            part_entity.PositionComp.LocalMatrix = Matrix.Identity;
            part_entity.Flags = EntityFlags.Visible | EntityFlags.NeedsDraw | EntityFlags.NeedsDrawFromParent | EntityFlags.InvalidateOnMove;
            part_entity.OnAddedToScene(parentEntity);

            AnimaPart part = new AnimaPart(this, parent, part_entity);
            if (!visible) part.Visible = false;
            m_partList.Add(part);
            return part;
        }

        /// <summary>
        /// Add anima part for animation, parent will be one of root's subpart.
        /// </summary>
        /// <param name="subpartName">root subpart name.</param>
        /// <param name="modelFile">>Model file name inside Models folder, excluding extension.</param>
        /// <param name="smoothAnim">smooth animation, linear lerp between keyframes.</param>
        /// <param name="visible">initialize it visible?</param>
        /// <returns>part or null for failure.</returns>
        public AnimaPart AddPartToSubpart(string subpartName, string modelFile, bool smoothAnim = false, bool visible = true)
        {
            if (m_entity == null) return null;
            if (!m_entity.Subparts.ContainsKey(subpartName)) return null;

            // Create part
            MyEntity parentEntity = m_entity.Subparts[subpartName];
            MyEntity part_entity = new MyEntity();
            part_entity.Init(null, m_modelsFolder + modelFile + ".mwm", parentEntity, null, null);
            part_entity.Render.EnableColorMaskHsv = true;
            part_entity.Render.ColorMaskHsv = m_entity.Render.ColorMaskHsv;
            part_entity.Render.PersistentFlags = MyPersistentEntityFlags2.CastShadows;
            part_entity.PositionComp.LocalMatrix = Matrix.Identity;
            part_entity.Flags = EntityFlags.Visible | EntityFlags.NeedsDraw | EntityFlags.NeedsDrawFromParent | EntityFlags.InvalidateOnMove;
            part_entity.OnAddedToScene(parentEntity);

            AnimaPart part = new AnimaPart(this, null, part_entity);
            if (!visible) part.Visible = false;
            m_partList.Add(part);
            return part;
        }

        // Hmm... can't exactly remove subpart easily because of parenting (will need to invalidate all childs MyEntity/AnimaParts/List<AnimaParts>)...

        /// <summary>
        /// Test player distance with block.
        /// </summary>
        /// <param name="maxDistance">distance in meters.</param>
        /// <returns>true if inside range, false if outside.</returns>
        public bool TestPlayerDistance(double maxDistance)
        {
            return Vector3D.DistanceSquared(m_entity.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation) < (maxDistance * maxDistance);
        }

        /// <summary>
        /// Set smooth animation on all parts.
        /// </summary>
        /// <param name="smoothAnim">Enable smooth animation, linear lerp between keyframes.</param>
        public void SetSmoothAnimation(bool smoothAnim)
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.SmoothAnimation = smoothAnim;
            }
        }

        // AUTOMATIC ANIMATIONS

        /// <summary>
        /// Start playback on all parts.
        /// </summary>
        /// <param name="playMode">any of playback mode. Enum of Anima.Playback.</param>
        /// <param name="cursorPos">starting cursor position (in keyframes).</param>
        /// <remarks>cursor position will be clamped to part's animation range.</remarks>
        public void Play(Playback playMode, float cursorPos = 0f)
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.Play(playMode, cursorPos);
            }
        }

        /// <summary>
        /// Start playback on all parts.
        /// </summary>
        /// <param name="playMode">any of playback mode. Enum of Anima.Playback.</param>
        /// <param name="cursorPos">starting cursor position (normalized between 0.0f to 1.0f).</param>
        /// <remarks>cursor position will be clamped to part's animation range.</remarks>
        public void PlayNormal(Playback playMode, float cursorPos = 0f)
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.PlayNormal(playMode, cursorPos);
            }
        }

        /// <summary>
        /// Stop playback on all parts.
        /// </summary>
        public void Stop()
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.Stop();
            }
        }

        /// <summary>
        /// Resume playback on all parts.
        /// </summary>
        public void Resume()
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.Resume();
            }
        }

        /// <summary>
        /// Pause playback on all parts.
        /// </summary>
        public void Pause()
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.Pause();
            }
        }

        /// <summary>
        /// Process the animation on all parts.
        /// </summary>
        /// <param name="elapsedTime">period elapsed since last call (in seconds).</param>
        public void Update(double elapsedTime)
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.Update(elapsedTime);
            }
        }

        /// <summary>
        /// Apply transformations on all parts.
        /// </summary>
        /// <param name="invokeCallback">if false, OnTransform callback won't be invoked.</param>
        public void ApplyTransformations(bool invokeCallback)
        {
            if (m_entity == null) return;
            foreach (var part in m_partList)
            {
                part.ApplyTransformations(invokeCallback);
            }
        }
    }

    /// <summary>
    /// Anima part class.
    /// </summary>
    public class AnimaPart
    {
        private Anima m_anima;
        private MyEntity m_entity;
        private AnimaPart m_parent;
        private bool m_smoothAnim;
        private bool m_enabled;
        private bool m_visible;

        // Callbacks
        private Action<AnimaPart> m_onTransform;
        private Action<AnimaPart> m_onComplete;

        // Automatic play
        private AnimaSeqBase m_seq;
        private Anima.Playback m_playMode;
        private Anima.Playback m_playMode2;
        private float m_cursorPos;
        private float m_speed;
        private int m_lastCursorPos;

        // Coloring
        private bool m_disableRootColor;

        // Transformations
        private Vector3 m_position;
        private Quaternion m_rotation;
        private Vector3 m_scale;

        /// <summary>
        /// Anima main class constructor.
        /// </summary>
        public AnimaPart(Anima anima, AnimaPart parent, MyEntity entity)
        {
            m_anima = anima;
            m_entity = entity;
            m_parent = parent;
            m_smoothAnim = false;
            m_enabled = true;
            m_visible = true;
            m_onTransform = null;
            m_onComplete = null;
            m_seq = null;
            m_playMode = Anima.Playback.HALT;
            m_playMode2 = Anima.Playback.HALT;
            m_cursorPos = 0f;
            m_speed = 1f;
            m_lastCursorPos = 0;
            m_disableRootColor = false;
            m_position = Vector3.Zero;
            m_rotation = Quaternion.Identity;
            m_scale = Vector3.One;
        }

        /// <summary>
        /// Get game's entity.
        /// </summary>
        public MyEntity Entity
        {
            get { return m_entity; }
        }

        /// <summary>
        /// Get parent part.
        /// </summary>
        /// <remarks>returns null if parent is root or subpart.</remarks>
        public AnimaPart Parent
        {
            get { return m_parent; }
        }

        /// <summary>
        /// Enable smooth animation, linear lerp between keyframes.
        /// </summary>
        public bool SmoothAnimation
        {
            get { return m_smoothAnim; }
            set { m_smoothAnim = value; }
        }

        /// <summary>
        /// Is part active?
        /// </summary>
        /// <remarks>While this is false, Update() and transformations aren't applied.</remarks>
        public bool Active
        {
            get { return m_enabled; }
        }

        /// <summary>
        /// Visible object? Writing will affect all child's parts!
        /// </summary>
        /// <remarks>will return false if not active!</remarks>
        public bool Visible
        {
            get { return m_entity.Render.Visible; }
            set
            {
                if (m_visible != value)
                {
                    m_visible = value;
                    m_entity.Render.Visible = m_visible && m_enabled;
                }
            }
        }

        /// <summary>
        /// Set/get sequence for playback. Use (sequence_class_name).Adquire().
        /// </summary>
        public AnimaSeqBase Sequence
        {
            get { return m_seq; }
            set { m_seq = value; }
        }

        /// <summary>
        /// Get playback mode.
        /// </summary>
        public Anima.Playback Playback
        {
            get { return m_playMode; }
        }

        /// <summary>
        /// Get cursor position in keyframes.
        /// </summary>
        public float Cursor
        {
            get { return m_cursorPos; }
        }

        /// <summary>
        /// Get cursor position in normalized range.
        /// </summary>
        public float CursorNormal
        {
            get
            {
                if (m_seq == null) return 0f;
                if (m_cursorPos <= m_seq.FrameStart) return 0f;
                if (m_cursorPos >= m_seq.FrameEnd) return 1f;
                return (m_cursorPos - m_seq.FrameStart) / m_seq.FramePeriod;
            }
        }

        /// <summary>
        /// Set/get playback speed (0.5 = half speed).
        /// </summary>
        public float Speed
        {
            get { return m_speed; }
            set { m_speed = value; }
        }

        /// <summary>
        /// Enable/disable custom color.
        /// </summary>
        /// <remarks>If disabled, color is matched to parent in Update().</remarks>
        public bool EnableColor
        {
            get { return m_disableRootColor; }
            set { m_disableRootColor = value; }
        }

        /// <summary>
        /// Set/get part color, in Hue/Saturation/Value.
        /// </summary>
        /// <remarks>Color can only be changed if EnableColor is true.</remarks>
        public Vector3 ColorHsv
        {
            get
            {
                if (m_entity == null) return Vector3.Zero;
                return m_entity.Render.ColorMaskHsv;
            }
            set
            {
                if (m_entity == null || !m_disableRootColor) return;
                m_entity.Render.ColorMaskHsv = value;
            }
        }

        /// <summary>
        /// Set/get position. (local matrix)
        /// </summary>
        public Vector3 Position
        {
            get { return m_position; }
            set { m_position = value; }
        }

        /// <summary>
        /// Set/get rotation. (local matrix)
        /// </summary>
        public Quaternion Rotation
        {
            get { return m_rotation; }
            set { m_rotation = value; }
        }

        /// <summary>
        /// Set/get scale. (local matrix)
        /// </summary>
        public Vector3 Scale
        {
            get { return m_scale; }
            set { m_scale = value; }
        }

        /// <summary>
        /// Set/get callback that is called just before sending transform.
        /// </summary>
        /// <remarks>This can be useful to perform procedual tranformations together with animation.</remarks>
        public Action<AnimaPart> OnTransform
        {
            get { return m_onTransform; }
            set { m_onTransform = value; }
        }

        /// <summary>
        /// Set/get callback that is called when the cursor goes outside the animation range.
        /// </summary>
        /// <remarks>Called when the animation ends or loops.</remarks>
        public Action<AnimaPart> OnComplete
        {
            get { return m_onComplete; }
            set { m_onComplete = value; }
        }

        /// <summary>
        /// Start playback.
        /// </summary>
        /// <param name="playMode">any of playback mode, enum of Anima.Playback.</param>
        /// <param name="cursorPos">starting cursor position (in keyframes).</param>
        /// <remarks>cursor position will be clamped to animation range.</remarks>
        public void Play(Anima.Playback playMode, float cursorPos = 0f)
        {
            if (m_entity == null || m_seq == null) return;
            m_playMode = playMode;
            m_playMode2 = playMode;
            if (cursorPos < m_seq.FrameStart) cursorPos = m_seq.FrameStart;
            else if (cursorPos > m_seq.FrameEnd) cursorPos = m_seq.FrameEnd;
            m_cursorPos = cursorPos;
            ProcAutoAnimation();
        }

        /// <summary>
        /// Start playback with normalized cursor.
        /// </summary>
        /// <param name="playMode">any of playback mode, enum of Anima.Playback.</param>
        /// <param name="cursorPos">starting cursor position (normalized between 0 to 1).</param>
        /// <remarks>cursor position will be clamped to animation range.</remarks>
        public void PlayNormal(Anima.Playback playMode, float cursorPos = 0f)
        {
            if (m_entity == null || m_seq == null) return;
            m_playMode = playMode;
            m_playMode2 = playMode;
            if (cursorPos < 0f) cursorPos = 0f;
            else if (cursorPos > 1f) cursorPos = 1f;
            m_cursorPos = cursorPos * m_seq.FramePeriod + m_seq.FrameStart;
            ProcAutoAnimation();
        }

        /// <summary>
        /// Stop playback.
        /// </summary>
        public void Stop()
        {
            if (m_entity == null) return;
            m_playMode = Anima.Playback.HALT;
            m_playMode2 = Anima.Playback.HALT;
        }

        /// <summary>
        /// Resume playback.
        /// </summary>
        public void Resume()
        {
            if (m_entity == null) return;
            if (m_playMode != Anima.Playback.HALT) return;
            m_playMode = m_playMode2;
            if (m_playMode != Anima.Playback.HALT) ProcAutoAnimation();
        }

        /// <summary>
        /// Pause playback.
        /// </summary>
        public void Pause()
        {
            if (m_entity == null) return;
            if (m_playMode == Anima.Playback.HALT) return;
            m_playMode2 = m_playMode;
            m_playMode = Anima.Playback.HALT;
        }

        /// <summary>
        /// Set "Emissive" material.
        /// </summary>
        /// <param name="emissivity">Emissivity between 0.0f to 1.0f, being 1.0f full emissive.</param>
        /// <param name="emissivePartColor">Emissive color, multiplied with diffuse texture.</param>
        public void SetEmissive(float emissivity, Color emissivePartColor)
        {
            if (m_entity == null) return;
            MyCubeBlockEmissive.SetEmissiveParts(m_entity, emissivity, emissivePartColor, Color.White);
        }

        /// <summary>
        /// Set "Emissive" and "Display" material.
        /// </summary>
        /// <param name="emissivity">Emissivity between 0.0f to 1.0f, being 1.0f full emissive.</param>
        /// <param name="emissivePartColor">"Emissive" color, multiplied with diffuse texture.</param>
        /// <param name="displayPartColor">"Display" color, multiplied with diffuse texture.</param>
        /// <remarks>Both materials are affected by emissivity, since Keen never uses "Display" material they may remove it in the future, use it at your own risk.</remarks>
        public void SetEmissive(float emissivity, Color emissivePartColor, Color displayPartColor)
        {
            if (m_entity == null) return;
            MyCubeBlockEmissive.SetEmissiveParts(m_entity, emissivity, emissivePartColor, displayPartColor);
        }

        /// <summary>
        /// Process the animation.
        /// </summary>
        /// <param name="elapsedTime">period elapsed since last call (in seconds).</param>
        /// <returns>true on success</returns>
        public void Update(double elapsedTime)
        {
            if (m_entity == null) return;

            float elapsed = (float)elapsedTime;
            if (elapsed == 0f || !m_enabled) return;

            // Apply root color
            if (!m_disableRootColor)
            {
                m_entity.Render.ColorMaskHsv = m_anima.Entity.Render.ColorMaskHsv;
            }

            // Apply animation
            if (m_playMode != Anima.Playback.HALT)
            {
                if (m_seq == null)
                {
                    m_playMode = Anima.Playback.HALT;
                    m_playMode2 = Anima.Playback.HALT;
                    return;
                }

                // Timelapse
                bool callLoop = false;
                m_cursorPos += elapsed * m_seq.FrameRate * m_speed;
                if (m_cursorPos < m_seq.FrameStart || m_cursorPos >= (m_seq.FrameEnd + 1f))
                {
                    if (m_playMode == Anima.Playback.ONCE)
                    {
                        m_playMode = Anima.Playback.HALT;
                        m_playMode2 = Anima.Playback.HALT;
                    }
                    else if (m_playMode == Anima.Playback.PINGPONG)
                    {
                        m_speed = -m_speed;
                    }
                    float newPos = (m_cursorPos - m_seq.FrameStart) % m_seq.FramePeriod;
                    if (newPos < 0f) newPos = m_seq.FramePeriod + newPos;
                    m_cursorPos = newPos + m_seq.FrameStart;
                    callLoop = true;
                }

                // Call callback
                if (callLoop && m_onComplete != null) m_onComplete(this);

                // Get keyframe and apply transformation if needed
                if (m_smoothAnim || ((int)m_cursorPos != m_lastCursorPos)) ProcAutoAnimation();
            }
        }

        /// <summary>
        /// Apply transformations.
        /// </summary>
        /// <param name="invokeCallback">if false, OnTransform callback won't be invoked.</param>
        public void ApplyTransformations(bool invokeCallback)
        {
            if (m_entity == null || !m_enabled) return;
            if (m_onTransform != null && invokeCallback) m_onTransform(this);
            m_entity.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(m_rotation, m_position, m_scale);
        }

        /// <summary>
        /// Apply local matrix.
        /// </summary>
        /// <param name="matrix">local matrix</param>
        /// <param name="invokeCallback">if false, OnTransform callback won't be invoked.</param>
        /// <remarks>transformations won't be extracted from the matrix.</remarks>
        public void ApplyLocalMatrix(ref Matrix matrix, bool invokeCallback)
        {
            if (m_entity == null || !m_enabled) return;
            if (m_onTransform != null && invokeCallback) m_onTransform(this);
            m_entity.PositionComp.LocalMatrix = matrix;
        }

        private void ProcAutoAnimation()
        {
            if (!m_enabled) return;

            if (m_seq == null)
            {
                m_playMode = Anima.Playback.HALT;
                m_playMode2 = Anima.Playback.HALT;
                return;
            }

            // Get keyframe and apply transformation
            AnimaSeqBase.Keyframe sframe;
            m_lastCursorPos = (int)m_cursorPos;
            if (!m_seq.GetKeyframeData(m_lastCursorPos, out sframe))
            {
                m_playMode = Anima.Playback.HALT;
                m_playMode2 = Anima.Playback.HALT;
                return;
            }

            // Smooth animation
            if (m_smoothAnim)
            {
                AnimaSeqBase.Keyframe sframe2;
                if (!m_seq.GetKeyframeData(m_lastCursorPos + 1, out sframe2))
                {
                    m_playMode = Anima.Playback.HALT;
                    m_playMode2 = Anima.Playback.HALT;
                    return;
                }
                float lerpValue = m_cursorPos - (float)m_lastCursorPos;
                sframe.position = Vector3.Lerp(sframe.position, sframe2.position, lerpValue);
                sframe.rotation = Quaternion.Lerp(sframe.rotation, sframe2.rotation, lerpValue);
                sframe.scale = Vector3.Lerp(sframe.scale, sframe2.scale, lerpValue);
            }

            // Apply trasformation
            m_position = sframe.position;
            m_rotation = sframe.rotation;
            m_scale = sframe.scale;
            if (m_onTransform != null) m_onTransform(this);
            m_entity.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(m_rotation, m_position, m_scale);
        }

        /// <summary>
        /// Used by Anima.Enable, shouldn't be called directly.
        /// </summary>
        public void Enable_Recv(bool enable)
        {
            if (m_enabled != enable)
            {
                m_enabled = enable;
                m_entity.Render.Visible = m_visible && m_enabled;
            }
        }
    }

    /// <summary>
    /// Workaround class to expose UpdateEmissiveParts() to public, useful for changing "Emissive" material.
    /// </summary>
    public class MyCubeBlockEmissive : MyCubeBlock
    {
        /// <summary>
        /// Set emissive parts
        /// </summary>
        /// <param name="entity">entity to apply changes</param>
        /// <param name="emissivity">Emissivity between 0.0f to 1.0f, being 1.0f full emissive.</param>
        /// <param name="emissivePartColor">"Emissive" color, multiplied with diffuse texture.</param>
        /// <param name="displayPartColor">"Display" color, multiplied with diffuse texture.</param>
        /// <remarks>Both materials are affected by emissivity, since Keen never uses "Display" material they may remove it in the future, use it at your own risk.</remarks>
        public static void SetEmissiveParts(MyEntity entity, float emissivity, Color emissivePartColor, Color displayPartColor)
        {
            if (entity != null) UpdateEmissiveParts(entity.Render.RenderObjectIDs[0], emissivity, emissivePartColor, displayPartColor);
        }
    }

    /// <summary>
    /// Anima sequence base.
    /// </summary>
    public abstract class AnimaSeqBase
    {
        private List<Keyframe> m_keyframes = null;
        private int m_gStart = 0, m_gEnd = 0;
        private float m_rate = 0f;
        private int m_kStart = 0, m_kEnd = 0;
        private string m_name = null;

        /// <summary>
        /// Keyframe data.
        /// </summary>
        public struct Keyframe
        {
            /// <summary>Position.</summary>
            public Vector3 position;
            /// <summary>Rotation (Quaternion).</summary>
            public Quaternion rotation;
            /// <summary>Scale.</summary>
            public Vector3 scale;
        }

        /// <summary>
        /// Get starting frame.
        /// </summary>
        public float FrameStart
        {
            get { return (float)(m_gStart); }
        }

        /// <summary>
        /// Get ending frame.
        /// </summary>
        public float FrameEnd
        {
            get { return (float)(m_gEnd); }
        }

        /// <summary>
        /// Get period in frames.
        /// </summary>
        public float FramePeriod
        {
            get { return (float)(m_gEnd - m_gStart + 1); }
        }

        /// <summary>
        /// Get frame rate.
        /// </summary>
        public float FrameRate
        {
            get { return (float)m_rate; }
        }

        /// <summary>
        /// Get sequence class name as string.
        /// </summary>
        public string Name
        {
            get { return m_name; }
        }

        /// <summary>
        /// Anima sequence base constructor, isn't to be called directly, Use (your_class_name).Adquire().
        /// </summary>
        protected AnimaSeqBase()
        {
            PData();
        }

        /// <summary>
        /// Adquire sequence, data is only loaded once and shared between multiple instances.
        /// </summary>
        /// <returns>static copy of the sequence</returns>
        public static AnimaSeqBase Adquire()
        {
            throw new ArgumentException("Cannot Adquire from sequence base!");
        }

        /// <summary>
        /// Discard stored data, call this on game unload to free memory.
        /// </summary>
        public abstract void DiscardStatic();

        /// <summary>
        /// Discard stored data, call this on game unload to free memory.
        /// </summary>
        public static void Discard()
        {
            throw new ArgumentException("Cannot Discard from sequence base!");
        }

        /// <summary>
        /// Get data associated to keyframe.
        /// </summary>
        /// <param name="keyframe">keyframe</param>
        /// <param name="data">output of data</param>
        /// <returns>true on success</returns>
        public bool GetKeyframeData(int keyframe, out AnimaSeqBase.Keyframe data)
        {
            if (m_kEnd == 0)
            {
                // No data! Shouldn't happen!
                data = new AnimaSeqBase.Keyframe();
                return false;
            }
            if (keyframe < m_kStart) keyframe = m_kStart;
            if (keyframe > m_kEnd) keyframe = m_kEnd;
            data = m_keyframes[keyframe - m_kStart];
            return true;
        }

        /// <summary>
        /// This holds the actual animation data exported by Blender
        /// </summary>
        protected abstract void PData();

        /// <summary>
        /// Private storage: initialization
        /// </summary>
        /// <param name="name">class name</param>
        /// <param name="g_start">frame start</param>
        /// <param name="g_end">frame end</param>
        /// <param name="rate">frame rate</param>
        /// <param name="kf_start">frame of first item</param>
        /// <param name="kf_end">frame of last item</param>
        /// <returns></returns>
        protected void PInit(string name, int g_start, int g_end, float rate, int kf_start, int kf_end)
        {
            m_keyframes = new List<Keyframe>();
            m_name = name;
            m_gStart = g_start;
            m_gEnd = g_end;
            m_rate = rate;
            m_kStart = kf_start;
            m_kEnd = kf_end;
        }

        /// <summary>
        /// Private storage: Add location, rotation and scaling
        /// </summary>
        /// <param name="locX">Location Y</param>
        /// <param name="locY">Location Y</param>
        /// <param name="locZ">Location Z</param>
        /// <param name="rotX">Quaternion rotation X</param>
        /// <param name="rotY">Quaternion rotation Y</param>
        /// <param name="rotZ">Quaternion rotation Z</param>
        /// <param name="rotW">Quaternion rotation W</param>
        /// <param name="scaleX">Scaling X</param>
        /// <param name="scaleY">Scaling Y</param>
        /// <param name="scaleZ">Scaling X</param>
        protected void PLocRotScale(float locX, float locY, float locZ, float rotX, float rotY, float rotZ, float rotW, float scaleX, float scaleY, float scaleZ)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = new Vector3(locX, locY, locZ);
            keyData.rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            keyData.scale = new Vector3(scaleX, scaleY, scaleZ);
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add location only
        /// </summary>
        /// <param name="locX">Location Y</param>
        /// <param name="locY">Location Y</param>
        /// <param name="locZ">Location Z</param>
        protected void PLocation(float locX, float locY, float locZ)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = new Vector3(locX, locY, locZ);
            keyData.rotation = Quaternion.Identity;
            keyData.scale = Vector3.One;
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add rotation only
        /// </summary>
        /// <param name="rotX">Quaternion rotation X</param>
        /// <param name="rotY">Quaternion rotation Y</param>
        /// <param name="rotZ">Quaternion rotation Z</param>
        /// <param name="rotW">Quaternion rotation W</param>
        protected void PRotation(float rotX, float rotY, float rotZ, float rotW)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = Vector3.Zero;
            keyData.rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            keyData.scale = Vector3.One;
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add scaling only
        /// </summary>
        /// <param name="scaleX">Scaling X</param>
        /// <param name="scaleY">Scaling Y</param>
        /// <param name="scaleZ">Scaling X</param>
        protected void PPScaling(float scaleX, float scaleY, float scaleZ)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = Vector3.Zero;
            keyData.rotation = Quaternion.Identity;
            keyData.scale = new Vector3(scaleX, scaleY, scaleZ);
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add location and rotation
        /// </summary>
        /// <param name="locX">Location Y</param>
        /// <param name="locY">Location Y</param>
        /// <param name="locZ">Location Z</param>
        /// <param name="rotX">Quaternion rotation X</param>
        /// <param name="rotY">Quaternion rotation Y</param>
        /// <param name="rotZ">Quaternion rotation Z</param>
        /// <param name="rotW">Quaternion rotation W</param>
        protected void PLocRot(float locX, float locY, float locZ, float rotX, float rotY, float rotZ, float rotW)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = new Vector3(locX, locY, locZ);
            keyData.rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            keyData.scale = Vector3.One;
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add location and scaling
        /// </summary>
        /// <param name="locX">Location Y</param>
        /// <param name="locY">Location Y</param>
        /// <param name="locZ">Location Z</param>
        /// <param name="scaleX">Scaling X</param>
        /// <param name="scaleY">Scaling Y</param>
        /// <param name="scaleZ">Scaling X</param>
        protected void PLocScale(float locX, float locY, float locZ, float scaleX, float scaleY, float scaleZ)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = new Vector3(locX, locY, locZ);
            keyData.rotation = Quaternion.Identity;
            keyData.scale = new Vector3(scaleX, scaleY, scaleZ);
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add rotation and scaling
        /// </summary>
        /// <param name="rotX">Quaternion rotation X</param>
        /// <param name="rotY">Quaternion rotation Y</param>
        /// <param name="rotZ">Quaternion rotation Z</param>
        /// <param name="rotW">Quaternion rotation W</param>
        /// <param name="scaleX">Scaling X</param>
        /// <param name="scaleY">Scaling Y</param>
        /// <param name="scaleZ">Scaling X</param>
        protected void PRotScale(float rotX, float rotY, float rotZ, float rotW, float scaleX, float scaleY, float scaleZ)
        {
            Keyframe keyData = new Keyframe();
            keyData.position = Vector3.Zero;
            keyData.rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            keyData.scale = new Vector3(scaleX, scaleY, scaleZ);
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Private storage: Add nothing
        /// </summary>
        protected void PNone()
        {
            Keyframe keyData = new Keyframe();
            keyData.position = Vector3.Zero;
            keyData.rotation = Quaternion.Identity;
            keyData.scale = Vector3.One;
            m_keyframes.Add(keyData);
        }

        /// <summary>
        /// Used internally.
        /// </summary>
        /// <param name="sequence">Sequence.</param>
        /// <returns>Sequence.</returns>
        protected static AnimaSeqBase PManAdd(AnimaSeqBase sequence)
        {
            return AnimaSeqManager.AddToList(sequence);
        }

        /// <summary>
        /// Used internally.
        /// </summary>
        /// <param name="sequence">Sequence.</param>
        /// <returns>null.</returns>
        protected static AnimaSeqBase PManRem(AnimaSeqBase sequence)
        {
            return AnimaSeqManager.RemoveFromList(sequence);
        }
    }

    /// <summary>
    /// Anima sequence singleton.
    /// </summary>
    public static class AnimaSeqManager
    {
        public static List<AnimaSeqBase> m_seqList = null;

        /// <summary>
        /// Used internally.
        /// </summary>
        /// <param name="sequence">Sequence.</param>
        /// <returns>Sequence.</returns>
        public static AnimaSeqBase AddToList(AnimaSeqBase sequence)
        {
            if (m_seqList == null) m_seqList = new List<AnimaSeqBase>();
            if (!m_seqList.Contains(sequence)) m_seqList.Add(sequence);
            return sequence;
        }

        /// <summary>
        /// Used internally.
        /// </summary>
        /// <param name="sequence">Sequence.</param>
        /// <returns>null.</returns>
        public static AnimaSeqBase RemoveFromList(AnimaSeqBase sequence)
        {
            if (m_seqList == null) return null;
            if (m_seqList.Contains(sequence)) m_seqList.Remove(sequence);
            return null;
        }

        /// <summary>
        /// Used internally. Discard stored data on all adquired sequences.
        /// </summary>
        public static void DiscardAll()
        {
            if (m_seqList == null) return;
            List<AnimaSeqBase> m_seqListShadow = new List<AnimaSeqBase>(m_seqList);   // There's a better way, but this seems the safest
            foreach (var sequence in m_seqListShadow)
            {
                sequence.DiscardStatic();
            }
            m_seqList.Clear();
            m_seqList = null;
        }
    }
}
