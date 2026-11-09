# Chapter 1 · Academic Pressure

The whole chapter happens around one school deadline: a research project due tomorrow at 11:59 PM. The player character uses the name the player enters at the start of Episode 1 (default "Alex").

Build it with `Mindscape > Chapter 1 - Academic Pressure > Build All Episodes`. That item also moves the Family Expectations episodes to Chapter 2 (once) and orders the build settings: menus, tutorial, Chapter 1, Chapter 2. Each episode also has its own build item. Save unrelated scene work first.

| Episode | Scene | Script | Built from |
|---|---|---|---|
| 1 · Tomorrow Already? (house) | `Chapter 1 - Episode 1` | `HouseEpisode` | a copy of `Chapter 2 - Episode 2` (house, street, bus) |
| 2 · The Announcement (classroom) | `Chapter 1 - Episode 2` | `ClassroomEpisode` | `Models/School_Grounds.glb` (Room 03) |
| 3 · Everyone Seems Ahead (cafeteria) | `Chapter 1 - Episode 3` | `CafeteriaEpisode` | `Models/School_Grounds.glb` (cafeteria) |
| 4 · One Step at a Time (dormitory) | `Chapter 1 - Episode 4` | `DormitoryEpisode` | `Models/School_Grounds.glb` (dormitory) |

Episodes 2-4 share one school grounds model, `Blender Files/school-grounds.blend` → `Assets/Models/School_Grounds.glb`: the street and bus stop outside the gate, the yard with a library and a hall, Rooms 01-04 on a covered corridor (Room 03 is furnished, the others are empty), the cafeteria at one end of the corridor and the dormitory at the other. The builders find everything by part name (`Room 03 | Door_Header`, `Cafeteria | Floor`, ...), so the layout can move in Blender; re-export the GLB and rebuild.

## Play

WASD move, mouse look, Shift run, E interact (gold markers), Space/Enter or Continue to advance, 1–9 or click to choose, Esc pause. There is no timer, score or failure state.

1. **House.** Enter a name. Phone: "Research Project Submission, Tomorrow 11:59 PM". Laptop: decision A "finish everything tonight" / B "start with one task first" (recorded), then thought identification ("What if I fail this project?" → What are you feeling?). Pack books and bag, make the bed, clear and wash the breakfast plate, leave by the front door, follow the markers to the bus stop, ride to school.
2. **Classroom.** The bus stops at the school gate; follow the gold markers across the yard and along the corridor to Room 03 (Shift runs). Walk in, sit, hear the announcement and the classmate who is almost finished. The morning's decision changes one inner line. Talk to Sam (three replies that converge), "feels overwhelmed" → "Take a moment" → the existing breathing exercise → "I feel a little calmer."
3. **Cafeteria.** Start outside Room 03 and follow the corridor to the cafeteria. Two students are ahead; the thought "Everyone is ahead of me, so I'm not good enough" goes to the **Thought Courtroom** (`Assets/Minigames/ThoughtCourtroom`): Evidence FOR, Evidence AGAINST, Balanced Thought (choose or write your own), Thought Examined.
4. **Dormitory.** Sit at the desk, see the unfinished project. Laptop task list replaces "Finish entire project" with four tasks; finish one. Reflection journal (typed, saved), self-reflection assessment (overwhelm 1–5, most useful coping strategy), "Your Reflection" (not a medical diagnosis), ending realization.

Episode 4 ends with "Begin Chapter 2", which opens Family Expectations.

## Database

`Assets/Database/MindscapeDatabase.cs` stores everything in SQLite at `Application.persistentDataPath/mindscape.db` and mirrors it to local storage (PlayerPrefs). `Mindscape > Chapter 1 - Academic Pressure > Reveal Database File` shows the file.

- Tables: `players` (name), `choices` (every decision with chapter, episode, key, value, time), `journal_entries`, `assessments`.
- SQLite comes from the operating system (`libsqlite3` on macOS/Linux, `winsqlite3.dll` on Windows 10+). To pin a version, drop the official `sqlite3.dll` into `Assets/Plugins`; it is loaded first.
- WebGL (the current build target) has no native SQLite, so the same API uses local storage only. If SQLite fails at runtime, the game logs one warning and continues on local storage.
- The menu Journal has a "CH. 1 Reflection Journal" tab that reads and writes the Episode 4 entry.

Choice keys are in `AcademicPressureChapter` (`laptop_plan`, `feeling`, `friend_reply`, `breathing`, `balanced_thought`, `first_task`, plus `evidence_for` / `evidence_against`).
