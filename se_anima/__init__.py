# The MIT License (MIT)
# 
# Copyright (c) 2016 JustBurn
# 
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
# 
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
# 
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

bl_info = {
	"name":        "Anima Script Export",
	"author":      "JustBurn",
	"version":     (0, 1, 0),
	"blender":     (2, 72, 0),
	"location":    "File > Export",
	"description": "Export model animations to use on Anima script",
	"warning":     "",
	"wiki_url":    "",
	"tracker_url": "",
	"category":    "Space Engineers",
}

import os
import bpy
import time
from mathutils import Matrix
from bpy_extras.io_utils import ExportHelper

anima_export_txt = "SE Anima Sequence (.cs)"

class ExportSEAnima(bpy.types.Operator, ExportHelper):
	bl_idname = "export_scene.spaceeng_anima"
	bl_label = "Export"
	bl_description = "Exports animations keys"
	bl_options = {'PRESET'}

	filename_ext = ".cs"
	filter_glob = bpy.props.StringProperty(default="*.cs", options={'HIDDEN'})
	namespace = bpy.props.StringProperty(name="Namespace", description="Exporting namespace for animation", default="AnimaData", options={'HIDDEN'})
	classname = bpy.props.StringProperty(name="Class Name", description="Class name, @ character is replaced with object name", default="Seq_@")
	f_start = bpy.props.IntProperty(name="Start", description="Starting frame", default=0, min=0, max=32767, step=1)
	f_end = bpy.props.IntProperty(name="End", description="Ending frame", default=0, min=0, max=32767, step=1)
	f_offset = bpy.props.IntProperty(name="Offset", description="Offset frame on export", default=0, min=-16384, max=16383, step=1)
	f_rate = bpy.props.FloatProperty(name="FPS", description="Frames per second", default=60, min=-600, max=600, step=1)
	exp_position = bpy.props.BoolProperty(name="Export location", description="Export location/position", default=True)
	exp_rotation = bpy.props.BoolProperty(name="Export rotation", description="Export rotation (Quaternion)", default=True)
	exp_scaling = bpy.props.BoolProperty(name="Export scaling", description="Export scaling", default=True)

	def write_frame(self, animdata, obj, frame):
		localmatrix = obj.matrix_local
		# Get transformations
		epsilon = 0.00001
		pos = localmatrix.to_translation()
		if pos.x > -epsilon and pos.x < epsilon:
			pos.x = 0
		if pos.y > -epsilon and pos.y < epsilon:
			pos.y = 0
		if pos.z > -epsilon and pos.z < epsilon:
			pos.z = 0
		rot = localmatrix.to_quaternion()
		if rot.x > -epsilon and rot.x < epsilon:
			rot.x = 0
		if rot.y > -epsilon and rot.y < epsilon:
			rot.y = 0
		if rot.z > -epsilon and rot.z < epsilon:
			rot.z = 0
		if rot.w > -epsilon and rot.w < epsilon:
			rot.w = 0
		scale = localmatrix.to_scale()
		if scale.x > -epsilon and scale.x < epsilon:
			scale.x = 0
		if scale.y > -epsilon and scale.y < epsilon:
			scale.y = 0
		if scale.z > -epsilon and scale.z < epsilon:
			scale.z = 0

		# Write to output
		if       self.exp_position and     self.exp_rotation and     self.exp_scaling:
			animdata.append( "PLocRotScale(%gf,%gf,%gf,%gf,%gf,%gf,%gf,%gf,%gf,%gf)" % (-pos.x, pos.z, pos.y, -rot.x, rot.z, rot.y, rot.w, scale.x, scale.z, scale.y) )
		elif     self.exp_position and not self.exp_rotation and not self.exp_scaling:
			animdata.append( "PLocation(%gf,%gf,%gf)" % (-pos.x, pos.z, pos.y) )
		elif not self.exp_position and     self.exp_rotation and not self.exp_scaling:
			animdata.append( "PRotation(%gf,%gf,%gf,%gf)" % (-rot.x, rot.z, rot.y, rot.w) )
		elif not self.exp_position and not self.exp_rotation and     self.exp_scaling:
			animdata.append( "PScaling(%gf,%gf,%gf)" % (scale.x, scale.z, scale.y) )
		elif     self.exp_position and     self.exp_rotation and not self.exp_scaling:
			animdata.append( "PLocRot(%gf,%gf,%gf,%gf,%gf,%gf,%gf)" % (-pos.x, pos.z, pos.y, -rot.x, rot.z, rot.y, rot.w) )
		elif     self.exp_position and not self.exp_rotation and     self.exp_scaling:
			animdata.append( "PLocScale(%gf,%gf,%gf,%gf,%gf,%gf)" % (-pos.x, pos.z, pos.y, scale.x, scale.z, scale.y) )
		elif not self.exp_position and     self.exp_rotation and     self.exp_scaling:
			animdata.append( "PRotScale(%gf,%gf,%gf,%gf,%gf,%gf,%gf)" % (-rot.x, rot.z, rot.y, rot.w, scale.x, scale.z, scale.y) )
		elif not self.exp_position and not self.exp_rotation and not self.exp_scaling:
			animdata.append( "PNone();\n" )
		return True

	def export_sequence(self, out, obj, class_name):
		g_start = self.f_start
		g_end = self.f_end
		g_offset = self.f_offset
		# Gather animation
		animdata = []
		curr_frame = bpy.context.scene.frame_current
		for frame in range(self.f_start, self.f_end + 1):
			bpy.context.scene.frame_set(frame)
			self.write_frame(animdata, obj, frame)
		bpy.context.scene.frame_set(curr_frame)
		# Data optimization, get top
		k_start = g_start
		thisframe = animdata[k_start - g_start]
		for frame in range(g_start + 1, g_end + 1):
			otherframe = animdata[frame - g_start]
			if thisframe != otherframe:
				break
			k_start = k_start + 1
		# Data optimization, get bottom
		k_end = g_end
		thisframe = animdata[k_end - g_start]
		for frame in range(g_end - 1, g_start - 1, -1):
			otherframe = animdata[frame - g_start]
			if thisframe != otherframe:
				break
			k_end = k_end - 1
		# Happens when the object is static
		if k_end < k_start:
			k_start = g_start
			k_end = g_start
		# Write class
		out.write( "    public class %s : AnimaSeqBase\n" % class_name )
		out.write( "    {\n" )
		out.write( "        private static AnimaSeqBase m_seq = null;\n" )
		out.write( "        new public static AnimaSeqBase Adquire()\n" )
		out.write( "        {\n" )
		out.write( "            if (m_seq == null) m_seq = PManAdd(new %s());\n" % class_name )
		out.write( "            return m_seq;\n" )
		out.write( "        }\n" )
		out.write( "        public override void DiscardStatic() { m_seq = PManRem(m_seq); }\n" )
		out.write( "        new public static void Discard() { m_seq = PManRem(m_seq); }\n" )
		out.write( "        protected override void PData()\n" )
		out.write( "        {\n" )
		out.write( "            PInit(\"%s\",%i,%i,%gf,%i,%i);\n" % (class_name, g_start + g_offset, g_end + g_offset, self.f_rate, k_start + g_offset, k_end + g_offset) )
		for frame in range(k_start, k_end + 1):
			out.write( "            %s;\n" % animdata[frame - g_start] )
		out.write( "        }\n" )
		out.write( "    }\n" )
		return True

	def get_objs_selection(self):
		# Enumerate all valid objects
		mobjects = []
		for obj in bpy.context.selected_objects:
			if obj.type == 'MESH':
				mobjects.append(obj)

		# Error if there isn't a valid object in selection
		obj = bpy.context.active_object
		if len(mobjects) == 0:
			self.report({'ERROR'}, "No valid object selected!")
			return None

		return mobjects

	def invoke(self, context, event):
		# Get selection
		if not self.get_objs_selection():
			return {'CANCELLED'}

		# Get timeline info
		self.f_start = bpy.context.scene.frame_start
		self.f_end = bpy.context.scene.frame_end
		self.f_rate = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base

		# Run if all fine!
		context.window_manager.fileselect_add(self)
		return {'RUNNING_MODAL'}

	def execute(self, context):
		# Get selection
		mobjects = self.get_objs_selection()
		if not mobjects:
			return {'CANCELLED'}

		# Force 'Object mode'
		bpy.ops.object.mode_set(mode='OBJECT')
		time_start = time.time()

		# Print timeline info
		if self.f_end < self.f_start:
			self.f_end = self.f_start
		if self.f_rate < 0:
			self.f_rate = 0
		print("Animation from %i to %i @ %.2g fps" % (self.f_start, self.f_end, self.f_rate))

		# Opening output file and write header
		out = open(self.filepath, 'w')
		out.write( "using AnimaScript;\n" )
		out.write( "\nnamespace %s\n{\n" % self.namespace )

		for obj in mobjects:
			seqname = self.classname.replace("@", obj.name)
			self.export_sequence(out, obj, seqname)

		# Done writing
		out.write( "}\n" )
		out.close()

		print( "Exported objects: %i" % len(mobjects) )
		print( "Export time: %.4f sec" % (time.time() - time_start) )
		self.report({'WARNING'}, "Success!\nExported objects: %i" % len(mobjects) )

		return {'FINISHED'}

def menu_func_export(self, context):
	self.layout.operator(ExportSEAnima.bl_idname, text=anima_export_txt)

def register():
	bpy.utils.register_class(ExportSEAnima)
	bpy.types.INFO_MT_file_export.append(menu_func_export)

def unregister():
	bpy.utils.unregister_class(ExportSEAnima)
	bpy.types.INFO_MT_file_export.remove(menu_func_export)

if __name__ == "__main__":
	register()
