using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

using triangle_ptr = System.UInt32;
using node_ptr = System.UInt32;

public class Voxelizer : MonoBehaviour
{
    public MeshFilter meshFilter;
    public ComputeShader voxelizeShader;
    public uint resolution;
    public bool drawAllNodes;
    [Range(0, 7)]
    public uint N;
    public bool drawNthChild;
    public bool drawVoxelByVoxel;
    public bool animate;
    [HideInInspector]
    public uint G;
    public bool drawGthGenerationOnly;

    public struct tripoly
    {
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;

        public Vector3[] vertices
        {
            get
            {
                return new Vector3[] { v0, v1, v2 };
            }
            set
            {
                v0 = value[0];
                v1 = value[1];
                v2 = value[2];
            }
        }

        public Vector3 position
        {
            get
            {
                return (v0 + v1 + v2) / 3f;
            }
        }

        public Vector3 normal
        {
            get
            {
                return Vector3.Cross(v1 - v0, v2 - v0).normalized;
            }
        }

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
        public node_ptr child0;
        public node_ptr child1;
        public node_ptr child2;
        public node_ptr child3;
        public node_ptr child4;
        public node_ptr child5;
        public node_ptr child6;
        public node_ptr child7;

        public node_ptr[] children
        {
            get
            {
                return new node_ptr[] { child0, child1, child2, child3, child4, child5, child6, child7 };
            }
            set
            {
                child0 = value[0];
                child1 = value[1];
                child2 = value[2];
                child3 = value[3];
                child4 = value[4];
                child5 = value[5];
                child6 = value[6];
                child7 = value[7];
            }
        }

        public TreeNode(Vector3 origin, float extends, node_ptr child0, node_ptr child1, node_ptr child2, node_ptr child3, node_ptr child4, node_ptr child5, node_ptr child6, node_ptr child7)
        {
            this.origin = origin;
            this.extends = extends;
            this.child0 = child0;
            this.child1 = child1;
            this.child2 = child2;
            this.child3 = child3;
            this.child4 = child4;
            this.child5 = child5;
            this.child6 = child6;
            this.child7 = child7;
        }
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
    public float leafSize = 0;

    [HideInInspector]
    public List<System.Tuple<int, int>> generations;
    [HideInInspector]
    public List<int> resolutions;

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

        if (!Mathf.IsPowerOfTwo((int)resolution))
        {
            Debug.LogWarning("resolution needs to be a power of 2");
            resolution = (uint)Mathf.ClosestPowerOfTwo((int)resolution);
        }

        if (model == null)
            model = meshFilter.sharedMesh;

        if (model == null)
            return;

        generations = new List<System.Tuple<int, int>>();
        resolutions = new List<int>();

        #region vertex shader
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
        #endregion

        #region voxelize
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

        maxExtend = Mathf.Max(size.x, size.y, size.z);
        float voxelSize = (maxExtend * 2f) / resolution;
        leafSize = voxelSize;
        uint maxVoxelCount = resolution * resolution * resolution;

        generationCount = (uint)Mathf.RoundToInt(Mathf.Log(maxVoxelCount) / Mathf.Log(8f)) + 1;

        int nodeAllocationCount = (int)maxNodes(generationCount);

