using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach this script to any object to turn it into a Hay Pile.
/// When the player lands on this object after spawning from the sky,
/// it will cushion their fall and unlock their movement.
/// </summary>
public class HayPile : MonoBehaviour
{
    [Tooltip("If true, completely zeros out the player's downward velocity on impact.")]
    public bool cushionFall = true;

    [Header("Intro Cutscene Settings")]
    [Tooltip("Name of the NPC GameObject to look at when landing. Leave empty to disable the intro cutscene.")]
    public string npcMarkerName = "IntroNPC";
    public string npcSpeakerName = "Farmer";
    [TextArea(2, 5)]
    public string[] dialogueLines = new string[] {
        "Welcome to the game!",
        "Good thing I put this hay here."
    };

    public void OnPlayerLanded(BallController player)
    {
        if (player == null || !player.IsOwner) return;

        // Restore normal camera state so CutscenePlayer can take over
        if (player.CutsceneCameraRef != null)
        {
            player.CutsceneCameraRef.useFixedPosition = false;
            player.CutsceneCameraRef.lookAtTarget = false;
        }

        // Only play the cutscene if an NPC name is provided
        if (string.IsNullOrEmpty(npcMarkerName)) return;

        // Build a dynamic cutscene
        CutsceneData introCutscene = ScriptableObject.CreateInstance<CutsceneData>();
        introCutscene.cutsceneName = "Intro_Greeting_" + player.NetworkObjectId;
        introCutscene.steps = new List<CutsceneStep>();

        introCutscene.steps.Add(new LockPlayerStep());
        
        introCutscene.steps.Add(new CameraTargetStep() { 
            markerName = npcMarkerName, 
            holdDuration = 0.5f 
        });

        if (dialogueLines != null)
        {
            foreach (string lineText in dialogueLines)
            {
                introCutscene.steps.Add(new DialogueStep() { 
                    line = new DialogueLine() { 
                        speakerName = npcSpeakerName, 
                        text = lineText, 
                        autoAdvance = false 
                    } 
                });
            }
        }

        introCutscene.steps.Add(new CameraTargetStep() { markerName = "" });

        CutsceneContext ctx = new CutsceneContext(player, null);
        
        // Find the NPC marker dynamically
        GameObject markerObj = GameObject.Find(npcMarkerName);
        if (markerObj != null)
        {
            ctx.namedPoints = new Dictionary<string, Transform>();
            ctx.namedPoints[npcMarkerName] = markerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[HayPile] Could not find GameObject named '{npcMarkerName}' for the intro cutscene camera target.");
        }

        CutscenePlayer.Instance.Play(introCutscene, ctx);
    }
}
