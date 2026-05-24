using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RLAgent : Agent
{
    /*
    public RLAgent()
    {
        stateManager = GameObject.FindObjectOfType<StateManager>();
        diceRoller = GameObject.FindObjectOfType<DiceRoller>();
    }
    */

    public DiceRoller diceRoller;
    public StateManager stateManager;
    public StoneStorage stoneStorage;

    public bool isMoving = false;

    public override void OnEpisodeBegin()
    {
        // Reset the environment and agent state here
        diceRoller = GameObject.FindObjectOfType<DiceRoller>();
        stateManager = GameObject.FindObjectOfType<StateManager>();
        stoneStorage = GameObject.FindObjectOfType<StoneStorage>();
        isMoving = false;
        ResetGame();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Collect observations about the game state here. We have a total of 34/36 observations we can make:

        PlayerStone[] pss = GameObject.FindObjectsOfType<PlayerStone>(); // 12 stones in total

        foreach( PlayerStone ps in pss )
        {
            sensor.AddObservation(ps); 
        }

        Tile[] tiles = GameObject.FindObjectsOfType<Tile>(); // 20 tiles in total

        foreach( Tile t in tiles )
        {
            sensor.AddObservation(t);
        }

        sensor.AddObservation(stateManager.CurrentPlayerId);
        sensor.AddObservation(stateManager.DiceTotal);
    }

    public void DoAI()
    {
        isMoving = true;
        RequestDecision();
        isMoving = false;
    }
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        
        if(stateManager.IsDoneRolling == false)
        {
            // We need to roll the dice!
            DoRoll();
            return;
        }

        // Interpret the actions and apply them to the game here
        

        if(stateManager.IsDoneClicking == false)
        {
            int stoneIndex = actionBuffers.DiscreteActions[0];
            Debug.Log("Received action: " + stoneIndex);
            // We have a die roll, but we need to pick a stone to move
            DoClick(stoneIndex);

            
            return;
        }
        

        // After applying the action, you can calculate the reward and call SetReward() accordingly
        // For example, you could give a positive reward for moving a stone closer to the goal and a negative reward for moving it further away
    }

    virtual protected void DoRoll()
    {
        diceRoller.RollTheDice();
    }

    virtual protected void DoClick(int index)
    {
        // Pick a stone to move, then "click" it.
        PlayerStone[] legalStones = GetLegalMoves();

        if(legalStones == null || legalStones.Length == 0)
        {
            // We have no legal moves.  How did we get here?
            // We might still be in a delayed coroutine somewhere. Let's not freak out.
            Debug.Log("Trying to click a stone but we have no legal moves. This might be because we're still waiting for a coroutine to finish. Ignoring this click.");
            return;
        }

        PlayerStone pickedStone = legalStones[index % legalStones.Length];
        Tile currentTile = pickedStone.CurrentTile;
        Tile futureTile = pickedStone.GetTileAhead(stateManager.DiceTotal);
        AddReward(-0.01f); // Small negative reward for each click to encourage shorter games

        if (currentTile != null && currentTile.IsRollAgain == true)
        {
            AddReward(0.05f); // Small positive reward for staying on a roll again tile
        }
        if (currentTile != null && currentTile.IsSideline == true)
        {
            AddReward(0.05f); // Small positive reward for being on a safe tile
        }
        if (futureTile != null && futureTile.IsScoringSpace == true)
        {
            AddReward(0.15f); // Small positive reward for moving onto a scoring tile
        }
        if (futureTile != null && futureTile.PlayerStone != null && futureTile.PlayerStone.PlayerId != pickedStone.PlayerId)
        {
            AddReward(0.10f); // Small positive reward for knocking an opponent's stone off
            stateManager.PlayerAIs[futureTile.PlayerStone.PlayerId].AddReward(-0.10f); // Small negative reward for the opponent losing a stone
        }
        if (futureTile != null && futureTile.IsRollAgain == true)
        {
            AddReward(0.05f); // Small positive reward for moving onto a roll again tile
        }
        if (futureTile != null && futureTile.IsSideline == true)
        {
            AddReward(0.05f); // Small positive reward for moving onto a safe tile
        }


        pickedStone.MoveMe();
        if (stateManager.PlayerScores[stateManager.CurrentPlayerId] >= 6)
        {
            AddReward(1.0f); // Big reward for winning the game
            stateManager.PlayerAIs[(stateManager.CurrentPlayerId + 1) % stateManager.NumberOfPlayers].AddReward(-1.0f); // Big negative reward for the opponent losing
        }
    }

    /// <summary>
    /// Returns a list of stones that can be legally moved
    /// </summary>
    protected PlayerStone[] GetLegalMoves()
    {
        List<PlayerStone> legalStones = new List<PlayerStone>();


        // If we rolled a zero, then we clearly have no legal moves.
        if(stateManager.DiceTotal == 0)
        {
            return legalStones.ToArray();
        }

        // Loop through all of a player's stones
        PlayerStone[] pss = GameObject.FindObjectsOfType<PlayerStone>();

        foreach( PlayerStone ps in pss )
        {
            if(ps.PlayerId == stateManager.CurrentPlayerId)
            {
                if( ps.CanLegallyMoveAhead( stateManager.DiceTotal) )
                {
                    legalStones.Add(ps);
                }
            }
        }
        Debug.Log("Legal Stones: " + legalStones.ToArray());
        return legalStones.ToArray();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Provide a heuristic for testing the agent without training
        // For example, you could use keyboard input to control the agent's actions
    }

    public void ResetGame()
    {
        resetTiles();
        ResetPlayerStonePosition();
        
        stateManager.CurrentPlayerId = 0;
        stateManager.DiceTotal = 0;
        stateManager.IsDoneRolling = false;
        stateManager.IsDoneClicking = false;
        stateManager.AnimationsPlaying = 0;
        isMoving = false;
        StartCoroutine(WaitASecond());
    }

    public void ResetPlayerStonePosition()
    {
        /*
        PlayerStone[] pss = GameObject.FindObjectsOfType<PlayerStone>();

        foreach( PlayerStone ps in pss )
        {
            if (ps.PlayerId == stateManager.CurrentPlayerId)
            {
                ps.ReturnToStorage();
                ps.CurrentTile.PlayerStone = null;
                ps.CurrentTile = null;
                
                ps.scoreMe = false;
                //stoneStorage.AddStoneToStorage(ps.gameObject);
            }
        }
        StartCoroutine(WaitASecond());
        */
        PlayerStone[] pss = GameObject.FindObjectsOfType<PlayerStone>();

        foreach( PlayerStone ps in pss )
        {
            Destroy(ps.gameObject);
        }
        StoneStorage[] ss = GameObject.FindObjectsOfType<StoneStorage>();
        ss[0].InstantiateStonePrefab();
        ss[1].InstantiateStonePrefab();
        StartCoroutine(WaitASecond());
    }

    public void resetTiles()
    {
        Tile[] tiles = GameObject.FindObjectsOfType<Tile>();

        foreach( Tile t in tiles )
        {
            t.PlayerStone = null;
        }
    }

    public IEnumerator WaitASecond()
    {
        yield return new WaitForSeconds(1f);
    }
}
