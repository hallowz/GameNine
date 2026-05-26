using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu: Voidborne > Safeholds > Setup Vol 10.2
///
/// Creates NPCDefinition SOs, DialogueTree SOs, and NPC prefabs for all six
/// Safehold characters. Also creates ArchivistCell fragment-highlight logic.
/// </summary>
public static class SafeholdSetup102
{
    private const string NpcFolder       = "Assets/ScriptableObjects/NPCs";
    private const string DialogueFolder  = "Assets/ScriptableObjects/Dialogues";
    private const string PrefabFolder    = "Assets/Prefabs/NPCs";

    [MenuItem("Voidborne/Safeholds/Setup Vol 10.2 — NPCs & Dialogue")]
    public static void Setup()
    {
        EnsureFolders();
        CreateDialogueTrees();
        CreateNPCDefinitions();
        CreateNPCPrefabs();
        CreateArchivistFragmentWatcher();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SafeholdSetup102] Vol 10.2 setup complete — 6 NPCs, 6 dialogue trees created.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Dialogue Trees
    // ═══════════════════════════════════════════════════════════════

    private static void CreateDialogueTrees()
    {
        // ---- ElderMoss ----
        var elderTree = Make<DialogueTree>(DialogueFolder, "Dialogue_ElderMoss");
        elderTree.startNodeId = "root";
        elderTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "Elder Moss",
                text = "Stranger... you carry the Maker's Mark. I have not seen its like since the Great Silence fell. "
                     + "You are welcome in Ashfen — but I have many questions.",
                choices = new()
                {
                    new() { text = "Who is 'the Maker'?",      nextNodeId = "maker" },
                    new() { text = "Tell me about Ashfen.",    nextNodeId = "ashfen" },
                    new() { text = "I need to find a Stronghold.", nextNodeId = "stronghold_hint",
                        condition = new() { type = DialogueCondition.ConditionType.None } },
                    new() { text = "The device on my wrist — do you know what it is?",
                        nextNodeId = "cortex_react",
                        condition = new() {
                            type = DialogueCondition.ConditionType.HasIndexDevice } },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "maker",
                speakerName = "Elder Moss",
                text = "The Maker built the world before the VORD came. The old songs call them the Architect — "
                     + "a being of impossible knowledge who wove the land itself. "
                     + "We keep the faith because the faith kept us.",
                choices = new()
                {
                    new() { text = "What happened to the Architect?", nextNodeId = "maker_fate" },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "maker_fate",
                speakerName = "Elder Moss",
                text = "No one knows. The songs say the Maker sealed something terrible away — and was sealed with it. "
                     + "Some of us believe the Strongholds are what remains. "
                     + "But the VORD guard them now.",
                choices = new()
                {
                    new() { text = "I'll find out the truth.", nextNodeId = "maker_hope" },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "maker_hope",
                speakerName = "Elder Moss",
                text = "Then you carry more than the Mark, Stranger. You carry our hope. Go carefully.",
                choices = new()
                {
                    new() {
                        text = "Tell me where Stronghold 1 is.",
                        nextNodeId = "stronghold_hint",
                        action = new() {
                            type = DialogueAction.ActionType.AcceptQuest,
                            // quest assigned in setup after MSQ assets exist
                        }
                    },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "ashfen",
                speakerName = "Elder Moss",
                text = "We survive in the fungal marshes because the VORD find the spores "
                     + "disruptive to their sensors. Small mercy. We trade with The Delve below "
                     + "when the tunnels are clear — and sometimes with Spire's Rest, if the sky is quiet.",
                choices = new()
                {
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "stronghold_hint",
                speakerName = "Elder Moss",
                text = "The nearest Stronghold stands in the old ruins to the east — we call it the Warden's Shell. "
                     + "VORD patrols circle it twice a day. Scout Wren knows the gap in their route.",
                choices = new()
                {
                    new() { text = "Thank you.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "cortex_react",
                speakerName = "Elder Moss",
                text = "...\n\n"
                     + "That is not just a device, Stranger. That is the Maker's hand. "
                     + "The songs describe it — the Wrist of the Architect, the instrument of Restoration. "
                     + "The elders thought it was myth.\n\n"
                     + "I need to sit down.",
                choices = new()
                {
                    new() { text = "I only just found it.", nextNodeId = "cortex_react2" },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "cortex_react2",
                speakerName = "Elder Moss",
                text = "Then the Architect's work is not finished. Find the Strongholds. "
                     + "Restore the device. Whatever the VORD truly are — you may be the only one "
                     + "who can stop them from finishing what they started.",
                choices = new()
                {
                    new() { text = "I will.", nextNodeId = "" },
                }
            },
        };
        EditorUtility.SetDirty(elderTree);

        // ---- Scout Wren ----
        var wrenTree = Make<DialogueTree>(DialogueFolder, "Dialogue_ScoutWren");
        wrenTree.startNodeId = "root";
        wrenTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "Scout Wren",
                text = "You're new. Didn't die on the way in — good start. "
                     + "I'm Wren. I map VORD patrol routes for fun. Yes, *fun*. Don't judge me.",
                choices = new()
                {
                    new() { text = "Tell me about the VORD patrols.",  nextNodeId = "patrols" },
                    new() { text = "Any work I can do around here?",   nextNodeId = "quests" },
                    new() { text = "The Stronghold to the east — safe path?", nextNodeId = "stronghold_path" },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "patrols",
                speakerName = "Scout Wren",
                text = "Three units sweep the eastern ruins on a 40-minute cycle. "
                     + "There's a 6-minute window starting roughly at dawn where the gap between "
                     + "patrol one and patrol two is wide enough to slip through. Don't be slow.",
                choices = new()
                {
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "quests",
                speakerName = "Scout Wren",
                text = "Actually, yeah. I need someone to plant sensor nullifiers along the northern ridge — "
                     + "VORD have been expanding their scan radius. Interested?",
                choices = new()
                {
                    new() { text = "Count me in.",  nextNodeId = "quest_accept",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "wren_quest_accepted" } },
                    new() { text = "Maybe later.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "quest_accept",
                speakerName = "Scout Wren",
                text = "Grab the nullifiers from the supply crate by the east gate. "
                     + "Three placements along the northern ridge. "
                     + "Don't let the VORD see you do it — it defeats the whole point.",
                choices = new()
                {
                    new() { text = "Understood.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "stronghold_path",
                speakerName = "Scout Wren",
                text = "Head east until you hit the dead relay towers, then bear south. "
                     + "The fungal canopy gives cover until the last 200 metres — after that "
                     + "you're exposed. Move fast or move at dawn. Your call.",
                choices = new()
                {
                    new() { text = "Thanks.", nextNodeId = "" },
                }
            },
        };
        EditorUtility.SetDirty(wrenTree);

        // ---- ForgeKeeper Tar ----
        var tarTree = Make<DialogueTree>(DialogueFolder, "Dialogue_ForgeKeeperTar");
        tarTree.startNodeId = "root";
        tarTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "ForgeKeeper Tar",
                text = "Surface dweller. You're either brave or stupid — the tunnels sort those out quick. "
                     + "I'm Tar. I keep the forges running and the Delve from collapsing. What do you want?",
                choices = new()
                {
                    new() { text = "I need refined materials.",        nextNodeId = "trade" },
                    new() { text = "Can you teach me automation?",     nextNodeId = "automation" },
                    new() { text = "I proved myself up top. Any work?", nextNodeId = "quest_check" },
                    new() { text = "The Vehicle Workbench — I need the schematic.", nextNodeId = "workbench",
                        condition = new() {
                            type = DialogueCondition.ConditionType.QuestComplete,
                            questId = "MSQ_04_TheDelve" } },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "trade",
                speakerName = "ForgeKeeper Tar",
                text = "Shop's open. Don't touch anything you're not paying for.",
                choices = new()
                {
                    new() { text = "Let me see what you have.",
                        nextNodeId = "",
                        action = new() { type = DialogueAction.ActionType.OpenShop } },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "automation",
                speakerName = "ForgeKeeper Tar",
                text = "The belt conveyors, the hoppers, the drill heads — all mine. "
                     + "Took me six years underground to work it out. "
                     + "I'll share what I know once I know you won't waste it.",
                choices = new()
                {
                    new() { text = "Fair enough. What do you need?", nextNodeId = "quest_check" },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "quest_check",
                speakerName = "ForgeKeeper Tar",
                text = "The lower shaft is flooded with VORD drones. I need the blockage cleared "
                     + "before we can access the deepest ore seam. Clear it out — then we'll talk.",
                choices = new()
                {
                    new() { text = "I'll handle it.",
                        nextNodeId = "quest_accept",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "tar_quest_accepted" } },
                    new() { text = "Not right now.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "quest_accept",
                speakerName = "ForgeKeeper Tar",
                text = "Lower shaft is two levels down, east tunnel. "
                     + "Watch for the crystallised gas pockets — a stray shot sets them off. "
                     + "Come back when it's done.",
                choices = new()
                {
                    new() { text = "On it.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "workbench",
                speakerName = "ForgeKeeper Tar",
                text = "You actually cleared that shaft. Didn't think you had it in you.\n\n"
                     + "Here. Vehicle Workbench schematic. I designed it myself — "
                     + "you'll need a decent forge to assemble it.",
                choices = new()
                {
                    new() { text = "Thank you, Tar.",
                        nextNodeId = "",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "tar_workbench_given" } },
                }
            },
        };
        EditorUtility.SetDirty(tarTree);

        // ---- Archivist Cell ----
        var cellTree = Make<DialogueTree>(DialogueFolder, "Dialogue_ArchivistCell");
        cellTree.startNodeId = "root";
        cellTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "Archivist Cell",
                text = "I've been cataloguing the Architect's documents for eleven years. "
                     + "Most of it is partial, corrupted, or written in notation I don't fully understand. "
                     + "But it tells a story — if you can see the shape of it.",
                choices = new()
                {
                    new() { text = "What have you found?",            nextNodeId = "findings" },
                    new() { text = "I have archive fragments with me.", nextNodeId = "fragment_check",
                        condition = new() {
                            type = DialogueCondition.ConditionType.HasItem_Any } },
                    new() { text = "The Strongholds — what do they do?", nextNodeId = "strongholds" },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "findings",
                speakerName = "Archivist Cell",
                text = "The Architect feared something they built. The later logs show countdown timers, "
                     + "sealing protocols, what looks like an evacuation that never completed. "
                     + "Whatever the VORD are — the Architect made them. And then tried to stop them.",
                choices = new()
                {
                    new() { text = "Made them? How?", nextNodeId = "vord_origin" },
                    new() { text = "Back.",            nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "vord_origin",
                speakerName = "Archivist Cell",
                text = "I'm not certain. The notation for their origin is in a cipher I haven't cracked. "
                     + "But the Strongholds seem to be override nodes — the Architect designed them "
                     + "to shut the VORD down. If only someone could access them.",
                choices = new()
                {
                    new() { text = "I can. I have The Index.", nextNodeId = "cortex_reveal",
                        condition = new() { type = DialogueCondition.ConditionType.HasIndexDevice } },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "cortex_reveal",
                speakerName = "Archivist Cell",
                text = "The Index... I've only seen it in schematics.\n\n"
                     + "Then you can do what the Architect couldn't finish. "
                     + "Each module restores a portion of the override capacity. "
                     + "Find all six — and you can shut them all down.",
                choices = new()
                {
                    new() { text = "That's the plan.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "fragment_check",
                speakerName = "Archivist Cell",
                text = "Let me see... yes. *Yes.* "
                     + "This fragment references the same event as Log Entry 7 in my Stronghold file. "
                     + "The timestamp matches — this was written during the sealing sequence. "
                     + "Where did you find it?",
                choices = new()
                {
                    new() { text = "In a Stronghold.",       nextNodeId = "fragment_stronghold" },
                    new() { text = "I'm not sure exactly.",  nextNodeId = "fragment_vague" },
                }
            },
            new() {
                nodeId = "fragment_stronghold",
                speakerName = "Archivist Cell",
                text = "That confirms the Strongholds are active archive nodes — the Architect was still "
                     + "writing during the sealing. There may be more fragments inside. "
                     + "Anything you find, bring it to me. I can cross-reference everything.",
                choices = new()
                {
                    new() { text = "Understood.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "fragment_vague",
                speakerName = "Archivist Cell",
                text = "It doesn't matter. It matches. The archive is real — and it's still out there. "
                     + "Bring me anything else you find.",
                choices = new()
                {
                    new() { text = "I will.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "strongholds",
                speakerName = "Archivist Cell",
                text = "Override nodes for the VORD directive, I believe. "
                     + "Six of them, placed at depth and altitude extremes — surface, underground, sky. "
                     + "The Architect needed coverage across all biome layers.",
                choices = new()
                {
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
        };
        EditorUtility.SetDirty(cellTree);

        // ---- Keeper Varis ----
        var varisTree = Make<DialogueTree>(DialogueFolder, "Dialogue_KeeperVaris");
        varisTree.startNodeId = "root";
        varisTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "Keeper Varis",
                text = "I've maintained this structure for twenty years. "
                     + "I don't know what it is — only that it hums when the sky changes "
                     + "and that the VORD leave it alone. That's reason enough to protect it.",
                choices = new()
                {
                    new() { text = "I know what it is.",           nextNodeId = "reveal" },
                    new() { text = "Tell me about Spire's Rest.", nextNodeId = "spire" },
                    new() { text = "Any work up here?",            nextNodeId = "quests" },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "reveal",
                speakerName = "Keeper Varis",
                text = "...\n\nThen tell me. I have earned the right to know what I've been guarding.",
                choices = new()
                {
                    new() { text = "It's a Stronghold. One of six built by the Architect.", nextNodeId = "reveal2" },
                    new() { text = "Actually, I'll show you.", nextNodeId = "reveal_device",
                        condition = new() { type = DialogueCondition.ConditionType.HasIndexDevice } },
                }
            },
            new() {
                nodeId = "reveal2",
                speakerName = "Keeper Varis",
                text = "The Architect...\n\n"
                     + "I have kept their work alive without knowing it. "
                     + "Whatever you need from me — access, protection, knowledge of the structure — it's yours.",
                choices = new()
                {
                    new() { text = "I need to access the module inside.", nextNodeId = "module_access",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "varis_ally" } },
                    new() { text = "Thank you, Keeper.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "reveal_device",
                speakerName = "Keeper Varis",
                text = "The structure... it's responding to that device. "
                     + "The lighting just changed. Twenty years and I've never seen it do that.\n\n"
                     + "What are you?",
                choices = new()
                {
                    new() { text = "Someone trying to finish what the Architect started.", nextNodeId = "reveal2" },
                }
            },
            new() {
                nodeId = "module_access",
                speakerName = "Keeper Varis",
                text = "The inner chamber is through the north gate — I can open it from here. "
                     + "No one has been inside since I arrived. "
                     + "Whatever is in there has been waiting a long time.",
                choices = new()
                {
                    new() { text = "Open it.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "spire",
                speakerName = "Keeper Varis",
                text = "We live on sky islands because the VORD rarely climb this high. "
                     + "The wind currents between islands are treacherous — "
                     + "most VORD constructs can't navigate them. "
                     + "Supply Runner Eli keeps us stocked.",
                choices = new()
                {
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "quests",
                speakerName = "Keeper Varis",
                text = "We've lost contact with the eastern island outpost. "
                     + "If you have a Gyrocopter or something that flies, I need someone to check on it.",
                choices = new()
                {
                    new() { text = "I'll look into it.",
                        nextNodeId = "",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "varis_quest_accepted" } },
                    new() { text = "Not right now.", nextNodeId = "root" },
                }
            },
        };
        EditorUtility.SetDirty(varisTree);

        // ---- Supply Runner Eli ----
        var eliTree = Make<DialogueTree>(DialogueFolder, "Dialogue_SupplyRunnerEli");
        eliTree.startNodeId = "root";
        eliTree.nodes = new List<DialogueNode>
        {
            new() {
                nodeId = "root",
                speakerName = "Supply Runner Eli",
                text = "Just got in from the southern run. Vael's patrol moved *again* — "
                     + "had to swing forty degrees west to avoid the scan radius. "
                     + "I swear that General gets smarter every cycle. You a flier?",
                choices = new()
                {
                    new() { text = "I am, yes.",                              nextNodeId = "flier_yes" },
                    new() { text = "Not yet.",                                nextNodeId = "flier_no" },
                    new() { text = "Tell me about Vael's patrol patterns.",   nextNodeId = "vael" },
                    new() { text = "Signal Dampener — can you get me one?",   nextNodeId = "dampener" },
                    new() { text = "Farewell.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "flier_yes",
                speakerName = "Supply Runner Eli",
                text = "Good. I need a relief runner for the northern island chain — "
                     + "I've been doing double shifts since Marsh got grounded. "
                     + "Pay is modest but the view's incredible.",
                choices = new()
                {
                    new() { text = "I'll take the run.",
                        nextNodeId = "run_briefing",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "eli_quest_accepted" } },
                    new() { text = "Not right now.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "flier_no",
                speakerName = "Supply Runner Eli",
                text = "Get a Gyrocopter. Spire's Rest is no place for someone who can't leave in a hurry. "
                     + "I can point you at parts if you're building one.",
                choices = new()
                {
                    new() { text = "What parts do I need?", nextNodeId = "gyro_parts" },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "gyro_parts",
                speakerName = "Supply Runner Eli",
                text = "Rotor Frame, two BasicRotors minimum, a combustion engine, and some glass. "
                     + "I've got spare rotors and an engine head in my supply cache — "
                     + "you can have them for the right trade.",
                choices = new()
                {
                    new() { text = "Deal.", nextNodeId = "" },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "run_briefing",
                speakerName = "Supply Runner Eli",
                text = "Three drops — Outpost Thorn, High Relay, and the Shepherd's Shelf. "
                     + "Cargo is already packed. Don't fly through Sector 7 — "
                     + "Vael has a stationary scan unit there.",
                choices = new()
                {
                    new() { text = "Got it.", nextNodeId = "" },
                }
            },
            new() {
                nodeId = "vael",
                speakerName = "Supply Runner Eli",
                text = "Vael is General THREE — a sky specialist. "
                     + "Expanded scan radius, rotates unpredictably, and she tracks thermal signatures. "
                     + "A Signal Dampener reduces how far she detects you. "
                     + "Without one, fly high and fast.",
                choices = new()
                {
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
            new() {
                nodeId = "dampener",
                speakerName = "Supply Runner Eli",
                text = "I've got a schematic. "
                     + "You'll need Copper Plate and a Circuit Board — not cheap, but worth every bit. "
                     + "Fits on any vehicle with a utility slot.",
                choices = new()
                {
                    new() { text = "Give me the schematic.",
                        nextNodeId = "",
                        action = new() { type = DialogueAction.ActionType.TriggerWorldEvent,
                                         worldEventId = "eli_dampener_given" } },
                    new() { text = "Back.", nextNodeId = "root" },
                }
            },
        };
        EditorUtility.SetDirty(eliTree);
    }

    // ═══════════════════════════════════════════════════════════════
    //  NPC Definitions
    // ═══════════════════════════════════════════════════════════════

    private static void CreateNPCDefinitions()
    {
        // Helper: load the tree we just created
        DialogueTree LoadTree(string name) =>
            AssetDatabase.LoadAssetAtPath<DialogueTree>($"{DialogueFolder}/{name}.asset");

        // Ashfen
        var elderMoss = Make<NPCDefinition>(NpcFolder, "ElderMoss");
        elderMoss.npcId        = "elder_moss";
        elderMoss.npcName      = "Elder Moss";
        elderMoss.role         = "Community leader of Ashfen. Keeper of the old songs and myths of the Maker.";
        elderMoss.safeholdName = "Ashfen";
        elderMoss.worldPosition  = new Vector3(120f, 0f, 85f);
        elderMoss.defaultDialogue = LoadTree("Dialogue_ElderMoss");
        EditorUtility.SetDirty(elderMoss);

        var scoutWren = Make<NPCDefinition>(NpcFolder, "ScoutWren");
        scoutWren.npcId        = "scout_wren";
        scoutWren.npcName      = "Scout Wren";
        scoutWren.role         = "Young Kin scout. Maps VORD patrol routes for Ashfen.";
        scoutWren.safeholdName = "Ashfen";
        scoutWren.worldPosition  = new Vector3(135f, 0f, 90f);
        scoutWren.defaultDialogue = LoadTree("Dialogue_ScoutWren");
        EditorUtility.SetDirty(scoutWren);

        // The Delve
        var forgekeeperTar = Make<NPCDefinition>(NpcFolder, "ForgeKeeperTar");
        forgekeeperTar.npcId        = "forgekeeper_tar";
        forgekeeperTar.npcName      = "ForgeKeeper Tar";
        forgekeeperTar.role         = "Gruff engineer. Maintains The Delve's forges and automation.";
        forgekeeperTar.safeholdName = "The Delve";
        forgekeeperTar.worldPosition  = new Vector3(0f, -80f, 20f);
        forgekeeperTar.defaultDialogue = LoadTree("Dialogue_ForgeKeeperTar");
        EditorUtility.SetDirty(forgekeeperTar);

        var archivistCell = Make<NPCDefinition>(NpcFolder, "ArchivistCell");
        archivistCell.npcId        = "archivist_cell";
        archivistCell.npcName      = "Archivist Cell";
        archivistCell.role         = "Keeper of recovered Architect documents in The Delve.";
        archivistCell.safeholdName = "The Delve";
        archivistCell.worldPosition  = new Vector3(-10f, -80f, 25f);
        archivistCell.defaultDialogue = LoadTree("Dialogue_ArchivistCell");
        EditorUtility.SetDirty(archivistCell);

        // Spire's Rest
        var keeperVaris = Make<NPCDefinition>(NpcFolder, "KeeperVaris");
        keeperVaris.npcId        = "keeper_varis";
        keeperVaris.npcName      = "Keeper Varis";
        keeperVaris.role         = "Has maintained Stronghold 4 for twenty years, unaware of its nature.";
        keeperVaris.safeholdName = "Spire's Rest";
        keeperVaris.worldPosition  = new Vector3(50f, 300f, 200f);
        keeperVaris.defaultDialogue = LoadTree("Dialogue_KeeperVaris");
        EditorUtility.SetDirty(keeperVaris);

        var supplyRunnerEli = Make<NPCDefinition>(NpcFolder, "SupplyRunnerEli");
        supplyRunnerEli.npcId        = "supply_runner_eli";
        supplyRunnerEli.npcName      = "Supply Runner Eli";
        supplyRunnerEli.role         = "Runs resupply missions between sky islands. Expert on Vael's patrol routes.";
        supplyRunnerEli.safeholdName = "Spire's Rest";
        supplyRunnerEli.worldPosition  = new Vector3(60f, 300f, 210f);
        supplyRunnerEli.defaultDialogue = LoadTree("Dialogue_SupplyRunnerEli");
        EditorUtility.SetDirty(supplyRunnerEli);
    }

    // ═══════════════════════════════════════════════════════════════
    //  NPC Prefabs (capsule placeholder models)
    // ═══════════════════════════════════════════════════════════════

    private static void CreateNPCPrefabs()
    {
        string[] npcIds = {
            "ElderMoss", "ScoutWren", "ForgeKeeperTar",
            "ArchivistCell", "KeeperVaris", "SupplyRunnerEli"
        };
        Color[] npcColors = {
            new Color(0.3f, 0.6f, 0.3f),  // ElderMoss — green
            new Color(0.5f, 0.7f, 0.9f),  // ScoutWren — light blue
            new Color(0.7f, 0.4f, 0.2f),  // ForgeKeeperTar — orange-brown
            new Color(0.6f, 0.5f, 0.8f),  // ArchivistCell — purple
            new Color(0.8f, 0.8f, 0.7f),  // KeeperVaris — off-white
            new Color(0.9f, 0.7f, 0.3f),  // SupplyRunnerEli — gold
        };

        for (int i = 0; i < npcIds.Length; i++)
        {
            string prefabPath = $"{PrefabFolder}/NPC_{npcIds[i]}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                continue;

            // Root object
            var root   = new GameObject($"NPC_{npcIds[i]}");

            // Body capsule
            var body   = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name  = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale    = new Vector3(0.6f, 1f, 0.6f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = npcColors[i];
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Head sphere
            var head   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name  = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            head.transform.localScale    = new Vector3(0.55f, 0.55f, 0.55f);
            Object.DestroyImmediate(head.GetComponent<SphereCollider>());
            head.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // NPCController + trigger sphere
            var ctrl = root.AddComponent<NPCController>();
            NPCDefinition def = AssetDatabase.LoadAssetAtPath<NPCDefinition>(
                $"{NpcFolder}/{npcIds[i]}.asset");
            ctrl.definition = def;

            // Collider for physics detection (IInteractable uses OverlapSphere but a
            // collider is still needed for raycasts from camera-based systems)
            var sc = root.GetComponent<SphereCollider>() ?? root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius    = 5f;

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ArchivistCell fragment highlight watcher
    // ═══════════════════════════════════════════════════════════════

    private static void CreateArchivistFragmentWatcher()
    {
        // The watcher script was created separately as ArchivistFragmentWatcher.cs
        // Just verify the asset exists — if not, the script needs a compile cycle first.
        string path = "Assets/Scripts/Quests/ArchivistFragmentWatcher.cs";
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null) return;
        // Script will be created in the same batch; see companion file.
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { NpcFolder, DialogueFolder, PrefabFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                string child  = System.IO.Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }

    private static T Make<T>(string folder, string name) where T : ScriptableObject
    {
        string path = $"{folder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
