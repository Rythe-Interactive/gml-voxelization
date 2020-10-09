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
    public int dataCount;
    [HideInInspector]
    public uint root;
    int nodeCount;
    float maxExtend;
    [HideInInspector]
    public tripoly[] triangles;
    [HideInInspector]
    public int triangleCount;
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

        octreeBufferSize = 0;
        triangleBufferSize = 0;
        hierarchyBufferSize = 0;
        indexBufferSize = 0;
        vertexBufferSize = 0;
        triangleCount = 0;
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

    ComputeBuffer octree;
    int octreeBufferSize = 0;
    ComputeBuffer triangleBuffer;
    int triangleBufferSize = 0;
    ComputeBuffer hierarchyBuffer;
    int hierarchyBufferSize = 0;
    ComputeBuffer countBuffer;

    ComputeBuffer indexBuffer;
    int indexBufferSize = 0;
    ComputeBuffer vertexBuffer;
    int vertexBufferSize = 0;

    Stopwatch clock = new Stopwatch();
    Stopwatch GPUclock = new Stopwatch();
    int[] counter = new int[1] { 0 };

    private void Update()
    {
        Voxelize();
    }

    public void Voxelize()
    {
        clock.Restart();

        if (resolution == 0)
            return;

        if (model == null)
        {
            model = meshFilter.sharedMesh;
            if (model == null)
                return;
        }

        generationCount = 0;

        maxExtend = Mathf.Max(model.bounds.extents.x, model.bounds.extents.y, model.bounds.extents.z);
        float voxelSize = (maxExtend * 2f) / resolution;
        uint maxVoxelCount = (resolution + 1) * (resolution + 1) * (resolution + 1);

        if (triangleCount < model.triangles.Length / 3)
            triangles = new tripoly[model.triangles.Length / 3];
        triangleCount = model.triangles.Length / 3;

        if (triangleBufferSize < triangleCount)
        {
            triangleBufferSize = (int)triangleCount;

            if (triangleBuffer != null)
                triangleBuffer.Dispose();

            triangleBuffer = new ComputeBuffer(triangleCount, sizeof(float) * 9, ComputeBufferType.Structured);
            Debug.Log("triangle buffer resize.");
        }

        if (indexBufferSize < model.triangles.Length)
        {
            indexBufferSize = model.triangles.Length;

            if (indexBuffer != null)
                indexBuffer.Dispose();

            indexBuffer = new ComputeBuffer(model.triangles.Length, sizeof(int), ComputeBufferType.Structured);
        }
        indexBuffer.SetData(model.triangles);

        if (vertexBufferSize < model.vertices.Length)
        {
            vertexBufferSize = model.vertices.Length;

            if (vertexBuffer != null)
                vertexBuffer.Dispose();

            vertexBuffer = new ComputeBuffer(model.vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        }
        vertexBuffer.SetData(model.vertices);

        int processMeshKernel = voxelizeShader.FindKernel("ProcessMesh");
        voxelizeShader.SetBuffer(processMeshKernel, "triangles", triangleBuffer);
        voxelizeShader.SetMatrix("modelmatrix", meshFilter.transform.localToWorldMatrix);
        voxelizeShader.SetInt("triangleCount", triangleCount);

        voxelizeShader.SetBuffer(processMeshKernel, "indices", indexBuffer);
        voxelizeShader.SetBuffer(processMeshKernel, "vertices", vertexBuffer);

        GPUclock.Restart();
        voxelizeShader.Dispatch(processMeshKernel, Mathf.RoundToInt(triangleCount / 1024f), 1, 1);
        GPUclock.Stop();
        uint minGenerationCount = (uint)Mathf.RoundToInt(Mathf.Log(maxVoxelCount) / Mathf.Log(8f));

        int nodeAllocationCount = (int)maxNodes(minGenerationCount);

        if (octreeBufferSize < nodeAllocationCount)
        {
            octreeBufferSize = nodeAllocationCount;

            if (octree != null)
                octree.Dispose();

            octree = new ComputeBuffer(nodeAllocationCount, sizeof(float) * 4 + sizeof(uint) * 12, ComputeBufferType.Counter);
            Debug.Log("octree buffer resize.");
        }
        octree.SetCounterValue(0);

        if (hierarchyBufferSize < nodeAllocationCount)
        {
            hierarchyBufferSize = nodeAllocationCount;

            if (hierarchyBuffer != null)
                hierarchyBuffer.Dispose();

            hierarchyBuffer = new ComputeBuffer(nodeAllocationCount, sizeof(uint), ComputeBufferType.Structured);
            Debug.Log("hierarchy buffer resize.");
        }
        hierarchyBuffer.SetData(new uint[nodeAllocationCount]);

        int voxelKernel = voxelizeShader.FindKernel("Voxelize");
        voxelizeShader.SetBuffer(voxelKernel, "octree", octree);
        voxelizeShader.SetBuffer(voxelKernel, "triangles", triangleBuffer);
        voxelizeShader.SetBuffer(voxelKernel, "hierarchy", hierarchyBuffer);
        voxelizeShader.SetInt("resolution", (int)resolution);
        voxelizeShader.SetFloat("voxelSize", voxelSize);

        uint threadCount;
        uint temp;
        voxelizeShader.GetKernelThreadGroupSizes(voxelKernel, out threadCount, out temp, out temp);
        int groupCount = Mathf.CeilToInt((float)maxVoxelCount / threadCount);

        GPUclock.Start();
        voxelizeShader.Dispatch(voxelKernel, groupCount, 1, 1);
        GPUclock.Stop();

        if (countBuffer == null)
            countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
        // Copy the count.
        countBuffer.SetCounterValue(0);
        ComputeBuffer.CopyCount(octree, countBuffer, 0);

        // Retrieve it into array.
        countBuffer.GetData(counter);

        nodeCount = counter[0];

        if (data == null || data.Length < counter[0])
            data = new TreeNode[counter[0]];
        octree.GetData(data);
        dataCount = counter[0];

        clock.Stop();
        System.TimeSpan elapsed = clock.Elapsed;
        System.TimeSpan GPUelapsed = GPUclock.Elapsed;

        Debug.Log("Voxelization took " + elapsed.TotalMilliseconds + "ms total with " + GPUelapsed.TotalMilliseconds + "ms of GPU time.");
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

            for (int i = 0; i < dataCount; i++)
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
            for (int i = 0; i < dataCount; i++)
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
                        if (idx >= triangleCount)
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
                        if (idx >= triangleCount)
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
                        if (idx >= triangleCount)
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
                        if (idx >= triangleCount)
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

        //if (generationCount != oldGenerationCount)
        //{
        //    Debug.Log("generated " + (generationCount + 1) + " generations");
        //    int gen = 0;
        //    foreach (float size in sizes)
        //    {
        //        Debug.Log("generation " + gen++ + " has size " + size);
        //    }
        //}
    }

    void DrawNthChildBranch(TreeNode node)
    {
        Gizmos.DrawWireCube(node.origin, new Vector3(node.extends, node.extends, node.extends) * 2f);

        switch (N)
        {
            case 0:
                if (node.child0 != 0)
                {
                    DrawNthChildBranch(data[node.child0 - 1]);
                }
                break;
            case 1:
                if (node.child1 != 0)
                {
                    DrawNthChildBranch(data[node.child1 - 1]);
                }
                break;
            case 2:
                if (node.child2 != 0)
                {
                    DrawNthChildBranch(data[node.child2 - 1]);
                }
                break;
            case 3:
                if (node.child3 != 0)
                {
                    DrawNthChildBranch(data[node.child3 - 1]);
                }
                break;
            case 4:
                if (node.child4 != 0)
                {
                    DrawNthChildBranch(data[node.child4 - 1]);
                }
                break;
            case 5:
                if (node.child5 != 0)
                {
                    DrawNthChildBranch(data[node.child5 - 1]);
                }
                break;
            case 6:
                if (node.child6 != 0)
                {
                    DrawNthChildBranch(data[node.child6 - 1]);
                }
                break;
            case 7:
                if (node.child7 != 0)
                {
                    DrawNthChildBranch(data[node.child7 - 1]);
                }
                break;
        }
    }
}
