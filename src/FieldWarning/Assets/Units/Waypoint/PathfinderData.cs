﻿/**
 * Copyright (c) 2017-present, PFW Contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in
 * compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the License is
 * distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See
 * the License for the specific language governing permissions and limitations under the License.
 */

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Priority_Queue;
using EasyRoads3Dv3;

using PFW.Units.Component.Movement;

public class PathfinderData
{
    private const string GRAPH_FILE_SUFFIX = "_pathfinder_graph.dat";

    private static PathArc INVALID_ARC = new PathArc(null, null);
    private const float SPARSE_GRID_SPACING = 1000f * TerrainConstants.MAP_SCALE;
    private const float ROAD_GRID_SPACING = 200f * TerrainConstants.MAP_SCALE;
    private const float NODE_PRUNE_DIST_THRESH = 10f * TerrainConstants.MAP_SCALE;
    private const float ARC_MAX_DIST = 2500f * TerrainConstants.MAP_SCALE;

    public Terrain Terrain;
    public TerrainMap Map;
    public List<PathNode> Graph;

    /// <summary>
    /// If a node is in the open set, it has been seen
    /// but it hasn't been visited yet.
    /// </summary>
    private FastPriorityQueue<PathNode> _openSet;

    public PathfinderData(Terrain terrain)
    {
        Terrain = terrain;
        Map = new TerrainMap(terrain);
        Graph = new List<PathNode>();

        string sceneName = SceneManager.GetActiveScene().name;
        string scenePathWithFilename = SceneManager.GetActiveScene().path;
        string sceneDirectory = Path.GetDirectoryName(scenePathWithFilename);
        string graphFile = Path.Combine(sceneDirectory, sceneName + GRAPH_FILE_SUFFIX);

        if (!ReadGraph(graphFile)) {
            BuildGraph();
            WriteGraph(graphFile);
        }
    }

    /// <summary>
    /// Load the precomputed pathfinding graph from file.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool ReadGraph(string file)
    {
        if (!File.Exists(file))
            return false;

        try {
            Stream stream = File.Open(file, FileMode.Open);
            BinaryFormatter formatter = new BinaryFormatter();

            Graph = (List<PathNode>)formatter.Deserialize(stream);
            stream.Close();

            _openSet = new FastPriorityQueue<PathNode>(Graph.Count + 1);
            return true;
        } catch (Exception exception) {
            Debug.Log("Error reading graph file: " + exception.Message);
            return false;
        }
    }

    private void WriteGraph(String file)
    {
        Stream stream = File.Open(file, FileMode.Create);
        BinaryFormatter formatter = new BinaryFormatter();

        formatter.Serialize(stream, Graph);
        stream.Close();
    }

