using System.Linq;
using Mindscape.EpisodeOne;
using Mindscape.EpisodeTwo;
using Mindscape.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FamilyEpisodeTwoBuilder
{
    const string Path="Assets/Scenes/"+EpisodeTwoStory.SceneName+".unity";
    static Transform root;
    static Material cream, teal, dark, gold, glass, road, green, wood;    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 2/Build Before the Bell")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        if(SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save the open scene before building.");
        var source=EditorSceneManager.OpenScene("Assets/Scenes/"+EpisodeOneStory.SceneName+".unity");
        var first=Object.FindAnyObjectByType<EpisodeOneStory>(); first.nextEpisodeScene=EpisodeTwoStory.SceneName;
        EditorSceneManager.MarkSceneDirty(source); EditorSceneManager.SaveScene(source);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        foreach(var obj in source.GetRootGameObjects()) if(new[]{"Main Camera","Directional Light","Global Volume","EventSystem","House And Lot","Player"}.Contains(obj.name))
        { var clone=Object.Instantiate(obj); clone.name=obj.name; SceneManager.MoveGameObjectToScene(clone,scene); }
        root=new GameObject("Episode 2 - Before the Bell").transform;
        var original=source.GetRootGameObjects().First(x=>x.name=="Episode 1 - Family Expectations");
        foreach(Transform prop in original.transform) if(new[]{"Study desk","Desk leg","Desk lamp base","Evening desk light","Bedside alarm clock","Study notebook interaction"}.Contains(prop.name))
        { var clone=Object.Instantiate(prop.gameObject,root); clone.name=prop.name; clone.transform.SetPositionAndRotation(prop.position,prop.rotation); }
        EditorSceneManager.CloseScene(source,true);
        cream=Mat("Ivory",new Color32(233,222,195,255)); teal=Mat("Harbor teal",new Color32(44,102,105,255)); dark=Mat("Charcoal",new Color32(35,47,52,255)); gold=Mat("Warm gold",new Color32(219,170,88,255)); glass=Mat("Window blue",new Color32(105,153,169,255)); road=Mat("Asphalt",new Color32(77,84,85,255)); green=Mat("Sage",new Color32(111,138,100,255)); wood=Mat("Cedar",new Color32(139,99,65,255));
        var story=root.gameObject.AddComponent<EpisodeTwoStory>();
        story.player=GameObject.Find("Player").transform; story.storyCamera=GameObject.Find("Main Camera").GetComponent<Camera>();
        story.sun=GameObject.Find("Directional Light").GetComponent<Light>(); story.sun.intensity=1.3f; story.sun.color=new Color(1,.91f,.77f); story.sun.transform.rotation=Quaternion.Euler(42,-35,0);
        story.deskLamp=root.Find("Evening desk light").GetComponent<Light>(); story.deskLamp.intensity=1;
        story.alarmClock=root.Find("Bedside alarm clock");
        Label("07:50",new Vector3(-13.2f,2.02f,-14.261f),Quaternion.Euler(0,180,0),.43f,.21f,1.1f,new Color(1,.85f,.5f),root);
        story.player.position=new Vector3(-16.8f,.65f,-11);
        var movement=story.player.GetComponent<PlayerMovement>(); movement.cameraTransform=story.storyCamera.transform; movement.enabled=true;
        story.player.GetComponent<PlayerControls>().dialogueOpen=false;
        foreach(var t in GameObject.Find("House And Lot").GetComponentsInChildren<Transform>(true))
            if(t.name.StartsWith("Pedestrian gate |")) t.gameObject.SetActive(false);
        DoorSetup.LeaveOpen(GameObject.Find("House And Lot").transform,"Door_Bedroom_Left","Door_Bedroom_Right");
        story.exit=Pivot("Front door interaction",new Vector3(-22.1f,2,5));
        story.studyView=Pivot("Late study camera",new Vector3(-17.8f,3.9f,-10.5f)); story.studyView.LookAt(new Vector3(-14.8f,2.5f,-6.6f));
        var id=Pivot("Student ID",new Vector3(-17.5f,2.45f,-13.4f));
        Box("ID card",id.position,new Vector3(.58f,.035f,.8f),cream,id); Box("Portrait",id.position+new Vector3(0,.025f,-.17f),new Vector3(.28f,.015f,.25f),teal,id);
        for(int i=0;i<3;i++) Box("Printed line",id.position+new Vector3(0,.026f,.06f+i*.08f),new Vector3(.35f,.012f,.016f),dark,id);
        var kit=Pivot("Examination materials",new Vector3(-15.25f,2.43f,-6.7f));
        Box("Review sheets",kit.position,new Vector3(.7f,.07f,.9f),cream,kit); Box("Pencil case",kit.position+new Vector3(.18f,.14f,.12f),new Vector3(.25f,.16f,.7f),teal,kit); Box("Pencil",kit.position+new Vector3(-.24f,.06f,0),new Vector3(.035f,.035f,.7f),gold,kit);
        var bag=MakeBag("School bag",new Vector3(-24,1.3f,2),root);
        story.items=new[]{id,kit,bag};
        foreach(var item in story.items) { Box("Objective glint",item.position+Vector3.up*.65f,Vector3.one*.13f,gold,item,false); CollectibleModels.ApplyTo(item); PrefabUtility.SaveAsPrefabAsset(item.gameObject,"Assets/EpisodeTwo/Prefabs/"+item.name+".prefab"); }
        story.carriedBag=MakeBag("Carried school bag",Vector3.zero,root).gameObject; story.carriedBag.transform.SetParent(story.player,false); story.carriedBag.transform.localPosition=new Vector3(0,1.75f,-.35f); story.carriedBag.transform.localScale=Vector3.one*.8f; story.carriedBag.SetActive(false);
        story.phoneModel=Box("Phone",Vector3.zero,new Vector3(.28f,.5f,.04f),dark,root,false); story.phoneModel.transform.SetParent(story.player,false); story.phoneModel.transform.localPosition=new Vector3(.5f,1.8f,.6f); CollectibleModels.ApplyTo(story.phoneModel.transform); PrefabUtility.SaveAsPrefabAsset(story.phoneModel,"Assets/EpisodeTwo/Prefabs/Phone.prefab"); story.phoneModel.SetActive(false);
        BuildStreet(story);
        RenderSettings.ambientLight=new Color(.62f,.66f,.7f); RenderSettings.fog=true; RenderSettings.fogColor=new Color(.72f,.79f,.80f); RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.007f;
        story.storyCamera.farClipPlane=200; story.storyCamera.transform.position=new Vector3(-17.9f,4.1f,-11.5f); story.storyCamera.transform.LookAt(story.alarmClock.position);
        BedroomLayout.Apply(scene); // same clear-doorway bedroom as Episode 1
        CollectibleModels.Apply(scene);
        EditorSceneManager.SaveScene(scene,Path);
        var scenes=EditorBuildSettings.scenes.ToList(); if(!scenes.Any(x=>x.path==Path)) scenes.Add(new EditorBuildSettingsScene(Path,true)); else scenes.First(x=>x.path==Path).enabled=true; EditorBuildSettings.scenes=scenes.ToArray();
        // Add the playable episode and return destinations to custom scene-list profiles without changing their platform or entry scene.
        foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if(profile==null || !profile.overrideGlobalScenes) continue;
            var list=profile.scenes.Select(x=>new EditorBuildSettingsScene(x.path,x.enabled)).ToList();
            foreach(var entry in scenes.Where(x=>x.enabled))
            { if(!list.Any(x=>x.path==entry.path)) list.Add(new EditorBuildSettingsScene(entry.path,true)); }
            profile.scenes=new EditorBuildSettingsScene[0]; profile.scenes=list.ToArray(); EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Episode 2 built: "+Path);
    }
    static void BuildStreet(EpisodeTwoStory story)
    {
        // The street, sidewalks, houses across the road, streetlights and bus shelter are part of the house model.
        HouseModelSync.AddMissingParts(GameObject.Find("House And Lot").transform);
        var street=Pivot("Neighborhood",Vector3.zero);
        // Invisible limits keep the short playable route distinct from decorative streets.
        Boundary("Road boundary",new Vector3(0,3,25.2f),new Vector3(130,6,.2f),street);
        Boundary("West route end",new Vector3(-28,3,22.5f),new Vector3(.2f,6,5),street);
        Boundary("East route end",new Vector3(31,3,22.5f),new Vector3(.2f,6,5),street);
        Label("BUS\n02 / SCHOOL",new Vector3(27,3.7f,22.67f),Quaternion.Euler(0,180,0),1.6f,.95f,3,gold.color,root);
        story.stop=Pivot("Wait here",new Vector3(24,1.6f,23)); story.boardingPoint=Pivot("Board bus",new Vector3(26.2f,1.5f,24.5f));
        story.routeMarkers=Pivot("Route markers",Vector3.zero).gameObject;
        foreach(var at in new[]{new Vector3(-22,.23f,21.5f),new Vector3(-13,.23f,22.5f),new Vector3(-3,.23f,22.5f),new Vector3(7,.23f,22.5f),new Vector3(17,.23f,22.5f),new Vector3(24,.23f,23)})
        { var mark=Box("Gold route tile",at,new Vector3(.7f,.025f,.7f),gold,story.routeMarkers.transform,false); mark.transform.rotation=Quaternion.Euler(0,45,0); }
        var bus=Pivot("School route commuter bus",new Vector3(24,0,28));
        Box("Bus lower body",new Vector3(24,1.25f,28),new Vector3(9,1.4f,3.2f),teal,bus);
        Box("Bus upper body",new Vector3(24,2.7f,28),new Vector3(9,1.5f,3.2f),cream,bus);
        Box("Bus roof",new Vector3(24,3.5f,28),new Vector3(9.1f,.18f,3.3f),teal,bus);
        foreach(float z in new[]{26.37f,29.63f})
        { for(int i=0;i<5;i++) Box("Passenger window",new Vector3(20.4f+i*1.55f,2.7f,z),new Vector3(1.25f,1.05f,.035f),glass,bus,false); Box("Gold stripe",new Vector3(24,1.75f,z),new Vector3(9,.12f,.045f),gold,bus,false); }
        Box("Windshield",new Vector3(28.52f,2.7f,28),new Vector3(.04f,1.2f,2.8f),glass,bus,false);
        Box("Door recess",new Vector3(26.4f,1.7f,26.33f),new Vector3(1.3f,2.6f,.07f),dark,bus,false);
        Box("Boarding step",new Vector3(26.4f,.45f,26.1f),new Vector3(1.3f,.15f,.5f),gold,bus,false);
        foreach(float x in new[]{21.2f,26.7f}) foreach(float z in new[]{26.4f,29.6f})
        { var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name="Wheel"; wheel.transform.SetParent(bus); wheel.transform.position=new Vector3(x,.75f,z); wheel.transform.rotation=Quaternion.Euler(90,0,0); wheel.transform.localScale=new Vector3(1.4f,.22f,1.4f); wheel.GetComponent<Renderer>().sharedMaterial=dark; Object.DestroyImmediate(wheel.GetComponent<Collider>()); }
        foreach(float z in new[]{26.85f,29.15f}) Box("Headlight",new Vector3(28.54f,1.2f,z),new Vector3(.05f,.3f,.5f),gold,bus,false);
        Label("02   SCHOOL",new Vector3(24,3.3f,26.33f),Quaternion.identity,5,.3f,2.2f,dark.color,bus);
        PrefabUtility.SaveAsPrefabAsset(bus.gameObject,"Assets/EpisodeTwo/Prefabs/Commuter bus.prefab"); story.bus=bus; bus.gameObject.SetActive(false);
    }
    static Transform MakeBag(string name,Vector3 at,Transform parent)
    { var p=Pivot(name,at,parent); Box("Canvas",at,new Vector3(.85f,1.2f,.48f),teal,p,false); Box("Pocket",at+new Vector3(0,-.1f,-.28f),new Vector3(.66f,.45f,.13f),dark,p,false); Box("Handle",at+Vector3.up*.68f,new Vector3(.35f,.1f,.14f),gold,p,false); return p; }
    static void Label(string words,Vector3 at,Quaternion rotation,float width,float height,float size,Color color,Transform parent)
    { var go=new GameObject(words,typeof(TextMeshPro)); go.transform.SetParent(parent); go.transform.SetPositionAndRotation(at,rotation); var label=go.GetComponent<TextMeshPro>(); label.font=MinigameUI.Font(false); label.text=words; label.fontSize=size; label.alignment=TextAlignmentOptions.Center; label.color=color; label.rectTransform.sizeDelta=new Vector2(width,height); }
    static void Boundary(string name,Vector3 at,Vector3 size,Transform parent)
    { var p=Pivot(name,at,parent); p.gameObject.AddComponent<BoxCollider>().size=size; }
    static Transform Pivot(string name,Vector3 at,Transform parent=null)
    { var p=new GameObject(name).transform; p.SetParent(parent==null?root:parent); p.position=at; return p; }
    static GameObject Box(string name,Vector3 at,Vector3 size,Material mat,Transform parent=null,bool collider=true)
    { var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent==null?root:parent); o.transform.position=at; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=mat; if(!collider) Object.DestroyImmediate(o.GetComponent<Collider>()); return o; }
    static Material Mat(string name,Color color)
    { foreach(var dir in new[]{"Assets/EpisodeTwo/Materials","Assets/EpisodeTwo/Prefabs","Assets/Resources/EpisodeTwo"}) System.IO.Directory.CreateDirectory(dir); var path="Assets/EpisodeTwo/Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); } m.color=color; m.SetFloat("_Smoothness",.15f); return m; }
}
