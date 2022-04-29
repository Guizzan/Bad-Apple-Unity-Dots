using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using System.Linq;

public struct Pixel
{
    public int PrefabIndex;
    public Translation Position;
    public bool dontSkip;
}

public class DotsBadApple : MonoBehaviour
{
    public bool useJobs;
    public bool AutoStart;
    public int BatchPixels;
    [Tooltip("Pixel prefabs should be in an order of darkest pixel to brightest pixel")]
    public GameObject[] Pixels;
    public bool SkipDarkestPixel;
    public Vector2Int ScreenSize;
    public Vector2 AspectRatio;

    public VideoClip _videoClip;
    public bool _skipOnDrop = true;
    public bool _loop;
    public bool _inversePixels;
    [Range(0f, 10f)]
    public float _playbackSpeed = 1;

    public Font CountFont;

    private WebCamTexture _webcamTexture;
    private VideoPlayer _videoPlayer;
    [HideInInspector]
    public string[] _webcams;
    [HideInInspector]
    public int _webcamIndex;

    private EntityManager entityManager;
    private NativeList<Entity> EntityPrefabs;
    private NativeList<Entity> SpawnedEntities;

    private int _appleCount;
    private void Awake()
    {
        SpawnedEntities = new NativeList<Entity>(Allocator.Persistent);
        EntityPrefabs = new NativeList<Entity>(Allocator.Persistent);
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, null);
        foreach (GameObject item in Pixels)
        {
            EntityPrefabs.Add(GameObjectConversionUtility.ConvertGameObjectHierarchy(item, settings));
        }
        Camera.main.transform.localPosition = new Vector3(ScreenSize.x * AspectRatio.x * 0.5f, (ScreenSize.x * AspectRatio.x * 0.5f) + (ScreenSize.y * AspectRatio.y * 0.5f), ScreenSize.y * AspectRatio.y * 0.5f);
    }
    private void Start()
    {
        if (!AutoStart) return;
        StartCoroutine(StartVideoPlayer());
    }
    void OnGUI()
    {
        GUI.skin.label.font = CountFont;
        GUI.Label(new Rect(Screen.width - 450, Screen.height - 100, 500, 100), $"Apple Count: {_appleCount}");
    }
    private void Update()
    {
        if (_webcamTexture == null) return;
        Texture2D frame = GetTexture2DFromWebcamTexture(_webcamTexture);
        frame.Apply();
        frame = Resize(frame, ScreenSize.x, ScreenSize.y);
        RenderNewFrame(frame.GetPixels32());
    }
    public IEnumerator StartVideoPlayer()
    {
        SetVideoPlayer();
        _videoPlayer.Prepare();
        while (!_videoPlayer.isPrepared)
        {
            Debug.Log("Preparing...");
            yield return null;
        }
        Debug.Log("Starting Video!");
        _videoPlayer.sendFrameReadyEvents = true;
        _videoPlayer.frameReady += OnVideoPlayerFrame;
        _videoPlayer.Play();
    }
    public void StartWebcam()
    {
        Debug.Log("Reading webcam feed of " + _webcams[_webcamIndex]);
        _webcamTexture = new WebCamTexture(_webcams[_webcamIndex], ScreenSize.x, ScreenSize.y, 30);
        _webcamTexture.Play();
    }
    public void ForceStop()
    {
        _webcamTexture = null;
        if (GetComponent<VideoPlayer>() == null) return;
        _videoPlayer.Stop();
        _videoPlayer.clip = null;
        _videoPlayer.frameReady -= OnVideoPlayerFrame;
        Destroy(GetComponent<VideoPlayer>());
        Destroy(GetComponent<AudioSource>());
        //ClearFrame();
    }
    void OnVideoPlayerFrame(VideoPlayer source, long frameIdx)
    {
        RenderTexture renderTexture = source.texture as RenderTexture;
        Texture2D frame = new Texture2D(renderTexture.width, renderTexture.height);
        RenderTexture.active = renderTexture;
        frame.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        frame.Apply();
        RenderTexture.active = null;
        frame = Resize(frame, ScreenSize.x, ScreenSize.y);
        RenderNewFrame(frame.GetPixels32());
    }
    private void RenderNewFrame(Color32[] texColors)
    {
        ClearFrame();
        if (useJobs)
        {
            NativeArray<Color32> colorArray = new NativeArray<Color32>(texColors.Length, Allocator.TempJob);
            NativeArray<Pixel> PixelsData = new NativeArray<Pixel>(texColors.Length, Allocator.TempJob);

            for (int i = 0; i < texColors.Length; i++)
            {
                colorArray[i] = texColors[i];
            }

            RenderJob job = new RenderJob()
            {
                texColors = colorArray,
                SkipDarkestPixel = SkipDarkestPixel,
                _inversePixels = _inversePixels,
                PrefabsLenght = EntityPrefabs.Length,
                transformPos = new float3(transform.position),
                ScreenSize = new float2((Vector2)ScreenSize),
                PixelGaps = new float2(AspectRatio),
                Pixels = PixelsData
            };
            JobHandle handler = job.Schedule(texColors.Length, BatchPixels);
            handler.Complete();
            NativeArray<int> result = new NativeArray<int>(1, Allocator.TempJob);
            SetPixelPosJob job2 = new SetPixelPosJob()
            {
                PixelsData = PixelsData,
                SpawnedEntitites = SpawnedEntities,
                entityManager = entityManager,
                EntityPrefabs = EntityPrefabs,
                result = result
            };
            JobHandle handler2 = job2.Schedule();
            handler2.Complete();
            _appleCount = result[0];
            result.Dispose();
            colorArray.Dispose();
            PixelsData.Dispose();
        }
        else
        {
            for (int index = 0; index < texColors.Length; index++)
            {
                float grayScale = 0.299f * texColors[index].r + 0.587f * texColors[index].g + 0.114f * texColors[index].b;
                int i = Mathf.FloorToInt(Map(grayScale, 0, 255, 0, EntityPrefabs.Length - 1));
                if (!SkipDarkestPixel || i != (_inversePixels ? EntityPrefabs.Length - 1 : 0))
                {
                    i = _inversePixels ? EntityPrefabs.Length - i - 1 : i;
                    Entity instance = entityManager.Instantiate(EntityPrefabs[i]);
                    entityManager.SetComponentData(instance, new Translation { Value = new float3(index % ScreenSize.x * AspectRatio.x, 0, Mathf.FloorToInt(index / ScreenSize.y) * AspectRatio.y) + new float3(transform.position) });
                    SpawnedEntities.Add(instance);
                    _appleCount++;
                }
            }
        }
    }
    public void ClearFrame()
    {
        entityManager.DestroyEntity(SpawnedEntities);
        SpawnedEntities.Clear();
        _appleCount = 0;
    }
    private static Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
    {
        RenderTexture rt = new RenderTexture(targetX, targetY, 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);
        Texture2D result = new Texture2D(targetX, targetY);
        result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
        result.Apply();
        return result;
    }
    private static Texture2D GetTexture2DFromWebcamTexture(WebCamTexture webCamTexture)
    {
        Texture2D tx2d = new Texture2D(webCamTexture.width, webCamTexture.height);
        tx2d.SetPixels(webCamTexture.GetPixels());
        tx2d.Apply();
        return tx2d;
    }
    private void SetVideoPlayer()
    {
        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.clip = _videoClip;
        _videoPlayer.skipOnDrop = _skipOnDrop;
        _videoPlayer.isLooping = _loop;
        _videoPlayer.playbackSpeed = _playbackSpeed;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _videoPlayer.playOnAwake = false;
        _videoPlayer.SetTargetAudioSource(0, gameObject.AddComponent<AudioSource>());
    }
    private static float Map(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }
}

