using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class Utils {

  public static IEnumerator MovePiece(GamePlayer player) {
    GameManager gm = GameManager.instance;

    while (player.movesLeft > 0) {
      MapSpot nextSpot;
      var spots = player.currentSpot.nextSpots;
      nextSpot = spots[spots.Keys.ToList()[0]];

      if (spots.Count > 1) {
        gm.MakePlayerChooseDirection(player, spots.Keys.ToList());
        
        while (gm.playerWantsToGo == MovementDirection.WAITING) {
          yield return null;
        }

        var ds = DirectionSelector.instance;
        ds.DisableButtons(spots.Keys.ToList());
        nextSpot = spots[gm.playerWantsToGo];
      }

      var finalStop = player.movesLeft == 1;

      yield return player
        .StartCoroutine(
          Utils.MoveMe(
            player,
            nextSpot,
            finalStop
          )
        );

      gm.playerWantsToGo = MovementDirection.WAITING;
      
      yield return new WaitForSeconds(Constants.MOVE_DELAY);
      player.movesLeft--;
    }

    player.StartCoroutine(player.LandOnSpot());
  }

  public static IEnumerator MoveMe(GamePlayer player, MapSpot nextSpot, bool finalStop)
  {
    var target = nextSpot.gameObject.transform.position;
    var transform = player.gameObject.transform;
    var spotCount = nextSpot.currentPieces.Count;

    target = Utils.CalculatePosition(target, spotCount + 1,  spotCount);
  
    yield return player.StartCoroutine(
      Utils.MoveMe(
        transform,
        target,
        Constants.MOVE_TIME
      )
    );
  }

  public static IEnumerator MoveMe(Transform transform, Vector3 target, float moveTime)
  {
    var t = 0f;
    var position = transform.position;

    while(t < moveTime) {
      t += Time.deltaTime;
      var frac = t/moveTime;

      transform.position = Vector3.Lerp(position, target, frac);
      yield return null;
    }

    yield return null;
  }

  public static void CalculateRanks(List<GamePlayer> players) {
    var sorted = players
      .OrderByDescending(x => x.playerEmblems)
      .ThenByDescending(x => x.playerCash)
      .ToList();
    GamePlayer lastPlayer = new GamePlayer();

    for (var i = 0; i < Constants.MAX_PLAYERS; i++) {
      var thisPlayer = sorted[i];

      Utils.AssignRank(ref thisPlayer, ref lastPlayer, i);
    }
  }

  public static void AssignRank(
    ref GamePlayer player,
    ref GamePlayer lastPlayer,
    int index
  ) {
    var value = Utils.isRankSame(lastPlayer, player) ?
      lastPlayer.myRank.rank :
      index + 1;
    PlayerRank rank = new PlayerRank(){
      playerId = player.playerId,
      rank = value
    };

    player.myRank = rank;
    lastPlayer = player;
  }

  public static bool isRankSame(GamePlayer r1, GamePlayer r2) {
    var sameCash = (r1.playerCash == r2.playerCash);
    var sameEmblems = (r1.playerEmblems == r2.playerEmblems);

    return (sameCash && sameEmblems);
  }

  public static void MakePlayers(
    ref List<GamePlayer> players,
    GameObject pPrefab,
    GameObject cPrefab,
    List<GameObject> dicePrefab,
    Transform first,
    Transform diceParent
  ) {
    for (var i = 0; i < Constants.MAX_PLAYERS; i++) {
      var numHumans = PlayerPrefs.GetInt("PlayerCount", 0) + 1;
      var isCPU = !(i < numHumans);
      var prefab = (isCPU) ? cPrefab : pPrefab;
      var thisPlayer = Utils.MakePlayer(
        i,
        prefab,
        dicePrefab[i],
        first,
        diceParent,
        isCPU
      );

      players.Add(thisPlayer);
    }
  }

  public static GamePlayer MakePlayer(
    int playerId,
    GameObject prefab,
    GameObject dicePrefab,
    Transform first,
    Transform diceParent,
    bool isCPU
  ) {
    
    var playerGO = GameObject.Instantiate(prefab, first.position, first.rotation) as GameObject;
    var thisPlayer = playerGO.GetComponent<GamePlayer>();
    GameObject dice = GameObject.Instantiate(dicePrefab, diceParent) as GameObject;

    thisPlayer.playerId = playerId;
    thisPlayer.myDice = dice.GetComponent<DiceRoller>();
    thisPlayer.myRank = new PlayerRank() {
      playerId = playerId,
      rank = 1
    };
    thisPlayer.isCPU = isCPU;
    
    if(isCPU){
      thisPlayer.myCom = playerGO.GetComponent<ComPlayerController>();
    }

    return thisPlayer;
  }

  public static void DetermineTurnOrder(
    ref List<PlayerRoll> rolls,
    ref List<GamePlayer> players,
    ref List<Text> pStats,
    ref List<Text> pRanks
  ) {
    var newPlayerList = new List<GamePlayer>();
    IEnumerable<PlayerRoll> query = rolls.OrderByDescending(roll => roll.value);
    var i = 0;

    foreach (PlayerRoll roll in query) {
      var player = players[roll.playerId];

      player.turnOrder = i;
      player.UI_Stats = pStats[i];
      player.UI_Rank = pRanks[i];
      player.playerCash = Constants.PLAYER_START_CASH;
      player.myState = PlayerState.IDLE;
      newPlayerList.Add(player);
      i++;
    }

    players = newPlayerList;
  }

  public static void RedistributeWealth(Vector3 pos, ref List<GameObject> currentPieces) {
    var count = currentPieces.Count;

    if (count == 0) return;

    for (var i = 0; i < count; i++) {
      var piece = currentPieces[i];
      var player = piece.GetComponent<GamePlayer>();
      var position = Utils.CalculatePosition(pos, count, i);

      if (player.myState == PlayerState.MOVING) continue;

      player.StartCoroutine(Utils.MoveMe(piece.transform, position, Constants.MOVE_TIME/2f)); 
    }
  }

  public static Vector3 CalculatePosition(Vector3 pos, int count, int index) {
    if (count == 1) {
      return new Vector3(pos.x, pos.y + Constants.PIECE_Y_OFFSET, pos.z);
    }

    var angle = (2f * Mathf.PI) / count;
    var thisAngle = angle * index;
    var dist = Constants.PIECE_DISTANCE;
    var val1 = Mathf.Cos(thisAngle) * dist;
    var val2 =  Mathf.Sin(thisAngle) * dist;
    var posX = val1 + pos.x;
    var posY = pos.y + Constants.PIECE_Y_OFFSET;
    var posZ = val2 + pos.z;

    return new Vector3(posX, posY, posZ);
  }
}
