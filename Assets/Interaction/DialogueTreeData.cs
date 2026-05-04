using System;
using System.Collections.Generic;

[Serializable]
public class DialogueTree
{
    public List<DialogueNode> nodes;
}

[Serializable]
public class DialogueNode
{
    public string id;
    public string speakerName;
    public string text;
    public List<DialogueChoice> choices;
}

[Serializable]
public class DialogueChoice
{
    public string text;
    public string nextNodeId;
    public string eventId; // Added for future custom events
}
