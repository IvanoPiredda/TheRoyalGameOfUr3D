using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
        // Collect observations about the game state here. We have a total of 25 observations we can make:

        PlayerStone[] pss = Object.FindObjectsByType<PlayerStone>(FindObjectsSortMode.None);

        float myStonesInStorage = 0f;
        float myStonesInGoal = 0f;
        float opponentStonesInStorage = 0f;
        float opponentStonesInGoal = 0f;

        int myPlayerId = stateManager.CurrentPlayerId;

        foreach( PlayerStone ps in pss.OrderBy(ps => ps.PlayerId).ThenBy(ps => ps.StoneId) )
        {
            if (ps.PlayerId == myPlayerId)
            {
                if (ps.CurrentTile == ps.StartingTile)
                {
                    myStonesInStorage += 1f;
                }
                else if (ps.scoreMe == true)
                {
                    myStonesInGoal += 1f;
                }
            }
            else
            {
                if (ps.CurrentTile == ps.StartingTile)
                {
                    opponentStonesInStorage += 1f;
                }
                else if (ps.scoreMe == true)
                {
                    opponentStonesInGoal += 1f;
                }
            }
        }

        sensor.AddObservation(myStonesInStorage / 6.0f); // 1 for the number of my stones still in the storage
        sensor.AddObservation(myStonesInGoal / 6.0f); // 1 for the number of stones in goal
        sensor.AddObservation(opponentStonesInStorage / 6.0f); // 1 for the number of opponent's stones still in the storage
        sensor.AddObservation(opponentStonesInGoal / 6.0f); // 1 for the number of opponent's stones in goal

        Tile[] tiles = Object.FindObjectsByType<Tile>(FindObjectsSortMode.None); // 20 tiles in total 
        foreach( Tile t in tiles.OrderBy(t => t.TileId) )
        {
            if (t.PlayerStone != null)
            {
                if (t.PlayerStone.PlayerId == myPlayerId)
                {
                    sensor.AddObservation(1.0f); // My stone
                }
                else
                {
                    sensor.AddObservation(-1.0f); // Opponent's stone
                }
            }
            else
            {
                sensor.AddObservation(0.0f); // No stone on this tile
            }
        }

        sensor.AddObservation(stateManager.DiceTotal / 4.0f); // Normalize dice total to be between 0 and 1
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

    virtual protected void DoClick(int index)
    {
        // Pick a stone to move, then "click" it.
        //PlayerStone[] legalStones = GetLegalMoves();
        PlayerStone[] playerStones = Object.FindObjectsByType<PlayerStone>(FindObjectsSortMode.None);
        PlayerStone[] myStones = playerStones.Where(ps => ps.PlayerId == stateManager.CurrentPlayerId).OrderBy(ps => ps.StoneId).ToArray();


        PlayerStone pickedStone = myStones[index];
        Tile currentTile = pickedStone.CurrentTile;
        Tile futureTile = pickedStone.GetTileAhead(stateManager.DiceTotal);
        AddReward(-0.005f); // Small negative reward for each click to encourage shorter games

        if (currentTile != null && currentTile.IsRollAgain == true)
        {
            AddReward(0.01f); // Small positive reward for staying on a roll again tile
        }
        if (currentTile != null && currentTile.IsSideline == true)
        {
            AddReward(0.01f); // Small positive reward for being on a safe tile
        }
        if (futureTile != null && futureTile.IsScoringSpace == true)
        {
            AddReward(0.4f); // Small positive reward for moving onto a scoring tile
        }
        if (futureTile != null && futureTile.PlayerStone != null && futureTile.PlayerStone.PlayerId != pickedStone.PlayerId)
        {
            AddReward(0.2f); // Small positive reward for knocking an opponent's stone off
            //stateManager.PlayerAIs[futureTile.PlayerStone.PlayerId].AddReward(-0.10f); // Small negative reward for the opponent losing a stone
        }
        if (futureTile != null && futureTile.IsRollAgain == true)
        {
            AddReward(0.05f); // Small positive reward for moving onto a roll again tile
        }
        if (futureTile != null && futureTile.IsSideline == true)
        {
            AddReward(0.01f); // Small positive reward for moving onto a safe tile
        }


        pickedStone.MoveMe();
        /*
        // Check if we won the game!
        if (stateManager.PlayerScores[stateManager.CurrentPlayerId] >= 6)
        {
            AddReward(1.0f); // Big reward for winning the game
            stateManager.PlayerAIs[(stateManager.CurrentPlayerId + 1) % stateManager.NumberOfPlayers].AddReward(-1.0f); // Big negative reward for the opponent losing
        }
        */
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

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // If we have rolled the dice but haven't clicked a stone yet, then we need to mask out any illegal stone clicks.
        if(stateManager.IsDoneClicking == false && stateManager.IsDoneRolling == true)
        {
            PlayerStone[] playerStones = Object.FindObjectsByType<PlayerStone>(FindObjectsSortMode.None);
            //playerStones = playerStones.OrderBy(ps => ps.PlayerId).ThenBy(ps => ps.StoneId).ToArray();
            //PlayerStone[] legalStones = GetLegalMoves();
            PlayerStone[] myStones = playerStones.Where(ps => ps.PlayerId == stateManager.CurrentPlayerId).OrderBy(ps => ps.StoneId).ToArray();

            for(int i=0; i<6; i++)
            {
                if(myStones[i].CanLegallyMoveAhead(stateManager.DiceTotal) == false)
                {
                    actionMask.SetActionEnabled(0, i, false);
                }
                else
                {
                    actionMask.SetActionEnabled(0, i, true);
                }
            }
        }
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
