using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

using triangle_ptr = System.UInt32;
using node_ptr = System.UInt32;

public class Voxelizer : MonoBehaviour
{
    public MeshFilter meshFilter;
    public ComputeShader voxelizeShader;
    public uint resolution;
    public bool drawTriangles;
    public bool drawNodes;
    public uint G;
    public bool drawGthGenerationOnly;
    [Range(0, 7)]
    public uint N;
    public bool drawNthChild;

    public struct tripoly
    {
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;

        public tripoly(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
        }
    }

    public struct TreeNode
    {
        public Vector3 origin;
        public float extends;
        public triangle_ptr triangle0;
        public triangle_ptr triangle1;
        public triangle_ptr triangle2;
        public triangle_ptr triangle3;
        public node_ptr child0;
        public node_ptr child1;
        public node_ptr child2;
        public node_ptr child3;
        public node_ptr child4;
        public node_ptr child5;
        public node_ptr child6;
        public node_ptr child7;
    };

    Mesh model;
    [HideInInspector]
    public TreeNode[] data;
    [HideInInspector]
    public node_ptr[] hierarchy;
    [HideInInspector]
    public uint root;
    int nodeCount;
    float maxExtend;
    [HideInInspector]
    public List<tripoly> triangles = new List<tripoly>();
    [HideInInspector]
    public uint generationCount = 0;

    [HideInInspector]
    public Dictionary<float, int> generations;
    [HideInInspector]
    public Dictionary<int, int> resolutions;

    [HideInInspector]
    public bool drawGizmos;

    int Idx3ToIdx(Vector3Int idx3, int res)
    {
        return idx3.x + (idx3.z * res) + (idx3.y * res * res);
    }

    Vector3Int IdxToIdx3(int idx, int res)
    {
        return new Vector3Int(idx % res, idx / (res * res), (idx / res) % res);
    }

    private void OnValidate()
    {
        if (G >= generationCount)
            G = generationCount;
    }

    uint maxNodes(uint generation)
    {
        if (generation <= 0)
            return 1;
        return maxNodes(generation - 1) + (uint)Mathf.Pow(8, generation);
    }

