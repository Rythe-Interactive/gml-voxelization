//using UnityEngine;
//using System.Collections.Generic;
//using triangle_ptr = System.Int32;
//using node_ptr = System.Int32;


//class cpuvoxelize
//{
//    const int nullptr = 0;
//    public static int octreeCount = 0;
//    public static Voxelizer.TreeNode[] octree;
//    public static int[] hierarchy;
//    public static int resolution;
//    public static float voxelSize;
//    public static Voxelizer.tripoly[] triangles;
//    public static int triangleCount;
//    public static int[] indices;

//    static int Idx3ToIdx(Vector3Int idx3, int res)
//    {
//        return idx3.x + (idx3.z * res) + (idx3.y * res * res);
//    }

//    static Vector3Int IdxToIdx3(int idx, int res)
//    {
//        return new Vector3Int(idx % res, idx / (res * res), (idx / res) % res);
//    }

//    public static void run(Vector3Int groupsize, Vector3Int threadCount)
//    {
//        int i = 0;
//        for (int gx = 0; gx < groupsize.x; gx++)
//            for (int gy = 0; gy < groupsize.y; gy++)
//                for (int gz = 0; gz < groupsize.z; gx++)
//                    for (int tx = 0; tx < threadCount.x; tx++)
//                        for (int ty = 0; ty < threadCount.y; ty++)
//                            for (int tz = 0; tz < threadCount.z; tz++)
//                            {
//                                if(!compute(new Vector3Int(gx * threadCount.x + tx, gy * threadCount.y + ty, gz * threadCount.z + tz),
//                                    new Vector3Int(gx, gy, gz), i))
//                                    return;
//                                i++;
//                            }
//    }

//    static bool compute(Vector3Int id, Vector3Int groupIdx, int threadIdx)
//    {
//        int idx = id.x;

//        // Allows the shader to fast forward through everything without effecting anything else. Also allows the use of memory barriers.
//        bool returned = false;

//        int nodesInGeneration = resolution * resolution * resolution;

//        if (idx >= nodesInGeneration) // if the index is outside the bounds escape.
//            return false;

//        Vector3Int idx3 = IdxToIdx3(idx, resolution);
//        float fullSize = resolution * voxelSize;
//        float boundSize = voxelSize;
//        float extends = boundSize * 0.5f;
//        Vector3 origin = (new Vector3(idx3.x, idx3.y, idx3.z) * boundSize) + new Vector3(extends, extends, extends) - new Vector3(fullSize * 0.5f, fullSize * 0.5f, fullSize * 0.5f);

//        triangle_ptr[] triangleIndices = new triangle_ptr[4];
//        triangleIndices[0] = nullptr;
//        triangleIndices[1] = nullptr;
//        triangleIndices[2] = nullptr;
//        triangleIndices[3] = nullptr;

//        bool trianglesFound = false;
//        uint lastIdx = 0;

//        {// Get intersecting triangles.
//            for (int i = 0; i < triangleCount; i++)
//            {
//                Bounds b = new Bounds(origin, new Vector3(extends * 2, extends * 2));
//                if (IsIntersecting(b, triangles[i]))
//                {
//                    trianglesFound = true;
//                    triangleIndices[lastIdx] = i + 1;
//                    lastIdx++;
//                    if (lastIdx >= 4)
//                        break;
//                }
//            }
//        }

//        int maxGenerationCount = (int)(Mathf.Log(nodesInGeneration) / Mathf.Log(8f)) + 1;

//        int generationResolution = resolution;
//        int childrenResolution = resolution;
//        int totalNodes = 0;
//        int childTotal = 0;
//        int generationalIdx = idx;
//        Vector3Int generationIdx3 = idx3;
//        int childIdx = 0;
//        Vector3Int childIdx3 = new Vector3Int();

//        {
//            for (uint generation = 0; generation < maxGenerationCount; generation++)
//            {
//                //									triangles													children
//                Voxelizer.TreeNode node = new Voxelizer.TreeNode(origin, extends, /*0*/nullptr, /*1*/nullptr, /*2*/nullptr, /*3*/nullptr, /*0*/nullptr, /*1*/nullptr, /*2*/nullptr, /*3*/nullptr, /*4*/nullptr, /*5*/nullptr, /*6*/nullptr, /*7*/nullptr);

//                if (!returned)
//                {
//                    if (generation == 0) // Set triangles only for leaf nodes.
//                    {
//                        node.triangle0 = (uint)triangleIndices[0];
//                        node.triangle1 = (uint)triangleIndices[1];
//                        node.triangle2 = (uint)triangleIndices[2];
//                        node.triangle3 = (uint)triangleIndices[3];
//                    }
//                    else // Set children only for any branch generation.
//                    {
//                        node.child0 = (uint)hierarchy[childTotal + childIdx]; // 0, 0, 0
//                        int childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(1, 0, 0), childrenResolution);
//                        node.child1 = (uint)hierarchy[childHidx]; // 1, 0, 0
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(1, 0, 1), childrenResolution);
//                        node.child2 = (uint)hierarchy[childHidx]; // 1, 0, 1
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(0, 0, 1), childrenResolution);
//                        node.child3 = (uint)hierarchy[childHidx]; // 0, 0, 1
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(0, 1, 0), childrenResolution);
//                        node.child4 = (uint)hierarchy[childHidx]; // 0, 1, 0
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(1, 1, 0), childrenResolution);
//                        node.child5 = (uint)hierarchy[childHidx]; // 1, 1, 0
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(1, 1,1), childrenResolution);
//                        node.child6 = (uint)hierarchy[childHidx]; // 1, 1, 1
//                        childHidx = childTotal + Idx3ToIdx(childIdx3 + new Vector3Int(0,1,1), childrenResolution);
//                        node.child7 = (uint)hierarchy[childHidx]; // 0, 1, 1
//                    }
//                }