        if (octreeBufferSize < nodeAllocationCount)
        {
            octreeBufferSize = nodeAllocationCount;

            if (octree != null)
                octree.Dispose();

            octree = new ComputeBuffer(nodeAllocationCount, sizeof(float) * 4 + sizeof(uint) * 8, ComputeBufferType.Counter);
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
        hierarchyBuffer.SetCounterValue(0);

        int generation0Kernel = voxelizeShader.FindKernel("Generation0");
        voxelizeShader.SetBuffer(generation0Kernel, "octree", octree);
        voxelizeShader.SetBuffer(generation0Kernel, "hierarchy", hierarchyBuffer);
        voxelizeShader.SetInt("generationStart", 0);
        voxelizeShader.SetInt("resolution", (int)resolution);
        voxelizeShader.SetFloat("voxelSize", voxelSize);
        voxelizeShader.SetFloat("bounds", maxExtend);

        triangleBuffer.GetData(triangles);
        voxelizeShader.SetBuffer(generation0Kernel, "triangles", triangleBuffer);

        uint threadCount;
        uint temp;
        voxelizeShader.GetKernelThreadGroupSizes(generation0Kernel, out threadCount, out temp, out temp);
        int groupCount = Mathf.CeilToInt((float)maxVoxelCount / threadCount);
        voxelizeShader.Dispatch(generation0Kernel, groupCount, 1, 1);

        int generationNKernel = voxelizeShader.FindKernel("GenerationN");
        voxelizeShader.SetBuffer(generationNKernel, "octree", octree);
        voxelizeShader.SetBuffer(generationNKernel, "hierarchy", hierarchyBuffer);
        voxelizeShader.GetKernelThreadGroupSizes(generationNKernel, out threadCount, out temp, out temp);

        if (countBuffer == null)
        {
            countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
            countBuffer.SetCounterValue(1);
        }

        int res = (int)resolution;
        counter[0] = 0;
        int childstart = 0;
        int prevGenStart = 0;
        int[] genStart = { 0 };
        for (int i = 0; i < generationCount; i++)
        {
            childstart = (int)counter[0];
            ComputeBuffer.CopyCount(hierarchyBuffer, countBuffer, 0);
            countBuffer.GetData(counter);

            prevGenStart = genStart[0];
            ComputeBuffer.CopyCount(octree, countBuffer, 0);
            countBuffer.GetData(genStart);

            generations.Add(System.Tuple.Create(prevGenStart, genStart[0]));
            resolutions.Add(res);

            res /= 2;
            voxelSize *= 2;
            voxelizeShader.SetInt("childGenerationStart", childstart);
            voxelizeShader.SetInt("generationStart", (int)counter[0]);
            voxelizeShader.SetInt("resolution", (int)res);
            voxelizeShader.SetFloat("voxelSize", voxelSize);
            voxelizeShader.SetFloat("bounds", maxExtend);

            groupCount = Mathf.CeilToInt((float)(res * res * res) / threadCount);
            voxelizeShader.Dispatch(generationNKernel, groupCount, 1, 1);

            if (res == 1)
            {
                generationCount = (uint)i + 1;
                break;
            }
        }

        prevGenStart = genStart[0];
        ComputeBuffer.CopyCount(octree, countBuffer, 0);
        countBuffer.GetData(counter);
        generations.Add(System.Tuple.Create(prevGenStart, (int)counter[0]));
        resolutions.Add(res);

        if (data == null || data.Length < counter[0])
        {
            Debug.Log("data array resize.");
            data = new TreeNode[counter[0]];
        }
        octree.GetData(data, 0, 0, (int)counter[0]);

        dataCount = (int)counter[0];
        root = (uint)dataCount - 1;
        #endregion
    }

    //public void VoxelizeOld()
    //{
    //    if (resolution == 0)
    //        return;

    //    if (!Mathf.IsPowerOfTwo((int)resolution))
    //    {
    //        Debug.LogWarning("resolution needs to be a power of 2");
    //        resolution = (uint)Mathf.ClosestPowerOfTwo((int)resolution);
    //    }

    //    if (model == null)
    //        model = meshFilter.sharedMesh;

    //    if (model == null)
    //        return;

    //    generationCount = 0;
    //    #region vertex shader
    //    if (triangleCount < model.triangles.Length / 3)
    //        triangles = new tripoly[model.triangles.Length / 3];
    //    triangleCount = model.triangles.Length / 3;

    //    if (triangleBufferSize < triangleCount)
    //    {
    //        triangleBufferSize = (int)triangleCount;

    //        if (triangleBuffer != null)
    //            triangleBuffer.Dispose();

    //        triangleBuffer = new ComputeBuffer(triangleCount, sizeof(float) * 9, ComputeBufferType.Structured);
    //        Debug.Log("triangle buffer resize.");
    //    }

    //    if (indexBufferSize < model.triangles.Length)
    //    {
    //        indexBufferSize = model.triangles.Length;

    //        if (indexBuffer != null)
    //            indexBuffer.Dispose();

    //        indexBuffer = new ComputeBuffer(model.triangles.Length, sizeof(int), ComputeBufferType.Structured);
    //    }
    //    indexBuffer.SetData(model.triangles);

    //    if (vertexBufferSize < originalvertices.Length)
    //    {
    //        vertexBufferSize = originalvertices.Length;

    //        if (vertexBuffer != null)
    //            vertexBuffer.Dispose();

    //        vertexBuffer = new ComputeBuffer(originalvertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
    //    }
    //    vertexBuffer.SetData(originalvertices);

    //    int processMeshKernel = voxelizeShader.FindKernel("ProcessMesh");
    //    voxelizeShader.SetBuffer(processMeshKernel, "triangles", triangleBuffer);
    //    voxelizeShader.SetMatrix("modelmatrix", meshFilter.transform.localToWorldMatrix);
    //    voxelizeShader.SetInt("triangleCount", triangleCount);
    //    voxelizeShader.SetFloat("time", Time.time);
    //    voxelizeShader.SetBool("animate", animate);

    //    voxelizeShader.SetBuffer(processMeshKernel, "indices", indexBuffer);
    //    voxelizeShader.SetBuffer(processMeshKernel, "vertices", vertexBuffer);

    //    voxelizeShader.Dispatch(processMeshKernel, Mathf.RoundToInt(triangleCount / 1024f), 1, 1);
    //    #endregion

