using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System;

public class Generator3D : MonoBehaviour {
    enum CellType {
        None,
        Room,
        Hallway,
        Stairs
    }

    class Room {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size) {
            bounds = new BoundsInt(location, size);
        }

        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }

    [SerializeField]
    Vector3Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector3Int roomMaxSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;
    [SerializeField]
    Material greenMaterial;
    [SerializeField]
    GameObject floorPrefab;
    [SerializeField]
    GameObject wallPrefab;
    [SerializeField]
    GameObject ceilingPrefab;
    [SerializeField]
    GameObject stairPrefab;
    [SerializeField]
    bool showCube;

    Random random;
    Grid3D<CellType> grid;
    Grid3D<bool> gridIsPlaced;
    Grid3D<bool> gridIsChecked;
    List<Vector3> doors;
    List<Room> rooms;
    List<List<Vector3Int>> paths;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;

    Vector3 floorBoundsRatio;
    Vector3 wallBoundsRatio;

    void Start() {
        random = new Random(0);
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        gridIsPlaced = new Grid3D<bool>(size, Vector3Int.zero);
        gridIsChecked = new Grid3D<bool>(size, Vector3Int.zero);
        rooms = new List<Room>();
        doors = new List<Vector3>();
        paths = new List<List<Vector3Int>>();

        Bounds floorBounds = floorPrefab.GetComponent<Renderer>().bounds;
        floorBoundsRatio = new Vector3((1 / floorBounds.size.x), (1 / floorBounds.size.x * floorBounds.size.y), (1 / floorBounds.size.z));

        Bounds wallBounds = wallPrefab.GetComponent<Renderer>().bounds;
        wallBoundsRatio = new Vector3((1 / wallBounds.size.y * wallBounds.size.x), (1 / wallBounds.size.y), (1 / wallBounds.size.z));

        PlaceRooms();
        
        Triangulate();
        CreateHallways();
        PathfindHallways();
        // StartCoroutine(PathfindHallways());
        PlaceWalls();
        ReplaceRooms();
    }

    void PlaceRooms() {
        for (int i = 0; i < roomCount; i++) {
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                random.Next(0, size.y),
                random.Next(0, size.z)
            );

            Vector3Int roomSize = new Vector3Int(
                random.Next(1, roomMaxSize.x + 1),
                random.Next(1, roomMaxSize.y + 1),
                random.Next(1, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), roomSize + new Vector3Int(2, 0, 2));

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z) {
                add = false;
            }

            if (add) {
                rooms.Add(newRoom);
                if (showCube)
                {
                    PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);
                }

                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void ReplaceRooms()
    {
        // get ratio of cube to room prefab and resize room prefab and replace

        foreach (var room in rooms)
        {
            foreach (var pos in room.bounds.allPositionsWithin)
            {
                
            }

            for (int i = 0; i < room.bounds.size.x; i++)
            {
                for (int j = 0; j < room.bounds.size.z; j++)
                {
                    PlaceFloor(room.bounds.position + new Vector3Int(i, 0, j) + new Vector3(0.5f, 0, 0.5f), floorBoundsRatio);
                }
            }

            for (int i = 0; i < room.bounds.size.y; i++)
            {
                for (int j = 0; j < room.bounds.size.z; j++)
                {
                    var wallPos = room.bounds.position + new Vector3Int(0, i, j);

                    // Debug.Log("wallPosz: " + wallPos);

                    if (!doors.Contains((Vector3)wallPos))
                    {
                        PlaceWall(wallPos + new Vector3(0, 0.5f, 0.5f), wallBoundsRatio);
                    }
                    else
                    {
                        if ((grid[wallPos + Vector3Int.left] == CellType.None || grid[wallPos + Vector3Int.right] == CellType.None) && wallPos == room.bounds.position)
                        {
                            PlaceWall(wallPos + new Vector3(0, 0.5f, 0.5f), wallBoundsRatio);
                        }
                    }

                    var oppositeWallPos = room.bounds.position + new Vector3Int(room.bounds.size.x, i, j);

                    // Debug.Log("oppositeWallPosz: " + oppositeWallPos);

                    if (!doors.Contains((Vector3)oppositeWallPos))
                    {
                        PlaceWall(oppositeWallPos + new Vector3(0, 0.5f, 0.5f), wallBoundsRatio);
                    }
                    else
                    {
                        if ((grid[oppositeWallPos + Vector3Int.left] == CellType.None || grid[oppositeWallPos + Vector3Int.right] == CellType.None) && oppositeWallPos == room.bounds.position)
                        {
                            PlaceWall(oppositeWallPos + new Vector3(0, 0.5f, 0.5f), wallBoundsRatio);
                        }
                    }
                }
            }

            for (int i = 0; i < room.bounds.size.y; i++)
            {
                for (int j = 0; j < room.bounds.size.x; j++)
                {
                    var wallPos = room.bounds.position + new Vector3Int(j, i, 0);

                    // Debug.Log("wallPosx: " + wallPos);

                    if (!doors.Contains((Vector3)wallPos))
                    {
                        PlaceWall(wallPos + new Vector3(0.5f, 0.5f, 0), wallBoundsRatio, 90f);
                    }
                    else
                    {
                        if ((grid[wallPos + Vector3Int.forward] == CellType.None || grid[wallPos + Vector3Int.back] == CellType.None) && wallPos == room.bounds.position)
                        {
                            PlaceWall(wallPos + new Vector3(0.5f, 0.5f, 0), wallBoundsRatio, 90f);
                        }
                    }

                    var oppositeWallPos = room.bounds.position + new Vector3Int(j, i, room.bounds.size.z);

                    // Debug.Log("oppositeWallPosx: " + oppositeWallPos);

                    if (!doors.Contains((Vector3)oppositeWallPos))
                    {
                        PlaceWall(oppositeWallPos + new Vector3(0.5f, 0.5f, 0), wallBoundsRatio, 90f);
                    }
                    else
                    {
                        if ((grid[oppositeWallPos + Vector3Int.forward] == CellType.None || grid[oppositeWallPos + Vector3Int.back] == CellType.None) && oppositeWallPos == room.bounds.position)
                        {
                            PlaceWall(oppositeWallPos + new Vector3(0.5f, 0.5f, 0), wallBoundsRatio, 90f);
                        }
                    }
                }
            }
        }
    }

    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector3)room.bounds.position + new Vector3(room.bounds.size.x / 2, 0, room.bounds.size.z / 2), room));
        }

        delaunay = Delaunay3D.Triangulate(vertices);
    }

    void CreateHallways() {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> minimumSpanningTree = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(minimumSpanningTree);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways() {
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector3Int((int)startPosf.x, (int)startRoom.bounds.position.y, (int)startPosf.z);
            var endPos = new Vector3Int((int)endPosf.x, (int)endRoom.bounds.position.y, (int)endPosf.z);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost();

                var delta = b.Position - a.Position;

                if (delta.y == 0) {
                    //flat hallway
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    //heuristic

                    if (grid[b.Position] == CellType.Stairs) {
                        return pathCost;
                    } else if (grid[b.Position] == CellType.Room) {
                        pathCost.cost += 5;
                    } else if (grid[b.Position] == CellType.None) {
                        pathCost.cost += 1;
                    }

                    pathCost.traversable = true;
                } else {
                    //staircase
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) return pathCost;

                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    //base cost + heuristic

                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset)) {
                        return pathCost;
                    }

                    if (grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None) {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;
                    }

                    paths.Add(path);

                    if (i > 0) {
                        var prev = path[i - 1];

                        var delta = current - prev;

                        if (delta.y != 0) {
                            Bounds stairBounds = stairPrefab.GetComponent<Renderer>().bounds;

                            float stairBoundsRatioX = 0f;
                            float stairBoundsRatioZ = 1 / stairBounds.size.z;
                            float stairBoundsRatioY = Mathf.Abs(delta.y) / stairBounds.size.y;
                            float rotation = 0f;
                            float wallRotation = 0f;
                            Vector3 wallOffset = Vector3.zero;
                            Vector3 oppositeWallOffset = Vector3.zero;
                            Vector3 endWallCenterOffset = Vector3.zero;

                            if (delta.x == 0)
                            {
                                if ((delta.y > 0 && delta.z > 0) || (delta.y < 0 && delta.z < 0))
                                {
                                    rotation = -90f;
                                    stairBoundsRatioX = (Mathf.Abs(delta.z) - 1f) / stairBounds.size.x;
                                }
                                else if ((delta.y < 0 && delta.z > 0) || (delta.y > 0 && delta.z < 0))
                                {
                                    rotation = 90f;
                                    stairBoundsRatioX = (Mathf.Abs(delta.z) - 1f) / stairBounds.size.x;
                                }

                                wallOffset = new Vector3(0f, 0.5f, 0.5f);
                                oppositeWallOffset = new Vector3(1f, 0.5f, 0.5f);
                                endWallCenterOffset = new Vector3(0.5f, 0.5f, 0f);
                            }
                            else
                            {
                                if ((delta.y > 0 && delta.x > 0) || (delta.y < 0 && delta.x < 0))
                                {
                                    stairBoundsRatioX = (Mathf.Abs(delta.x) - 1f)/ stairBounds.size.x;
                                }
                                else if ((delta.y < 0 && delta.x > 0) || (delta.y > 0 && delta.x < 0))
                                {
                                    stairBoundsRatioX = -(Mathf.Abs(delta.x) - 1f) / stairBounds.size.x;
                                }

                                wallRotation = 90f;
                                wallOffset = new Vector3(0.5f, 0.5f, 0f);
                                oppositeWallOffset = new Vector3(0.5f, 0.5f, 1f);
                                endWallCenterOffset = new Vector3(0f, 0.5f, 0.5f);
                            }       

                            int xDir = Mathf.Clamp(delta.x, -1, 1);
                            int zDir = Mathf.Clamp(delta.z, -1, 1);
                            Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                            Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                            Vector3 verticalPlacement = delta.y < 0 ? verticalOffset : Vector3.zero;

                            PlaceStair(prev + verticalPlacement + (Vector3)horizontalOffset * 1.5f + new Vector3(0.5f, 0.5f, 0.5f), new Vector3(stairBoundsRatioX, stairBoundsRatioY, stairBoundsRatioZ), rotation);
                            
                            grid[prev + horizontalOffset] = CellType.Stairs;
                            grid[prev + horizontalOffset * 2] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset * 2] = CellType.Stairs;

                            Vector3 endWallVerticalOffset = Vector3.zero;
                            Vector3 endWallHorizontalOffset = Vector3.zero;

                            if (delta.y > 0)
                            {
                                endWallVerticalOffset = verticalOffset;
                                if (delta.x > 0 || delta.z > 0)
                                {
                                    endWallHorizontalOffset += horizontalOffset;
                                }
                            }
                            else
                            {
                                endWallHorizontalOffset = horizontalOffset * 2;
                                if (delta.x > 0 || delta.z > 0)
                                {
                                    endWallHorizontalOffset += horizontalOffset;
                                }
                            }

                            //TODO: Remove walls between double stairs

                            PlaceWall(prev + horizontalOffset + wallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + horizontalOffset * 2 + wallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + verticalOffset + horizontalOffset + wallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + verticalOffset + horizontalOffset * 2 + wallOffset, wallBoundsRatio, wallRotation);

                            PlaceWall(prev + horizontalOffset + oppositeWallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + horizontalOffset * 2 + oppositeWallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + verticalOffset + horizontalOffset + oppositeWallOffset, wallBoundsRatio, wallRotation);
                            PlaceWall(prev + verticalOffset + horizontalOffset * 2 + oppositeWallOffset, wallBoundsRatio, wallRotation);

                            PlaceWall(prev + endWallVerticalOffset + endWallHorizontalOffset + endWallCenterOffset, wallBoundsRatio, Mathf.Abs(wallRotation - 90f));

                            if (showCube)
                            {
                                PlaceStairs(prev + horizontalOffset);
                                PlaceStairs(prev + horizontalOffset * 2);
                                PlaceStairs(prev + verticalOffset + horizontalOffset);
                                PlaceStairs(prev + verticalOffset + horizontalOffset * 2);
                            }
                        }

                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f), current + new Vector3(0.5f, 0.5f, 0.5f), Color.blue, 100, false);
                    }
                }
                
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];
                    
                    if (grid[current] == CellType.Hallway) {
                        if (!gridIsPlaced[current])
                        {
                            PlaceFloor(current + new Vector3(0.5f, 0f, 0.5f), floorBoundsRatio);

                            gridIsPlaced[current] = true;
                        }
                        if (showCube)
                        {
                            PlaceHallway(current);
                        }

                        // yield return new WaitForSeconds(0.05f);
                    }
                }
            }
        }

        // PlaceWalls();
        // yield return new WaitForSeconds(0.05f);
        // ReplaceRooms();
    }

    void PlaceWalls()
    {
        foreach (var path in paths)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var pos = path[i];

                if (grid[path[i]] == CellType.Hallway && !gridIsChecked[pos])
                {
                    if (!grid.InBounds(pos + Vector3Int.left) || grid[pos + Vector3Int.left] == CellType.None)
                    {
                        PlaceWall(pos + new Vector3(0f, 0.5f, 0.5f), wallBoundsRatio);
                    }
                    
                    if (!grid.InBounds(pos + Vector3Int.right) || grid[pos + Vector3Int.right] == CellType.None)
                    {
                        PlaceWall(pos + new Vector3(1f, 0.5f, 0.5f), wallBoundsRatio);
                    }
                    
                    if (!grid.InBounds(pos + Vector3Int.forward) || grid[pos + Vector3Int.forward] == CellType.None)
                    {
                        PlaceWall(pos + new Vector3(0.5f, 0.5f, 1f), wallBoundsRatio, 90f);
                    }
                    
                    if (!grid.InBounds(pos + Vector3Int.back) || grid[pos + Vector3Int.back] == CellType.None)
                    {
                        PlaceWall(pos + new Vector3(0.5f, 0.5f, 0f), wallBoundsRatio, 90f);
                    }

                    if (grid[path[i - 1]] == CellType.Room)
                    {
                        var delta = pos - path[i - 1];

                        if (delta == Vector3Int.left)
                        {
                            doors.Add(path[i - 1]);
                        }
                        else if (delta == Vector3Int.right)
                        {
                            doors.Add(pos);
                        }
                        else if (delta == Vector3Int.forward)
                        {
                            doors.Add(pos);
                        }
                        else if (delta == Vector3Int.back)
                        {
                            doors.Add(path[i - 1]);
                        }
                    }

                    if (grid[path[i + 1]] == CellType.Room)
                    {
                        var delta = path[i + 1] - pos;

                        if (delta == Vector3Int.left)
                        {
                            doors.Add(pos);
                        }
                        else if (delta == Vector3Int.right)
                        {
                            doors.Add(path[i + 1]);
                        }
                        else if (delta == Vector3Int.forward)
                        {
                            doors.Add(path[i + 1]);
                        }
                        else if (delta == Vector3Int.back)
                        {
                            doors.Add(pos);
                        }
                    }
                    
                    gridIsChecked[pos] = true;
                }
            }
        }
    }

    void PlaceWall(Vector3 location, Vector3 wallBoundsRatio, float rotation = 0f)
    {
        GameObject wall = Instantiate(wallPrefab, location, Quaternion.identity);
        wall.transform.localScale = wallBoundsRatio;
        Bounds currentWallBounds = wall.GetComponent<Renderer>().bounds;
        wall.transform.position += wall.transform.position - currentWallBounds.center;
        wall.transform.rotation = Quaternion.Euler(0, rotation, 0);
    }

    void PlaceFloor(Vector3 location, Vector3 floorBoundsRatio)
    {
        GameObject floor = Instantiate(floorPrefab, location, Quaternion.identity);
        floor.transform.localScale = floorBoundsRatio;
        Bounds currentFloorBounds = floor.GetComponent<Renderer>().bounds;
        floor.transform.position += floor.transform.position - currentFloorBounds.center;
    }

    void PlaceStair(Vector3 location, Vector3 stairBoundsRatio, float rotation = 0f)
    {
        GameObject stair = Instantiate(stairPrefab, location, Quaternion.identity);
        stair.transform.localScale = stairBoundsRatio;
        Bounds currentStairBounds = stair.GetComponent<Renderer>().bounds;
        stair.transform.position += stair.transform.position - currentStairBounds.center;
        stair.transform.rotation = Quaternion.Euler(0, rotation, 0);
    }

    Bounds GetPrefabBounds(Renderer[] renderers)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var renderer in renderers)
        {
            if (bounds.extents == Vector3.zero)
            {
                bounds = renderer.bounds;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    void PlaceCube(Vector3Int location, Vector3Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity);
        go.GetComponent<Transform>().localScale = size;
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector3Int location, Vector3Int size) {
        PlaceCube(location, size, redMaterial);
    }

    void PlaceHallway(Vector3Int location) {
        PlaceCube(location, new Vector3Int(1, 1, 1), blueMaterial);
    }

    void PlaceStairs(Vector3Int location) {
        PlaceCube(location, new Vector3Int(1, 1, 1), greenMaterial);
    }
}
