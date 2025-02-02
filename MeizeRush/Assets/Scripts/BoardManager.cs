using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BoardManager : MonoBehaviour {
  public int boardRows, boardColumns;
  public int minRoomSize, maxRoomSize;
  [SerializeField]
  public TileMapVisualizer tileMapVisualizer;
  public GameObject floorTile;
  public HashSet<Vector2Int> walls;
  private GameObject[,] boardPositionsFloor;
  public HashSet<Vector2Int> roomFloors = new();
  public HashSet<Vector2Int> corridorFloors = new();
  public byte[,] map;
  public int scale;

  public class SubDungeon {
    public SubDungeon left, right;

    public Rect rect;

    public Rect room = new(-1, -1, 0, 0);

    public int debugId;
    private static int debugCounter = 0;
    public List<Rect> corridors = new();

    public SubDungeon(Rect mrect) {
      rect = mrect;
      debugId = debugCounter;
      debugCounter++;
    }
    public bool IAmLeaf() {
      if (left == null && right == null) {
        return true;
      }
      return false;
    }
    public bool Split(int minRoomSize, int maxRoomSize) {
      if (!IAmLeaf()) {
        return false;
      }
      bool splitH;
      if (rect.width / rect.height >= 1.25) {
        splitH = false;
      } else if (rect.height / rect.width >= 1.25) {
        splitH = true;
      } else {
        splitH = Random.Range(0.0f, 1.0f) > 0.5;
      }

      if (Mathf.Min(rect.height, rect.width) / 2 < minRoomSize) {
        // Debug.Log("Sub-dungeon " + debugId + " will be a leaf");
        return false;
      }

      if (splitH) {
        // split so that the resulting sub-dungeons widths are not too small
        // (since we are splitting horizontally)
        int split = Random.Range(minRoomSize, (int)(rect.width - minRoomSize));

        left = new SubDungeon(new Rect(rect.x, rect.y, rect.width, split));
        right = new SubDungeon(
            new Rect(rect.x, rect.y + split, rect.width, rect.height - split));
      } else {
        int split = Random.Range(minRoomSize, (int)(rect.height - minRoomSize));
        left = new SubDungeon(new Rect(rect.x, rect.y, split, rect.height));
        right = new SubDungeon(
            new Rect(rect.x + split, rect.y, rect.width - split, rect.height));
      }

      return true;
    }
    public void CreateRoom() {

      if (left != null) {
        left.CreateRoom();
      }
      if (right != null) {
        right.CreateRoom();
      }
      if (left != null && right != null) {
        CreateCorridorBetween(left, right);
      }

      if (IAmLeaf()) {
        int roomWidth = (int)Random.Range(rect.width / 2, rect.width - 2);
        int roomHeight = (int)Random.Range(rect.height / 2, rect.height - 2);
        int roomX = (int)Random.Range(1, rect.width - roomWidth - 1);
        int roomY = (int)Random.Range(1, rect.height - roomHeight - 1);

        // room position will be absolute in the board, not relative to the
        // sub-dungeon
        room = new Rect(rect.x + roomX, rect.y + roomY, roomWidth, roomHeight);
        // Debug.Log("Created room " + room + " in sub-dungeon " + debugId + " "
        // + rect);
      }
    }
    public Rect GetRoom() {
      if (IAmLeaf()) {
        return room;
      }
      if (left != null) {
        Rect lroom = left.GetRoom();
        if (lroom.x != -1) {
          return lroom;
        }
      }
      if (right != null) {
        Rect rroom = right.GetRoom();
        if (rroom.x != -1) {
          return rroom;
        }
      }

      // workaround non nullable structs
      return new Rect(-1, -1, 0, 0);
    }
    public void CreateCorridorBetween(SubDungeon left, SubDungeon right) {
      Rect lroom = left.GetRoom();
      Rect rroom = right.GetRoom();

      // Debug.Log("Creating corridor(s) between " + left.debugId + "(" + lroom
      // + ") and " + right.debugId + " (" + rroom + ")");

      // attach the corridor to a random point in each room
      Vector2 lpoint =
          new Vector2((int)Random.Range(lroom.x + 1, lroom.xMax - 1),
                      (int)Random.Range(lroom.y + 1, lroom.yMax - 1));
      Vector2 rpoint =
          new Vector2((int)Random.Range(rroom.x + 1, rroom.xMax - 1),
                      (int)Random.Range(rroom.y + 1, rroom.yMax - 1));

      // always be sure that left point is on the left to simplify the code
      if (lpoint.x > rpoint.x) {
        Vector2 temp = lpoint;
        lpoint = rpoint;
        rpoint = temp;
      }

      int w = (int)(lpoint.x - rpoint.x);
      int h = (int)(lpoint.y - rpoint.y);

      // Debug.Log("lpoint: " + lpoint + ", rpoint: " + rpoint + ", w: " + w +
      // ", h: " + h);

      // if the points are not aligned horizontally
      if (w != 0) {
        // choose at random to go horizontal then vertical or the opposite
        if (Random.Range(0, 1) > 2) {
          // add a corridor to the right
          corridors.Add(new Rect(lpoint.x, lpoint.y, Mathf.Abs(w) + 1, 1));

          // if left point is below right point go up
          // otherwise go down
          if (h < 0) {
            corridors.Add(new Rect(rpoint.x, lpoint.y, 1, Mathf.Abs(h)));
          } else {
            corridors.Add(new Rect(rpoint.x, lpoint.y, 1, -Mathf.Abs(h)));
          }
        } else {
          // go up or down
          if (h < 0) {
            corridors.Add(new Rect(lpoint.x, lpoint.y, 1, Mathf.Abs(h)));
          } else {
            corridors.Add(new Rect(lpoint.x, rpoint.y, 1, Mathf.Abs(h)));
          }

          // then go right
          corridors.Add(new Rect(lpoint.x, rpoint.y, Mathf.Abs(w) + 1, 1));
        }
      } else {
        // if the points are aligned horizontally
        // go up or down depending on the positions
        if (h < 0) {
          corridors.Add(
              new Rect((int)lpoint.x, (int)lpoint.y, 1, Mathf.Abs(h)));
        } else {
          corridors.Add(
              new Rect((int)rpoint.x, (int)rpoint.y, 1, Mathf.Abs(h)));
        }
      }

      // Debug.Log("Corridors: ");
      foreach (Rect corridor in corridors) {
        // Debug.Log("corridor: " + corridor);
      }
    }
  }
  public void CreateBSP(SubDungeon subDungeon) {
    // Debug.Log("Splitting sub-dungeon " + subDungeon.debugId + ": " +
    // subDungeon.rect);
    if (subDungeon.IAmLeaf()) {
      // if the sub-dungeon is too large
      if (subDungeon.rect.width > maxRoomSize ||
          subDungeon.rect.height > maxRoomSize ||
          Random.Range(0.0f, 1.0f) > 0.25) {

        if (subDungeon.Split(minRoomSize, maxRoomSize)) {
          // Debug.Log("Splitted sub-dungeon " + subDungeon.debugId + " in "
          //   + subDungeon.left.debugId + ": " + subDungeon.left.rect + ", "
          //   + subDungeon.right.debugId + ": " + subDungeon.right.rect);

          CreateBSP(subDungeon.left);
          CreateBSP(subDungeon.right);
        }
      }
    }
  }
  public void DrawRooms(SubDungeon subDungeon) {
    if (subDungeon == null) {
      return;
    }
    if (subDungeon.IAmLeaf()) {
      HashSet<Vector2Int> floors = new HashSet<Vector2Int>();
      for (int i = (int)subDungeon.room.x; i < subDungeon.room.xMax; i++) {
        for (int j = (int)subDungeon.room.y; j < subDungeon.room.yMax; j++) {
          Vector2Int pos = new Vector2Int(i, j);
          floors.Add(pos);
          roomFloors.Add(pos);
          // GameObject instance = Instantiate(floorTile, new Vector2(i, j),
          // Quaternion.identity) as GameObject;
          // instance.transform.SetParent(transform);
          // boardPositionsFloor[i, j] = instance;
        }
      }
      tileMapVisualizer.PaintFloorTiles(floors, 7);
    } else {
      DrawRooms(subDungeon.left);
      DrawRooms(subDungeon.right);
    }
  }
  void DrawCorridors(SubDungeon subDungeon) {
    if (subDungeon == null) {
      return;
    }

    DrawCorridors(subDungeon.left);
    DrawCorridors(subDungeon.right);
    foreach (Rect corridor in subDungeon.corridors) {
      HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
      for (int i = (int)corridor.x; i < corridor.xMax; i++) {
        for (int j = (int)corridor.y; j < corridor.yMax; j++) {
          if (boardPositionsFloor[i, j] == null) {
            Vector2Int pos = new Vector2Int(i, j);
            corridors.Add(pos);
            corridorFloors.Add(pos);

            // GameObject instance = Instantiate(corridorTile, new Vector3(i, j,
            // 0f), Quaternion.identity) as GameObject;
            // instance.transform.SetParent(transform);
            // boardPositionsFloor[i, j] = instance;
          }
        }
      }
      tileMapVisualizer.PaintCorridorTiles(corridors, 1);
    }
    // Start is called before the first frame update
  }
  public void getAllFloors(SubDungeon subDungeon) {
    if (subDungeon == null) {
      return;
    }
    if (subDungeon.IAmLeaf()) {
      for (int i = (int)subDungeon.room.x; i < subDungeon.room.xMax; i++) {
        for (int j = (int)subDungeon.room.y; j < subDungeon.room.yMax; j++) {
          roomFloors.Add(new Vector2Int(i, j));
        }
      }
    } else {
      getAllFloors(subDungeon.left);
      getAllFloors(subDungeon.right);
    }
  }
  public void getAllCorridors(SubDungeon subDungeon) {
    if (subDungeon == null) {
      return;
    }

    getAllCorridors(subDungeon.left);
    getAllCorridors(subDungeon.right);
    foreach (Rect corridor in subDungeon.corridors) {
      for (int i = (int)corridor.x; i < corridor.xMax; i++) {
        for (int j = (int)corridor.y; j < corridor.yMax; j++) {
          if (boardPositionsFloor[i, j] == null) {
            corridorFloors.Add(new Vector2Int(i, j));
            // GameObject instance = Instantiate(corridorTile, new Vector3(i, j,
            // 0f), Quaternion.identity) as GameObject;
            // instance.transform.SetParent(transform);
            // boardPositionsFloor[i, j] = instance;
          }
        }
      }
    }
  }
  void Start() {
    SubDungeon rootSubDungeon =
        new SubDungeon(new Rect(0, 0, boardRows, boardColumns));
    CreateBSP(rootSubDungeon);
    rootSubDungeon.CreateRoom();
    boardPositionsFloor = new GameObject[boardRows, boardColumns];
    DrawCorridors(rootSubDungeon);
    DrawRooms(rootSubDungeon);
    HashSet<Vector2Int> allTiles = new HashSet<Vector2Int>(roomFloors);
    allTiles.UnionWith(corridorFloors);
    walls = WallGenerator.CreateWalls(allTiles, tileMapVisualizer, 0);
    map = new byte[boardRows, boardColumns];
    for (int i = 0; i < boardRows - 1; i++) {
      for (int j = 0; j < boardColumns - 1; j++) {
        map[i, j] = 1;
      }
    }
    foreach (Vector2Int p in corridorFloors) {
      map[p.x, p.y] = 0;
    }
    foreach (Vector2Int p in roomFloors) {
      map[p.x, p.y] = 0;
    }
  }
}
public static class Direction2D {
  public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int> {
    new Vector2Int(0, 1),  // UP
    new Vector2Int(1, 0),  // RIGHT
    new Vector2Int(0, -1), // DOWN
    new Vector2Int(-1, 0)  // LEFT
  };

  public static List<Vector2Int> diagonalDirectionsList = new List<Vector2Int> {
    new Vector2Int(1, 1),   // UP-RIGHT
    new Vector2Int(1, -1),  // RIGHT-DOWN
    new Vector2Int(-1, -1), // DOWN-LEFT
    new Vector2Int(-1, 1)   // LEFT-UP
  };

  public static List<Vector2Int> eightDirectionsList = new List<Vector2Int> {
    new Vector2Int(0, 1),   // UP
    new Vector2Int(1, 1),   // UP-RIGHT
    new Vector2Int(1, 0),   // RIGHT
    new Vector2Int(1, -1),  // RIGHT-DOWN
    new Vector2Int(0, -1),  // DOWN
    new Vector2Int(-1, -1), // DOWN-LEFT
    new Vector2Int(-1, 0),  // LEFT
    new Vector2Int(-1, 1)   // LEFT-UP

  };

  public static Vector2Int GetRandomCardinalDirection() {
    return cardinalDirectionsList[UnityEngine.Random.Range(
        0, cardinalDirectionsList.Count)];
  }
}