    public void ClearLog()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
    }

    public void Voxelize()
    {
        if (resolution == 0)
            return;

        model = meshFilter.sharedMesh;
        if (model == null)
            return;

        ClearLog();
        generationCount = 0;
        triangles.Clear();

        maxExtend = Mathf.Max(model.bounds.extents.x, model.bounds.extents.y, model.bounds.extents.z);
        float voxelSize = (maxExtend * 2f) / resolution;

        uint maxVoxelCount = (resolution + 1) * (resolution + 1) * (resolution + 1);

        Debug.Log("voxel size: " + voxelSize + ", max size: " + (maxExtend * 2f) + ", max voxel count: " + maxVoxelCount);

        List<triangle_ptr> indices = new List<triangle_ptr>();
        for (uint i = 0; i < model.triangles.Length; i += 3)
        {
            indices.Add(i / 3);
            triangles.Add(new tripoly(meshFilter.transform.localToWorldMatrix * model.vertices[model.triangles[i]], meshFilter.transform.localToWorldMatrix * model.vertices[model.triangles[i + 1]], meshFilter.transform.localToWorldMatrix * model.vertices[model.triangles[i + 2]]));
        }

        uint triangleCount = (uint)(triangles.Count);
        uint minGenerationCount = (uint)Mathf.RoundToInt(Mathf.Log(maxVoxelCount) / Mathf.Log(8f));

        int nodeAllocationCount = (int)maxNodes(minGenerationCount);

        ComputeBuffer octree = new ComputeBuffer(nodeAllocationCount, sizeof(float) * 4 + sizeof(uint) * 12, ComputeBufferType.Counter);
        octree.SetCounterValue(0);

        Debug.Log("triangle count: " + triangleCount);
        Debug.Log("minimum generations: " + minGenerationCount);
        Debug.Log("allocated memory for " + nodeAllocationCount + " nodes.");

        ComputeBuffer indexBuffer = new ComputeBuffer((int)triangleCount, sizeof(uint), ComputeBufferType.Structured);
        indexBuffer.SetData(indices);

        ComputeBuffer triangleBuffer = new ComputeBuffer((int)triangleCount, sizeof(float) * 9, ComputeBufferType.Structured);
        triangleBuffer.SetData(triangles);

        ComputeBuffer hierarchyBuffer = new ComputeBuffer(nodeAllocationCount, sizeof(uint), ComputeBufferType.Structured);
        hierarchy = new uint[nodeAllocationCount];
        hierarchyBuffer.SetData(hierarchy);

        int voxelKernel = voxelizeShader.FindKernel("CSMain");
        voxelizeShader.SetBuffer(voxelKernel, "octree", octree);
        voxelizeShader.SetBuffer(voxelKernel, "triangleIndeces", indexBuffer);
        voxelizeShader.SetBuffer(voxelKernel, "triangles", triangleBuffer);
        voxelizeShader.SetBuffer(voxelKernel, "hierarchy", hierarchyBuffer);
        voxelizeShader.SetInt("resolution", (int)resolution);
        voxelizeShader.SetFloat("voxelSize", voxelSize);
        voxelizeShader.SetInt("triangleCount", (int)triangleCount);

        uint threadCount;
        uint temp;
        voxelizeShader.GetKernelThreadGroupSizes(voxelKernel, out threadCount, out temp, out temp);
        int groupCount = Mathf.CeilToInt((float)maxVoxelCount / threadCount);
        Debug.Log("group count: " + groupCount + ", thread count: " + threadCount);

        Stopwatch clock = new Stopwatch();

        clock.Start();
        voxelizeShader.Dispatch(voxelKernel, groupCount, 1, 1);
        System.TimeSpan elapsed = clock.Elapsed;
        clock.Stop();
        Debug.Log("voxelization took: " + elapsed.TotalMilliseconds + "ms");

        hierarchyBuffer.GetData(hierarchy);


        var countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

        // Copy the count.
        ComputeBuffer.CopyCount(octree, countBuffer, 0);

        // Retrieve it into array.
        int[] counter = new int[1] { 0 };
        countBuffer.GetData(counter);

        Debug.Log("generated " + counter[0] + " nodes.");
        nodeCount = counter[0];

        data = new TreeNode[counter[0]];
        octree.GetData(data);
    }

    private void OnDrawGizmos()
    {
        if (drawGizmos)
            DrawGizmos();
    }

    public void DrawGizmos()
    {
        uint oldGenerationCount = generationCount;
        float biggestSize = 0;
        root = 0;
        SortedSet<float> sizes = new SortedSet<float>();

        if (data != null)
        {
            float extend = -1;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].extends != 0 && extend != data[i].extends)
                {
                    extend = data[i].extends;
                    sizes.Add(extend);

                    if (biggestSize < data[i].extends)
                    {
                        biggestSize = data[i].extends;
                        root = (uint)i;
                    }
                }
            }

            int gen = 0;
            generations = new Dictionary<float, int>();
            resolutions = new Dictionary<int, int>();
            int res = (int)resolution;
            foreach (float size in sizes)
            {
                resolutions[gen] = res;
                generations[size] = gen;
                res /= 2;
                gen++;
            }

            generationCount = (uint)(sizes.Count - 1);

            int generation = -1;
            extend = -1;
            for (int i = 0; i < data.Length; i++)
            {
                if (extend != data[i].extends)
                {
                    extend = data[i].extends;
                    generation = generations[extend];
                    res = resolutions[generation];
                }

                bool hasTriangle = false;
                Gizmos.color = Color.magenta;

                if (data[i].triangle0 != 0)
                {
                    hasTriangle = true;
                    int idx = (int)data[i].triangle0 - 1;

                    if (drawTriangles)
                        if (idx >= triangles.Count)
                        {
                            Debug.Log(idx);
                            data[i].triangle0 = 0;
                        }
                        else
                        {
                            tripoly triangle = triangles[idx];
                            Gizmos.DrawLine(triangle.v0, triangle.v1);
                            Gizmos.DrawLine(triangle.v1, triangle.v2);
                            Gizmos.DrawLine(triangle.v2, triangle.v0);
                        }
                }
                if (data[i].triangle1 != 0)
                {
                    hasTriangle = true;
                    int idx = (int)data[i].triangle1 - 1;

                    if (drawTriangles)
                        if (idx >= triangles.Count)
                        {
                            Debug.Log(idx);
                            data[i].triangle1 = 0;
                        }
                        else
                        {
                            tripoly triangle = triangles[idx];
                            Gizmos.DrawLine(triangle.v0, triangle.v1);
                            Gizmos.DrawLine(triangle.v1, triangle.v2);
                            Gizmos.DrawLine(triangle.v2, triangle.v0);
                        }
                }
                if (data[i].triangle2 != 0)
                {
                    hasTriangle = true;
                    int idx = (int)data[i].triangle2 - 1;

                    if (drawTriangles)
                        if (idx >= triangles.Count)
                        {
                            Debug.Log(idx);
                            data[i].triangle2 = 0;
                        }
                        else
                        {
                            tripoly triangle = triangles[idx];
                            Gizmos.DrawLine(triangle.v0, triangle.v1);
                            Gizmos.DrawLine(triangle.v1, triangle.v2);
                            Gizmos.DrawLine(triangle.v2, triangle.v0);
                        }
                }
                if (data[i].triangle3 != 0)
                {
                    hasTriangle = true;
                    int idx = (int)data[i].triangle3 - 1;

                    if (drawTriangles)
                        if (idx >= triangles.Count)
                        {
                            Debug.Log(idx);
                            data[i].triangle3 = 0;
                        }
                        else
                        {
                            tripoly triangle = triangles[idx];
                            Gizmos.DrawLine(triangle.v0, triangle.v1);
                            Gizmos.DrawLine(triangle.v1, triangle.v2);
                            Gizmos.DrawLine(triangle.v2, triangle.v0);
                        }
                }

                if (hasTriangle)
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.blue;

                if (drawGthGenerationOnly && generation != G)
                    continue;

                if (drawNodes)
                {
                    Gizmos.DrawWireCube(data[i].origin, new Vector3(data[i].extends, data[i].extends, data[i].extends) * 2f);
                }
            }

            if (drawNthChild)
                DrawNthChildBranch(data[root]);
        }

        if (generationCount != oldGenerationCount)
        {
            Debug.Log("generated " + (generationCount + 1) + " generations");
            int gen = 0;
            foreach (float size in sizes)
            {
                Debug.Log("generation " + gen++ + " has size " + size);
            }
        }
    }

    void DrawNthChildBranch(TreeNode node)
    {
        Gizmos.DrawWireCube(node.origin, new Vector3(node.extends, node.extends, node.extends) * 2f);

        switch (N)
        {
            case 0:
                if (node.child0 != 0)
                {
                    if (node.child0 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child0 - 1] == 0)
                        return;

                    if (hierarchy[node.child0 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child0 - 1] - 1]);
                }
                break;
            case 1:
                if (node.child1 != 0)
                {
                    if (node.child1 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child1 - 1] == 0)
                        return;

                    if (hierarchy[node.child1 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child1 - 1] - 1]);
                }
                break;
            case 2:
                if (node.child2 != 0)
                {
                    if (node.child2 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child2 - 1] == 0)
                        return;

                    if (hierarchy[node.child2 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child2 - 1] - 1]);
                }
                break;
            case 3:
                if (node.child3 != 0)
                {
                    if (node.child3 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child3 - 1] == 0)
                        return;

                    if (hierarchy[node.child3 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child3 - 1] - 1]);
                }
                break;
            case 4:
                if (node.child4 != 0)
                {
                    if (node.child4 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child4 - 1] == 0)
                        return;

                    if (hierarchy[node.child4 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child4 - 1] - 1]);
                }
                break;
            case 5:
                if (node.child5 != 0)
                {
                    if (node.child5 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child5 - 1] == 0)
                        return;

                    if (hierarchy[node.child5 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child5 - 1] - 1]);
                }
                break;
            case 6:
                if (node.child6 != 0)
                {
                    if (node.child6 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child6 - 1] == 0)
                        return;

                    if (hierarchy[node.child6 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child6 - 1] - 1]);
                }
                break;
            case 7:
                if (node.child7 != 0)
                {
                    if (node.child7 - 1 >= hierarchy.Length)
                    {
                        Debug.Log("dafuq?H");
                        return;
                    }

                    if (hierarchy[node.child7 - 1] == 0)
                        return;

                    if (hierarchy[node.child7 - 1] - 1 >= data.Length)
                    {
                        Debug.Log("dafuq?D");
                        return;
                    }

                    DrawNthChildBranch(data[hierarchy[node.child7 - 1] - 1]);
                }
                break;
        }
    }
}
