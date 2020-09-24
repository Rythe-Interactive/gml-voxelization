using UnityEngine;
using System.Collections.Generic;

using triangle_ptr = System.UInt32;
using node_ptr = System.UInt32;

public class Voxelize : MonoBehaviour
{
    public Mesh model;
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

    TreeNode[] data;
    int nodeCount;
    float maxExtend;

    uint maxNodes(uint generation)
    {
        if (generation <= 0)
            return 0;
        return maxNodes(generation - 1) + (uint)Mathf.Pow(8, generation);
    }

    public void Awake()
    {
        if (resolution == 0)
            return;

        uint triangleCount = (uint)(model.triangles.Length / 3);
        maxExtend = Mathf.Max(model.bounds.extents.x, model.bounds.extents.y, model.bounds.extents.z);
        float voxelSize = (maxExtend * 2f) / resolution;

        uint voxelCount = resolution * resolution * resolution;

        Debug.Log("voxelSize: " + voxelSize + ", max size: " + (maxExtend * 2f));

        List<tripoly> triangles = new List<tripoly>();
        List<triangle_ptr> indices = new List<triangle_ptr>();
        for (uint i = 0; i < model.triangles.Length / 3; i += 3)
        {
            indices.Add(i / 3);
            triangles.Add(new tripoly(model.vertices[i], model.vertices[i + 1], model.vertices[i + 2]));
        }

        uint minGenerationCount = (uint)(Mathf.Log(triangleCount) / Mathf.Log(8f));

        ComputeBuffer octree = new ComputeBuffer((int)maxNodes(minGenerationCount * 3) * 3, sizeof(float) * 4 + sizeof(uint) * 12, ComputeBufferType.Append);
        octree.SetCounterValue(0);

        Debug.Log("triangle count: " + triangleCount);
        Debug.Log("minimum generations: " + minGenerationCount);
        Debug.Log("allocated memory for " + (maxNodes(minGenerationCount * 3) * 3) + " nodes.");

        ComputeBuffer indexBuffer = new ComputeBuffer((int)triangleCount, sizeof(uint), ComputeBufferType.Structured);
        indexBuffer.SetData(indices);

        ComputeBuffer triangleBuffer = new ComputeBuffer((int)triangleCount, sizeof(float) * 9, ComputeBufferType.Structured);
        triangleBuffer.SetData(triangles);

        int voxelKernel = voxelizeShader.FindKernel("CSMain");
        voxelizeShader.SetBuffer(voxelKernel, "octree", octree);
        voxelizeShader.SetBuffer(voxelKernel, "triangleIndeces", indexBuffer);
        voxelizeShader.SetBuffer(voxelKernel, "triangles", triangleBuffer);
        voxelizeShader.SetInt("resolution", (int)resolution);
        voxelizeShader.SetFloat("voxelSize", voxelSize);
        voxelizeShader.SetInt("triangleCount", (int)triangleCount);

        uint threadCount;
        uint temp;
        voxelizeShader.GetKernelThreadGroupSizes(voxelKernel, out threadCount, out temp, out temp);
        voxelizeShader.Dispatch(voxelKernel, Mathf.CeilToInt((float)voxelCount / threadCount), 1, 1);

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

    int generationCount = 0;
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
                Gizmos.color = new Color(1 - (generation / 2f), 0, generation / 2f);
                Gizmos.DrawWireCube(data[i].origin, new Vector3(data[i].extends, data[i].extends, data[i].extends) * 2f);
            }
        }

        if (generationCount != oldGenerationCount)
            Debug.Log("generated " + (generationCount + 1) + " generations");
    }
}
