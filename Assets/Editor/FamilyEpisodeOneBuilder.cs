using System.Linq;
using Mindscape.EpisodeOne;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FamilyEpisodeOneBuilder
{
    const string Episode = "Assets/Scenes/" + EpisodeOneStory.SceneName + ".unity";
    const string Folder = "Assets/EpisodeOne/Materials";
    static Transform root;
    static Material wood, brass, paper, teal, navy, charcoal;
    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Build Family Expectations Gameplay")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode before building.");
        var scene = EditorSceneManager.OpenScene(Episode);
        // Preserve the original prototype objects, including all their serialized configuration.
        string[] legacy = { "NPC System", "Quest - Chapter 1 Episode 1", "UI_Toolkit_QuestHUD", "PauseMenu", "collectibles", "Breathing Minigame", "UI_Toolkit_StressHUD", "Mobile Controls", "NPC Waypoints", "Worry Courtroom" };
        foreach (var go in scene.GetRootGameObjects()) if (legacy.Contains(go.name)) go.SetActive(false);
        var previous = scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Episode 1 - Family Expectations");
        if(previous != null) Object.DestroyImmediate(previous);
        root = new GameObject("Episode 1 - Family Expectations").transform;
        wood=Mat("Warm walnut",new Color32(123,83,54,255)); brass=Mat("Soft brass",new Color32(224,176,99,255)); paper=Mat("Exam paper",new Color32(242,230,203,255));
        teal=Mat("Notebook teal",new Color32(51,100,102,255)); navy=Mat("Canvas school bag",new Color32(48,65,81,255)); charcoal=Mat("Graphite",new Color32(41,43,44,255));
        // Desk occupies the clear front corner of the existing west bedroom.
        Cube("Study desk",new Vector3(-16.5f,2.3f,-6.7f),new Vector3(3.3f,.18f,1.25f),wood);
        foreach(float x in new[]{-17.9f,-15.1f}) foreach(float z in new[]{-7.15f,-6.25f}) Cube("Desk leg",new Vector3(x,1.42f,z),new Vector3(.13f,1.75f,.13f),wood);
        var exam=Cube("Examination paper",new Vector3(-17.3f,2.41f,-6.8f),new Vector3(.65f,.025f,.85f),paper);
        for(int i=0;i<7;i++) Cube("Answer line",new Vector3(-17.3f,2.426f,-6.55f-i*.075f),new Vector3(i==0?.45f:.50f,.006f,.009f),charcoal,exam.transform);
        var notebook=Cube("Study notebook",new Vector3(-15.65f,2.46f,-6.75f),new Vector3(.78f,.12f,1.0f),teal);
        Cube("Notebook pages",new Vector3(-15.63f,2.46f,-6.76f),new Vector3(.71f,.065f,.95f),paper,notebook.transform);
        Cube("Notebook elastic",new Vector3(-15.41f,2.525f,-6.75f),new Vector3(.035f,.009f,1f),brass,notebook.transform);
        var bag=Cube("School bag memory",new Vector3(-17.5f,1.2f,-9f),new Vector3(.85f,1.2f,.48f),navy);
        Cube("Front pocket",new Vector3(-17.5f,1.1f,-9.28f),new Vector3(.66f,.45f,.13f),teal,bag.transform);
        Cube("Bag handle",new Vector3(-17.5f,1.88f,-9),new Vector3(.35f,.10f,.14f),brass,bag.transform);
        for(int i=0;i<2;i++) Cube("Shoulder strap",new Vector3(-17.76f+i*.52f,1.28f,-8.73f),new Vector3(.09f,.86f,.08f),charcoal,bag.transform);
        var lamp=Cube("Desk lamp base",new Vector3(-16.55f,2.43f,-6.3f),new Vector3(.4f,.08f,.3f),brass);
        Cube("Desk lamp stem",new Vector3(-16.55f,2.85f,-6.3f),new Vector3(.07f,.80f,.07f),brass,lamp.transform);
        Cube("Desk lamp shade",new Vector3(-16.55f,3.24f,-6.3f),new Vector3(.55f,.19f,.40f),teal,lamp.transform);
        var light=new GameObject("Evening desk light",typeof(Light)).GetComponent<Light>(); light.transform.SetParent(root); light.transform.position=new Vector3(-16.5f,3.1f,-6.6f); light.type=LightType.Point; light.color=new Color(1,.77f,.43f); light.intensity=2; light.range=6;
        var clock=Cube("Bedside alarm clock",new Vector3(-13.2f,2.02f,-14.4f),new Vector3(.55f,.32f,.2f),navy);
        Cube("Clock face",new Vector3(-13.2f,2.02f,-14.28f),new Vector3(.44f,.23f,.02f),brass,clock.transform);
        var objects=new[]{exam.transform,bag.transform,notebook.transform};
        foreach(var obj in objects)
        {
            var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name="Memory marker"; marker.transform.SetParent(obj,false);
            // Account for scaled primitive parent so the marker remains a small world-space glint.
            marker.transform.SetParent(root,true); marker.transform.localScale=Vector3.one*.12f; marker.GetComponent<Renderer>().sharedMaterial=brass; Object.DestroyImmediate(marker.GetComponent<Collider>());
            // Put each prop beneath an unscaled pivot, so offsets and highlights are meaningful.
            var pivot=new GameObject(obj.name+" interaction").transform; pivot.SetParent(root); pivot.position=obj.position; obj.SetParent(pivot,true); marker.transform.SetParent(pivot,true); marker.transform.localPosition=Vector3.up*.7f;
        }
        var p=GameObject.Find("Player"); var movement=p.GetComponent<PlayerMovement>();
        p.transform.SetPositionAndRotation(BedroomLayout.PlayerStart,Quaternion.Euler(0,BedroomLayout.PlayerStartYaw,0));
        var controls=p.GetComponent<PlayerControls>(); controls.enabled=false; controls.tasksOpen=controls.pauseOpen=controls.dialogueOpen=controls.questOpen=controls.minigameOpen=false;
        foreach(var c in p.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
        movement.moveSpeed=2.5f; movement.runSpeed=3.5f; movement.cameraTargetHeight=1.8f; movement.cameraOffset=new Vector3(0,.7f,-2.5f); movement.minDistance=1.2f; movement.maxDistance=3.8f; movement.cameraShoulderOffset=.35f;
        var controller=root.gameObject.AddComponent<EpisodeOneStory>(); controller.player=p.transform; controller.storyCamera=Camera.main; controller.memoryObjects=objects.Select(x=>x.parent).ToArray(); controller.deskLight=light;
        var camera=Camera.main; camera.transform.position=new Vector3(-16.7f,3.5f,-11.3f); camera.transform.LookAt(new Vector3(-14.8f,2,-6.6f)); camera.fieldOfView=58;
        var sun=GameObject.Find("Directional Light").GetComponent<Light>(); sun.color=new Color(1,.84f,.66f); sun.intensity=1.05f; sun.transform.rotation=Quaternion.Euler(35,-35,0);
        controller.sunlight=sun; controller.nextEpisodeScene=Mindscape.EpisodeTwo.EpisodeTwoStory.SceneName;
        // Brief examination tableau: a remembered room, without introducing Episode 3 gameplay.
        var stage=new GameObject("Examination memory tableau").transform; stage.SetParent(root);
        Cube("Classroom floor",new Vector3(100,-.15f,0),new Vector3(14,.3f,14),wood,stage);
        Cube("Classroom back wall",new Vector3(100,3,4),new Vector3(14,6,.2f),paper,stage);
        Cube("Classroom left wall",new Vector3(93,3,-1.5f),new Vector3(.2f,6,11),paper,stage);
        Cube("Classroom right wall",new Vector3(107,3,-1.5f),new Vector3(.2f,6,11),paper,stage);
        Cube("Classroom ceiling",new Vector3(100,6,-1.5f),new Vector3(14,.2f,11),paper,stage);
        Cube("Board frame",new Vector3(100,3.4f,3.90f),new Vector3(5.2f,2.2f,.08f),wood,stage);
        Cube("Chalkboard",new Vector3(100,3.4f,3.85f),new Vector3(5,2,.08f),teal,stage);
        for(int row=0;row<2;row++) for(int col=0;col<3;col++)
        {
            Vector3 at=new Vector3(97+col*3,1.4f,1-row*3);
            Cube("Examination desk",at,new Vector3(1.9f,.16f,1.2f),wood,stage);
            Cube("Answer sheet",at+new Vector3(0,.1f,0),new Vector3(.65f,.02f,.8f),paper,stage);
            Cube("Pencil",at+new Vector3(.45f,.13f,0),new Vector3(.04f,.04f,.5f),brass,stage);
            foreach(float dx in new[]{-.8f,.8f}) Cube("Desk support",at+new Vector3(dx,-.7f,0),new Vector3(.1f,1.3f,.8f),charcoal,stage);
            Cube("Empty chair",at+new Vector3(0,-.55f,-1),new Vector3(.8f,.14f,.75f),teal,stage);
            Cube("Chair back",at+new Vector3(0,.1f,-1.35f),new Vector3(.8f,1.2f,.1f),teal,stage);
            foreach(float dx in new[]{-.3f,.3f}) Cube("Chair support",at+new Vector3(dx,-1.0f,-1),new Vector3(.08f,.7f,.6f),charcoal,stage);
        }
        var face=GameObject.CreatePrimitive(PrimitiveType.Cylinder); face.name="Examination clock"; face.transform.SetParent(stage); face.transform.position=new Vector3(103.8f,4.3f,3.75f); face.transform.rotation=Quaternion.Euler(90,0,0); face.transform.localScale=new Vector3(1.1f,.06f,1.1f); face.GetComponent<Renderer>().sharedMaterial=paper;
        Cube("Clock hour hand",new Vector3(103.8f,4.43f,3.66f),new Vector3(.04f,.28f,.02f),charcoal,stage);
        Cube("Clock minute hand",new Vector3(103.96f,4.3f,3.65f),new Vector3(.35f,.035f,.02f),charcoal,stage);
        var memoryLight=new GameObject("Classroom soft light",typeof(Light)).GetComponent<Light>(); memoryLight.transform.SetParent(stage); memoryLight.transform.position=new Vector3(100,5,-1); memoryLight.type=LightType.Point; memoryLight.color=new Color(1,.92f,.79f); memoryLight.range=13; memoryLight.intensity=2;
        var memoryView=new GameObject("Examination memory camera mark").transform; memoryView.SetParent(root); memoryView.position=new Vector3(100,3.1f,-5.3f); memoryView.LookAt(new Vector3(100.8f,2.5f,1.3f)); controller.examMemoryView=memoryView;
        var scattered=new GameObject("Morning search fragments"); scattered.transform.SetParent(root); controller.morningItems=scattered;
        Cube("Student ID",new Vector3(-17.8f,.62f,-8.15f),new Vector3(.38f,.02f,.55f),paper,scattered.transform);
        Cube("ID photo",new Vector3(-17.8f,.635f,-8.05f),new Vector3(.20f,.008f,.18f),teal,scattered.transform);
        Cube("Dropped revision notes",new Vector3(-17.5f,.62f,-9.75f),new Vector3(.62f,.02f,.78f),paper,scattered.transform);
        scattered.SetActive(false);
        BedroomLayout.Apply(scene); // clear doorway: wardrobe, desk, chair and bag placed away from the door
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
        RenderSettings.ambientLight=new Color(.60f,.63f,.68f);
        CollectibleModels.Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // Keep the old cutscene scene as an entry point, with its original content preserved inactive.
        var cutscene=EditorSceneManager.OpenScene("Assets/Scenes/Cutscene - Episode 1.unity");
        foreach(var go in cutscene.GetRootGameObjects()) if(go.GetComponent<EpisodeOneEntry>()==null) go.SetActive(false);
        if(Object.FindAnyObjectByType<EpisodeOneEntry>()==null) new GameObject("Episode 1 Entry").AddComponent<EpisodeOneEntry>();
        EditorSceneManager.MarkSceneDirty(cutscene); EditorSceneManager.SaveScene(cutscene);
        EditorSceneManager.OpenScene(Episode);
        AssetDatabase.SaveAssets();
        Debug.Log("Family Expectations Episode 1 built: opening, 3 memories, journal, pause, ending, checkpoint, menu entry.");
    }

    static Material Mat(string name, Color color)
    {
        if(!AssetDatabase.IsValidFolder("Assets/EpisodeOne/Materials")) AssetDatabase.CreateFolder("Assets/EpisodeOne","Materials");
        string path=Folder+"/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
        mat.color=color; mat.SetFloat("_Smoothness",.15f); EditorUtility.SetDirty(mat); return mat;
    }
    static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=position; go.transform.localScale=scale;
        go.transform.SetParent(parent!=null?parent:root,true); go.GetComponent<Renderer>().sharedMaterial=material; return go;
    }
}
