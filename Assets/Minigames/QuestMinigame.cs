using UnityEngine;

// Anything the Quest can open and wait on (breathing exercise, Worry Courtroom, ...).
// Implement Open/Close, call Quest.NotifyMinigameCompleted(this) when the player finishes,
// and Quest.NotifyMinigameClosed(this) if they leave early so it can be opened again.
public abstract class QuestMinigame : MonoBehaviour
{
    public abstract bool IsOpen { get; }
    public abstract void Open(Quest quest);
    public abstract void Close();
    [Tooltip("Name shown in the quest list. Leave blank to use the GameObject's name.")]
    [SerializeField] string questDisplayName = "";
    public virtual string QuestDisplayName => string.IsNullOrWhiteSpace(questDisplayName) ? name : questDisplayName;
}