    //    #region voxelize
    //    if (vertices == null || vertices.Length < vertexBufferSize)
    //        vertices = new Vector3[vertexBufferSize];

    //    vertexBuffer.GetData(vertices);
    //    Vector3 size = new Vector3(0, 0, 0);
    //    for (int i = 0; i < vertices.Length; i++)
    //    {
    //        size.x = Mathf.Max(size.x, Mathf.Abs(vertices[i].x));
    //        size.y = Mathf.Max(size.y, Mathf.Abs(vertices[i].y));
    //        size.z = Mathf.Max(size.z, Mathf.Abs(vertices[i].z));
    //    }

    //    maxExtend = Mathf.Max(size.x, size.y, size.z);
    //    float voxelSize = (maxExtend * 2f) / resolution;
    //    uint maxVoxelCount = (resolution + 1) * (resolution + 1) * (resolution + 1);

    //    uint minGenerationCount = (uint)Mathf.RoundToInt(Mathf.Log(maxVoxelCount) / Mathf.Log(8f)) + 1;

    //    int nodeAllocationCount = (int)maxNodes(minGenerationCount);

    //    if (octreeBufferSize < nodeAllocationCount)
    //    {
    //        octreeBufferSize = nodeAllocationCount;

    //        if (octree != null)
    //            octree.Dispose();
    //        if (finaloctree != null)
    //            finaloctree.Dispose();

    //        octree = new ComputeBuffer(nodeAllocationCount, sizeof(float) * 4 + sizeof(uint) * 12, ComputeBufferType.Counter);
    //        finaloctree = new ComputeBuffer(nodeAllocationCount, sizeof(float) * 4 + sizeof(uint) * 12, ComputeBufferType.Counter);
    //        Debug.Log("octree buffer resize.");
    //    }
    //    octree.SetCounterValue(0);

    //    if (hierarchyBufferSize < nodeAllocationCount)
    //    {
    //        hierarchyBufferSize = nodeAllocationCount;

    //        if (hierarchyBuffer != null)
    //            hierarchyBuffer.Dispose();
    //        if (finalhierarchyBuffer != null)
    //            finalhierarchyBuffer.Dispose();

    //        hierarchyBuffer = new ComputeBuffer(nodeAllocationCount, sizeof(uint), ComputeBufferType.Structured);
    //        finalhierarchyBuffer = new ComputeBuffer(nodeAllocationCount, sizeof(uint), ComputeBufferType.Structured);
    //        Debug.Log("hierarchy buffer resize.");
    //    }
    //    hierarchyBuffer.SetData(new uint[nodeAllocationCount]);

    //    int voxelKernel = voxelizeShader.FindKernel("Voxelize");
    //    voxelizeShader.SetBuffer(voxelKernel, "octree", octree);
    //    voxelizeShader.SetBuffer(voxelKernel, "triangles", triangleBuffer);
    //    voxelizeShader.SetBuffer(voxelKernel, "hierarchy", hierarchyBuffer);
    //    voxelizeShader.SetInt("resolution", (int)resolution);
    //    voxelizeShader.SetFloat("voxelSize", voxelSize);

    //    uint threadCount;
    //    uint temp;
    //    voxelizeShader.GetKernelThreadGroupSizes(voxelKernel, out threadCount, out temp, out temp);
    //    int groupCount = Mathf.CeilToInt((float)maxVoxelCount / threadCount);

    //    voxelizeShader.Dispatch(voxelKernel, groupCount, 1, 1);
    //    #endregion

    //    #region reduce
    //    if (countBuffer == null)
    //    {
    //        countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
    //        countBuffer.SetCounterValue(1);
    //    }

    //    // Copy the count.
    //    ComputeBuffer.CopyCount(octree, countBuffer, 0);
    //    // Retrieve it into array.
    //    countBuffer.GetData(counter);

    //    int reduceKernel = voxelizeShader.FindKernel("Reduce");
    //    voxelizeShader.SetBuffer(reduceKernel, "octree", octree);
    //    voxelizeShader.SetBuffer(reduceKernel, "finaloctree", finaloctree);
    //    voxelizeShader.SetBuffer(reduceKernel, "hierarchy", hierarchyBuffer);
    //    voxelizeShader.SetBuffer(reduceKernel, "finalhierarchy", finalhierarchyBuffer);

    //    voxelizeShader.GetKernelThreadGroupSizes(reduceKernel, out threadCount, out temp, out temp);
    //    groupCount = Mathf.CeilToInt((float)counter[0] / threadCount);
    //    voxelizeShader.Dispatch(reduceKernel, groupCount, 1, 1);

    //    // Copy the count.
    //    ComputeBuffer.CopyCount(finaloctree, countBuffer, 0);
    //    // Retrieve it into array.
    //    countBuffer.GetData(counter);
    //    #endregion