public struct SetPixelPosJob : IJob
{
    [ReadOnly]
    public NativeArray<Pixel> PixelsData;
    public NativeList<Entity> SpawnedEntitites;
    [ReadOnly]
    public NativeArray<Entity> EntityPrefabs;
    public EntityManager entityManager;
    public NativeArray<int> result;
    public void Execute()
    {
        for (int index = 0; index < PixelsData.Length; index++)
        {
            if (PixelsData[index].dontSkip)
            {
                Entity instance = entityManager.Instantiate(EntityPrefabs[PixelsData[index].PrefabIndex]);
                SpawnedEntitites.Add(instance);
                entityManager.SetComponentData(instance, PixelsData[index].Position);
                result[0]++;
            }
        }
    }
}
[BurstCompile]
public struct RenderJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color32> texColors;
    [ReadOnly]
    public bool SkipDarkestPixel;
    [ReadOnly]
    public bool _inversePixels;
    [ReadOnly]
    public int PrefabsLenght;
    public NativeArray<Pixel> Pixels;
    [ReadOnly]
    public float3 transformPos;
    [ReadOnly]
    public float2 ScreenSize;
    [ReadOnly]
    public float2 PixelGaps;
    public void Execute(int index)
    {
        float grayScale = 0.299f * texColors[index].r + 0.587f * texColors[index].g + 0.114f * texColors[index].b;
        int i = (int)math.floor(Map(grayScale, 0, 255, 0, PrefabsLenght - 1));
        if (!SkipDarkestPixel || i != (_inversePixels ? PrefabsLenght - 1 : 0))
        {
            i = _inversePixels ? PrefabsLenght - i - 1 : i;
            Pixels[index] = new Pixel
            {
                Position = new Translation
                {
                    Value = new float3(index % ScreenSize.x * PixelGaps.x, 0, (int)math.floor(index / ScreenSize.y) * PixelGaps.y) + transformPos
                },
                PrefabIndex = i,
                dontSkip = true
            };
        }
    }
    private static float Map(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(DotsBadApple))]
public class DotsBadAppleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DotsBadApple script = (DotsBadApple)target;
        if (GUILayout.Button("Play Video"))
        {
            script.StartCoroutine(script.StartVideoPlayer());
        }
        if (GUILayout.Button("Start Webcam"))
        {
            script.StartWebcam();
        }
        if (GUILayout.Button("Stop Video / Webcam"))
        {
            script.ForceStop();
        }
        EditorGUILayout.Space();
        GUIContent arrayList = new GUIContent("Selected Webcam");
        script._webcamIndex = EditorGUILayout.Popup(arrayList, script._webcamIndex, script._webcams.ToArray());
        if (GUILayout.Button("Refresh Webcams"))
        {
            script._webcams = WebCamTexture.devices.Select(x => x.name).ToArray();
        }
    }
}
#endif