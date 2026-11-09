import bpy, math
from mathutils import Vector
from pathlib import Path
root=Path('/Users/arieljerickogacilo/mindscape-3d')
bpy.ops.wm.read_factory_settings(use_empty=True)
for x,name in [(0,'BaldCharacter')]:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(root/'Assets/Characters/BaldCharacter'/f'{name}.glb'))
    obs=set(bpy.data.objects)-before
    for ob in obs:
        if ob.type=='ARMATURE':
            ob.animation_data_clear()
            for b in ob.pose.bones:
                b.location=(0,0,0); b.rotation_mode='QUATERNION'; b.rotation_quaternion=(1,0,0,0)
    bpy.context.view_layer.update()
    verts=[o.matrix_world@Vector(c) for o in obs if o.type=='MESH' for c in o.bound_box]
    lo=Vector(tuple(min(v[i] for v in verts) for i in range(3))); hi=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
    scale=2.5/(hi.z-lo.z)
    parent=bpy.data.objects.new(name+'_layout',None); bpy.context.collection.objects.link(parent)
    for ob in obs:
        if ob.parent not in obs: ob.parent=parent
    parent.scale=(scale,)*3; parent.location=(x-scale*(hi.x+lo.x)/2,-scale*(hi.y+lo.y)/2,-scale*lo.z)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object
mat=bpy.data.materials.new('neutral'); mat.diffuse_color=(.15,.18,.20,1);floor.data.materials.append(mat)
bpy.ops.object.camera_add(location=(3,-10,3.2)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,1.3))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=3.4
s=bpy.context.scene;s.camera=cam
for loc,power,size in [((-3,-4,6),900,5),((4,-1,4),700,4),((0,3,5),900,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1.3))-o.location).to_track_quat('-Z','Y').to_euler()
s.world=bpy.data.worlds.new('World');s.world.color=(.3,.3,.3)
s.render.engine='CYCLES';s.cycles.samples=24;s.render.resolution_x=800;s.render.resolution_y=800;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.render.filepath=str(root/'output/imagegen/player-current-reference.png');bpy.ops.render.render(write_still=True)