    //    #region link
    //    int linkKernel = voxelizeShader.FindKernel("Link");
    //    voxelizeShader.SetBuffer(linkKernel, "finaloctree", finaloctree);
    //    voxelizeShader.SetBuffer(linkKernel, "finalhierarchy", finalhierarchyBuffer);

    //    voxelizeShader.GetKernelThreadGroupSizes(linkKernel, out threadCount, out temp, out temp);
    //    groupCount = Mathf.CeilToInt((float)counter[0] / threadCount);
    //    voxelizeShader.Dispatch(linkKernel, groupCount, 1, 1);

    //    #endregion

    //    if (data == null || data.Length < counter[0])
    //    {
    //        Debug.Log("data array resize.");
    //        data = new TreeNode[counter[0]];
    //    }
    //    finaloctree.GetData(data, 0, 0, (int)counter[0]);

    //    dataCount = (int)counter[0];
    //    Debug.Log(dataCount + " nodes generated");
    //}

    private void OnDrawGizmos()
    {
        if (drawGizmos)
            DrawGizmos();
    }

    public void DrawGizmos()
    {
        SortedSet<float> sizes = new SortedSet<float>();

        if (data != null)
        {
            Gizmos.matrix = meshFilter.transform.localToWorldMatrix;

            Gizmos.color = new Color(1, 1, 1, 0.1f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(maxExtend, maxExtend, maxExtend) * 2f);

            if (drawAllNodes)
            {
                Gizmos.color = new Color(0, 1, 0, 0.1f);

                for (int i = 0; i < dataCount; i++)
                {
                    Gizmos.DrawWireCube(data[i].origin, new Vector3(data[i].extends, data[i].extends, data[i].extends) * 2f);
                }
            }

            if (drawGthGenerationOnly)
            {
                Gizmos.color = new Color(1, 1, 0, 0.1f);
                for (int i = generations[(int)G].Item1; i < generations[(int)G].Item2; i++)
                {
                    Gizmos.DrawWireCube(data[i].origin, new Vector3(data[i].extends, data[i].extends, data[i].extends) * 2f);
                }
            }

            Gizmos.color = new Color(1, 0, 1, 1);

            if (drawNthChild)
                DrawNthChildBranch(data[root]);

            if (drawVoxelByVoxel)
                DrawVoxelByVoxel();
        }
    }

    void DrawNthChildBranch(TreeNode node)
    {
        Gizmos.DrawWireCube(node.origin, new Vector3(node.extends, node.extends, node.extends) * 2f);

        if (node.extends * 2f <= leafSize)
            return;

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

    List<TreeNode> voxelsToDraw;
    List<tripoly> trianglesToDraw;
    bool coroutineRunning = false;

    void DrawVoxelByVoxel()
    {
        if (!coroutineRunning)
            StartCoroutine(RootCoroutine());

        Gizmos.color = new Color(1, 0, 1, 1);

        for (int i = 0; i < voxelsToDraw.Count; i++)
            Gizmos.DrawWireCube(voxelsToDraw[i].origin, new Vector3(voxelsToDraw[i].extends, voxelsToDraw[i].extends, voxelsToDraw[i].extends) * 2f);

        Gizmos.color = new Color(1, 0, 0, 1);

        for (int i = 0; i < trianglesToDraw.Count; i++)
        {
            var verts = trianglesToDraw[i].vertices;
            for (int j = 0; j < verts.Length; j++)
                Gizmos.DrawLine(verts[j], verts[(j + 1) % verts.Length]);
        }

    }

    IEnumerator RootCoroutine()
    {
        coroutineRunning = true;
        while (true)
        {
            voxelsToDraw = new List<TreeNode>();
            trianglesToDraw = new List<tripoly>();
            yield return StartCoroutine(DrawVoxelByVoxelCoroutine(data[root]));
        }
    }

    IEnumerator DrawVoxelByVoxelCoroutine(TreeNode node)
    {
        voxelsToDraw.Add(node);

        if (node.extends * 2f > leafSize)
        {
            yield return new WaitForSeconds(0.5f);
            var children = node.children;

            for (int i = 0; i < 8; i++)
            {
                if (children[i] != 0)
                    yield return StartCoroutine(DrawVoxelByVoxelCoroutine(data[children[i] - 1]));
            }
        }
        else
        {
            var children = node.children;

            for (int i = 0; i < 8; i++)
            {
                if (children[i] != 0)
                {
                    uint tridx = children[i] - 1;
                    if (tridx < 0 || tridx >= triangles.Length)
                        Debug.Log("whut... " + tridx);

                    trianglesToDraw.Add(triangles[tridx]);
                }
            }
            yield return new WaitForSeconds(0.5f);
            trianglesToDraw.Clear();
        }

        voxelsToDraw.RemoveAt(voxelsToDraw.Count - 1);
    }
}
