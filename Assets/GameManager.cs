﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour {
    // Public GameObjects to be assigned in editor
    public GameObject OneByOnePrefab;
    public Camera Camera;
    public List<GameObject> MonsterPrefabsList;
    public List<GameObject> PlayerPrefabList;

    public GameObject MenuCanvas, InGameCanvas;
    public TMP_Text TextCurrentActor, TextHP, TextAC, TextAtkName, TextAtkRoll, TextDmgRoll, TextSpeedLeft, TextTurnTracker;

    [HideInInspector] public static GameManager instance;

    // Data structures to support running the game
    private List<Actor> actors;
    private int currentActorTurn;
    public Turn currentTurnStats;
    private int playerCount, monsterCount;
    [HideInInspector] public static STATES state = STATES.MENU;
    public enum STATES { MENU, AWAITING_INPUT, ANIMATING_ACTION };

    // Predefined Scenarios
    private SceneActor[] skeletonScene;
    private SceneActor[] OTHERScene; // TODO: Make this one, too!

    // The game board, available for inspection
    // each Space object contains a reference to a OneByOne (GameObject). Use this to find actual world unit coordinates of each game space
    [HideInInspector] public Space[,] spaces;
    public GameObject SpacesHolder; // an empty GameObject to hold all the spaces. Simply to reduce clutter...doesn't improve performance, I think

    // Properties of the spaces
    public int RowsX = 60, ColsZ = 60;
    private const float DropFromHeight = 10f;
    private const float Margin = 0.05f;
    private const float SpaceHeight = 0.2f;
    public Vector3 SPACE_HEIGHT_MOD;

    private const float cameraSpeed = 4;


    void Start () {
        GameManager.instance = this;
        SPACE_HEIGHT_MOD = new Vector3(0f, SpaceHeight, 0f);
        PopupTextController.Initialize();

        // Build the predefined scenarios
        skeletonScene = new SceneActor[] {
            new SceneActor(true, 0, 25, 25, new Color(0, 0.47f, 1f, 0.58f)),
            new SceneActor(false, 0, 25, 28, new Color(1f, 0, 0, 0.58f))
        };

        // Generate game board made of one-by-one squares
        spaces = new Space[RowsX, ColsZ];
        GenerateSquares();
        ResetBoard();

        // DEBUG
        Invoke("StartScenarioSkeletons", 2);
    }

    public void NextTurn() {
        // Turn of highlight for previous token
        if (currentActorTurn >= 0) { // skip for first turn
            ((Behaviour) actors[currentActorTurn].tokenRef.GetComponent("Halo")).enabled = false;
        }

        // Update counter for new turn
        currentActorTurn = (currentActorTurn + 1) % actors.Count;

        // Set text for this actor
        TextCurrentActor.text = actors[currentActorTurn].ActorName;
        TextHP.text = "HP: " + actors[currentActorTurn].HP;
        TextAC.text = "AC: " + actors[currentActorTurn].AC;
        TextAtkName.text = actors[currentActorTurn].AttackName;
        TextAtkRoll.text = "1d20 + " + actors[currentActorTurn].AttackMod;
        TextDmgRoll.text = actors[currentActorTurn].DamageDieNum + "d" + actors[currentActorTurn].DamageDieMagnitude + " + " + actors[currentActorTurn].DamageMod;
        TextSpeedLeft.text = actors[currentActorTurn].Speed + " Spaces";

        // Track this turn
        currentTurnStats = new Turn {MovementLeft = actors[currentActorTurn].Speed};

        // Change visuals for this actor's turn
        MouseHoverHighlight.MouseOverColor = actors[currentActorTurn].ActorColor;

        // Set state
        SetState(STATES.AWAITING_INPUT);
    }

    public void CheckForTurnCompleted() {
        if (currentTurnStats.MovementLeft == 0 && currentTurnStats.HasAttackHappened) {
            // Current turn actor is out of movement and has already attacked
            NextTurn();
        }
    }

    // Contains the information for a current turn. Temporary: will be deleted after one turn is done
    public class Turn {
        public int MovementLeft;
        public bool HasAttackHappened = false;
    }

    public void SetState(STATES newSate) {
        state = newSate;
        if (newSate == STATES.AWAITING_INPUT) {
            MouseHoverHighlight.isEffectActive = true;
            ((Behaviour) actors[currentActorTurn].tokenRef.GetComponent("Halo")).enabled = true;
        } else if (newSate == STATES.ANIMATING_ACTION) {
            MouseHoverHighlight.isEffectActive = false;
            ((Behaviour) actors[currentActorTurn].tokenRef.GetComponent("Halo")).enabled = false;
        } else if (newSate == STATES.MENU) {
            MouseHoverHighlight.isEffectActive = false;
            ((Behaviour) actors[currentActorTurn].tokenRef.GetComponent("Halo")).enabled = false;
            ResetBoard(); // put the squares back in their reset position
        }
    }

    private static int RollDice(int numDice, int diceMagnitude, int mod) {
        Debug.Log("Rolling: " + numDice + "d" + diceMagnitude + " + " + mod);
        int diceTotal = mod;
        for (int i = 0; i < numDice; ++i) {
            diceTotal += Random.Range(1, diceMagnitude);
        }
        Debug.Log(" = " + diceTotal);
        return diceTotal;
    }

    public void OnClickStartButton() {
        StartScenarioSkeletons();
    }

    public void StartScenarioSkeletons() {
        // Reset the scene and place the new scene's tokens
        ResetAndBuildScene(skeletonScene);

        // Roll init and sort
        RollInit();

        NextTurn();
    }

    private void ResetAndBuildScene(SceneActor[] predefinedSceneActors) {
        // Reset the scene to blank
        ReleaseBoard();
        actors = new List<Actor>();
        playerCount = 0;
        monsterCount = 0;
        currentActorTurn = -1; // -1 so turns actually start a 0

        // Build scene from predefined
        foreach (SceneActor actorData in predefinedSceneActors) {
            // Create GameObject and place it in the correct square
            GameObject newGameObject;
            if (actorData.IsPlayer) {
                newGameObject = (GameObject) Instantiate(instance.PlayerPrefabList[actorData.PrefabIndex]);
                playerCount++;
            } else {
                newGameObject = (GameObject) Instantiate(instance.MonsterPrefabsList[actorData.PrefabIndex]);
                monsterCount++;
            }
            Space spaceToPlace = spaces[actorData.x, actorData.z];
            Vector3 squareBasis = spaceToPlace.gameSpace.transform.position;
            newGameObject.transform.position = new Vector3(squareBasis.x, DropFromHeight + 1, squareBasis.z);

            TokenStats stats = newGameObject.GetComponent<TokenStats>();
            Actor newActor = new Actor(newGameObject, actorData.x, actorData.z, actorData.ActorColor, actorData.IsPlayer, stats.characterName, stats.HP, stats.AC, stats.InitativeMod, stats.Speed, stats.AttackName, stats.AttackMod, stats.DamageDiceNum,
                stats.DamageDiceMagnitude, stats.DamageMod);
            spaces[actorData.x, actorData.z].isBlocked = true;

            actors.Add(newActor);
        }
    }

    private void RollInit() {
        foreach (Actor actor in actors) {
            actor.RollInit();
        }
        actors.Sort((a, b) => b.Initative.CompareTo(a.Initative));
        string turnTrackerList = "";
        foreach (Actor actor in actors) {
            turnTrackerList += actor.Initative + " - " + actor.ActorName + "\n";
        }
        TextTurnTracker.text = turnTrackerList;
    }

    // Instantiate square objects, but don't make them active yet
    private void GenerateSquares() {
        // Set up X,Z containers
        for (int x = 0; x < RowsX; x++) {
            for (int z = 0; z < ColsZ; z++) {
                spaces[x, z] = new Space(x, z);
            }
        }

        // Block any spaces that are impassible
        // A tree!
        spaces[29, 14].isBlocked = true;
        // A big rock!
        spaces[12, 32].isBlocked = true;
        spaces[13, 25].isBlocked = true;
        spaces[13, 26].isBlocked = true;
        spaces[13, 32].isBlocked = true;
        spaces[14, 26].isBlocked = true;
        spaces[14, 27].isBlocked = true;
        spaces[14, 28].isBlocked = true;
        spaces[14, 29].isBlocked = true;
        spaces[14, 30].isBlocked = true;
        spaces[14, 31].isBlocked = true;
        spaces[14, 32].isBlocked = true;
        spaces[15, 27].isBlocked = true;
        spaces[15, 28].isBlocked = true;
        spaces[15, 29].isBlocked = true;
        spaces[15, 30].isBlocked = true;
        spaces[15, 31].isBlocked = true;

        for (int x = 0; x < RowsX; x++) {
            for (int z = 0; z < ColsZ; z++) {
                if (!spaces[x, z].isBlocked) {
                    spaces[x, z].gameSpace = (GameObject) Instantiate(instance.OneByOnePrefab, SpacesHolder.transform);
                }
            }
        }
    }

    // Place squares back in the original position for a new game scenario
    private void ResetBoard() {
        for (int x = 0; x < RowsX; x++) {
            for (int z = 0; z < ColsZ; z++) {
                if (!spaces[x, z].isBlocked) {
                    spaces[x, z].gameSpace.transform.position = new Vector3(x + Margin, DropFromHeight, z + Margin);
                    spaces[x, z].gameSpace.SetActive(false);
                }
            }
        }
    }
    // Re-activate all squares so they fall
    private void ReleaseBoard() {
        for (int x = 0; x < RowsX; x++) {
            for (int z = 0; z < ColsZ; z++) {
                if (!spaces[x, z].isBlocked) {
                    spaces[x, z].gameSpace.SetActive(true);
                }
            }
        }
    }

    // Recevied from any arbitrary GameObject with the OnClick-Message script attached
    public void MessageClickedToken(GameObject goClicked) {
        Debug.Log("Attacking! " + goClicked);
        // Check if attack is possible

        // Do attack mechanics

        // Animate attack
        actors[currentActorTurn].tokenRef.GetComponent<TokenAttacker>().AttackTowards(goClicked);

        // Finalise attack
        currentTurnStats.HasAttackHappened = true;
        CheckForTurnCompleted();
    }

    // Recevied from any arbitrary GameObject with the OnClick-Message script attached
    public void MessageClickedSpace(Vector2 coord) {
        WalkActor(actors[currentActorTurn], (int) coord.x, (int) coord.y);
    }
    // Walk a player or monster token to a space
    private void WalkActor(Actor actor, int xTo, int zTo) {
        // Find a path to the desired square, by getting a queue of sqaures to hop over
        LinkedList<TokenWalker.Hop> hopsQueue = Pathfind.FindPath(actor.x, actor.z, xTo, zTo);

        if (hopsQueue != null) {
            if (hopsQueue.Count > currentTurnStats.MovementLeft) {
                PopupTextController.PopupText("Not Enough Movement", spaces[xTo, zTo].gameSpace.transform);
            } else {
                // change the token's stored properties to its final position
                spaces[actor.x, actor.z].isBlocked = false;
                actor.x = xTo;
                actor.z = zTo;
                spaces[xTo, zTo].isBlocked = true;

                // start the hopping at the first one. will continue until hopsQueue is empty
                SetState(STATES.ANIMATING_ACTION);
//                NextHopToken(actor.tokenRef);
                actor.tokenRef.GetComponent<TokenWalker>().WalkPath(hopsQueue);
            }
        } else {
                PopupTextController.PopupText("Pathfinding failed", spaces[xTo, zTo].gameSpace.transform);
        }
    }

    void Update () {
        float deltaX = 0f, deltaZ = 0f;

        // Move the camera along the diagonals
        if (Input.GetKey(KeyCode.A)) {
            deltaX += cameraSpeed * Time.deltaTime;
            deltaZ -= cameraSpeed * Time.deltaTime;
        } else if (Input.GetKey(KeyCode.D)) {
            deltaX -= cameraSpeed * Time.deltaTime;
            deltaZ += cameraSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.W)) {
            deltaX -= cameraSpeed * Time.deltaTime;
            deltaZ -= cameraSpeed * Time.deltaTime;
        } else if (Input.GetKey(KeyCode.S)) {
            deltaX += cameraSpeed * Time.deltaTime;
            deltaZ += cameraSpeed * Time.deltaTime;
        }
        if (deltaX != 0f || deltaZ != 0f) {
            Camera.transform.position = new Vector3(Camera.transform.position.x + deltaX, Camera.transform.position.y, Camera.transform.position.z + deltaZ);
        }
	}

    // A struct to hold information about the game board spaces
    public class Space {
        public GameObject gameSpace = null; // public reference to the OneByOne GameObject pointed to by this space
        public int x, z; // public reference to this object's position in the grid
        public bool isBlocked = false; // Define if this space is impassible
        public Space(int x, int z) {
            this.x = x;
            this.z = z;
        }
    }

    // A class to define a Prebuilt Scenario, stored as an array of SceneActors
    // Stores each token's initial position and properties
    public class SceneActor {
        public bool IsPlayer; // grab GameObject from player list or monster list
        public int PrefabIndex; // which item in the list of players/monsters does this Actor refer to?
        public int x, z; // location on the grid to start the token
        public Color ActorColor;
        public SceneActor(bool isPlayer, int prefabIndex, int x, int z, Color actorColor) {
            IsPlayer = isPlayer;
            PrefabIndex = prefabIndex;
            this.x = x;
            this.z = z;
            ActorColor = actorColor;
        }
    }

    // A struct to hold an actor on the game board
    // A list of these will make up a scene
    public class Actor {
        public GameObject tokenRef;
        public int x, z;
        public bool IsPlyaer;
        public bool IsAlive = true;
        public string ActorName;
        public int HP, AC, InitativeMod, Speed;
        public int Initative;
        public string AttackName;
        public int AttackMod, DamageDieNum, DamageDieMagnitude, DamageMod;
        public Color ActorColor; // the colour to surround this token with indicating it is the active Actor, and to use as the cursor highlight
        public Actor(GameObject tokenRef, int x, int z, Color actorColor, bool isPlyaer, string actorName, int hp, int ac, int initativeMod, int speed, string attackName, int attackMod, int damageDieNum, int damageDieMagnitude, int damageMod) {
            this.tokenRef = tokenRef;
            this.x = x;
            this.z = z;
            ActorColor = actorColor;
            IsPlyaer = isPlyaer;
            ActorName = actorName;
            HP = hp;
            AC = ac;
            InitativeMod = initativeMod;
            Speed = speed;
            AttackName = attackName;
            AttackMod = attackMod;
            DamageDieNum = damageDieNum;
            DamageDieMagnitude = damageDieMagnitude;
            DamageMod = damageMod;
        }

        public void RollInit() {
            Initative = RollDice(1, 20, InitativeMod);
        }
    }
}
