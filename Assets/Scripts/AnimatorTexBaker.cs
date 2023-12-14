using System.Collections;
using UnityEngine;

public class AnimatorTexBaker : MonoBehaviour
{
    public Animator animator;

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

            foreach (AnimationClip clip in animationClips)
            {
                Debug.Log("Animation clip name: " + clip.name);
                Debug.Log("Animation clip length: " + clip.length);
                Debug.Log("Animation clip frame rate: " + clip.frameRate);
                Debug.Log("Animation frame number: " + (int)clip.length / clip.frameRate);

                // To prevent data lose useing next power of 2 valueo of actual frame numbers here
                int frameNumber = Mathf.NextPowerOfTwo((int)(clip.length / (1.0f / clip.frameRate)));
                Debug.Log("Frame number is: " + frameNumber);

                // Bake a mesh in each frame
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
                }
            }
        }
    }
}
