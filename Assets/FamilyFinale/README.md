# Chapter 2 finale

Episode 4 — The Weight Behind the Worry: examination-paper reflection, three worry choices, family dinner with two responses, and return to the bedroom.

Episode 5 — What I Left Behind: examine the certificate, drawing sketchbook and notebook in any order, choose a reflection, then go to bed for the unresolved nighttime-thought sequence. The photograph is scenery, not a fourth objective.

Open `Assets/Scenes/Chapter 2 - Episode 4.unity` or `Chapter 2 - Episode 5.unity`, or select them in the main menu. Movement/look use the existing player controls; E interacts, Space advances dialogue, number keys select choices, and Esc pauses. Chapter completion does not launch the unbuilt Chapter 3.

The scenes reuse the house and player from Episode 1 and family FBX assets. `FamilyFinaleBuilder` generates both scenes, loading artwork, and build entries. Running its menu action replaces the generated scenes; save custom scene edits elsewhere first. It does not save changes to the source Episode 1 scene.

`FamilyFinaleVerification.Run()` in Play mode exercises all three worry/reflection branches, both dinner responses, different keepsake orders, interaction proximity and completion saves. It temporarily suppresses SQLite writes and restores affected PlayerPrefs. Results are written to `Temp/FamilyFinaleVerification.txt`. This is story-flow coverage, not a substitute for a manual controller/collision playthrough or platform build.
