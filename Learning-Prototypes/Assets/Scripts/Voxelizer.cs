using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

using triangle_ptr = System.UInt32;
using node_ptr = System.UInt32;

public class Voxelizer : MonoBehaviour
{
    public ComputeShader voxelizeShader;
    public uint resolution;

    struct tripoly
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

    struct TreeNode
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
    TreeNode[] data;
    int nodeCount;
    float maxExtend;
    List<tripoly> triangles = new List<tripoly>();
    int generationCount = 0;

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

    public void Awake()
    {
        Voxelize();
    }

    public void Voxelize()
    {
        if (resolution == 0)
            return;

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null)
            return;

        model = filter.sharedMesh;
        if (model == null)
            return;

        ClearLog();
        generationCount = 0;
        triangles.Clear();

        maxExtend = Mathf.Max(model.bounds.extents.x, model.bounds.extents.y, model.bounds.extents.z);
        float voxelSize = (maxExtend * 2f) / resolution;

        uint maxVoxelCount = (resolution+1) * (resolution + 1) * (resolution + 1);

        Debug.Log("voxel size: " + voxelSize + ", max size: " + (maxExtend * 2f) + ", max voxel count: " + maxVoxelCount);

        List<triangle_ptr> indices = new List<triangle_ptr>();
        for (uint i = 0; i < model.triangles.Length; i += 3)
        {
            indices.Add(i / 3);
            triangles.Add(new tripoly(transform.localToWorldMatrix * model.vertices[model.triangles[i]], transform.localToWorldMatrix * model.vertices[model.triangles[i + 1]], transform.localToWorldMatrix * model.vertices[model.triangles[i + 2]]));
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
        uint[] hierarchy = new uint[nodeAllocationCount];
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

    void OnDrawGizmos()
    {
        int oldGenerationCount = generationCount;
        if (data != null)
        {
            int generation = -1;
            float extend = -1;
            for (int i = 0; i < data.Length; i++)
            {
                if (extend != data[i].extends)
                {
                    extend = data[i].extends;
                    generation++;
                    generationCount = Mathf.Max(generationCount, generation);
                }

                bool hasTriangle = false;
                Gizmos.color = Color.magenta;

                if (data[i].triangle0 != 0)
                {
                    hasTriangle = true;
                    int idx = (int)data[i].triangle0 - 1;

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
                //continue;

                Gizmos.DrawWireCube(data[i].origin, new Vector3(data[i].extends, data[i].extends, data[i].extends) * 2f);
            }
        }

        if (generationCount != oldGenerationCount)
            Debug.Log("generated " + (generationCount + 1) + " generations");
    }
}