    private void BuildGraph()
    {
        Graph.Clear();
        Vector3 size = Terrain.terrainData.size;

        // TODO: Add nodes for terrain features

        // Add nodes for roads
        ERModularRoad[] roads = (ERModularRoad[])GameObject.FindObjectsOfType(typeof(ERModularRoad));
        foreach (ERModularRoad road in roads) {
            for (int i = 0; i < road.middleIndentVecs.Count; i++) {
                Vector3 roadVert = road.middleIndentVecs[i];
                if (Mathf.Abs(roadVert.x) < size.x/2 && Mathf.Abs(roadVert.z) < size.z/2)
                    Graph.Add(new PathNode(roadVert, true));

                if (i < road.middleIndentVecs.Count - 1) {
                    Vector3 nextRoadVert = road.middleIndentVecs[i+1];
                    Vector3 stretch = nextRoadVert - roadVert;
                    int nIntermediate = (int)(stretch.magnitude / ROAD_GRID_SPACING);
                    stretch /= 1 + nIntermediate;
                    Vector3 intermediatePosition = roadVert;

                    for (int j = 0; j < nIntermediate; j++) {
                        intermediatePosition += stretch;

                        bool mysteryComparison =
                            Mathf.Abs(intermediatePosition.x) < size.x / 2
                            &&
                            Mathf.Abs(intermediatePosition.z) < size.z / 2;
                        if (mysteryComparison)
                            Graph.Add(new PathNode(intermediatePosition, true));
                    }
                }
            }
        }

        /*// Fill in any big open spaces with a sparse grid in case the above missed anything important
        Vector3 newPos = new Vector3(0f, 0f, 0f);
        for (float x = -size.x/2; x < size.x/2; x += SparseGridSpacing / 10) {
            for (float z = -size.z/2; z < size.z/2; z += SparseGridSpacing / 10) {
                newPos.Set(x, 0f, z);

                float minDist = float.MaxValue;
                foreach (PathNode node in graph)
                    minDist = Mathf.Min(minDist, Vector3.Distance(newPos, node.position));

                if (minDist > SparseGridSpacing)
                    graph.Add(new PathNode(newPos));
            }
        }*/

        // Remove nodes that are right on top of each other
        for (int i = 0; i < Graph.Count; i++) {
            for (int j = i + 1; j < Graph.Count; j++) {
                if ((Position(Graph[i]) - Position(Graph[j])).magnitude < NODE_PRUNE_DIST_THRESH)
                    Graph.RemoveAt(j);
            }
        }

        _openSet = new FastPriorityQueue<PathNode>(Graph.Count + 1);

        // Compute arcs for all pairs of nodes within cutoff distance
        for (int i = 0; i < Graph.Count; i++) {
            for (int j = i + 1; j < Graph.Count; j++) {
                if ((Position(Graph[i]) - Position(Graph[j])).magnitude < ARC_MAX_DIST)
                    AddArc(Graph[i], Graph[j]);
            }
        }

        // Remove unnecessary arcs.
        // An arc in necessary if for any MobilityType, the direct path
        // between the nodes is at least as good as the optimal global path.
        // This is a brute force approach and it might be too slow.
        List<PathNode> path = new List<PathNode>();
        for (int i = 0; i < Graph.Count; i++) {
            for (int j = i + 1; j < Graph.Count; j++) {

                PathArc arc = GetArc(Graph[i], Graph[j]);
                if (arc.Equals(INVALID_ARC))
                    continue;

                bool necessary = false;
                foreach (MobilityType mobility in MobilityType.MobilityTypes) {
                    if (arc.Time[mobility.Index] == Pathfinder.FOREVER)
                        continue;

                    float time = FindPath(path,
                        Position(Graph[i]), Position(Graph[j]),
                        mobility, 0f, MoveCommandType.Fast);

                    if (arc.Time[mobility.Index] < 1.5 * time) {
                        necessary = true;
                        break;
                    }
                }

                if (!necessary)
                    RemoveArc(Graph[i], Graph[j]);
            }
        }
    }

    public PathArc GetArc(PathNode node1, PathNode node2)
    {
        for (int i = 0; i < node1.Arcs.Count; i++) {
            PathArc arc = node1.Arcs[i];
            if (arc.Node1 == node2 || arc.Node2 == node2)
                return arc;
        }
        return INVALID_ARC;
    }

    public void AddArc(PathNode node1, PathNode node2)
    {
        PathArc arc = new PathArc(node1, node2);
        node1.Arcs.Add(arc);
        node2.Arcs.Add(arc);

        // Compute the arc's traversal time for each MobilityType
        foreach (MobilityType mobility in MobilityType.MobilityTypes) {
            arc.Time[mobility.Index] = Pathfinder.FindLocalPath(
                this, Position(node1), Position(node2), mobility, 0f);
        }
    }

    public void RemoveArc(PathNode node1, PathNode node2)
    {
        PathArc arc = GetArc(node1, node2);
        node1.Arcs.Remove(arc);
        node2.Arcs.Remove(arc);
    }

