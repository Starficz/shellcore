﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LandPlatformGenerator : MonoBehaviour {


    // TODO: create nodes, create paths, create platform generation based on blueprint
    public static LandPlatformGenerator instance { private set; get; }
    public LandPlatform blueprint;
    private List<GameObject> tiles;
    private List<Rect> areas;
    private List<NavigationNode> nodes;

    private Dictionary<NavigationNode, int> areaIDByNode;
    private float tileSize;
    private Color color;
    private Vector2 offset;
    public static bool CheckOnGround(Vector3 position)
    {
        for (int i = 0; i < instance.tiles.Count; i++)
        {
            if (instance.tiles[i].GetComponent<SpriteRenderer>().bounds.Contains(position))
            {
                return true;
            }
        }
        return false;
    }

    public void SetColor(Color color) {
        this.color = color;
    }
    public void BuildTiles(LandPlatform platform) {

        blueprint = platform;

        if (!blueprint || blueprint.prefabs.Length <= 0)
            return;
        
        if(tiles != null) Unload();

        tileSize = blueprint.prefabs[0].GetComponent<SpriteRenderer>().bounds.size.x;

        var cols = blueprint.columns;
        var rows = blueprint.rows;
        offset = new Vector2 
        {
            x = -tileSize * (cols-1)/2,
            y = +tileSize * (rows-1)/2
        };

        tiles = new List<GameObject>();
        areas = new List<Rect>();

        for(int i = 0; i < blueprint.tilemap.Length; i++) {

            var pos = new Vector3
            {
                x = offset.x + tileSize * (i % cols),
                y = offset.y - tileSize * (i / cols),
                z = 0
            };

            switch(blueprint.tilemap[i]) {
                case -1:
                    break;
                default:
                    var tile = Instantiate(blueprint.prefabs[blueprint.tilemap[i]], pos, Quaternion.identity);
                    if(color != null) tile.GetComponent<SpriteRenderer>().color = color;
                    tiles.Add(tile);
                    tile.transform.parent = transform;
                    areas.Add(new Rect(pos.x, pos.y, tileSize, tileSize));
                    break;
            }
        }
        BuildNodes();
    }

    private void Awake()
    {
        instance = this;
    }

    public void Unload()
    {
        for(int i = 0; i < tiles.Count; i++)
        {
            Destroy(tiles[i]);
        }
        tiles.Clear();
    }

    struct NavigationNode
    {
        public static bool operator ==(NavigationNode x, NavigationNode y)
        {
            return x.pos == y.pos;
        }
        public static bool operator !=(NavigationNode x, NavigationNode y)
        {
            return x.pos != y.pos;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NavigationNode))
            {
                return false;
            }

            var node = (NavigationNode)obj;
            return pos.Equals(node.pos);
        }

        public override int GetHashCode()
        {
            return 991532785 + EqualityComparer<Vector2>.Default.GetHashCode(pos);
        }

        public Vector2 pos;
        public List<NavigationNode> neighbours;
        public List<float> distances;
        public NavigationNode(Vector2 position)
        {
            pos = position;
            neighbours = new List<NavigationNode>();
            distances = new List<float>();
        }
    }
    void BuildNodes()
    {
        Debug.Log("Building nodes...");

        float dToCenter = tileSize / 3f; // node distance to center on one axis
        nodes = new List<NavigationNode>();
        areaIDByNode = new Dictionary<NavigationNode, int>();
        for (int i = 0; i < blueprint.rows; i++)
        {
            for (int j = 0; j < blueprint.columns; j++)
            {
                if (blueprint.tilemap[i * blueprint.columns + j] > -1)
                {
                    //create nodes
                    if (!isValidTile(i, j))
                        continue;

                    bool right = isValidTile(i, j + 1);
                    bool up = isValidTile(i - 1, j);
                    bool left = isValidTile(i, j - 1);
                    bool down = isValidTile(i + 1, j);

                    // Debug.Log(right + "" + up + left + down + " " + ((i - 1) * blueprint.columns + j + 1) + " " + isValidTile(i - 1, j + 1) + " " + (i * blueprint.columns + j));
                    if ((!right && !up) || (right && up && !isValidTile(i - 1, j + 1))) //check if the tile is a corner
                        nodes.Add(new NavigationNode(new Vector2(j * tileSize + dToCenter, -i * tileSize + dToCenter) + offset));
                    if ((!left && !up) || (left && up && !isValidTile(i - 1, j - 1)))
                        nodes.Add(new NavigationNode(new Vector2(j * tileSize - dToCenter, -i * tileSize + dToCenter) + offset));
                    if ((!left && !down) || (left && down && !isValidTile(i + 1, j - 1)))
                        nodes.Add(new NavigationNode(new Vector2(j * tileSize - dToCenter, -i * tileSize - dToCenter) + offset));
                    if ((!right && !down) || (right && down && !isValidTile(i + 1, j + 1)))
                        nodes.Add(new NavigationNode(new Vector2(j * tileSize + dToCenter, -i * tileSize - dToCenter) + offset));
                }
            }
        }

        int debugCount = 0;

        //connect nodes
         Debug.Log("Connecting nodes...");
         int currentAreaID = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if(!areaIDByNode.ContainsKey(nodes[i])) {
                areaIDByNode.Add(nodes[i], currentAreaID++);
            }
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (isInLoS(nodes[i].pos, nodes[j].pos))
                {
                    if(!areaIDByNode.ContainsKey(nodes[j])) 
                    {
                        areaIDByNode.Add(nodes[j], areaIDByNode[nodes[i]]);
                    }
                    nodes[i].neighbours.Add(nodes[j]);
                    nodes[j].neighbours.Add(nodes[i]);
                    float d = (nodes[i].pos - nodes[j].pos).magnitude;
                    nodes[i].distances.Add(d);
                    nodes[j].distances.Add(d);
                    debugCount++;
                }
            }
        }

        Debug.Log("Done! Nodes: " + nodes.Count + " Connections: " + debugCount);
    }

    bool isValidTile(int x, int y)
    {
        bool limitCheck = x < blueprint.rows && y < blueprint.columns && x >= 0 && y >= 0;
        bool selfIsTile = limitCheck && blueprint.tilemap[x * blueprint.columns + y] > -1;
        bool selfIsWithinLengthLimit = selfIsTile && blueprint.tilemap[x * blueprint.columns + y] < blueprint.prefabs.Length;
        bool final = selfIsWithinLengthLimit && blueprint.prefabs[blueprint.tilemap[x * blueprint.columns + y]] != null;
        return final;
    }

    private void OnDrawGizmosSelected()
    {
        if (blueprint == null)
            return;

        var v3 = Input.mousePosition;
        v3.z = 10.0f;
        v3 = Camera.main.ScreenToWorldPoint(v3);
        Vector2 mPos = v3;

        if (areas != null)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                if (new Rect(-0.5f * tileSize + offset.x, -0.5f * tileSize - offset.y, blueprint.columns * tileSize, blueprint.rows * tileSize).Contains(mPos))
                {
                    int x = -Mathf.FloorToInt((mPos.y - offset.y )/ tileSize + 0.5f);
                    int y = Mathf.FloorToInt((mPos.x - offset.x) / tileSize + 0.5f);
                    if (isValidTile(x, y))
                    {
                        Gizmos.color = new Color(0, 100, 150);
                        Gizmos.DrawCube(new Vector3(y * tileSize, -x * tileSize, 0) + (Vector3)offset, new Vector3(tileSize, tileSize, 0));
                    }
                }
            }
        }

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                Gizmos.color = new Color(0, 100, 255);
                Gizmos.DrawSphere(nodes[i].pos, 0.2f);
                UnityEditor.Handles.Label(nodes[i].pos + Vector2.right, nodes[i].pos.ToString());
                Gizmos.color = new Color(200, 0, 0, 100);
                for (int j = 0; j < nodes[i].neighbours.Count; j++)
                {
                    Gizmos.DrawLine(nodes[i].pos, nodes[i].neighbours[j].pos);
                }
            }
        }
    }
    bool isInLoS(Vector2 p1, Vector2 p2)
    {
        Vector2 p12 = (p1 - offset) / tileSize; //+ Vector2.one * 0.5f;
        p12.x += 0.5f;
        p12.y = -p12.y + 0.5f;
        Vector2 p22 = (p2 - offset) / tileSize; //+ Vector2.one * 0.5f;
        p22.x += 0.5f;
        p22.y = -p22.y + 0.5f;

        float d = (p22 - p12).magnitude;

        Vector2 step = (p22 - p12) / (d * 10f);
        Vector2 point = p12;
        float stepLength = step.magnitude;
        //TODO: get normals, use them
        //Debug.Log(p12 + " " + p22 + step);
        for (float i = 0; i < d; i += stepLength)
        {
            //Debug.Log((int)checkPoint.y + " " + (int)checkPoint.x);
            if (!isValidTile(Mathf.FloorToInt(point.y), Mathf.FloorToInt(point.x)))
            {
                //Debug.Log("failed" + p1 + " " + p2);
                return false;
            }
            point += step;
        }

        //Debug.Log("passed" + p1 + " " + p2);
        return true;
    }

    private class PathfindNode
    {
        public NavigationNode node;
        public PathfindNode parent;
        public float d;

        public PathfindNode(NavigationNode node, PathfindNode parent, float distance)
        {
            this.node = node;
            this.parent = parent;
            d = distance;
        }
    }
    public static Vector2[] pathfind(Vector2 startPos, Vector2 targetPos)
    {
        if (instance.blueprint == null)
            return null;

        if (instance.nodes == null)
            return null;

        if (startPos == targetPos)
            return null;

        if (instance.isInLoS(startPos, targetPos)) {
            return new Vector2[] {targetPos};
        }
        //find node closest to start and end positions
        NavigationNode start = getNearestNode(startPos, true);
        NavigationNode end = getNearestNode(targetPos, true);

        var openList = new List<PathfindNode>();
        var closedList = new List<PathfindNode>();

        //TODO: try adding all nodes in LoS to open list
        openList.Add(new PathfindNode(start, null, 0f));
        
        while (openList.Count > 0)
        {
            // Get next node with shortest total distance
            PathfindNode current = openList[0];
            float shortest = float.MaxValue;
            for (int i = 0; i < openList.Count; i++)
            {
                if(openList[i].d < shortest)
                {
                    shortest = openList[i].d;
                    current = openList[i];
                }
            }

            // Check if the goal has been reached
            if(current.node == end)
            {
                var path = new List<Vector2>();
                PathfindNode node = current;
                do
                {
                    Debug.Log(node.node.pos);
                    path.Add(node.node.pos);
                    node = node.parent;
                }
                while (node != null && node.parent != null);

                // Try skipping the first node
                if(!instance.isInLoS(path[path.Count - 1], startPos))
                    path.Add(node.node.pos);

                 if(instance.isInLoS(end.pos, targetPos)) 
                {
                    // if the second last path is in the line of sight you can skip the last one to the target
                    if(path.Count > 1 && instance.isInLoS(path[1], targetPos)) {
                        path[0] = targetPos;
                    } else // otherwise add the target position as the last destination
                        path.Insert(0, targetPos);
                }
                return path.ToArray();
            }

            for(int i = 0; i < current.node.neighbours.Count; i++)
            {
                bool closed = false;
                for(int j = 0; j < closedList.Count; j++)
                {
                    if(closedList[j].node == current.node.neighbours[i])
                    {
                        closed = true;
                        break;
                    }
                }
                if(!closed)
                {
                    openList.Add(new PathfindNode(current.node.neighbours[i], current, current.d + current.node.distances[i]));
                }
            }

            openList.Remove(current);
            closedList.Add(current);
        }
        return null;
    }

    static NavigationNode getNearestNode(Vector2 pos, bool los = false)
    {
        Vector2 pos2 = pos;// + Vector2.one * 0.5f * instance.tileSize;
        NavigationNode nearest = new NavigationNode(Vector2.zero);
        float minD = float.MaxValue;
        for (int i = 0; i < instance.nodes.Count; i++)
        {
            if(los && !instance.isInLoS(instance.nodes[i].pos, pos2))
            {
                continue;
            }
            float d = (pos2 - instance.nodes[i].pos).sqrMagnitude;
            if (d < minD)
            {
                nearest = instance.nodes[i];
                minD = d;
            }
        }
        return nearest;
    }
}