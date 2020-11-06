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
    public bool animate;

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
    [HideInInspector]
    public Mesh model;
    [HideInInspector]
    public TreeNode[] data;
    [HideInInspector]
    public int dataCount;
    [HideInInspector]
    public uint root;
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

    public void OnValidate()
    {
        if (G >= generationCount)
            G = generationCount;

        octreeBufferSize = 0;
        triangleBufferSize = 0;
        hierarchyBufferSize = 0;
        indexBufferSize = 0;
        vertexBufferSize = 0;
        triangleCount = 0;

        model = meshFilter.sharedMesh;

        if (model == null)
            return;

        if (originalvertices == null)
            originalvertices = model.vertices;
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

    Vector3[] vertices;
    Vector3[] originalvertices;

    ComputeBuffer indexBuffer;
    int indexBufferSize = 0;
    ComputeBuffer vertexBuffer;
    int vertexBufferSize = 0;

    uint[] counter = new uint[1] { 0 };

    private void Update()
    {
        Voxelize();
    }

    public void Voxelize()
    {
        if (resolution == 0)
            return;

        if (model == null)
            model = meshFilter.sharedMesh;

        if (model == null)
            return;

        generationCount = 0;

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

        if (vertexBufferSize < originalvertices.Length)
        {
            vertexBufferSize = originalvertices.Length;

            if (vertexBuffer != null)
                vertexBuffer.Dispose();

            vertexBuffer = new ComputeBuffer(originalvertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        }
        vertexBuffer.SetData(originalvertices);

        int processMeshKernel = voxelizeShader.FindKernel("ProcessMesh");
        voxelizeShader.SetBuffer(processMeshKernel, "triangles", triangleBuffer);
        voxelizeShader.SetMatrix("modelmatrix", meshFilter.transform.localToWorldMatrix);
        voxelizeShader.SetInt("triangleCount", triangleCount);
        voxelizeShader.SetFloat("time", Time.time);
        voxelizeShader.SetBool("animate", animate);

        voxelizeShader.SetBuffer(processMeshKernel, "indices", indexBuffer);
        voxelizeShader.SetBuffer(processMeshKernel, "vertices", vertexBuffer);

        voxelizeShader.Dispatch(processMeshKernel, Mathf.RoundToInt(triangleCount / 1024f), 1, 1);

        if (vertices == null || vertices.Length < vertexBufferSize)
            vertices = new Vector3[vertexBufferSize];

        vertexBuffer.GetData(vertices);
        Vector3 size = new Vector3(0, 0, 0);
        for (int i = 0; i < vertices.Length; i++)
        {
            size.x = Mathf.Max(size.x, Mathf.Abs(vertices[i].x));
            size.y = Mathf.Max(size.y, Mathf.Abs(vertices[i].y));
            size.z = Mathf.Max(size.z, Mathf.Abs(vertices[i].z));
        }

        //if (Application.isPlaying)
        //{
        //    meshFilter.mesh.vertices = vertices;
        //    meshFilter.mesh.bounds = new Bounds(new Vector3(0, 0, 0), size * 2f);
        //    meshFilter.mesh.UploadMeshData(false);
        //    meshFilter.mesh.MarkModified();
        //}

        maxExtend = Mathf.Max(size.x, size.y, size.z);
        float voxelSize = (maxExtend * 2f) / resolution;
        uint maxVoxelCount = (resolution + 1) * (resolution + 1) * (resolution + 1);

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


        voxelizeShader.Dispatch(voxelKernel, groupCount, 1, 1);

        if (countBuffer == null)
        {
            countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
            countBuffer.SetCounterValue(1);
        }

        // Copy the count.
        ComputeBuffer.CopyCount(octree, countBuffer, 0);
        // Retrieve it into array.
        countBuffer.GetData(counter);

        if (data == null || data.Length < counter[0])
        {
            Debug.Log("data array resize.");
            data = new TreeNode[counter[0]];
        }
        octree.GetData(data, 0, 0, (int)counter[0]);
        dataCount = (int)counter[0];
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
            Gizmos.matrix = meshFilter.transform.localToWorldMatrix;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(maxExtend, maxExtend, maxExtend) * 2f);

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
                    Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.2f);
                else
                    Gizmos.color = new Color(0, 0, 1, 0.2f);

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