//                // Filter out whether we are the 1st child of our parent or not.
//                Vector3Int subIdx3 = new Vector3Int(
//                idx3.x % Mathf.FloorToInt(Mathf.Pow(2, generation)),
//                idx3.y % Mathf.FloorToInt(Mathf.Pow(2, generation)),
//                idx3.z % Mathf.FloorToInt(Mathf.Pow(2, generation)));

//                if (subIdx3.x != 0 || subIdx3.y != 0 || subIdx3.z != 0)
//                {
//                    returned = true;
//                }

//                if (!returned)
//                {
//                    if (generation == 0)
//                    {
//                        if (trianglesFound)
//                        {
//                            int indx = octreeCount;
//                            octreeCount++;
//                            octree[indx] = node;
//                            hierarchy[totalNodes + generationalIdx] = indx + 1;
//                        }
//                    }
//                    else
//                    {
//                        bool hasChildren = false;
//                        hasChildren = hasChildren || (node.child0 != nullptr);
//                        hasChildren = hasChildren || (node.child1 != nullptr);
//                        hasChildren = hasChildren || (node.child2 != nullptr);
//                        hasChildren = hasChildren || (node.child3 != nullptr);
//                        hasChildren = hasChildren || (node.child4 != nullptr);
//                        hasChildren = hasChildren || (node.child5 != nullptr);
//                        hasChildren = hasChildren || (node.child6 != nullptr);
//                        hasChildren = hasChildren || (node.child7 != nullptr);

//                        //if (hasChildren)
//                        {
//                            int indx = octreeCount;
//                            octreeCount++;
//                            octree[indx] = node;
//                            hierarchy[totalNodes + generationalIdx] = indx + 1;
//                        }
//                    }
//                }

//                if (extends * 2f >= fullSize)
//                {
//                    returned = true;
//                }

//                childIdx = generationalIdx;
//                childTotal = totalNodes;
//                totalNodes += nodesInGeneration;
//                nodesInGeneration = nodesInGeneration / 8;
//                childrenResolution = generationResolution;
//                generationResolution = generationResolution / 2;
//                childIdx3 = generationIdx3;
//                generationIdx3.x = Mathf.FloorToInt(generationIdx3.x * 0.5f);
//                generationIdx3.y = Mathf.FloorToInt(generationIdx3.y * 0.5f);
//                generationIdx3.z = Mathf.FloorToInt(generationIdx3.z * 0.5f);
//                generationalIdx = Idx3ToIdx(generationIdx3, generationResolution);
//                extends *= 2f;
//                origin = (new Vector3(idx3.x, idx3.y, idx3.z) * boundSize) + new Vector3(extends - (fullSize * 0.5f), extends - (fullSize * 0.5f), extends - (fullSize * 0.5f));
//            }
//        }
//        return true;
//    }

//    static bool IsIntersecting(Bounds box, Voxelizer.tripoly triangle)
//    {
//        float triangleMin, triangleMax;
//        float boxMin, boxMax;
//        // Test the box normals (x-, y- and z-axes)
//        var boxNormals = new Vector3[] {
//        new Vector3(1,0,0),
//        new Vector3(0,1,0),
//        new Vector3(0,0,1)
//        };

//        var boxVertices = new Vector3[]
//        {
//            box.min,
//            new Vector3(box.max.x, box.min.y, box.min.z),
//            new Vector3(box.max.x, box.min.y, box.max.z),
//            new Vector3(box.min.x, box.min.y, box.max.z),
//            new Vector3(box.max.x, box.max.y, box.min.z),
//            new Vector3(box.max.x, box.max.y, box.max.z),
//            new Vector3(box.min.x, box.max.y, box.max.z),
//            box.max
//        };

//        for (int i = 0; i < 3; i++)
//        {
//            Vector3 n = boxNormals[i];
//            Project(triangle.vertices, boxNormals[i], out triangleMin, out triangleMax);
//            if (triangleMax < box.min[i] || triangleMin > box.max[i])
//                return false; // No intersection possible.
//        }

//        // Test the triangle normal
//        double triangleOffset = Vector3.Dot(triangle.normal, triangle.v0);
//        Project(boxVertices, triangle.normal, out boxMin, out boxMax);
//        if (boxMax < triangleOffset || boxMin > triangleOffset)
//            return false; // No intersection possible.

//        // Test the nine edge cross-products
//        Vector3[] triangleEdges = new Vector3[] {
//        triangle.v0-triangle.v1,
//        triangle.v1-triangle.v2,
//        triangle.v2-triangle.v0
//    };
//        for (int i = 0; i < 3; i++)
//            for (int j = 0; j < 3; j++)
//            {
//                // The box normals are the same as it's edge tangents
//                Vector3 axis = Vector3.Cross(triangleEdges[i], boxNormals[j]);
//                Project(boxVertices, axis, out boxMin, out boxMax);
//                Project(triangle.vertices, axis, out triangleMin, out triangleMax);
//                if (boxMax <= triangleMin || boxMin >= triangleMax)
//                    return false; // No intersection possible
//            }

//        // No separating axis found.
//        return true;
//    }

//    static void Project(IEnumerable<Vector3> points, Vector3 axis,
//            out float min, out float max)
//    {
//        min = float.PositiveInfinity;
//        max = float.NegativeInfinity;
//        foreach (var p in points)
//        {
//            float val = Vector3.Dot(axis, p);
//            if (val < min) min = val;
//            if (val > max) max = val;
//        }
//    }
//}
