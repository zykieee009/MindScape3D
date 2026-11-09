# Mindscape change and bug list

This is a planning list, not an implementation record. Items 1-7 come from the direct request. Items 8-16 are suggestions or bug reports in the attached screenshots and `sirdavelist.pdf`; their wording does not override the direct request.

| # | Type | Area | Change or bug to fix | Done when |
|---|---|---|---|---|
| 1 | Change | Before main menu | Show a brief heads-up / content warning before the main menu. | The warning appears before first play and explains the game's potentially stressful themes in clear language; the player can continue to the menu. |
| 2 | Change | Registration | Present a data privacy notice during account creation. | Players can read what data is collected, why, where it is stored, and how it is handled before completing registration. |
| 3 | Change | Introduction / onboarding | State the game's objectives, what players can expect, and possible benefits during or after play. | Players see concise, realistic objectives before starting; wording does not promise a clinical outcome. |
| 4 | Change | Courtroom | After the third strike, automatically open the breathing exercise with a 3-5 second countdown first. | The third strike triggers one countdown and then the exercise; repeat strikes cannot open duplicate popups. |
| 5 | Change | Final assessment | Add an explicit consent checkbox and data privacy notice before the assessment. | The checkbox starts unchecked; the assessment runs only after affirmative consent, and declining has a clear path that does not silently record answers. |
| 6 | Bug | Journal tab | Fix journal entries appearing out of chapter / episode order and any errors when opening the tab. | Opening the tab works reliably; entries display in chapter and episode sequence with clear numbering. |
| 7 | Bug | Main menu, offline mode | Hide the Continue button while offline, as requested. | Switching to offline mode removes Continue from the main menu; returning online restores it only when appropriate for that account. |
| 8 | Bug | NPC dialogue (screenshot 1) | NPCs sometimes address the player with a different name from the one entered. | Every dialogue line that uses the player's name resolves to the active profile / save name, including after switching profiles or modes. |
| 9 | Bug | Hotline information UI (screenshot 2) | An emoji or icon renders as an unreadable missing glyph beside “Official hotline information.” | Replace it with a supported icon or plain text; the label remains readable on supported platforms. |
| 10 | Change | Minigame Lab (screenshot 3) | Remove the separate minigame menu and its entry point, per the attached feedback. | The menu is no longer accessible from the main menu, while breathing exercises required by the story still work. |
| 11 | Change | Login (PDF) | Add a show / hide password control. | Players can toggle visibility while entering a password; it starts hidden. |
| 12 | Bug | New install / account switching (PDF) | Prevent a previous player's name or progress from carrying into a new player's session, especially when switching from offline play to a newly created online account or installing on another device. | A new profile starts with its own name prompt and save state; existing offline progress is associated or migrated only through an explicit choice. |
| 13 | Change | Dialogue and UI copy (PDF) | Review grammar, spelling, and clarity across dialogue and interface text. | Reported lines are corrected and a complete copy pass is performed. |
| 14 | Change | Assessments after chapters / episodes (PDF) | Check assessment timing and prompts so questions about feelings appear after relevant events and describe the intended time frame. | Questions appear at the appropriate point and do not assume feelings have changed before the player has experienced the relevant scene. |
| 15 | Policy / implementation decision | Account deletion and data retention (PDF) | Define and implement the retention and recovery behavior for deleted accounts and archived data. The PDF mentions a possible two-year period, but that duration needs an approved policy before implementation. | The app's deletion flow, privacy notice, and backend behavior match an agreed retention policy; users are told what is deleted or retained. |
| 16 | Bug | In-game camera (PDF) | Lower or adjust the camera to show the player character better and prevent walls from blocking the view. | The character remains visible in normal play and the camera recovers cleanly in narrow corridors or against walls. |

## Suggested order

1. Privacy and consent: 2, 5, 15.
2. Progress and player identity: 7, 8, 12.
3. Story flow and assessment: 1, 3, 4, 14.
4. Journal, interface, and presentation: 6, 9, 10, 11, 13, 16.

## Decisions to settle before implementation

- Items 7 and 12: how offline players resume a local save if Continue is hidden in offline mode. The direct request calls for hiding it, while the PDF describes offline progress being saved.
- Item 10: whether the minigame menu should be removed from all builds or only the player-facing main menu.
- Item 15: the approved retention period and account recovery rules.
