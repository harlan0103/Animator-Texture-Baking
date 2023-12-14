using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public struct VertexInfo
{
    public Vector3 position;
    public Vector3 normal;
}

public enum KERNELS
{
    GenerateVertexInfoArray = 0,
    TextureGeneration = 1
}

public class AnimatorTexBaker : MonoBehaviour
{
    public Animator animator;
    public ComputeShader computeShader;

    public RenderTexture renderTex;

    // Debug
    //public VertexInfo[] debug_vertexInfoArray;

    private Vector3 positionOffset = new Vector3(1.5f, 0.0f, 0.0f);

    // Start is called before the first frame update
    IEnumerator Start()
    {
        // Get animation clips and skinned mesh renderer list
        AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
        SkinnedMeshRenderer[] skinnedMeshRendererList = GetComponentsInChildren<SkinnedMeshRenderer>();

        // In case there are multiple skinned mesh renderer in one model
        // For each skinned mesh renderer bake its own animation texture
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRendererList)
        {
            // vertexCnt is the number of vertices of skinned mesh renderer
            // textureWidth is the next power of 2 value of vertex
            // To prevent data lose in Compute Shader calculate
            // Use textureWidth as element numbers to create compute buffers
            int vertexCnt = skinnedMeshRenderer.sharedMesh.vertexCount;
            int textureWidth = Mathf.NextPowerOfTwo(vertexCnt);

            Debug.Log("Number of vertices: " + vertexCnt);

            foreach (AnimationClip clip in animationClips)
            {
                Debug.Log("Animation clip name: " + clip.name);
                Debug.Log("Animation clip length: " + clip.length);
                Debug.Log("Animation frame number: " + (int)clip.length / clip.frameRate);

                // To prevent data lose useing next power of 2 valueo of actual frame numbers here
                int frameNumber = Mathf.NextPowerOfTwo((int)(clip.length / (1.0f / clip.frameRate)));
                Debug.Log("Frame number is: " + frameNumber);

                // Create an array to store all vertex info
                VertexInfo[] vertexInfoArray = new VertexInfo[textureWidth * frameNumber];

                // Create a new render texture for using
                RenderTexture positionTex = new RenderTexture(textureWidth, frameNumber, 0, RenderTextureFormat.ARGBHalf);
                positionTex.enableRandomWrite = true;
                positionTex.Create();

                // Bake a mesh in each frame
                int startIdx = 0;
                for (int i = 0; i < frameNumber; i++)
                {
                    animator.Play(clip.name, 0, (float)i / frameNumber);
                    yield return new WaitForEndOfFrame();

                    Mesh bakedMesh = new Mesh();
                    skinnedMeshRenderer.BakeMesh(bakedMesh);

                    // Create a game object in the scene
                    GameObject newGameObject = new GameObject("BakedMeshObject");
                    newGameObject.AddComponent<MeshRenderer>();
                    MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();
                    meshFilter.mesh = bakedMesh;
                    newGameObject.GetComponent<MeshRenderer>().material = skinnedMeshRenderer.material;
                    newGameObject.transform.position = new Vector3(0.0f, 0.0f, 0.0f) + positionOffset * i;

                    // Store vertex position into an array in each frame
                    // Use Compute Shader to accelerate
                    ComputeBuffer positionBuffer = new ComputeBuffer(textureWidth, sizeof(float) * 3);
                    ComputeBuffer normalBuffer = new ComputeBuffer(textureWidth, sizeof(float) * 3);
                    ComputeBuffer vertexInfoBuffer = new ComputeBuffer(textureWidth, sizeof(float) * 6);

                    positionBuffer.SetData(bakedMesh.vertices);
                    normalBuffer.SetData(bakedMesh.normals);

                    // Set buffers and properties
                    computeShader.SetBuffer((int)KERNELS.GenerateVertexInfoArray, "_PositionBuffer", positionBuffer);
                    computeShader.SetBuffer((int)KERNELS.GenerateVertexInfoArray, "_NormalBuffer", normalBuffer);
                    computeShader.SetBuffer((int)KERNELS.GenerateVertexInfoArray, "_VertexInfoBuffer", vertexInfoBuffer);
                    computeShader.SetInt("_VertexCnt", vertexCnt);

                    computeShader.Dispatch((int)KERNELS.GenerateVertexInfoArray, Mathf.CeilToInt(textureWidth / 8), 1, 1);

                    VertexInfo[] vertexInfoArrayTemp = new VertexInfo[textureWidth];
                    vertexInfoBuffer.GetData(vertexInfoArrayTemp);

                    // Add this vertexInfo group into the large array
                    Array.Copy(vertexInfoArrayTemp, 0, vertexInfoArray, startIdx, vertexInfoArrayTemp.Length);
                    startIdx += vertexInfoArrayTemp.Length;

                    // ==== Debug ====
                    //debug_vertexInfoArray = vertexInfoArrayTemp;

                    // Release compute buffer after using
                    positionBuffer.Release();
                    normalBuffer.Release();
                    vertexInfoBuffer.Release();
                }

                // After get all vertex information of current skinned mesh renderer with current animation clip
                // Send data to Compute Shader to generate render textures
                // Each render texture has dimension (vertexCnt X frameNumbers)
                // The x-axis is the differen vertices
                // where as y-axis is the vertex position changes in different frames
                ComputeBuffer vertexInfoArrayBuffer = new ComputeBuffer(textureWidth * frameNumber, sizeof(float) * 6);
                vertexInfoArrayBuffer.SetData(vertexInfoArray);

                // Set compute buffer and property value for kernel in compute shader
                computeShader.SetBuffer((int)KERNELS.TextureGeneration, "_VertexInfoBuffer", vertexInfoArrayBuffer);
                computeShader.SetTexture((int)KERNELS.TextureGeneration, "_PositionTex", positionTex);
                computeShader.SetInt("_TexWidth", textureWidth);
                computeShader.Dispatch((int)KERNELS.TextureGeneration, Mathf.CeilToInt(textureWidth / 8), Mathf.CeilToInt(frameNumber / 8), 1);

                vertexInfoArrayBuffer.Release();

                renderTex = positionTex;
                Debug.Log("================ DONE ================");
            }
        }
    }
}