    // Run the A* algorithm and put the result in path
    // If no path was found, return 'forever' and put only the destination in path
    // Returns the total path time
    public float FindPath(
        List<PathNode> path,
        Vector3 start,
        Vector3 destination,
        MobilityType mobility,
        float unitRadius,
        MoveCommandType command)
    {
        path.Clear();
        path.Add(new PathNode(destination, false));

        PathNode cameFromDest = null;
        float gScoreDest = Pathfinder.FindLocalPath(this, start, destination, mobility, unitRadius);

        if (gScoreDest < Pathfinder.FOREVER) {
            if (command == MoveCommandType.Slow || command == MoveCommandType.Reverse)
                return gScoreDest;
        }

        // Initialize with all nodes accessible from the starting point
        // (this can be optimized later by throwing out some from the start)
        _openSet.Clear();
        foreach (PathNode neighbor in Graph) {
            neighbor.IsClosed = false;
            neighbor.CameFrom = null;
            neighbor.GScore = Pathfinder.FOREVER;
            Vector3 neighborPos = Position(neighbor);

            if ((start - neighborPos).magnitude < ARC_MAX_DIST) {
                float gScoreNew = Pathfinder.FindLocalPath(this, start, neighborPos, mobility, unitRadius);
                if (gScoreNew < Pathfinder.FOREVER) {
                    neighbor.GScore = gScoreNew;
                    float fScoreNew = gScoreNew + TimeHeuristic(neighborPos, destination, mobility);
                    _openSet.Enqueue(neighbor, fScoreNew);
                }
            }
        }

        while (_openSet.Count > 0) {

            PathNode current = _openSet.Dequeue();
            current.IsClosed = true;

            if (gScoreDest < current.Priority)
                break;

            foreach (PathArc arc in current.Arcs) {
                PathNode neighbor = arc.Node1 == current ? arc.Node2 : arc.Node1;

                if (neighbor.IsClosed)
                    continue;

                float arcTime = arc.Time[mobility.Index];
                if (arcTime >= Pathfinder.FOREVER)
                    continue;

                float gScoreNew = current.GScore + arcTime;
                if (gScoreNew >= neighbor.GScore)
                    continue;

                float fScoreNew = gScoreNew + TimeHeuristic(Position(neighbor), destination, mobility);

                if (!_openSet.Contains(neighbor)) {
                    _openSet.Enqueue(neighbor, fScoreNew);
                } else {
                    _openSet.UpdatePriority(neighbor, fScoreNew);
                }
                neighbor.GScore = gScoreNew;
                neighbor.CameFrom = current;
            }

            float arcTimeDest = Pathfinder.FOREVER;
            if (Vector3.Distance(Position(current), destination) < ARC_MAX_DIST)
                arcTimeDest = Pathfinder.FindLocalPath(this, Position(current), destination, mobility, unitRadius);
           // Debug.Log(openSet.Count + " " + Position(current) + " " + current.isRoad + " " + Vector3.Distance(Position(current), destination) + " " + (current.gScore + arcTimeDest) + " " + gScoreDest);
            if (arcTimeDest >= Pathfinder.FOREVER)
                continue;
            if (arcTimeDest < Pathfinder.FOREVER && command == MoveCommandType.Slow)
                arcTimeDest = 0f;

            float gScoreDestNew = current.GScore + arcTimeDest;
            if (gScoreDestNew < gScoreDest) {
                gScoreDest = gScoreDestNew;
                cameFromDest = current;
            }

        }

        // Reconstruct best path
        PathNode node = cameFromDest;
        while (node != null) {
            path.Add(node);
            node = node.CameFrom;
        }
        return gScoreDest;
    }

    private float TimeHeuristic(Vector3 pos1, Vector3 pos2, MobilityType mobility)
    {
        return Vector3.Distance(pos1, pos2)*3/4;
    }

    // There is no way to mark the Vector3 class as Serializable, so this is the ugly workaround
    public static Vector3 Position(PathNode node)
    {
        return new Vector3(node.x, node.y, node.z);
    }
}

[Serializable()]
public class PathNode : FastPriorityQueueNode
{
    //public readonly Vector3 position;
    public readonly bool IsRoad;
    public readonly List<PathArc> Arcs;
    public readonly float x, y, z;

    /// <summary>
    /// Cost from the start node to this node.
    /// </summary>
    public float GScore;

    /// <summary>
    /// A closed node is a node that has already been visited (evaluated).
    /// </summary>
    public bool IsClosed;
    public PathNode CameFrom;

    public PathNode(Vector3 position, bool isRoad)
    {
        //this.position = position;
        x = position.x;
        y = position.y;
        z = position.z;
        IsRoad = isRoad;
        Arcs = new List<PathArc>(4);
    }
}

[Serializable()]
public struct PathArc
{
    public PathNode Node1, Node2;
    public float[] Time;

    public PathArc(PathNode node1, PathNode node2)
    {
        Time = new float[MobilityType.MobilityTypes.Count];
        this.Node1 = node1;
        this.Node2 = node2;
    }
}
